// StateMachineRunner.cs —— 状态机图的逐帧解释器（纯 C#，基于冻结的 NodeEditor 核心，照 DialogueRunner 成例）。
// FSM = control-flow 的强运行时变体：运行时恰好持有一条「活动路径」（HSM 栈，每层一个当前节点——State 或
// SubMachine），转移条件持续求值、命中即切换。规格 = SM0（生命周期契约 / tick 内固定顺序 / 转移求值顺序）：
//   · Tick(dt) = ① 转移求值阶段（活动栈自外向内、层内 AnyState 先，priority 升序稳定排序，首个 condition==true
//     生效短路；命中 → 旧态自内向外 OnExit → 新态自外向内 OnEnter → 在新态上继续连锁，全 tick ≤16 次）
//     ② 更新阶段（转移稳定后活动路径自外向内每层跑一次 OnUpdate，父 SubMachine 先于当前子态）。
//   · 转移目标为 Exit：子机层 → 结束子层回父层（父 SubMachine 不退出、保持活动，记「子机已完结」，
//     其转移每 tick 照常求值，子层不再 tick）；顶层 → 整机停机（OnStopped）。
//   · 生命周期/条件全部经可组合 Unit 槽（onEnter/onUpdate/onExit:ActionUnit、condition:ConditionUnit）；
//     空槽跳过（条件空 = 恒真）。
//   · Capture/Restore：只重建 HSM 栈与黑板值，不跑 OnEnter/OnExit、不发事件（存档恢复不得重触发进入动作）。
// Runtime 程序集 —— 不引用 UnityEditor / RuntimeGraphRegistry（红线 §6）；编辑器调试器只经 IRuntimeGraph 观察。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using NodeEditor;

namespace StateMachine
{
    public sealed class StateMachineRunner : IRuntimeGraph, IActiveRuntimeGraphSource
    {
        // 单 tick 连锁转移预算：命中转移后在新态上立即继续求值，合计超过此值 → LogWarning 并停止连锁
        //（本 tick 不再转移，机器不停机）。防无条件转移环把一帧拖死。
        const int MaxChainedTransitions = 16;
        // HSM 嵌套深度护栏：子机自引用/环引用是校验 ERROR，但运行时仍须防住未校验图把 EnterNode 递归爆栈。
        const int MaxDepth = 32;

        readonly NodeGraphAsset m_Graph;
        readonly NodeRegistry m_Registry;
        readonly StateMachineRunContext m_Ctx;   // 可组合单元求值上下文（IScopedBlackboard + 领域事件 sink）

        // HSM 栈：一层 = 一张正在运行的图 + 该层当前活动节点。[0] 恒为顶层图；进入 SubMachine 即压入其子图层
        //（子机图共用同一个 registry）。childFinished = 本层 current（SubMachine）的子机已到 Exit 完结：
        // 子层已弹出、不再 tick，但 SubMachine 本身保持活动，其出向转移每 tick 照常求值。
        sealed class Layer
        {
            public NodeGraphAsset graph;
            public Dictionary<string, NodeInstance> index;
            public NodeInstance current;
            public bool childFinished;
        }
        readonly List<Layer> m_Layers = new();

        // 每图 O(1) 实例索引缓存（照 DialogueRunner #6）：重复进出的子机图复用其索引，无需逐跳扫描。
        readonly Dictionary<NodeGraphAsset, Dictionary<string, NodeInstance>> m_IndexCache = new();

        // 转移候选的复用缓冲：(Transition 实例, priority, 来源序)。priority 主序、来源序 tiebreak 稳定排序
        //（多 AnyState 按 instances 列表序、单节点内按 connections 序）。
        readonly List<(NodeInstance node, int priority, int seq)> m_Candidates = new();

        // 状态机调试只高亮当前活动路径；结构节点和已退出节点始终保持 None。

        bool m_Running;

        // ---- 事件（C# event，Player 转发给场景/UnityEvent 接线）----
        public event Action<string> OnStateEntered;   // instanceId（State 与 SubMachine 都算状态）
        public event Action<string> OnStateExited;    // instanceId
        public event Action<string> OnMachineEvent;   // 领域单元 FireMachineEventAction 发出的自定义事件名
        public event Action OnStopped;                // 整机结束（顶层到 Exit 或调用 Stop）

