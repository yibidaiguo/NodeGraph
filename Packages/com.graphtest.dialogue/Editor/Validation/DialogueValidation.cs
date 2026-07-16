// DialogueValidation.cs — 对话专属的编辑期校验，通过 Extensions 钩子注册进已冻结的
// NodeEditor.Editor GraphValidator（GraphValidator.cs 本身从不在此处
// 编辑）。GraphDebugger.RevalidateAndPaint 已经会对每个打开的 graph 调用
// GraphValidator.ValidateAll，因此只要在此注册，这些检查就能免费地实时绘制出来。
//
// 覆盖通用检查看不到的部分：
//   - 单个 Start 节点（通用的 CheckEntry 只验证 entryInstanceIds 非空且
//     可解析；它没有把 "Start" 视为对话专属单例种类的概念）。
//   - Jump -> Label 解析（DialogueRunner.ResolveJump 按 labelName 字符串匹配，而非按
//     instance id/port 连线，因此 CheckArity 和 CheckReachability 都无法捕获拼写错误或
//     重复的 label 名称）。
//   - BBKey 类型的参数（如 Jump 之外那些动态按 instance 解析的 key）从不在 NodeDefinition 的
//     静态 Reads/Writes 列表中声明，因此通用的 CheckBlackboardKeys 从不会看到它们。
//   - 可组合单元（unitOverrides）内部用 [BlackboardKey] 标记的键（如 BlackboardCompareCondition.key、
//     SetVariable*Action.key）藏在多态单元树里——CheckUnitKeys 递归遍历并校验它们引用的变量是否已声明。
// 仅 Editor/ 程序集 —— 本文件无运行时依赖。

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Dialogue;
using NodeEditor;

namespace Dialogue.EditorUI
{
    [InitializeOnLoad]
    public static class DialogueValidation
    {
        static DialogueValidation()
        {
            GraphValidator.RegisterExtension("dialogue", CheckAll);
            NodeAdmission.Register("dialogue", CheckDefinitionAvailability);
        }

        static NodeAvailabilityVerdict CheckDefinitionAvailability(NodeAvailabilityContext ctx)
        {
            if (ctx.graph == null || ctx.graph.module != DialogueGraphScaffold.Module)
                return NodeAvailabilityVerdict.Allow;
            if (string.IsNullOrEmpty(ctx.definition.Module))
                return NodeAvailabilityVerdict.Allow;
            return ctx.definition is DialogueNodeDefinition
                ? NodeAvailabilityVerdict.Allow
                : NodeAvailabilityVerdict.Deny(L("val.dialogue.definitionUnavailable", "This node is not available in a Dialogue graph."));
        }

        // 校验消息本地化（与框架 GraphValidator 同一 idiom）：命中兜底表则用之，否则回退内联英文；带 {0} 占位的用 string.Format 套参数。
        // 中文种子在 DialogueSetup.SetupLocalization 的 val.* 条目。面向作者的可见文案一律走 Localizer（开发规范 C11）。
        static string L(string key, string en) => NodeEditor.EditorUI.Localizer.UI(key, en);
        static string L(string key, string enFormat, params object[] args) => string.Format(NodeEditor.EditorUI.Localizer.UI(key, enFormat), args);

        public static IEnumerable<ValidationIssue> CheckAll(NodeGraphAsset g, NodeRegistry reg, BlackboardSet bb)
        {
            if (g == null || reg == null) return Enumerable.Empty<ValidationIssue>();

            var dialogueInstances = g.instances
                .Select(i => (inst: i, def: reg.Find(i.definitionId) as DialogueNodeDefinition))
                .Where(p => p.def != null)
                .ToList();
            if (dialogueInstances.Count == 0)
                return Enumerable.Empty<ValidationIssue>();   // 不是对话 graph —— 没有属于我们要检查的内容

            var issues = new List<ValidationIssue>();
            issues.AddRange(CheckSingleStart(dialogueInstances));
            issues.AddRange(CheckJumpTargets(dialogueInstances));
            issues.AddRange(CheckBBKeyParams(dialogueInstances, bb));
            issues.AddRange(CheckUnitKeys(dialogueInstances, bb));
            return issues;
        }

        static IEnumerable<ValidationIssue> CheckSingleStart(List<(NodeInstance inst, DialogueNodeDefinition def)> instances)
        {
            var starts = instances.Where(p => p.def.Kind == DialogueNodeKind.Start).ToList();
            if (starts.Count == 0)
            {
                yield return ValidationIssue.Error(GraphValidator.GraphIssueTarget, L("val.noStart", "dialogue graph has no Start node"));
                yield break;
            }
            foreach (var extra in starts.Skip(1))
                yield return ValidationIssue.Error(extra.inst.instanceId, L("val.oneStart", "only one Start node is allowed per dialogue graph"));
        }

