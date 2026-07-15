// DialogueRunner.cs — 对话图的控制流解释器（基于冻结的 NodeEditor 核心）。
// 实现第三层控制流运行时形态（runtime-interfaces.md）：一个执行指针（POINTER）在图上行走；
// 每个节点执行 Enter/Execute/Exit，其中 Execute 产出一个 StepResult —— Advance(port)（沿一条边前进）、
// Waiting（在 Line/Choice 处挂起，直到调用方驱动我们继续）或 Done（该分支/图结束）。公开的
// Run/Advance/Choose API 是面向调用方、驱动该指针的泵；OnLine/OnChoices/OnEvent/OnEnd 是
// 表现层接缝。子对话压入一个栈帧；Capture/Restore 序列化指针 + blackboard + 调用栈。
// 运行时程序集 —— 不引用 UnityEditor（红线 §6）。
//
// 已内建的缺陷修复（契约 §Runner）：
//   #1 Jump 按 targetLabel 解析 -> 找到 labelName 匹配的 Label，然后从 Label.next 继续。
//   #2 当一个 Choice 没有任何可见（VISIBLE）选项时，若接线了 'fallback' 边则沿其前进，否则 OnEnd —— 绝不死锁。
//   #3 即时跳转泵受 MaxSteps 限制；溢出时 -> Debug.LogError + OnEnd（遇到环不会挂死）。
//   #4 选项可见性由其 gate 条件单元决定（无 gate => 始终可见），与 Condition 节点共用同一套可组合条件单元。
//   #6 每个图都缓存一个 Dictionary<instanceId,NodeInstance> 索引 —— O(1) 查找，无需逐跳扫描。
//   #8 [Serializable] DialogueState + Capture()/Restore() 往返序列化指针 + blackboard + 子对话调用栈。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NodeEditor;

namespace Dialogue
{
    // 一个被呈现的 Line 交给 UI 的内容。镜像 DialogueLineEntry 的表现字段，但 text 已针对
    // 当前语言解析完毕，因此调用方渲染时无需触碰 database。
    public struct DialogueLineView { public string speaker; public string text; public Sprite portrait; public AudioClip voice; }

    // 一个 Choice 中可被选择的一行。index 是调用方回传给 Choose() 的参数；它只对可见（VISIBLE）
    // 选项进行索引（被门控掉的选项不在其中），因此 Choose(i) 与 OnChoices[i] 始终对齐。
    public struct DialogueOptionView { public int index; public string text; }

    public class DialogueRunner : IRuntimeGraph, IActiveRuntimeGraphSource
    {
        // 表现层接缝 —— runner 从不渲染；它触发这些事件并挂起（Line/Choice）或结束。
        public event Action<DialogueLineView> OnLine;
        public event Action<IReadOnlyList<DialogueOptionView>> OnChoices;
        public event Action<string, string> OnEvent;   // (eventId, arg)
        public event Action OnEnd;

        // 活动的、每个实例独有的 blackboard。对外暴露，以便测试/UI 在一次运行前后播种或检视变量。
        public DialogueBlackboard Blackboard { get; }

        // #3 单次 Continue 泵的即时跳转预算：一个控制流图可以在两次挂起之间串联任意多个
        // 即时节点（Start/Set/Event/Condition/Jump/Label/Sub/End），但一个有限的对话在单次泵中
        // 绝不会超出此值 —— 只有即时节点的环（CYCLE，如 Jump->Label->Jump、Condition 自循环）才会。
        // 该计数器在每次挂起时重置，因此对话的总长度是无界的。
        const int MaxSteps = 10000;

        readonly NodeRegistry m_Registry;
        readonly BlackboardSet m_BB;          // 这张图的有效黑板（全局⊕模块⊕组合并视图），供播种 / Capture / VarType
        readonly DialogueDatabase m_Db;
        readonly string m_Lang;
        readonly DialogueRunContext m_Ctx;   // 可组合单元求值上下文（IScopedBlackboard + 事件出口）