        public StateMachineRunner(NodeGraphAsset graph, NodeRegistry registry, StateMachineRunContext ctx)
        {
            m_Graph = graph; m_Registry = registry; m_Ctx = ctx;
            // 领域事件汇流：单元经 ctx.blackboard（即 m_Ctx）FireEvent → 转发为本机 OnMachineEvent。
            if (m_Ctx != null) m_Ctx.OnEvent += name => OnMachineEvent?.Invoke(name);
        }

        public bool IsRunning => m_Running;

        // 本次运行的每实例黑板（照 DialogueRunner.Blackboard 成例）——外部系统（输入/感知适配器）经它注入
        // 或读取变量：如 BlackboardInputWriter 把移动输入写进来，转移条件下一 tick 即可读到。
        public StateMachineBlackboard Blackboard => m_Ctx?.Blackboard;

        // 活动栈路径：各层当前节点的 instanceId 以 '/' 连接（跨会话稳定，存档/断言用）。未运行 → 空串。
        public string CurrentStatePath => string.Join("/", m_Layers.Select(l => l.current?.instanceId));

        // 同路径的显示名版（调试/UI 用）：每节点 displayName 回退到定义的（本地化烘焙）名。
        public string DisplayPath => string.Join("/", m_Layers.Select(l => DisplayNameOf(l.current)));

        // ---- IRuntimeGraph（播放模式下由编辑器调试器经 RuntimeGraphRegistry 桥读取）----

        public Status StatusOf(string instanceId)
        {
            for (int i = 0; i < m_Layers.Count; i++)
                if (m_Layers[i].current != null && m_Layers[i].current.instanceId == instanceId)
                    return Status.Running;   // 活动路径：当前态 + 其活动 SubMachine 祖先链
            return Status.None;
        }

        // 状态机没有逐节点运行时对象（进度条之类）——StatusOf 的路径高亮就是全部内容。
        public object RuntimeNodeOf(string instanceId) => null;

        public NodeGraphAsset ActiveGraph =>
            m_Running && m_Layers.Count > 0 ? m_Layers[m_Layers.Count - 1].graph : null;

        public bool OwnsGraph(NodeGraphAsset graph) =>
            graph != null && (graph == m_Graph || m_Layers.Any(layer => layer.graph == graph));

        // ---- 生命周期泵 --------------------------------------------------------------------------------

        // 定位顶层 Entry → 沿 out 连线进入初始态，自外向内跑 onEnter 链（进入 SubMachine 即下钻子机 Entry）。
        // 不跑 onUpdate（首次 Tick 才跑）。Entry 缺失 / 初始态未接线 → 报错停机（不启动）。
        public void Start()
        {
            if (m_Running) Stop();   // 重启：先干净退出上一轮（Stop 幂等）
            m_Layers.Clear();

            var top = MakeLayer(m_Graph);
            if (top == null)
            {
                Debug.LogError("StateMachineRunner: graph is null — cannot start.");
                return;
            }
            var entry = FindEntry(top);
            if (entry == null)
            {
                Debug.LogError($"StateMachineRunner: graph '{m_Graph.name}' has no Entry node — cannot start.");
                return;
            }
            var initial = Target(top, entry, "out");
            if (initial == null)
            {
                Debug.LogError($"StateMachineRunner: graph '{m_Graph.name}' Entry has no initial state wired to its 'out' port — cannot start.");
                return;
            }

            m_Layers.Add(top);
            m_Running = true;
            EnterNode(0, initial, 0f);
        }

        // 一次完整 tick（顺序钉死，规格「tick 内固定顺序」）：① 转移求值连锁 ② 更新阶段。
        public void Tick(float dt)
        {
            if (!m_Running) return;

            // ① 转移求值阶段：连锁直到无转移命中、整机停机、或达到单 tick 预算。
            int fired = 0;
            while (m_Running)
            {
                var hit = FindHit(dt);
                if (hit == null) break;
                if (fired >= MaxChainedTransitions)
                {
                    Debug.LogWarning($"StateMachineRunner: more than {MaxChainedTransitions} chained transitions in one tick — chaining stopped for this tick (check for an unconditional transition cycle).");
                    break;
                }
                Fire(hit, dt);
                fired++;
            }
            if (!m_Running) return;   // 顶层到 Exit → 已停机，本 tick 无更新阶段

            // ② 更新阶段：转移稳定后，活动路径自外向内每层跑一次 onUpdate（父 SubMachine 先于当前子态；
            // 已完结子机层已弹出，天然不跑；当 tick 被退出的旧态不在活动路径上，也天然不跑）。
            for (int i = 0; i < m_Layers.Count; i++)
                RunAction(m_Layers[i].current, "onUpdate", dt);
        }

