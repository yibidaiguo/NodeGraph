// StateMachineValidation.cs — 状态机专属的编辑期校验，通过 Extensions 钩子注册进已冻结的
// NodeEditor.Editor GraphValidator（照 DialogueValidation 成例；框架文件从不在此编辑）。
// GraphDebugger.RevalidateAndPaint 已对每张打开的图调用 GraphValidator.ValidateAll，
// 因此只要在此注册，这些检查就能免费地实时绘制出来。
//
// 覆盖框架内置检查（CheckArity/CheckReachability/CheckBlackboardKeys/CheckEntry）看不到的领域语义
//（SM0 规格「校验规则」节 1–7 条）：
//   1. sm.transition.wiring   — Transition 恰有 ≥1 入 / 1 出；入端只可来自 State/AnyState/SubMachine，
//                               出端只可到 State/SubMachine/Exit（与连接矩阵同一集合，兜底同源——矩阵拦拖拽，
//                               本检查拦「已存在的坏边/跨域误连」）。
//   2. sm.anystate.noincoming — AnyState 无入边（ERROR）。
//   3. sm.submachine.graph    — SubMachine 的 graph 引用非空、module=="statemachine"、含 Entry；
//                               自引用/环引用沿子机链 DFS = ERROR（无限递归）。
//   4. sm.exit.toplevel       — Exit 无出端口，天然由 CheckArity 管，此处不重复。
//   5. sm.state.deadend       — State 无任何出转移 = WARN（"stay forever" 要有意为之）。
//   6. sm.transition.nocondition — 空 condition = 恒真（语义在 [ParamDoc]，不告警）；仅当同一源节点存在
//                               排在恒真转移之后的转移（被挡死、永不触发）时 WARN。
//   7. sm.entry.target        — 由连接矩阵（StateMachineConnectionRules）强制，此处不重复。
// 仅 Editor/ 程序集 —— 本文件无运行时依赖。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using NodeEditor;

namespace StateMachine.EditorUI
{
    [InitializeOnLoad]
    public static class StateMachineValidation
    {
        static StateMachineValidation()
        {
            GraphValidator.RegisterExtension("statemachine", CheckAll);
            // 节点可用性谓词（照 DialogueValidation 成例）：状态机图里只允许放本域节点——
            // 搜索框可见 ≠ 在当前图中合法（Task/VISION 已知偏差的教训），在源头拦别域节点混入。
            NodeAdmission.Register("statemachine", CheckDefinitionAvailability);
        }

        static NodeAvailabilityVerdict CheckDefinitionAvailability(NodeAvailabilityContext ctx)
        {
            if (ctx.graph == null || ctx.graph.module != StateMachineGraphScaffold.Module)
                return NodeAvailabilityVerdict.Allow;
            if (string.IsNullOrEmpty(ctx.definition.Module))
                return NodeAvailabilityVerdict.Allow;
            return ctx.definition is StateMachineNodeDefinition
                ? NodeAvailabilityVerdict.Allow
                : NodeAvailabilityVerdict.Deny(L("val.sm.definitionUnavailable", "This node is not available in a State Machine graph."));
        }

        // 校验消息本地化（与框架 GraphValidator 同一 idiom）：命中兜底表则用之，否则回退内联英文；
        // 带 {0} 占位的用 string.Format 套参数。中文种子在 StateMachineSetup.SetupLocalization 的 val.sm.* 条目（坑#12）。
        static string L(string key, string en) => NodeEditor.EditorUI.Localizer.UI(key, en);
        static string L(string key, string enFormat, params object[] args) => string.Format(NodeEditor.EditorUI.Localizer.UI(key, enFormat), args);

        public static IEnumerable<ValidationIssue> CheckAll(NodeGraphAsset g, NodeRegistry reg, BlackboardSet bb)
        {
            if (g == null || reg == null) return Enumerable.Empty<ValidationIssue>();

            var smInstances = g.instances
                .Select(i => (inst: i, def: reg.Find(i.definitionId) as StateMachineNodeDefinition))
                .Where(p => p.def != null)
                .ToList();
            if (smInstances.Count == 0)
                return Enumerable.Empty<ValidationIssue>();   // 不是状态机图——没有属于我们要检查的内容（域判定照 DialogueValidation）

            // 全图入边索引（含非本域来源——兜底捕获跨域误连；一条连接一条记录，多重边各算一次）。
            var incoming = new Dictionary<string, List<NodeInstance>>();
            foreach (var src in g.instances)
                foreach (var c in src.connections)
                {
                    if (!incoming.TryGetValue(c.toInstanceId, out var list)) incoming[c.toInstanceId] = list = new List<NodeInstance>();
                    list.Add(src);
                }

            var issues = new List<ValidationIssue>();
            issues.AddRange(CheckTransitionWiring(g, reg, smInstances, incoming));
            issues.AddRange(CheckAnyStateNoIncoming(smInstances, incoming));
            issues.AddRange(CheckSubMachineGraphs(g, reg, smInstances));
            issues.AddRange(CheckStateDeadEnds(smInstances));
            issues.AddRange(CheckBlockedTransitions(smInstances));
            return issues;
        }