        // #6 每个图的 O(1) 查找表，按 NodeGraphAsset 构建一次并复用（重新进入的子对话会保留其索引）。
        // m_Index/m_Labels 指向当前活动（ACTIVE）图的映射表；每当 m_Graph 改变时它们随之切换。
        readonly Dictionary<NodeGraphAsset, Dictionary<string, NodeInstance>> m_IndexCache = new();
        readonly Dictionary<NodeGraphAsset, Dictionary<string, NodeInstance>> m_LabelCache = new();
        Dictionary<string, NodeInstance> m_Index = Empty;
        Dictionary<string, NodeInstance> m_Labels = Empty;
        static readonly Dictionary<string, NodeInstance> Empty = new();

        // 子对话调用栈：进入一个 SubDialogue 时，我们压入调用方图 + 待恢复的节点，
        // 切换到子图并运行它；弹栈时恢复调用方并从其 'next' 继续。
        struct Frame { public NodeGraphAsset graph; public string returnNodeId; }
        readonly Stack<Frame> m_Stack = new();

        NodeGraphAsset m_Graph;             // 当前活动的图（概念上调用栈的栈顶）
        NodeInstance m_Current;             // 执行指针：当前被呈现的节点（Line/Choice）
        List<NodeInstance> m_PendingOptions;// 被呈现的 Choice 背后的可见 Options，按 view.index 索引

        // IRuntimeGraph 的后备数据：本次播放中指针经过的每一个 instanceId（由 Run()/Restore() 清空），
        // 以便编辑器调试器把"走过的路径"与"从未到达"区分着上色。
        readonly HashSet<string> m_Visited = new();

        public DialogueRunner(NodeRegistry registry, BlackboardSet blackboard, DialogueDatabase db, string lang)
        {
            m_Registry = registry; m_BB = blackboard; m_Db = db; m_Lang = lang;
            Blackboard = new DialogueBlackboard(blackboard);
            // 单元上下文：黑板读写 + 把 FireEventAction 触发的事件接到表现层 OnEvent。
            m_Ctx = new DialogueRunContext(Blackboard);
            m_Ctx.OnEvent += (id, a) => OnEvent?.Invoke(id, a);
        }

        // ---- IRuntimeGraph（播放模式下由编辑器的 GraphDebugger 读取；见框架 Editor/Support/RuntimeGraphRegistry.cs）---

        // 停泊的节点为 Running；本次播放中指针已经过的其他任何节点为 Success
        // （控制流行走没有逐节点的失败结果）；从未到达的节点为 None（变暗）。
        public Status StatusOf(string instanceId)
        {
            if (m_Current != null && instanceId == m_Current.instanceId) return Status.Running;
            return m_Visited.Contains(instanceId) ? Status.Success : Status.None;
        }

        // 对话节点没有用于调试模式 #5 内联视图（如 Wait 进度条）的逐节点运行时对象 ——
        // 上面 StatusOf 的路径/指针高亮在这里就是全部内容。
        public object RuntimeNodeOf(string instanceId) => null;

        public NodeGraphAsset ActiveGraph => m_Current != null ? m_Graph : null;

        public bool OwnsGraph(NodeGraphAsset graph) =>
            graph != null && (graph == m_Graph || m_Stack.Any(frame => frame.graph == graph));

        // ---- 公开的泵 ------------------------------------------------------------------------------------

        // 从图的 Start 节点开始一次全新的播放。对 null/空图安全（立即结束）。
        public void Run(NodeGraphAsset graph)
        {
            m_Stack.Clear();
            m_PendingOptions = null;
            m_Current = null;
            m_Visited.Clear();
            SetGraph(graph);
            Continue(StartOf(graph));
        }

