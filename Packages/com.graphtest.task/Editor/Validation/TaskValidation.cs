using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using NodeEditor;
using NodeEditor.EditorUI;
using TaskEditor;

namespace TaskEditor.EditorUI
{
    [InitializeOnLoad]
    public static class TaskValidation
    {
        static readonly HashSet<TaskNodeKind> DagKinds = new() { TaskNodeKind.Task, TaskNodeKind.Gate };
        static readonly HashSet<TaskNodeKind> StepKinds = new()
        {
            TaskNodeKind.Start, TaskNodeKind.Objective, TaskNodeKind.Condition, TaskNodeKind.Action,
            TaskNodeKind.WaitEvent, TaskNodeKind.Jump, TaskNodeKind.Label,
            TaskNodeKind.Complete, TaskNodeKind.Fail
        };

        static TaskValidation()
        {
            GraphValidator.RegisterExtension("task", CheckAll);
            NodeDefinitionAvailability.Register("task", CheckDefinitionAvailability);
        }

        static string L(string key, string en) => Localizer.UI(key, en);
        static string L(string key, string enFormat, params object[] args) =>
            string.Format(Localizer.UI(key, enFormat), args);

        static NodeAvailabilityVerdict CheckDefinitionAvailability(NodeAvailabilityContext ctx)
        {
            if (ctx.graph == null || ctx.graph.module != TaskGraphScaffold.Module)
                return NodeAvailabilityVerdict.Allow;
            if (ctx.definition is not TaskNodeDefinition taskDefinition)
                return DefinitionUnavailable();

            var allowedKinds = ctx.graph.graphType switch
            {
                GraphType.DependencyDag => DagKinds,
                GraphType.ControlFlow => StepKinds,
                _ => null
            };
            return allowedKinds != null && allowedKinds.Contains(taskDefinition.Kind)
                ? NodeAvailabilityVerdict.Allow
                : DefinitionUnavailable();
        }

        static NodeAvailabilityVerdict DefinitionUnavailable() =>
            NodeAvailabilityVerdict.Deny(L("val.task.definitionUnavailable", "This node is not available in this Task graph."));

        public static IEnumerable<ValidationIssue> CheckAll(NodeGraphAsset g, NodeRegistry reg, BlackboardSet bb)
        {
            if (g == null || reg == null) return Enumerable.Empty<ValidationIssue>();

            var taskInstances = g.instances
                .Select(i => (inst: i, def: reg.Find(i.definitionId) as TaskNodeDefinition))
                .Where(p => p.def != null)
                .ToList();
            if (taskInstances.Count == 0) return Enumerable.Empty<ValidationIssue>();

            var issues = new List<ValidationIssue>();
            if (g.graphType == GraphType.DependencyDag)
            {
                issues.AddRange(CheckSelfEdges(taskInstances));
                issues.AddRange(CheckTaskIds(taskInstances));
                issues.AddRange(CheckStepGraphRefs(taskInstances));
            }
            else if (g.graphType == GraphType.ControlFlow)
            {
                issues.AddRange(CheckSingleStart(taskInstances));
                issues.AddRange(CheckReachability(taskInstances));
                issues.AddRange(CheckJumpTargets(taskInstances));
                issues.AddRange(CheckTerminalReachability(taskInstances));
                issues.AddRange(CheckBlackboardKeyParams(taskInstances, bb));
                issues.AddRange(CheckUnitKeys(taskInstances, bb));
            }

            return issues;
        }

        static IEnumerable<ValidationIssue> CheckSelfEdges(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            foreach (var (inst, _) in instances)
                foreach (var c in inst.connections)
                    if (c.toInstanceId == inst.instanceId)
                        yield return ValidationIssue.Error(inst.instanceId, L("val.task.selfEdge", "task dependency edges must not point back to the same node"));
        }