        // ---- 1. sm.transition.wiring：入出两端的数量与种类（与连接矩阵同一集合，兜底同源）----
        static IEnumerable<ValidationIssue> CheckTransitionWiring(
            NodeGraphAsset g, NodeRegistry reg,
            List<(NodeInstance inst, StateMachineNodeDefinition def)> sm,
            Dictionary<string, List<NodeInstance>> incoming)
        {
            var byId = sm.ToDictionary(p => p.inst.instanceId, p => p);

            foreach (var (inst, def) in sm.Where(p => p.def.Kind == StateMachineNodeKind.Transition))
            {
                // 入端：≥1 条，且来源限 State/AnyState/SubMachine。
                incoming.TryGetValue(inst.instanceId, out var sources);
                if (sources == null || sources.Count == 0)
                {
                    yield return ValidationIssue.Error(inst.instanceId,
                        L("val.sm.transWiring.noIn", "Transition has no incoming edge (needs at least one from State / Any State / Sub Machine)"));
                }
                else
                {
                    foreach (var src in sources)
                    {
                        var srcDef = reg.Find(src.definitionId);
                        var srcKind = (srcDef as StateMachineNodeDefinition)?.Kind;
                        if (srcKind != StateMachineNodeKind.State
                            && srcKind != StateMachineNodeKind.AnyState
                            && srcKind != StateMachineNodeKind.SubMachine)
                            yield return ValidationIssue.Error(inst.instanceId,
                                L("val.sm.transWiring.badSource", "Transition input only accepts State / Any State / Sub Machine (got '{0}')", NameOf(srcDef, src)));
                    }
                }

                // 出端：恰好 1 条，且目标限 State/SubMachine/Exit。
                var outs = inst.connections.Where(c => c.fromPort == "to").ToList();
                if (outs.Count != 1)
                    yield return ValidationIssue.Error(inst.instanceId,
                        L("val.sm.transWiring.outCount", "Transition must have exactly one outgoing edge (has {0})", outs.Count));
                foreach (var c in outs)
                {
                    byId.TryGetValue(c.toInstanceId, out var tgt);
                    var tgtKind = tgt.def?.Kind;
                    if (tgtKind != StateMachineNodeKind.State
                        && tgtKind != StateMachineNodeKind.SubMachine
                        && tgtKind != StateMachineNodeKind.Exit)
                        yield return ValidationIssue.Error(inst.instanceId,
                            L("val.sm.transWiring.badTarget", "Transition can only target State / Sub Machine / Exit (got '{0}')", TargetName(g, reg, c.toInstanceId)));
                }
            }
        }

        // ---- 2. sm.anystate.noincoming：AnyState 是 source-only 伪节点，任何入边都是接错 ----
        static IEnumerable<ValidationIssue> CheckAnyStateNoIncoming(
            List<(NodeInstance inst, StateMachineNodeDefinition def)> sm,
            Dictionary<string, List<NodeInstance>> incoming)
        {
            foreach (var (inst, def) in sm.Where(p => p.def.Kind == StateMachineNodeKind.AnyState))
                if (incoming.TryGetValue(inst.instanceId, out var sources) && sources.Count > 0)
                    yield return ValidationIssue.Error(inst.instanceId,
                        L("val.sm.anystate.noincoming", "Any State must not have incoming edges"));
        }

        // ---- 3. sm.submachine.graph：子图引用非空 + module 核对 + 含 Entry + 自/环引用沿子机链 DFS ----
        static IEnumerable<ValidationIssue> CheckSubMachineGraphs(
            NodeGraphAsset g, NodeRegistry reg,
            List<(NodeInstance inst, StateMachineNodeDefinition def)> sm)
        {
            foreach (var (inst, def) in sm.Where(p => p.def.Kind == StateMachineNodeKind.SubMachine))
            {
                var sub = ParamResolver.ResolveObject(inst, "graph") as NodeGraphAsset;
                if (sub == null)
                {
                    yield return ValidationIssue.Error(inst.instanceId,
                        L("val.sm.submachine.noGraph", "Sub Machine has no sub graph assigned"));
                    continue;
                }
                if (sub.module != "statemachine")
                {
                    yield return ValidationIssue.Error(inst.instanceId,
                        L("val.sm.submachine.wrongModule", "sub graph '{0}' is not a state-machine graph (module must be \"statemachine\")", sub.name));
                    continue;   // 别的模块的图，后续 Entry/环检查按本域语义解读没有意义
                }
                if (!HasEntry(sub, reg))
                    yield return ValidationIssue.Error(inst.instanceId,
                        L("val.sm.submachine.noEntry", "sub graph '{0}' contains no Entry node", sub.name));
                // 自引用（sub == g）与环引用（子机链回到任一祖先图）统一用带路径栈的 DFS 捕获。
                var path = new HashSet<NodeGraphAsset> { g };
                if (HasCycle(sub, reg, path))
                    yield return ValidationIssue.Error(inst.instanceId,
                        L("val.sm.submachine.cycle", "sub-machine chain forms a cycle: sub graph '{0}' leads back to an ancestor graph (infinite recursion)", sub.name));
            }
        }