        // 与 DialogueRunner 的 ResolveJump/label-map 保持一致：按名称对 Label 分组（重复名称
        // 在运行时会悄悄地“先到先得”——将其标记出来），然后检查每个 Jump 的 targetLabel 能否解析。
        static IEnumerable<ValidationIssue> CheckJumpTargets(List<(NodeInstance inst, DialogueNodeDefinition def)> instances)
        {
            var issues = new List<ValidationIssue>();
            var byName = new Dictionary<string, List<NodeInstance>>();
            foreach (var (inst, def) in instances.Where(p => p.def.Kind == DialogueNodeKind.Label))
            {
                var name = ParamResolver.Resolve(inst, def, "labelName") ?? "";
                if (!byName.TryGetValue(name, out var list)) byName[name] = list = new List<NodeInstance>();
                list.Add(inst);
            }
            foreach (var kv in byName)
                if (kv.Value.Count > 1)
                    foreach (var dup in kv.Value)
                        issues.Add(ValidationIssue.Warn(dup.instanceId,
                            L("val.dupLabel", "duplicate label name '{0}' - Jump resolution silently picks the first match", kv.Key)));

            foreach (var (inst, def) in instances.Where(p => p.def.Kind == DialogueNodeKind.Jump))
            {
                var target = ParamResolver.Resolve(inst, def, "targetLabel") ?? "";
                if (string.IsNullOrEmpty(target))
                    issues.Add(ValidationIssue.Error(inst.instanceId, L("val.jumpNoTarget", "Jump has no targetLabel set")));
                else if (!byName.ContainsKey(target))
                    issues.Add(ValidationIssue.Error(inst.instanceId, L("val.jumpNoMatch", "Jump targetLabel '{0}' matches no Label node", target)));
            }
            return issues;
        }

        static IEnumerable<ValidationIssue> CheckBBKeyParams(List<(NodeInstance inst, DialogueNodeDefinition def)> instances, BlackboardSet bb)
        {
            if (bb == null) yield break;
            foreach (var (inst, def) in instances)
                foreach (var p in def.Parameters.Where(p => p.type?.kind == TypeKind.BlackboardKeyRef))
                {
                    var key = ParamResolver.Resolve(inst, def, p.name);
                    if (string.IsNullOrEmpty(key)) continue;   // 空键 = 该可选黑板引用未设置（合法，非拼写错误）
                    if (!bb.Has(key))
                        yield return ValidationIssue.Warn(inst.instanceId, L("val.paramUndefinedKey", "'{0}' references undefined blackboard key '{1}'", p.name, key));
                }
        }

        // 递归遍历每个实例的可组合单元树（unitOverrides），收集所有 [BlackboardKey] 字段引用的键，
        // 校验其是否为已声明的黑板变量（空键跳过 = 未设置，并非拼写错误）。
        static IEnumerable<ValidationIssue> CheckUnitKeys(List<(NodeInstance inst, DialogueNodeDefinition def)> instances, BlackboardSet bb)
        {
            if (bb == null) yield break;
            foreach (var (inst, _) in instances)
                foreach (var ov in inst.unitOverrides)
                    foreach (var key in CollectKeys(ov?.value))
                        if (!string.IsNullOrEmpty(key) && !bb.Has(key))
                            yield return ValidationIssue.Warn(inst.instanceId, L("val.unitUndefinedKey", "unit references undefined blackboard key '{0}'", key));
        }

        // 收集一棵单元树里所有 [BlackboardKey] string 字段的值（含嵌套单元 / 单元列表）。
        static IEnumerable<string> CollectKeys(Unit u)
        {
            if (u == null) yield break;
            foreach (var f in u.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.IsStatic) continue;
                var ft = f.FieldType;
                if (ft == typeof(string) && f.GetCustomAttribute<BlackboardKeyAttribute>() != null)
                {
                    yield return (string)f.GetValue(u);
                }
                else if (typeof(Unit).IsAssignableFrom(ft))
                {
                    foreach (var k in CollectKeys((Unit)f.GetValue(u))) yield return k;
                }
                else if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>)
                         && typeof(Unit).IsAssignableFrom(ft.GetGenericArguments()[0])
                         && f.GetValue(u) is System.Collections.IEnumerable list)
                {
                    foreach (var item in list)
                        foreach (var k in CollectKeys(item as Unit)) yield return k;
                }
            }
        }
    }
}
