using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NodeEditor;
using UnityEngine;

namespace TaskEditor
{
    public class TaskRunner : IRuntimeGraph, IActiveRuntimeGraphSource, IDisposable
    {
        const int MaxSteps = 10000;
        const string InvalidGraphReason = "task.runner.invalidGraph";

        readonly NodeRegistry m_Registry;
        readonly NodeGraphAsset m_TaskGraph;
        readonly BlackboardSet m_BB;
        readonly Dictionary<string, NodeInstance> m_DagIndex = new();
        readonly Dictionary<string, NodeInstance> m_TaskById = new(StringComparer.Ordinal);
        readonly Dictionary<NodeGraphAsset, Dictionary<string, NodeInstance>> m_IndexCache = new();
        readonly Dictionary<NodeGraphAsset, Dictionary<string, NodeInstance>> m_LabelCache = new();
        readonly Dictionary<string, int> m_ObjectiveProgress = new(StringComparer.Ordinal);
        readonly HashSet<string> m_Visited = new();

        static readonly Dictionary<string, NodeInstance> Empty = new();

        Dictionary<string, NodeInstance> m_Index = Empty;
        Dictionary<string, NodeInstance> m_Labels = Empty;
        NodeGraphAsset m_StepGraph;
        NodeInstance m_ActiveTask;
        NodeInstance m_Current;
        bool m_Disposed;

        public static event Action<IRuntimeGraph> OnRunnerCreated;
        public static event Action<IRuntimeGraph> OnRunnerDisposed;

        public event Action<string> OnTaskStarted;
        public event Action<string> OnTaskCompleted;
        public event Action<string, string> OnTaskFailed;
        public event Action<string, string, int, int> OnObjectiveUpdated;
        public event Action<string, string, object> OnCustomEvent;

        public TaskBlackboard Blackboard { get; }
        public TaskJournal Journal { get; } = new();
        public string ActiveTaskId { get; private set; }

        public TaskRunner(NodeRegistry registry, NodeGraphAsset taskGraph, BlackboardSet blackboard)
        {
            m_Registry = registry;
            m_TaskGraph = taskGraph;
            m_BB = blackboard;
            Blackboard = new TaskBlackboard(blackboard);
            BuildDagIndex(taskGraph);
            OnRunnerCreated?.Invoke(this);
        }

        public Status StatusOf(string instanceId)
        {
            if (m_Disposed) return Status.None;
            if (m_Current != null && instanceId == m_Current.instanceId) return Status.Running;
            return m_Visited.Contains(instanceId) ? Status.Success : Status.None;
        }

        public object RuntimeNodeOf(string instanceId) => null;

        public NodeGraphAsset ActiveGraph => !m_Disposed && m_Current != null ? m_StepGraph : null;

        public bool OwnsGraph(NodeGraphAsset graph) =>
            graph != null && (graph == m_TaskGraph || graph == m_StepGraph);

        public bool IsAvailable(string taskId)
        {
            var task = FindTask(taskId);
            if (task == null) return false;
            if (Journal.IsCompleted(taskId) && !IsRepeatable(task)) return false;
            var sources = PrerequisiteSources(task).ToList();
            return sources.Count == 0 || sources.All(s => PrerequisiteSatisfied(s, new HashSet<string>()));
        }

        public bool StartTask(string taskId)
        {
            ThrowIfDisposed();
            if (ActiveTaskId != null || !IsAvailable(taskId)) return false;

            m_ActiveTask = FindTask(taskId);
            ActiveTaskId = taskId;
            m_ObjectiveProgress.Clear();
            m_Visited.Clear();
            m_Current = null;
            OnTaskStarted?.Invoke(taskId);

            var stepGraph = StepGraphOf(m_ActiveTask);
            if (stepGraph == null)
            {
                CompleteActiveTask();
                return true;
            }

            SetGraph(stepGraph);
            Continue(StartOf(stepGraph));
            return true;
        }

        public void ReportObjective(string objectiveId, int amount = 1)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(objectiveId)) return;
            m_ObjectiveProgress.TryGetValue(objectiveId, out var current);
            current += amount;
            m_ObjectiveProgress[objectiveId] = current;

            if (ActiveTaskId == null || m_Current == null || KindOf(m_Current) != TaskNodeKind.Objective)
                return;
            if (!string.Equals(Param(m_Current, "objectiveId"), objectiveId, StringComparison.Ordinal))
                return;