        static IEnumerable<ValidationIssue> CheckTaskIds(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            var taskIds = new List<(NodeInstance inst, string id)>();
            foreach (var (inst, def) in instances.Where(p => p.def.Kind == TaskNodeKind.Task))
            {
                var id = ParamResolver.Resolve(inst, def, "taskId") ?? "";
                if (string.IsNullOrWhiteSpace(id))
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.taskIdMissing", "Task node has no taskId set"));
                else
                    taskIds.Add((inst, id));
            }

            foreach (var group in taskIds.GroupBy(p => p.id, System.StringComparer.Ordinal).Where(g => g.Count() > 1))
                foreach (var dup in group)
                    yield return ValidationIssue.Error(dup.inst.instanceId, L("val.task.taskIdDuplicate", "duplicate taskId '{0}'", group.Key));
        }

        static IEnumerable<ValidationIssue> CheckStepGraphRefs(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            foreach (var (inst, def) in instances.Where(p => p.def.Kind == TaskNodeKind.Task))
            {
                var value = ParamResolver.ResolveObject(inst, "stepGraph");
                if (value == null) continue;
                if (value is not NodeGraphAsset stepGraph)
                {
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.stepGraphWrongType", "stepGraph must reference a NodeGraphAsset"));
                    continue;
                }
                if (stepGraph.module != TaskGraphScaffold.Module)
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.stepGraphWrongModule", "stepGraph must belong to the task module"));
                if (stepGraph.graphType != GraphType.ControlFlow)
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.stepGraphWrongGraphType", "stepGraph must be a control-flow graph"));
            }
        }

        static IEnumerable<ValidationIssue> CheckSingleStart(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            var starts = instances.Where(p => p.def.Kind == TaskNodeKind.Start).ToList();
            if (starts.Count == 0)
            {
                yield return ValidationIssue.Error(GraphValidator.GraphIssueTarget, L("val.task.noStart", "task step graph has no Start node"));
                yield break;
            }
            foreach (var extra in starts.Skip(1))
                yield return ValidationIssue.Error(extra.inst.instanceId, L("val.task.oneStart", "only one Start node is allowed per task step graph"));
        }