        // 调用方确认了被呈现的 Line 并请求下一拍。除非我们停泊在某个 Line 上，否则不做任何操作，
        // 这样一次走神的 Advance()（双击、在 Choice 期间前进）就无法让指针失步。
        public void Advance()
        {
            if (m_Current != null && KindOf(m_Current) == DialogueNodeKind.Line)
            {
                var from = m_Current;
                m_Current = null;
                Continue(Next(from, "next"));
            }
        }

        // 调用方按视图索引选择一个可见选项。越界 / 无待决选择会被忽略
        // （防御性：来自 UI 的坏索引绝不会抛异常或跳到错误的分支）。
        public void Choose(int index)
        {
            if (m_PendingOptions == null || index < 0 || index >= m_PendingOptions.Count) return;
            var option = m_PendingOptions[index];
            m_PendingOptions = null;
            m_Current = null;
            Continue(Next(option, "next"));
        }

        // ---- 行走 ---------------------------------------------------------------------------------------

        // 驱动指针从 `node` 出发，穿过即时节点，直到我们挂起（Line/Choice）或结束。
        // 这是把控制流的"Execute 返回 StepResult"循环具体化：每次迭代计算当前节点的效果 + 它的下一条边
        // （Advance），停泊指针（在 Line/Choice 处 Waiting），或停止（Done -> 弹出子对话栈或 OnEnd）。
        // `node` 为 null 表示一条悬空/被切断的边 -> Done。
        void Continue(NodeInstance node)
        {
            int steps = 0;
            while (true)
            {
                if (++steps > MaxSteps)   // #3 即时节点环防护
                {
                    Debug.LogError($"DialogueRunner: exceeded {MaxSteps} steps without reaching a Line/Choice/End " +
                                   "— aborting (likely an instant-node cycle, e.g. Jump<->Label). Ending dialogue.");
                    End();
                    return;
                }

                if (node == null) { node = PopOrEnd(); if (node == null) return; continue; }

                m_Visited.Add(node.instanceId);   // IRuntimeGraph：此节点位于走过的路径上
                switch (KindOf(node))
                {
                    case DialogueNodeKind.Start:       node = Next(node, "next"); break;
                    case DialogueNodeKind.Action:      RunActions(node); node = Next(node, "next"); break;
                    case DialogueNodeKind.Condition:   node = Next(node, EvalPredicate(node) ? "true" : "false"); break;
                    case DialogueNodeKind.Label:       node = Next(node, "next"); break;            // #1 直通目标
                    case DialogueNodeKind.Jump:        node = ResolveJump(node); break;             // #1 按 labelName 重定向
                    case DialogueNodeKind.SubDialogue: node = EnterSub(node); break;
                    case DialogueNodeKind.Line:        m_Current = node; PresentLine(node); return; // 挂起（Waiting）
                    case DialogueNodeKind.Choice:
                        node = PresentChoices(node);
                        if (node == null) return;   // 已挂起（已触发 OnChoices）或已结束
                        break;                       // 没有可见选项 -> fallback 目标：继续行走
                    case DialogueNodeKind.End:         node = null; break;                          // Done -> 弹栈/OnEnd
                    default:                           node = null; break;
                }
            }
        }

        // 子对话返回 / 图结束。弹出一个栈帧并从该 SubDialogue 的 'next' 继续，或者 —— 在最外层 ——
        // 触发一次 OnEnd。返回 null 以便调用方的循环终止。
        NodeInstance PopOrEnd()
        {
            if (m_Stack.Count > 0)
            {
                var f = m_Stack.Pop();
                SetGraph(f.graph);
                return Next(Find(f.returnNodeId), "next");
            }
            End();
            return null;
        }

        void End() { m_Current = null; m_PendingOptions = null; OnEnd?.Invoke(); }

        NodeInstance EnterSub(NodeInstance node)
        {
            var sub = ParamResolver.ResolveObject(node, "subGraph") as NodeGraphAsset;
            if (sub == null) return Next(node, "next");                       // 未设置子图 -> 跳过它
            m_Stack.Push(new Frame { graph = m_Graph, returnNodeId = node.instanceId });
            SetGraph(sub);
            return StartOf(sub);
        }