        // 退出当前整条 HSM 栈（自内向外逐层 onExit）并抛 OnStopped。幂等。
        public void Stop() => StopInternal(0f);

        void StopInternal(float dt)
        {
            if (!m_Running) return;
            m_Running = false;
            ExitLayersFrom(0, dt);   // 自内向外逐层 onExit（含顶层当前态）
            OnStopped?.Invoke();
        }

        // ---- 转移求值（规格「转移求值顺序」：自外向内、层内 AnyState 先、priority 升序稳定）----

        sealed class Hit { public int layer; public NodeInstance transition; public NodeInstance target; }

        Hit FindHit(float dt)
        {
            for (int li = 0; li < m_Layers.Count; li++)
            {
                var layer = m_Layers[li];

                // 该层全部 AnyState 的转移先（多 AnyState 按 instances 列表序、单节点内按 connections 序，
                // priority 升序稳定排序）；目标已在当前活动路径上则跳过（AnyState 自转移防抖）。
                m_Candidates.Clear();
                foreach (var inst in layer.graph.instances)
                    if (KindOf(inst) == StateMachineNodeKind.AnyState)
                        CollectTransitions(layer, inst);
                var hit = FirstHit(li, antiJitter: true, dt);
                if (hit != null) return hit;

                // 再该层当前活动节点（State 或 SubMachine）的转移（同排序）。
                // 显式自环不受防抖限制——作者画了就是要退出重进。
                m_Candidates.Clear();
                CollectTransitions(layer, layer.current);
                hit = FirstHit(li, antiJitter: false, dt);
                if (hit != null) return hit;

                // 本层无命中 → 继续更内层（外层转移覆盖内层——HSM 标准语义）。
            }
            return null;
        }

        // 把 source 自 'transitions' 出口连出的 Transition 节点按连接序追加进候选缓冲。
        void CollectTransitions(Layer layer, NodeInstance source)
        {
            if (source == null) return;
            foreach (var c in source.connections)
            {
                if (c.fromPort != "transitions") continue;
                var t = Find(layer, c.toInstanceId);
                if (t != null && KindOf(t) == StateMachineNodeKind.Transition)
                    m_Candidates.Add((t, Priority(t), m_Candidates.Count));
            }
        }

        // 候选缓冲稳定排序（priority 主序、来源序 tiebreak）后，返回首个 condition==true 的可用转移。
        Hit FirstHit(int li, bool antiJitter, float dt)
        {
            m_Candidates.Sort((a, b) => a.priority != b.priority ? a.priority.CompareTo(b.priority) : a.seq.CompareTo(b.seq));
            var layer = m_Layers[li];
            foreach (var (t, _, _) in m_Candidates)
            {
                var target = Target(layer, t, "to");
                var kind = target != null ? KindOf(target) : null;
                if (kind != StateMachineNodeKind.State && kind != StateMachineNodeKind.SubMachine &&
                    kind != StateMachineNodeKind.Exit)
                    continue;                                              // 悬空/非法目标：校验期问题，运行时跳过
                if (antiJitter && OnActivePath(target.instanceId)) continue;   // 防每 tick Exit/Enter 抖动
                if (!ConditionTrue(t, dt)) continue;                       // 空 condition 槽 = 恒真
                return new Hit { layer = li, transition = t, target = target };
            }
            return null;
        }

        // 目标是否已在当前活动路径上（== 当前态或其活动祖先 SubMachine）。instanceId 全局唯一，
        // 故直接对整条栈的各层当前节点比对即可。
        bool OnActivePath(string instanceId)
        {
            for (int i = 0; i < m_Layers.Count; i++)
                if (m_Layers[i].current != null && m_Layers[i].current.instanceId == instanceId)
                    return true;
            return false;
        }