        static bool HasEntry(NodeGraphAsset g, NodeRegistry reg) =>
            g.instances.Any(i => (reg.Find(i.definitionId) as StateMachineNodeDefinition)?.Kind == StateMachineNodeKind.Entry);

        // 沿子机链的路径栈 DFS：path 含所有祖先图，撞见祖先即成环。图数量很小，朴素递归足够。
        static bool HasCycle(NodeGraphAsset sub, NodeRegistry reg, HashSet<NodeGraphAsset> path)
        {
            if (sub == null) return false;
            if (path.Contains(sub)) return true;
            path.Add(sub);
            foreach (var inst in sub.instances)
            {
                if ((reg.Find(inst.definitionId) as StateMachineNodeDefinition)?.Kind != StateMachineNodeKind.SubMachine) continue;
                if (HasCycle(ParamResolver.ResolveObject(inst, "graph") as NodeGraphAsset, reg, path)) return true;
            }
            path.Remove(sub);
            return false;
        }

        // ---- 5. sm.state.deadend：State 无任何出转移 = WARN（终态设计要有意为之——Total handling）----
        static IEnumerable<ValidationIssue> CheckStateDeadEnds(List<(NodeInstance inst, StateMachineNodeDefinition def)> sm)
        {
            foreach (var (inst, def) in sm.Where(p => p.def.Kind == StateMachineNodeKind.State))
                if (!inst.connections.Any(c => c.fromPort == "transitions"))
                    yield return ValidationIssue.Warn(inst.instanceId,
                        L("val.sm.state.deadend", "state has no outgoing transition - the machine stays here forever (ignore if this is an intentional terminal state)"));
        }

        // ---- 6. sm.transition.nocondition：空 condition = 恒真是合法语义，本身不告警；只有当同一源节点
        // 存在排在恒真转移之后的转移（按 priority 升序、同 priority 按连接序求值——被挡死、永不触发）时才 WARN。----
        static IEnumerable<ValidationIssue> CheckBlockedTransitions(List<(NodeInstance inst, StateMachineNodeDefinition def)> sm)
        {
            var byId = sm.ToDictionary(p => p.inst.instanceId, p => p);

            foreach (var (srcInst, srcDef) in sm.Where(p =>
                         p.def.Kind == StateMachineNodeKind.State
                         || p.def.Kind == StateMachineNodeKind.AnyState
                         || p.def.Kind == StateMachineNodeKind.SubMachine))
            {
                // 该源的出向转移，按运行时求值序排列：priority 升序，同 priority 按 connections 列表序（OrderBy 稳定排序）。
                var ordered = srcInst.connections
                    .Where(c => c.fromPort == "transitions")
                    .Select(c => byId.TryGetValue(c.toInstanceId, out var t) && t.def.Kind == StateMachineNodeKind.Transition ? t : default)
                    .Where(t => t.inst != null)
                    .OrderBy(t => PriorityOf(t.inst, t.def))
                    .ToList();

                int firstAlways = ordered.FindIndex(t => ParamResolver.ResolveUnit(t.inst, "condition") == null);
                if (firstAlways < 0) continue;
                for (int i = firstAlways + 1; i < ordered.Count; i++)
                    yield return ValidationIssue.Warn(ordered[i].inst.instanceId,
                        L("val.sm.transition.nocondition", "this transition never fires: an always-true (empty condition) transition from '{0}' is evaluated before it", NameOf(srcDef, srcInst)));
            }
        }

        static int PriorityOf(NodeInstance inst, NodeDefinition def) =>
            int.TryParse(ParamResolver.Resolve(inst, def, "priority"), out var p) ? p : 0;

        // 消息里点名节点：优先实例改名（displayName），否则按当前编辑器语言取定义显示名；定义缺失回退 definitionId。
        static string NameOf(NodeDefinition def, NodeInstance inst)
        {
            if (!string.IsNullOrEmpty(inst?.displayName)) return inst.displayName;
            return def != null ? NodeEditor.EditorUI.Localizer.NodeName(def) : inst?.definitionId ?? "?";
        }

        // 目标可能是跨域误连的非本域节点，故在全图实例里找（而非只在本域列表）。
        static string TargetName(NodeGraphAsset g, NodeRegistry reg, string instanceId)
        {
            var inst = g.instances.FirstOrDefault(i => i.instanceId == instanceId);
            return NameOf(inst != null ? reg.Find(inst.definitionId) : null, inst);
        }
    }
}