        // #1 解析一个 Jump：在活动图中找到 labelName 等于 targetLabel 的 Label，然后从该 Label 的 'next'
        // 继续（重新进入 Label 本身也可以 —— 它是直通的）。缺失的目标会切断流程（返回 null）而非抛异常；
        // 编辑器校验会暴露这个损坏的跳转。
        NodeInstance ResolveJump(NodeInstance node)
        {
            var target = Param(node, "targetLabel");
            if (string.IsNullOrEmpty(target)) return null;
            return m_Labels.TryGetValue(target, out var label) ? Next(label, "next") : null;
        }

        // ---- 表现层 -----------------------------------------------------------------------------------

        void PresentLine(NodeInstance node)
        {
            var key = Param(node, "lineKey");
            var entry = m_Db != null ? m_Db.Find(key) : null;
            OnLine?.Invoke(new DialogueLineView
            {
                speaker  = entry?.speaker,
                text     = m_Db != null ? m_Db.Resolve(key, m_Lang) : key,   // 无 DB -> 显示 key（可见的缺失）
                portrait = entry?.portrait,
                voice    = entry?.voice
            });
        }

        // 收集这个 Choice 背后的可见选项；呈现它们并挂起，或者 —— #2 —— 若没有任何可见选项，
        // 在接线了 'fallback' 边时沿其前进，否则结束。返回继续行走的下一个节点（fallback 目标
        // 或 null），或在触发 OnChoices 之后返回 null（我们在那里挂起）。对全部被门控的 Choice 绝不死锁。
        NodeInstance PresentChoices(NodeInstance node)
        {
            m_PendingOptions = node.connections.Where(c => c.fromPort == "options")
                .Select(c => Find(c.toInstanceId))
                .Where(o => o != null && KindOf(o) == DialogueNodeKind.Option && OptionVisible(o))
                .ToList();

            if (m_PendingOptions.Count == 0)                                  // #2 没有可见选项
            {
                m_PendingOptions = null;
                var fallback = Next(node, "fallback");
                if (fallback != null) return fallback;                       // 沿 fallback 边前进
                End();                                                       // 否则优雅结束
                return null;
            }

            m_Current = node;
            var views = m_PendingOptions.Select((o, i) => new DialogueOptionView
            {
                index = i,
                text  = m_Db != null ? m_Db.Resolve(Param(o, "optionKey"), m_Lang) : Param(o, "optionKey")
            }).ToList();
            OnChoices?.Invoke(views);
            return null;                                                      // 在该 Choice 处挂起
        }

        // 门控：无 gate 条件单元 => 始终可见；否则按其 Evaluate 决定可见性（与分支判定共用同一套条件单元）。
        bool OptionVisible(NodeInstance option) =>
            !(ParamResolver.ResolveUnit(option, "gate") is ConditionUnit g) || g.Evaluate(UnitCtx(option));

        // ---- 逻辑（委托给可组合单元）------------------------------------------------------------------------

        // 为单元求值组装 NodeContext：blackboard = 本次播放的运行上下文（读写 + 事件出口）。
        NodeContext UnitCtx(NodeInstance node) => new NodeContext { blackboard = m_Ctx, instanceId = node?.instanceId };

        // Condition 节点的谓词：取 predicate 条件单元求值（无单元 => false，分到 false 出口）。
        bool EvalPredicate(NodeInstance node) =>
            ParamResolver.ResolveUnit(node, "predicate") is ConditionUnit c && c.Evaluate(UnitCtx(node));

        // Action 节点：执行 actions 动作单元（一次多个用 Sequence 装饰；条件执行用 ConditionalAction）。
        void RunActions(NodeInstance node)
        {
            if (ParamResolver.ResolveUnit(node, "actions") is ActionUnit a) a.Execute(UnitCtx(node));
        }

        // ---- 图辅助方法 ----------------------------------------------------------------------------------

