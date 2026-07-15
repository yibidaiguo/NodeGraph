// GraphValidator.cs — 子层 4c（节点校验），编辑期/加载期校验。
// 仅依赖 4a 数据类型（不依赖 4b runtime——校验绝不触及执行）。
// 实现 layer-3 validation-logic.md。命名空间 NodeEditor。
// 可放在 Editor/ 程序集（校验属于创作层关注点），若需要在加载时使用也可放在 Runtime。

using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    public struct ValidationIssue
    {
        public enum Sev { Error, Warn }
        public Sev severity;
        public string target;     // 该问题所附着的 instanceId（或 edge id）
        public string message;
        public static ValidationIssue Error(string t, string m) => new ValidationIssue { severity = Sev.Error, target = t, message = m };
        public static ValidationIssue Warn (string t, string m) => new ValidationIssue { severity = Sev.Warn,  target = t, message = m };
    }

    // 每个方法转写对应的 layer-3 算法；ValidateAll 负责聚合。
    public static class GraphValidator
    {
        // 领域规则挂钩（缝，B10）：领域层按稳定 id 注册额外检查，无需改框架；ValidateAll 在内置检查之后跑它们。
        // 用私有字典封装而非裸 public List——同 id 重复注册覆盖并告警（捕获两个领域误用同一 id）；
        // [InitializeOnLoad] 跨域重载时静态状态重置、各领域静态构造各注册一次，故正常重载不刷告警。
        static readonly Dictionary<string, System.Func<NodeGraphAsset, NodeRegistry, BlackboardSet, IEnumerable<ValidationIssue>>> s_Extensions = new();

        public static void RegisterExtension(string id, System.Func<NodeGraphAsset, NodeRegistry, BlackboardSet, IEnumerable<ValidationIssue>> check)
        {
            if (string.IsNullOrEmpty(id) || check == null) return;
            if (s_Extensions.ContainsKey(id))
                UnityEngine.Debug.LogWarning($"NodeEditor: validation extension '{id}' already registered; overwriting.");
            s_Extensions[id] = check;
        }

        // 校验消息本地化：命中兜底表（当前编辑器语言）则用之，否则回退内联英文；带 {0} 占位的用 string.Format 套入参数。
        // 中文种子在 DialogueSetup.SetupLocalization 的 val.* 条目（add-if-missing）。面向作者的可见文案一律走 Localizer（开发规范 C11）。
        static string L(string key, string en) => NodeEditor.EditorUI.Localizer.UI(key, en);
        static string L(string key, string enFormat, params object[] args) => string.Format(NodeEditor.EditorUI.Localizer.UI(key, enFormat), args);

        // 聚合入口点——编辑器（5c）调用它来执行“校验并标注”。
        // bb 是 blackboard-key 检查所必需的（这张图的有效黑板：全局⊕模块⊕组合并视图）；没有它则跳过该检查。
        public static List<ValidationIssue> ValidateAll(NodeGraphAsset g, NodeRegistry reg, BlackboardSet bb = null)
        {
            var issues = new List<ValidationIssue>();
            if (g == null || reg == null) return issues;   // 没有 graph + registry 就无从校验
            issues.AddRange(CheckDefinitionAvailability(g, reg));
            issues.AddRange(CheckArity(g, reg));
            issues.AddRange(CheckConnectionRules(g, reg));
            issues.AddRange(CheckReachability(g, reg));
            issues.AddRange(CheckBlackboardKeys(g, reg, bb));
            // single-role/purity 规则针对 tick-tree/dataflow/dag。在 control-flow（dialogue、quests、
            // cutscenes）中 Control/Action 节点合法地携带路由/比较数据，因此此处跳过。
            if (g.graphType != GraphType.ControlFlow)
                issues.AddRange(CheckSingleRole(g, reg));
            issues.AddRange(CheckEntry(g, reg));
            // 类型相关的检查，按 graphType 分派：
            // acyclic 类型不得包含环；dataflow 还会对每条边做类型检查。
            if (g.graphType == GraphType.Dataflow || g.graphType == GraphType.TickTree
                || g.graphType == GraphType.DependencyDag)
                issues.AddRange(CheckCycle(g, reg));
            if (g.graphType == GraphType.TickTree)
                issues.AddRange(CheckTickTreeShape(g, reg));   // 单根 + 严格树
            if (g.graphType == GraphType.Dataflow)
                issues.AddRange(CheckEdgeTypes(g, reg));
            foreach (var ext in s_Extensions.Values)
                issues.AddRange(ext(g, reg, bb) ?? Enumerable.Empty<ValidationIssue>());
            return issues;
        }

        public static List<ValidationIssue> CheckDefinitionAvailability(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            foreach (var instance in g.instances)
            {
                var verdict = NodeDefinitionAvailability.Evaluate(g, reg.Find(instance.definitionId));
                if (!verdict.allowed)
                    issues.Add(ValidationIssue.Error(instance.instanceId, verdict.reason));
            }
            return issues;
        }

        public static List<ValidationIssue> CheckArity(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            foreach (var inst in g.instances)
            {
                var def = reg.Find(inst.definitionId);
                if (def == null) { issues.Add(ValidationIssue.Error(inst.instanceId, L("val.missingDef", "missing definition"))); continue; }
                // 按方向分别计数。一个 input port 的连接是到达它的边
                //（别人的 fromPort -> 本节点 toPort）；一个 output port 的连接是从它出发的边（本节点 fromPort）。
                // 若共用一个计数，当 in-port 和 out-port 同名时会错配（两者常常都叫 "value"）。
                foreach (var port in def.InputPorts)
                {
                    int count = g.instances.SelectMany(i => i.connections)
                                           .Count(c => c.toPort == port.name && c.toInstanceId == inst.instanceId);
                    if (!port.arity.Satisfies(count))
                        issues.Add(ValidationIssue.Error(inst.instanceId,
                            L("val.inArity", "input port '{0}' arity {1} violated (got {2})", port.name, port.arity.kind, count)));
                }
                foreach (var port in def.OutputPorts)
                {
                    int count = inst.connections.Count(c => c.fromPort == port.name);
                    if (!port.arity.Satisfies(count))
                        issues.Add(ValidationIssue.Error(inst.instanceId,
                            L("val.outArity", "output port '{0}' arity {1} violated (got {2})", port.name, port.arity.kind, count)));
                }

                // 同时强制执行那些重述了端口规则的声明式 Constraints（layer-3：ChildArity 即
                //“children 端口上的 arity”；PortType 断言某端口的期望类型）。这里正是
                // ChildArity / PortType 约束子类型的消费方，因而所有五种 Constraint 种类
                //（这两种 + RequiresEntryReachable + Custom + ReachabilitySeed）都有真实的消费者
                //（ReachabilitySeed 的消费方在 CheckReachability——把声明实例并进播种集）。
                foreach (var con in def.Constraints)
                {
                    if (con is ChildArity ca)
                    {
                        int count = inst.connections.Count(c => c.fromPort == ca.portName)
                                  + g.instances.SelectMany(i => i.connections)
                                               .Count(c => c.toPort == ca.portName && c.toInstanceId == inst.instanceId);
                        if (!ca.arity.Satisfies(count))
                            issues.Add(ValidationIssue.Error(inst.instanceId,
                                L("val.childArity", "ChildArity on port '{0}' violated (got {1})", ca.portName, count)));
                    }
                    else if (con is PortType pt)
                    {
                        var port = def.InputPorts.Concat(def.OutputPorts).FirstOrDefault(p => p.name == pt.portName);
                        if (port != null && !TypeRefCompat.Compatible(port.type, pt.type))
                            issues.Add(ValidationIssue.Error(inst.instanceId,
                                L("val.portType", "port '{0}' type does not match its PortType constraint", pt.portName)));
                    }
                }
            }
            return issues;
        }

        // 连接规则（include/exclude）——节点种类层面的“哪种能接哪种”，由领域层经
        // ConnectionRules.RegisterRule 注册（端口类型 TypeRefCompat / 数量 CheckArity 都管不到这一层）。
        // 实时拖拽（5a GetCompatiblePorts）已挡掉新拉的非法连线；此处兜底那些绕过实时检查留下的边：
        // 复制粘贴、规则上线前创作的老图、拖动端点重连。对每条违规边在源实例上报 Error，
        // 消息由规则给出（说清两端为何不能连）。两条路径共用 ConnectionRules.Evaluate，判定永远一致。
        public static List<ValidationIssue> CheckConnectionRules(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            foreach (var inst in g.instances)
            {
                var fromDef = reg.Find(inst.definitionId);
                if (fromDef == null) continue;   // 缺失定义已由 CheckArity 报告，不在此重复
                foreach (var c in inst.connections)
                {
                    var toInst = g.instances.FirstOrDefault(i => i.instanceId == c.toInstanceId);
                    var toDef = toInst != null ? reg.Find(toInst.definitionId) : null;
                    if (toDef == null) continue;
                    var verdict = ConnectionRules.Evaluate(fromDef, c.fromPort, toDef, c.toPort);
                    if (!verdict.allowed)
                        issues.Add(ValidationIssue.Error(inst.instanceId, verdict.reason));
                }
            }
            return issues;
        }

        public static List<ValidationIssue> CheckReachability(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            // 从 entry 出发的正向可达性仅适用于 entry 驱动的类型。Dataflow 是 pull
            // 模型——它的“死内容”是没有任何 sink 消费的节点（从 outputs 反向 BFS），属于
            // 不同的规则——因此此处跳过，而不是把每个节点都标为不可达。（CheckUnusedNodes TODO。）
            if (g.graphType != GraphType.ControlFlow && g.graphType != GraphType.TickTree
                && g.graphType != GraphType.DependencyDag)
                return issues;
            var seen = new HashSet<string>();
            // entryInstanceIds 可能为 null（尤其是 dependency-dag，它没有 entry 概念）；
            // 像 CheckEntry/CheckTickTreeShape 那样做防护，而不是在 Queue 构造函数中抛异常。
            // dependency-dag 没有 entry：它的根是没有入边的节点（与
            // CheckTickTreeShape 的 parentCount 同一概念），因此从这些节点而非空的 entry 列表来播种。
            // 播种：若设置了显式 entry 列表则用它；否则用 graph 的根节点（无入边）。
            // 在没有 entry 列表时从根节点播种，可让一个新创作的
            // control-flow / tick-tree / dag graph（一个 Start 下面接好了节点）的可达性变得有意义，而不是把
            // 每个节点都因空播种而标为不可达。（原本仅用于 DependencyDag；现已统一。）
            IEnumerable<string> seed = (g.entryInstanceIds != null && g.entryInstanceIds.Count > 0)
                ? g.entryInstanceIds
                : RootInstanceIds(g);
            // 并上所有「定义声明了 ReachabilitySeed 约束」的实例——source-only 播种源
            //（如状态机的 AnyState 伪节点：无入边、不在 entry 列表里，但其出向内容是活的，
            // 不应被标成 dead content）。这里正是 ReachabilitySeed 约束子类型的消费方。
            var seedSet = new HashSet<string>(seed);
            foreach (var inst2 in g.instances)
            {
                var def2 = reg.Find(inst2.definitionId);
                if (def2 != null && def2.Constraints.Any(c => c is ReachabilitySeed))
                    seedSet.Add(inst2.instanceId);
            }
            var queue = new Queue<string>(seedSet);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id)) continue;
                var inst = g.instances.FirstOrDefault(i => i.instanceId == id);
                if (inst == null) continue;
                foreach (var c in inst.connections) queue.Enqueue(c.toInstanceId);
            }
            foreach (var inst in g.instances)
            {
                if (seen.Contains(inst.instanceId)) continue;
                // 声明了 RequiresEntryReachable 的节点在不可达时是 ERROR（layer-3 严重度
                // 表）；否则不可达只是 WARN。这里正是 RequiresEntryReachable
                // 约束子类型的消费方——[SerializeReference] Constraint 层次结构的存在就是为了让 4c 能基于它分派。
                var def = reg.Find(inst.definitionId);
                bool requires = def != null && def.Constraints.Any(c => c is RequiresEntryReachable);
                issues.Add(requires
                    ? ValidationIssue.Error(inst.instanceId, L("val.unreachableRequired", "unreachable node declared RequiresEntryReachable"))
                    : ValidationIssue.Warn(inst.instanceId, L("val.unreachable", "unreachable node (dead content)")));
            }
            return issues;
        }

        public static List<ValidationIssue> CheckBlackboardKeys(NodeGraphAsset g, NodeRegistry reg, BlackboardSet bb = null)
        {
            var issues = new List<ValidationIssue>();
            if (bb == null) return issues;
            foreach (var inst in g.instances)
            {
                var def = reg.Find(inst.definitionId);
                if (def == null) continue;
                foreach (var refk in def.Reads.Concat(def.Writes))
                    if (!bb.Has(refk.key))
                        issues.Add(ValidationIssue.Warn(inst.instanceId, L("val.undefinedKey", "undefined blackboard key '{0}'", refk.key)));
            }
            return issues;
        }

        // Entry 存在性——entry 是旁列表 NodeGraphAsset.entryInstanceIds（layer-3 决策），
        // 而非每个节点上的标志。只有 entry 驱动的 graph 类型才需要 entry：control-flow 和 tick-tree。
        // Dataflow（pull-evaluate）和 dependency-dag（topological）没有 entry 概念，故跳过它们。
        public static List<ValidationIssue> CheckEntry(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            if (g.graphType != GraphType.ControlFlow && g.graphType != GraphType.TickTree)
                return issues;                                   // dataflow / dag：无需 entry
            if (g.entryInstanceIds == null || g.entryInstanceIds.Count == 0)
            {
                // 没有显式 entry 列表。对于 control-flow，我们从 graph 的根
                // 节点（无入边）推导出 entry，于是一个正常创作的 dialogue——一个 Start 下面
                // 接好了所有节点——无需作者手工维护 entryInstanceIds 即可通过校验。
                // 空 graph 不需要 entry；单个根就是 entry；多个根则有歧义
                //（把它们接到一个 entry 之下）；非空 graph 上零个根意味着每个节点都有
                // 入边（一个无头环），此时确实没有 entry。这些都是 graph 级别的
                // 问题（GraphIssueTarget），因此编辑器把它们路由到横幅，而不是 console 警告。
                if (g.graphType == GraphType.ControlFlow)
                {
                    if (g.instances.Count == 0) return issues;          // 空 graph：无可进入之处
                    var roots = RootInstanceIds(g).ToList();
                    if (roots.Count == 1) return issues;                // 单个根即为隐含的 entry
                    issues.Add(roots.Count == 0
                        ? ValidationIssue.Error(GraphIssueTarget, L("val.noEntryHeadless", "graph has no entry node (every node has an incoming edge — a headless cycle)"))
                        : ValidationIssue.Error(GraphIssueTarget, L("val.noSingleEntry", "graph has no single entry: {0} root nodes have no incoming edge — connect them under one entry, or designate an entry", roots.Count)));
                    return issues;
                }
                // tick-tree：需要一个显式的单根（CheckTickTreeShape 会强制校验数量）；
                // 此处空的 entry 列表是错误，而不会被推导。
                issues.Add(ValidationIssue.Error(GraphIssueTarget, L("val.noEntryEmpty", "graph has no entry node (entryInstanceIds is empty)")));
                return issues;
            }
            foreach (var id in g.entryInstanceIds)
                if (!g.instances.Any(i => i.instanceId == id))
                    issues.Add(ValidationIssue.Error(id, L("val.entryMissing", "entryInstanceIds references a missing instance")));
            return issues;
        }

        // 用于 graph 级（而非 node 级）问题的哨兵 target。编辑器检查到它后
        // 会把消息路由到横幅，而不是去查找某个节点视图。
        public const string GraphIssueTarget = "__graph__";

        // 根实例 = 那些没有入边的实例。它是 control-flow graph 的天然 entry，也是
        // 在未设置显式 entryInstanceIds 列表时的可达性播种来源。
        static IEnumerable<string> RootInstanceIds(NodeGraphAsset g)
        {
            var hasIncoming = new HashSet<string>(g.instances.SelectMany(i => i.connections).Select(c => c.toInstanceId));
            return g.instances.Select(i => i.instanceId).Where(id => !hasIncoming.Contains(id));
        }

        // tick-tree 形状（layer-3 validation-logic.md）：一棵 behavior tree 必须 (1) 恰好有一个根，
        // 且 (2) 是严格树——每个非根节点恰好有一个父节点。CheckCycle 已经排除了
        // back-edge；此处补上“共享子节点”的情况（父节点 >1），它不是环但同样非法。
        public static List<ValidationIssue> CheckTickTreeShape(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            if (g.graphType != GraphType.TickTree) return issues;

            // (1) 恰好一个根——根即 entry 旁列表（layer-3 entry 决策）
            if (g.entryInstanceIds == null || g.entryInstanceIds.Count != 1)
                issues.Add(ValidationIssue.Error(GraphIssueTarget,
                    L("val.tickOneRoot", "tick-tree must have exactly one root (has {0})", g.entryInstanceIds?.Count ?? 0)));

            // (2) 严格树——任何节点都不得有多于一个父节点
            var parentCount = new Dictionary<string, int>();
            foreach (var inst in g.instances)
                foreach (var c in inst.connections)
                    parentCount[c.toInstanceId] = parentCount.TryGetValue(c.toInstanceId, out var n) ? n + 1 : 1;
            foreach (var kv in parentCount)
                if (kv.Value > 1)
                    issues.Add(ValidationIssue.Error(kv.Key,
                        L("val.tickStrictTree", "tick-tree must be a strict tree, but this node has {0} parents", kv.Value)));
            return issues;
        }

        // 针对 acyclic graph 类型（dependency-dag、tick-tree、dataflow）的环检测。
        // 三色 DFS（layer-3 detectCycle）：指向 Gray（在栈上）节点的 back-edge 即为环。
        // 报告闭合环路的那个实例，以便编辑器高亮它。
        public static List<ValidationIssue> CheckCycle(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            var color = new Dictionary<string, int>();   // 0=White, 1=Gray, 2=Black
            foreach (var inst in g.instances) color[inst.instanceId] = 0;

            bool Dfs(string id)
            {
                color[id] = 1;                            // Gray：在栈上
                var inst = g.instances.FirstOrDefault(i => i.instanceId == id);
                if (inst != null)
                    foreach (var c in inst.connections)
                    {
                        if (!color.TryGetValue(c.toInstanceId, out var col)) continue;
                        if (col == 1) { issues.Add(ValidationIssue.Error(id, L("val.cycle", "cycle: this node's edge closes a loop (acyclic graph type)"))); return true; }
                        if (col == 0 && Dfs(c.toInstanceId)) return true;
                    }
                color[id] = 2;                            // Black：已完成
                return false;
            }

            foreach (var inst in g.instances)
                if (color[inst.instanceId] == 0 && Dfs(inst.instanceId))
                    break;                                 // 报告找到的第一个环
            return issues;
        }

        // dataflow 的边类型兼容性（layer-3 checkEdgeTypes）。Any 类型的端口跳过；
        // 否则源 output 类型必须与目标 input 类型兼容。
        public static List<ValidationIssue> CheckEdgeTypes(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            foreach (var inst in g.instances)
            {
                var fromDef = reg.Find(inst.definitionId);
                if (fromDef == null) continue;
                foreach (var c in inst.connections)
                {
                    var outPort = fromDef.OutputPorts.FirstOrDefault(p => p.name == c.fromPort);
                    var toInst = g.instances.FirstOrDefault(i => i.instanceId == c.toInstanceId);
                    if (toInst == null) continue;
                    var toDef = reg.Find(toInst.definitionId);
                    var inPort = toDef?.InputPorts.FirstOrDefault(p => p.name == c.toPort);
                    if (outPort?.type == null || inPort?.type == null) continue;
                    if (!TypeRefCompat.Compatible(outPort.type, inPort.type))
                        issues.Add(ValidationIssue.Error(inst.instanceId,
                            L("val.edgeType", "edge type mismatch: output '{0}' not compatible with input '{1}'", c.fromPort, c.toPort)));
                }
            }
            return issues;
        }

        // Single-role 检查——强制执行 node-schema.md 的“blackboardWrites by role”表
        // 以及“一个节点、一种角色”（layer-2 第 7 点）。违反意味着该节点承担了两项
        // 职责，应当拆分。
        public static List<ValidationIssue> CheckSingleRole(NodeGraphAsset g, NodeRegistry reg)
        {
            var issues = new List<ValidationIssue>();
            foreach (var inst in g.instances)
            {
                var def = reg.Find(inst.definitionId);
                if (def == null) continue;
                switch (def.Role)
                {
                    case NodeRole.Provider:
                        if (def.Writes.Count > 0)
                            issues.Add(ValidationIssue.Error(inst.instanceId, L("val.providerNoWrite", "Provider must not write blackboard (it has blackboardWrites)")));
                        break;
                    case NodeRole.Condition:
                        if (def.Writes.Count > 0)
                            issues.Add(ValidationIssue.Error(inst.instanceId, L("val.conditionNoWrite", "Condition must not write blackboard")));
                        if (def.OutputPorts.Count != 1)                       // 强制“恰好一个 outputPort”
                            issues.Add(ValidationIssue.Error(inst.instanceId,
                                L("val.conditionOneOut", "Condition must have exactly one output port (has {0})", def.OutputPorts.Count)));
                        else if (!(def.OutputPorts[0].type?.kind == TypeKind.Primitive &&
                                   def.OutputPorts[0].type?.primitive == PrimitiveType.Bool))
                            issues.Add(ValidationIssue.Error(inst.instanceId, L("val.conditionBoolOut", "Condition's single output must be Bool")));
                        break;
                    case NodeRole.Control:
                        if (def.Writes.Count > 0)
                            issues.Add(ValidationIssue.Error(inst.instanceId, L("val.controlNoWrite", "Control must not write blackboard")));
                        if (def.Parameters.Count > 0)
                            issues.Add(ValidationIssue.Warn(inst.instanceId, L("val.controlNoParams", "Control should carry no data parameters")));
                        break;
                    case NodeRole.Action:
                        if (def.Writes.Count == 0 && def.Purity != NodePurity.Domain)
                            issues.Add(ValidationIssue.Warn(inst.instanceId, L("val.actionNoEffect", "pure-logic Action with no blackboardWrites has no effect")));
                        break;
                }
            }
            return issues;
        }
    }
}