            var required = RequiredCount(m_Current);
            OnObjectiveUpdated?.Invoke(ActiveTaskId, objectiveId, current, required);
            if (current < required) return;

            var from = m_Current;
            m_Current = null;
            Continue(Next(from, "next"));
        }

        public void EmitEvent(string eventId, object payload = null)
        {
            ThrowIfDisposed();
            if (ActiveTaskId == null || m_Current == null || KindOf(m_Current) != TaskNodeKind.WaitEvent)
                return;
            if (!string.Equals(Param(m_Current, "eventId"), eventId, StringComparison.Ordinal))
                return;

            var payloadKey = Param(m_Current, "payloadKey");
            if (!string.IsNullOrWhiteSpace(payloadKey)) Blackboard.Set(payloadKey, payload);
            OnCustomEvent?.Invoke(ActiveTaskId, eventId, payload);

            var from = m_Current;
            m_Current = null;
            Continue(Next(from, "received"));
        }

        public TaskRunnerSnapshot Capture()
        {
            var snapshot = new TaskRunnerSnapshot
            {
                activeTaskId = ActiveTaskId,
                currentStepInstanceId = m_Current?.instanceId
            };
            snapshot.completedTaskIds.AddRange(Journal.CompletedTaskIds);
            snapshot.failedTaskIds.AddRange(Journal.FailedTaskIds);
            foreach (var entry in m_ObjectiveProgress)
                snapshot.objectiveProgress.Add(new TaskObjectiveProgressEntry { objectiveId = entry.Key, current = entry.Value });
            foreach (var id in m_Visited)
                snapshot.visitedStepInstanceIds.Add(id);
            if (m_BB != null)
                foreach (var v in m_BB.All())
                    snapshot.blackboard.Add(new TaskBlackboardEntry { key = v.key, value = UnitValues.ToInvariantString(Blackboard.Get(v.key)) });
            return snapshot;
        }

        public void Restore(TaskRunnerSnapshot snapshot)
        {
            ThrowIfDisposed();
            Journal.Load(snapshot?.completedTaskIds, snapshot?.failedTaskIds);
            m_ObjectiveProgress.Clear();
            m_Visited.Clear();
            m_Current = null;
            m_ActiveTask = null;
            ActiveTaskId = null;

            if (snapshot == null)
            {
                SetGraph(null);
                return;
            }

            foreach (var entry in snapshot.objectiveProgress)
                if (!string.IsNullOrEmpty(entry.objectiveId))
                    m_ObjectiveProgress[entry.objectiveId] = entry.current;
            foreach (var entry in snapshot.blackboard)
                if (!string.IsNullOrEmpty(entry.key))
                    Blackboard.Set(entry.key, UnitValues.To(VarType(entry.key), entry.value));
            var activeTask = FindTask(snapshot.activeTaskId);
            var stepGraph = StepGraphOf(activeTask);
            if (activeTask == null || stepGraph == null || string.IsNullOrEmpty(snapshot.currentStepInstanceId))
            {
                ClearActiveRuntimeState();
                return;
            }

            SetGraph(stepGraph);
            var current = Find(snapshot.currentStepInstanceId);
            if (current == null || !TryKindOf(current, out _))
            {
                ClearActiveRuntimeState();
                return;
            }

            ActiveTaskId = snapshot.activeTaskId;
            m_ActiveTask = activeTask;
            m_Current = current;
            foreach (var id in snapshot.visitedStepInstanceIds)
                if (!string.IsNullOrEmpty(id) && Find(id) != null)
                    m_Visited.Add(id);
            m_Visited.Add(m_Current.instanceId);
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            OnRunnerDisposed?.Invoke(this);
        }

        void ThrowIfDisposed()
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(TaskRunner));
        }

        void Continue(NodeInstance node)
        {
            var steps = 0;
            while (true)
            {
                if (++steps > MaxSteps)
                {
                    Debug.LogError($"TaskRunner: exceeded {MaxSteps} instant steps without parking or finishing.");
                    FailActiveTask("task.runner.stepLimit");
                    return;
                }

                if (node == null)
                {
                    FailActiveTask(InvalidGraphReason);
                    return;
                }

                if (!TryKindOf(node, out var kind))
                {
                    FailActiveTask(InvalidGraphReason);
                    return;
                }

                m_Visited.Add(node.instanceId);
                switch (kind)
                {
                    case TaskNodeKind.Start:
                        node = Next(node, "next");
                        break;
                    case TaskNodeKind.Objective:
                        if (ObjectiveSatisfied(node)) { node = Next(node, "next"); break; }
                        m_Current = node;
                        return;
                    case TaskNodeKind.Condition:
                        node = Next(node, EvaluateCondition(node) ? "true" : "false");
                        break;
                    case TaskNodeKind.Action:
                        RunAction(node);
                        node = Next(node, "next");
                        break;
                    case TaskNodeKind.WaitEvent:
                        m_Current = node;
                        return;
                    case TaskNodeKind.Jump:
                        node = ResolveJump(node);
                        break;
                    case TaskNodeKind.Label:
                        node = Next(node, "next");
                        break;
                    case TaskNodeKind.Complete:
                        CompleteActiveTask();
                        return;
                    case TaskNodeKind.Fail:
                        FailActiveTask(Param(node, "reasonKey"));
                        return;
                    default:
                        FailActiveTask(InvalidGraphReason);
                        return;
                }
            }
        }

        void CompleteActiveTask()
        {
            var taskId = ActiveTaskId;
            if (!string.IsNullOrEmpty(taskId))
            {
                Journal.MarkCompleted(taskId);
                OnTaskCompleted?.Invoke(taskId);
            }
            ActiveTaskId = null;
            m_ActiveTask = null;
            m_Current = null;
        }

        void ClearActiveRuntimeState()
        {
            ActiveTaskId = null;
            m_ActiveTask = null;
            m_Current = null;
            m_Visited.Clear();
            SetGraph(null);
        }

        void FailActiveTask(string reasonKey)
        {
            var taskId = ActiveTaskId;
            if (!string.IsNullOrEmpty(taskId))
            {
                Journal.MarkFailed(taskId);
                OnTaskFailed?.Invoke(taskId, reasonKey);
            }
            ActiveTaskId = null;
            m_ActiveTask = null;
            m_Current = null;
        }

        bool EvaluateCondition(NodeInstance node) =>
            ParamResolver.ResolveUnit(node, "predicate") is ConditionUnit condition && condition.Evaluate(UnitCtx(node));

        void RunAction(NodeInstance node)
        {
            if (ParamResolver.ResolveUnit(node, "actions") is ActionUnit action)
                action.Execute(UnitCtx(node));
        }

        bool ObjectiveSatisfied(NodeInstance node)
        {
            var objectiveId = Param(node, "objectiveId");
            return !string.IsNullOrEmpty(objectiveId)
                   && m_ObjectiveProgress.TryGetValue(objectiveId, out var current)
                   && current >= RequiredCount(node);
        }

        int RequiredCount(NodeInstance node)
        {
            var raw = Param(node, "requiredCount");
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Max(0, value)
                : 1;
        }

        NodeContext UnitCtx(NodeInstance node) => new NodeContext { blackboard = Blackboard, instanceId = node?.instanceId };

        void BuildDagIndex(NodeGraphAsset graph)
        {
            if (graph == null) return;
            foreach (var inst in graph.instances)
            {
                m_DagIndex[inst.instanceId] = inst;
                if (KindOf(inst) != TaskNodeKind.Task) continue;
                var taskId = Param(inst, "taskId");
                if (!string.IsNullOrEmpty(taskId) && !m_TaskById.ContainsKey(taskId))
                    m_TaskById[taskId] = inst;
            }
        }

        NodeInstance FindTask(string taskId) =>
            !string.IsNullOrEmpty(taskId) && m_TaskById.TryGetValue(taskId, out var task) ? task : null;

        NodeGraphAsset StepGraphOf(NodeInstance task) =>
            task == null ? null : ParamResolver.ResolveObject(task, "stepGraph") as NodeGraphAsset;

        bool IsRepeatable(NodeInstance task) =>
            bool.TryParse(Param(task, "repeatable"), out var repeatable) && repeatable;

        IEnumerable<NodeInstance> PrerequisiteSources(NodeInstance target)
        {
            if (target == null || m_TaskGraph == null) yield break;
            foreach (var inst in m_TaskGraph.instances)
                foreach (var connection in inst.connections)
                    if (connection.toInstanceId == target.instanceId && connection.toPort == "prerequisite")
                        yield return inst;
        }

        bool PrerequisiteSatisfied(NodeInstance node, HashSet<string> seen)
        {
            if (node == null || !seen.Add(node.instanceId)) return false;
            try
            {
                switch (KindOf(node))
                {
                    case TaskNodeKind.Task:
                        return Journal.IsCompleted(Param(node, "taskId"));
                    case TaskNodeKind.Gate:
                        var sources = PrerequisiteSources(node).ToList();
                        if (sources.Count == 0) return false;
                        var mode = GateMode(node);
                        return mode == TaskGateMode.Any
                            ? sources.Any(s => PrerequisiteSatisfied(s, seen))
                            : sources.All(s => PrerequisiteSatisfied(s, seen));
                    default:
                        return false;
                }
            }
            finally
            {
                seen.Remove(node.instanceId);
            }
        }

        TaskGateMode GateMode(NodeInstance node) =>
            Enum.TryParse<TaskGateMode>(Param(node, "mode"), out var mode) ? mode : TaskGateMode.All;

        NodeInstance ResolveJump(NodeInstance node)
        {
            var target = Param(node, "targetLabel");
            if (string.IsNullOrEmpty(target)) return null;
            return m_Labels.TryGetValue(target, out var label) ? Next(label, "next") : null;
        }

        void SetGraph(NodeGraphAsset graph)
        {
            m_StepGraph = graph;
            if (graph == null)
            {
                m_Index = Empty;
                m_Labels = Empty;
                return;
            }

            if (!m_IndexCache.TryGetValue(graph, out m_Index))
            {
                m_Index = graph.instances.ToDictionary(i => i.instanceId);
                m_IndexCache[graph] = m_Index;
            }

            if (!m_LabelCache.TryGetValue(graph, out m_Labels))
            {
                m_Labels = new Dictionary<string, NodeInstance>(StringComparer.Ordinal);
                foreach (var inst in graph.instances)
                {
                    if (KindOf(inst) != TaskNodeKind.Label) continue;
                    var label = Param(inst, "labelName");
                    if (!string.IsNullOrEmpty(label) && !m_Labels.ContainsKey(label))
                        m_Labels[label] = inst;
                }
                m_LabelCache[graph] = m_Labels;
            }
        }

        NodeInstance StartOf(NodeGraphAsset graph)
        {
            if (graph == null) return null;
            var entryId = graph.entryInstanceIds.FirstOrDefault();
            var entry = entryId != null ? Find(entryId) : null;
            return entry ?? graph.instances.FirstOrDefault(i => KindOf(i) == TaskNodeKind.Start);
        }

        NodeInstance Find(string instanceId) =>
            instanceId != null && m_Index.TryGetValue(instanceId, out var node) ? node : null;

        NodeInstance Next(NodeInstance node, string port) =>
            node == null ? null : Find(node.connections.FirstOrDefault(c => c.fromPort == port)?.toInstanceId);

        TaskNodeDefinition Def(NodeInstance node) => m_Registry?.Find(node.definitionId) as TaskNodeDefinition;
        bool TryKindOf(NodeInstance node, out TaskNodeKind kind)
        {
            var def = Def(node);
            if (def == null)
            {
                kind = default;
                return false;
            }
            kind = def.Kind;
            return true;
        }
        TaskNodeKind KindOf(NodeInstance node) =>
            TryKindOf(node, out var kind) ? kind : (TaskNodeKind)(-1);
        string Param(NodeInstance node, string name)
        {
            var def = Def(node);
            return def == null ? null : ParamResolver.Resolve(node, def, name);
        }

        TypeRef VarType(string key) => m_BB?.Find(key)?.type;

    }

    [Serializable]
    public class TaskRunnerSnapshot
    {
        public string activeTaskId;
        public string currentStepInstanceId;
        public List<string> completedTaskIds = new();
        public List<string> failedTaskIds = new();
        public List<TaskObjectiveProgressEntry> objectiveProgress = new();
        public List<TaskBlackboardEntry> blackboard = new();
        public List<string> visitedStepInstanceIds = new();
    }

    [Serializable]
    public class TaskObjectiveProgressEntry
    {
        public string objectiveId;
        public int current;
    }

    [Serializable]
    public class TaskBlackboardEntry
    {
        public string key;
        public string value;
    }
}