        DialogueNodeDefinition Def(NodeInstance n) => m_Registry?.Find(n.definitionId) as DialogueNodeDefinition;
        DialogueNodeKind KindOf(NodeInstance n) => Def(n)?.Kind ?? DialogueNodeKind.End;
        string Param(NodeInstance n, string name) { var d = Def(n); return d == null ? null : ParamResolver.Resolve(n, d, name); }
        TypeRef VarType(string key) => m_BB?.Find(key)?.type;

        // #6 针对活动（ACTIVE）图的缓存索引进行 O(1) 实例查找（惰性构建，按图复用）。
        NodeInstance Find(string id) => id != null && m_Index.TryGetValue(id, out var n) ? n : null;
        NodeInstance Next(NodeInstance n, string port) =>
            n == null ? null : Find(n.connections.FirstOrDefault(c => c.fromPort == port)?.toInstanceId);

        // 切换活动图并将 m_Index/m_Labels 指向它的映射表，在首次访问时构建并缓存它们。
        // label 映射（labelName -> Label 实例）支撑 #1 的跳转解析，无需逐跳扫描。
        void SetGraph(NodeGraphAsset g)
        {
            m_Graph = g;
            if (g == null) { m_Index = Empty; m_Labels = Empty; return; }

            if (!m_IndexCache.TryGetValue(g, out m_Index))
            {
                m_Index = new Dictionary<string, NodeInstance>(g.instances.Count);
                foreach (var inst in g.instances) m_Index[inst.instanceId] = inst;
                m_IndexCache[g] = m_Index;
            }
            if (!m_LabelCache.TryGetValue(g, out m_Labels))
            {
                m_Labels = new Dictionary<string, NodeInstance>();
                foreach (var inst in g.instances)
                    if (KindOf(inst) == DialogueNodeKind.Label)
                    {
                        var name = Param(inst, "labelName");
                        if (!string.IsNullOrEmpty(name) && !m_Labels.ContainsKey(name)) m_Labels[name] = inst;   // 名称重复时以第一个为准
                    }
                m_LabelCache[g] = m_Labels;
            }
        }

        // 图的入口节点：若存在则取第一个声明的 entryInstanceId，否则取第一个 Start 节点。
        NodeInstance StartOf(NodeGraphAsset g)
        {
            if (g == null) return null;
            var id = g.entryInstanceIds.FirstOrDefault();
            var entry = id != null ? Find(id) : null;
            return entry ?? g.instances.FirstOrDefault(i => KindOf(i) == DialogueNodeKind.Start);
        }

        // ---- #8 存档 / 读档 ---------------------------------------------------------------------------------
        //
        // DialogueState 捕获恢复所需的一切：指针（currentInstanceId）、blackboard
        // （按相同的解析规则扁平化为字符串条目）以及子对话调用栈。Restore() 重建它们，
        // 并重新呈现（RE-PRESENT）当前节点（重新触发 OnLine/OnChoices），使 UI 在恢复时重绘。
        //
        // 图引用持久化注意事项：NodeGraphAsset 在此无法按值序列化 —— DialogueState 是引擎无关的数据，
        // 而非 UnityEngine.Object 图。Capture() 把活动的 + 入栈的图作为活动对象引用记录进 state 的图引用中，
        // 用于会话内（IN-SESSION）的存/读档（常见场景：在同一次播放会话内快速存档再恢复）。要跨会话
        // （ACROSS sessions）持久化，调用方必须把这些图引用映射为稳定的 asset id（如 GUID），并在读档时
        // 重新解析；DialogueState 的原始 JSON 不会往返复原图指针。instanceId/labelName 值跨会话稳定；
        // 唯一需要外部解析的就是图对象身份（object identity）。
        // （DialogueState / DialogueFrame / BBEntry 是顶层可序列化类型 —— 见本类之下。）