        // 执行一次命中的转移：旧态（需退出的各层，自内向外）OnExit → 新态（自外向内）OnEnter。
        void Fire(Hit hit, float dt)
        {
            if (KindOf(hit.target) == StateMachineNodeKind.Exit)
            {
                if (hit.layer == 0) { StopInternal(dt); return; }   // 顶层到 Exit：整机结束（OnStopped）

                // 子机到 Exit：自内向外退出本层及更深各层的活动态并弹出这些层；父层 SubMachine 不退出、
                // 保持活动，仅记「子机已完结」——随后父层每 tick 照常求值其 transitions，子层不再 tick。
                ExitLayersFrom(hit.layer, dt);
                m_Layers[hit.layer - 1].childFinished = true;
                return;
            }

            // 普通转移（目标 State/SubMachine）：先自内向外退出更深各层 + 本层旧态，再自外向内进入新态。
            ExitLayersFrom(hit.layer + 1, dt);
            ExitCurrent(m_Layers[hit.layer], dt);
            EnterNode(hit.layer, hit.target, dt);
        }

        // ---- 进入 / 退出（生命周期契约：进入序自外向内，退出序自内向外）----

        // 把 node 设为该层当前态并跑 onEnter；若是 SubMachine 则继续下钻其子图 Entry 指向的初始态
        //（递归 = 自外向内 onEnter 链）。子图缺失/无 Entry/初始态未接线 → 告警并按「子机已完结」处置
        //（SubMachine 保持活动、其转移照常求值——不停机）。
        void EnterNode(int layerIndex, NodeInstance node, float dt)
        {
            var layer = m_Layers[layerIndex];
            layer.current = node;
            layer.childFinished = false;
            RunAction(node, "onEnter", dt);
            OnStateEntered?.Invoke(node.instanceId);

            if (KindOf(node) != StateMachineNodeKind.SubMachine) return;

            if (m_Layers.Count >= MaxDepth)
            {
                Debug.LogWarning($"StateMachineRunner: sub-machine nesting exceeded {MaxDepth} layers — treating sub machine '{node.instanceId}' as finished (check for circular sub-graph references).");
                layer.childFinished = true;
                return;
            }
            var sub = MakeLayer(ParamResolver.ResolveObject(node, "graph") as NodeGraphAsset);
            var subEntry = sub != null ? FindEntry(sub) : null;
            var subInitial = subEntry != null ? Target(sub, subEntry, "out") : null;
            if (subInitial == null)
            {
                Debug.LogWarning($"StateMachineRunner: sub machine '{node.instanceId}' has no runnable sub graph (missing graph reference, Entry node, or initial state) — treating it as finished.");
                layer.childFinished = true;
                return;
            }
            m_Layers.Add(sub);
            EnterNode(m_Layers.Count - 1, subInitial, dt);
        }

        // 自内向外退出并弹出 index >= keepCount 的各层（每层退出其当前活动节点）。
        void ExitLayersFrom(int keepCount, float dt)
        {
            for (int i = m_Layers.Count - 1; i >= keepCount; i--)
            {
                ExitCurrent(m_Layers[i], dt);
                m_Layers.RemoveAt(i);
            }
        }

        void ExitCurrent(Layer layer, float dt)
        {
            var node = layer.current;
            if (node == null) return;
            RunAction(node, "onExit", dt);
            OnStateExited?.Invoke(node.instanceId);
        }

        // ---- 快照（存档场景）----------------------------------------------------------------------------

        // 把活动栈路径 + 每个声明变量的当前值扁平化为可序列化快照（照 DialogueRunner.Capture 的扁平化方式；
        // 从未在 asset 中声明的运行时临时 key 不属于持久化契约，不捕获）。
        public StateMachineSnapshot Capture()
        {
            var s = new StateMachineSnapshot { statePath = CurrentStatePath };
            var bb = m_Ctx?.Blackboard;
            if (bb?.Declared != null)
                foreach (var v in bb.Declared.All())
                    s.vars.Add(new SnapshotVar { key = v.key, valueJson = UnitValues.ToInvariantString(bb.Get(v.key)) });
            return s;
        }