        static IEnumerable<ValidationIssue> CheckReachability(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            var start = instances.FirstOrDefault(p => p.def.Kind == TaskNodeKind.Start).inst;
            if (start == null) yield break;

            var byId = instances.ToDictionary(p => p.inst.instanceId, p => p);
            var labels = LabelsByName(instances);
            var seen = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(start.instanceId);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id)) continue;
                if (!byId.TryGetValue(id, out var current)) continue;
                foreach (var next in Successors(current.inst, current.def, labels))
                    if (!seen.Contains(next)) queue.Enqueue(next);
            }

            foreach (var (inst, _) in instances)
                if (!seen.Contains(inst.instanceId))
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.unreachableStep", "task step node is not reachable from Start"));
        }

        static IEnumerable<ValidationIssue> CheckJumpTargets(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            var labels = LabelsByName(instances);

            foreach (var (inst, def) in instances.Where(p => p.def.Kind == TaskNodeKind.Label))
            {
                var name = ParamResolver.Resolve(inst, def, "labelName") ?? "";
                if (string.IsNullOrWhiteSpace(name))
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.labelMissing", "Label node has no labelName set"));
            }

            foreach (var group in labels.Where(kv => kv.Value.Count > 1))
                foreach (var dup in group.Value)
                    yield return ValidationIssue.Error(dup.instanceId, L("val.task.dupLabel", "duplicate step label '{0}'", group.Key));

            foreach (var (inst, def) in instances.Where(p => p.def.Kind == TaskNodeKind.Jump))
            {
                var target = ParamResolver.Resolve(inst, def, "targetLabel") ?? "";
                if (string.IsNullOrWhiteSpace(target))
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.jumpNoTarget", "Jump has no targetLabel set"));
                else if (!labels.ContainsKey(target))
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.jumpNoMatch", "Jump targetLabel '{0}' matches no Label node", target));
            }
        }

        static IEnumerable<ValidationIssue> CheckTerminalReachability(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            var byId = instances.ToDictionary(p => p.inst.instanceId, p => p);
            var labels = LabelsByName(instances);

            bool CanReachTerminal(string id)
            {
                var seen = new HashSet<string>();
                var queue = new Queue<string>();
                queue.Enqueue(id);

                while (queue.Count > 0)
                {
                    var currentId = queue.Dequeue();
                    if (!seen.Add(currentId)) continue;
                    if (!byId.TryGetValue(currentId, out var current)) continue;
                    if (IsTerminal(current.def.Kind)) return true;
                    foreach (var next in Successors(current.inst, current.def, labels))
                        queue.Enqueue(next);
                }

                return false;
            }

            foreach (var (inst, def) in instances.Where(p => !IsTerminal(p.def.Kind)))
                if (!CanReachTerminal(inst.instanceId))
                    yield return ValidationIssue.Error(inst.instanceId, L("val.task.noTerminal", "no path from this step reaches Complete or Fail"));
        }

        static IEnumerable<ValidationIssue> CheckBlackboardKeyParams(List<(NodeInstance inst, TaskNodeDefinition def)> instances, BlackboardSet bb)
        {
            if (bb == null) yield break;
            foreach (var (inst, def) in instances)
                foreach (var p in def.Parameters.Where(p => p.type?.kind == TypeKind.BlackboardKeyRef))
                {
                    var key = ParamResolver.Resolve(inst, def, p.name);
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!bb.Has(key))
                        yield return ValidationIssue.Warn(inst.instanceId, L("val.task.paramUndefinedKey", "'{0}' references undefined blackboard key '{1}'", p.name, key));
                }
        }

        static IEnumerable<ValidationIssue> CheckUnitKeys(List<(NodeInstance inst, TaskNodeDefinition def)> instances, BlackboardSet bb)
        {
            if (bb == null) yield break;
            foreach (var (inst, _) in instances)
                foreach (var ov in inst.unitOverrides)
                    foreach (var key in CollectKeys(ov?.value))
                        if (!string.IsNullOrEmpty(key) && !bb.Has(key))
                            yield return ValidationIssue.Warn(inst.instanceId, L("val.task.unitUndefinedKey", "unit references undefined blackboard key '{0}'", key));
        }

        static Dictionary<string, List<NodeInstance>> LabelsByName(List<(NodeInstance inst, TaskNodeDefinition def)> instances)
        {
            var labels = new Dictionary<string, List<NodeInstance>>(System.StringComparer.Ordinal);
            foreach (var (inst, def) in instances.Where(p => p.def.Kind == TaskNodeKind.Label))
            {
                var name = ParamResolver.Resolve(inst, def, "labelName") ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!labels.TryGetValue(name, out var list))
                    labels[name] = list = new List<NodeInstance>();
                list.Add(inst);
            }
            return labels;
        }

        static IEnumerable<string> Successors(NodeInstance inst, TaskNodeDefinition def, Dictionary<string, List<NodeInstance>> labels)
        {
            foreach (var c in inst.connections)
                if (!string.IsNullOrEmpty(c.toInstanceId))
                    yield return c.toInstanceId;

            if (def.Kind != TaskNodeKind.Jump) yield break;
            var target = ParamResolver.Resolve(inst, def, "targetLabel") ?? "";
            if (!labels.TryGetValue(target, out var targets)) yield break;
            foreach (var label in targets)
                yield return label.instanceId;
        }

        static bool IsTerminal(TaskNodeKind kind) =>
            kind == TaskNodeKind.Complete || kind == TaskNodeKind.Fail;

        static IEnumerable<string> CollectKeys(Unit unit)
        {
            if (unit == null) yield break;
            foreach (var field in unit.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.IsStatic) continue;
                var type = field.FieldType;
                if (type == typeof(string) && field.GetCustomAttribute<BlackboardKeyAttribute>() != null)
                {
                    yield return (string)field.GetValue(unit);
                }
                else if (typeof(Unit).IsAssignableFrom(type))
                {
                    foreach (var key in CollectKeys((Unit)field.GetValue(unit))) yield return key;
                }
                else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
                         && typeof(Unit).IsAssignableFrom(type.GetGenericArguments()[0])
                         && field.GetValue(unit) is System.Collections.IEnumerable list)
                {
                    foreach (var item in list)
                        foreach (var key in CollectKeys(item as Unit)) yield return key;
                }
            }
        }
    }
}