        // 把活动的运行快照成一个可序列化的 DialogueState。blackboard 通过遍历 ASSET 声明的变量来捕获
        // （DialogueBlackboard 按设计只暴露 Get/Set —— 没有全局枚举）：每一个被书写的 key 都经由
        // Blackboard.Get 读回并扁平化为字符串。从未在 asset 中声明的 key（运行时临时写入的）不属于
        // 持久化契约的一部分，因此不会被捕获。
        public DialogueState Capture()
        {
            var s = new DialogueState
            {
                graph = m_Graph,
                currentInstanceId = m_Current?.instanceId
            };
            if (m_BB != null)
                foreach (var v in m_BB.All())
                    s.blackboard.Add(new BBEntry { key = v.key, value = UnitValues.ToInvariantString(Blackboard.Get(v.key)) });
            // m_Stack 是栈顶在先；反转一下，使保存的列表以最外层在先，且 Restore 可按序压栈。
            foreach (var f in m_Stack.Reverse())
                s.stack.Add(new Frame2 { graph = f.graph, returnNodeId = f.returnNodeId });
            return s;
        }

        // 恢复先前捕获的 state：重建 blackboard + 子对话栈 + 指针，然后重新呈现停泊的节点使 UI 重绘。
        // null state，或指针已无法解析的 state，会落到一个干净的已结束 runner，而非抛异常。
        public void Restore(DialogueState state)
        {
            if (state == null) { Run(null); return; }

            m_Visited.Clear();   // 恢复后的运行为调试器开启它自己全新的"走过的路径"

            // blackboard：把每个保存的字符串按其声明的 TypeRef 重新定型（往返复原 int/bool/float）。
            foreach (var e in state.blackboard)
                Blackboard.Set(e.key, ValueParse.To(VarType(e.key), e.value));

            // stack：保存时以最外层在先；压栈使 m_Stack 的栈顶重新成为最内层的调用方。
            m_Stack.Clear();
            foreach (var f in state.stack)
                m_Stack.Push(new Frame { graph = f.graph, returnNodeId = f.returnNodeId });

            SetGraph(state.graph);
            m_PendingOptions = null;
            m_Current = string.IsNullOrEmpty(state.currentInstanceId) ? null : Find(state.currentInstanceId);

            if (m_Current == null) { End(); return; }                        // 指针已失 -> 干净地结束
            m_Visited.Add(m_Current.instanceId);                              // 恢复后的指针同样位于路径上
            switch (KindOf(m_Current))
            {
                case DialogueNodeKind.Line:   PresentLine(m_Current); break; // 重新触发 OnLine
                case DialogueNodeKind.Choice: PresentChoices(m_Current); break; // 重新触发 OnChoices（重建选项）
                default:                      Continue(m_Current); break;    // 停泊在非挂起节点上 -> 继续运行
            }
        }

    }

    // ---- #8 可序列化的存/读档载荷（Runner 在上文承诺的"顶层可序列化类型"）----
    // 一次运行的引擎无关快照：指针（currentInstanceId）、扁平化的 blackboard 以及
    // 子对话调用栈。图引用是活动的 NodeGraphAsset 指针 —— 用于会话内存/读档没问题；
    // 跨会话持久化必须把它们映射为稳定的 asset id（见 Capture() 的图引用注意事项）。
    [Serializable]
    public class DialogueState
    {
        public NodeGraphAsset graph;            // 捕获时的活动图
        public string currentInstanceId;        // 停泊的指针（Line/Choice），结束时为 null
        public List<BBEntry> blackboard = new();// 每一个声明的变量，扁平化为字符串
        public List<Frame2> stack = new();       // 子对话调用栈，最外层在先
    }

    // 一个扁平化的 blackboard 变量（key + 不变量字符串值）。
    [Serializable]
    public class BBEntry { public string key; public string value; }

    // 一个保存的子对话栈帧：调用方图 + 待恢复的 SubDialogue 节点。
    [Serializable]
    public class Frame2 { public NodeGraphAsset graph; public string returnNodeId; }
}