        // 恢复快照：只重建 HSM 栈（按 statePath 逐层解析并重建子层结构）与黑板值——
        // 不跑 OnEnter/OnExit、不发事件（存档恢复不得重触发进入动作）。
        // 非法 statePath（实例不存在 / 结构不再匹配）→ LogWarning 并安全落空（机器停机态，不抛异常）。
        public void Restore(StateMachineSnapshot s)
        {
            m_Layers.Clear();
            m_Running = false;

            if (s == null)
            {
                Debug.LogWarning("StateMachineRunner.Restore: snapshot is null — machine left stopped.");
                return;
            }

            // 黑板：把每个保存的字符串按其声明的 TypeRef 重新定型（往返复原 int/bool/float）。
            var bb = m_Ctx?.Blackboard;
            if (bb != null)
                foreach (var v in s.vars)
                    bb.Set(v.key, UnitValues.To(bb.Declared?.Find(v.key)?.type, v.valueJson));

            if (string.IsNullOrEmpty(s.statePath)) return;   // 捕获时未在运行 → 保持停机

            var segs = s.statePath.Split('/');
            var layer = MakeLayer(m_Graph);
            if (layer == null) { WarnBadPath(s.statePath, "<top graph>"); return; }
            m_Layers.Add(layer);
            for (int i = 0; i < segs.Length; i++)
            {
                var node = Find(layer, segs[i]);
                var kind = node != null ? KindOf(node) : null;
                if (kind != StateMachineNodeKind.State && kind != StateMachineNodeKind.SubMachine)
                {
                    WarnBadPath(s.statePath, segs[i]);
                    return;
                }
                layer.current = node;

                bool last = i == segs.Length - 1;
                if (kind == StateMachineNodeKind.SubMachine)
                {
                    if (last) { layer.childFinished = true; break; }   // 路径止于 SubMachine = 其子机已完结
                    var sub = MakeLayer(ParamResolver.ResolveObject(node, "graph") as NodeGraphAsset);
                    if (sub == null) { WarnBadPath(s.statePath, segs[i + 1]); return; }
                    m_Layers.Add(sub);
                    layer = sub;
                }
                else if (!last)   // State 之下不该再有路径段
                {
                    WarnBadPath(s.statePath, segs[i + 1]);
                    return;
                }
            }
            m_Running = true;
        }

        void WarnBadPath(string path, string segment)
        {
            Debug.LogWarning($"StateMachineRunner.Restore: state path '{path}' no longer resolves (segment '{segment}') — machine left stopped.");
            m_Layers.Clear();
        }


        // ---- 逻辑（委托给可组合单元；空槽跳过 / 条件空 = 恒真）----

        void RunAction(NodeInstance node, string slot, float dt)
        {
            if (node != null && m_Ctx != null && ParamResolver.ResolveUnit(node, slot) is ActionUnit a)
                a.Execute(m_Ctx.ToNodeContext(dt, node.instanceId));
        }

        bool ConditionTrue(NodeInstance transition, float dt) =>
            !(ParamResolver.ResolveUnit(transition, "condition") is ConditionUnit c) ||
            c.Evaluate(m_Ctx != null ? m_Ctx.ToNodeContext(dt, transition.instanceId) : default);

        // ---- 图辅助方法 ----------------------------------------------------------------------------------

        StateMachineNodeDefinition DefOf(NodeInstance n) => m_Registry?.Find(n.definitionId) as StateMachineNodeDefinition;

        // definitionId 经 StableId="statemachine."+Kind 解析；解析不到（定义缺失/非本领域）→ null，
        // 各调用点据此把该实例排除在求值之外（绝不误当 Exit 之类的具体种类处置）。
        StateMachineNodeKind? KindOf(NodeInstance n) => DefOf(n)?.Kind;

        string DisplayNameOf(NodeInstance n) =>
            n == null ? "" : (!string.IsNullOrEmpty(n.displayName) ? n.displayName : DefOf(n)?.DisplayName ?? n.definitionId);

        int Priority(NodeInstance transition)
        {
            var d = DefOf(transition);
            var s = d != null ? ParamResolver.Resolve(transition, d, "priority") : null;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;
        }

        NodeInstance Find(Layer layer, string id) =>
            id != null && layer.index.TryGetValue(id, out var n) ? n : null;

        NodeInstance Target(Layer layer, NodeInstance n, string port) =>
            n == null ? null : Find(layer, n.connections.FirstOrDefault(c => c.fromPort == port)?.toInstanceId);

        NodeInstance FindEntry(Layer layer) =>
            layer.graph.instances.FirstOrDefault(i => KindOf(i) == StateMachineNodeKind.Entry);

        // 构造一层：图的 O(1) 实例索引按 NodeGraphAsset 缓存、构建一次并复用。
        Layer MakeLayer(NodeGraphAsset g)
        {
            if (g == null) return null;
            if (!m_IndexCache.TryGetValue(g, out var index))
            {
                index = new Dictionary<string, NodeInstance>(g.instances.Count);
                foreach (var inst in g.instances) index[inst.instanceId] = inst;
                m_IndexCache[g] = index;
            }
            return new Layer { graph = g, index = index };
        }
    }
}
