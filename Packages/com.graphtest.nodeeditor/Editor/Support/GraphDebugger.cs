// GraphDebugger.cs — 第 5 层（连线图编辑器），模板级别。
// 实时调试器：状态着色、运行中高亮、监视值、断点、
// 每个节点的运行时视图、编辑期校验标记。读取第 4 层运行时 + 第 3 层校验；
// 除非通过显式的交互入口，否则绝不改动执行过程。改编自 Behavior Designer。Editor/ 程序集。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层的数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{
    public class GraphDebugger
    {
        readonly GraphCanvas m_Canvas;
        readonly Dictionary<string, NodeView> m_ByInstance = new();
        IRuntimeGraph m_Runtime;          // 第 4 层运行中的图（接口见下文）
        IRuntimeGraphTrace m_Trace;       // 同一个执行器的可选轨迹面（没实现就是 null，功能自动降级）
        // instanceId -> 本次运行中被走到的次数。每帧从 m_Trace.Trace 重建，只收当前这张图的访问。
        readonly Dictionary<string, int> m_VisitCounts = new();
        BlackboardSet m_Blackboard;       // 这张图的有效黑板（全局⊕模块⊕组），用于 blackboard-key 校验；由 5d 在加载时设置
        bool m_Hooked;

        // 本次校验的问题计数，供工具栏状态条显示。
        public int ErrorCount { get; private set; }
        public int WarnCount { get; private set; }
        // 出问题的节点（错误在前、警告在后，同一节点只记一次）。顶栏状态 chip 靠它逐个跳过去 ——
        // 只报"2 错误"而不说错在哪，等于让人自己在几十个节点里找红边。
        readonly List<string> m_ProblemInstances = new();
        public IReadOnlyList<string> ProblemInstanceIds => m_ProblemInstances;
        // 每次重新校验完成后广播，让工具栏刷新状态条。
        public System.Action OnValidated;

        public GraphDebugger(GraphCanvas canvas) => m_Canvas = canvas;

        // 5d 调用此方法，使 blackboard-key 校验（4c CheckBlackboardKeys）真正运行起来。
        public void SetBlackboard(BlackboardSet bb) => m_Blackboard = bb;

        public void IndexViews(IEnumerable<NodeView> views)
        {
            m_ByInstance.Clear();
            foreach (var v in views) m_ByInstance[v.Instance.instanceId] = v;
        }

        // --- 编辑期校验（#6）：运行第 3 层算法（第 4 层实现），绘制标记 ---
        public void RevalidateAndPaint()
        {
            // 先从实时 canvas 重新同步索引，这样按变更触发的校验（通过 OnGraphChanged 增删的
            // 节点）会标记到正确的视图上——而不仅仅是加载时捕获的那一组。
            IndexViews(m_Canvas.nodes.ToList().ConvertAll(n => (NodeView)n));
            foreach (var v in m_ByInstance.Values) v.ClearValidationMarks();
            ErrorCount = 0; WarnCount = 0;
            m_ProblemInstances.Clear();
            var problemWarns = new List<string>();
            if (m_Canvas.Asset == null || m_Canvas.Registry == null) { m_Canvas.SetBanner(null); OnValidated?.Invoke(); return; }
            var graphLevel = new List<string>();
            foreach (var issue in GraphValidator.ValidateAll(m_Canvas.Asset, m_Canvas.Registry, m_Blackboard))
            {
                bool isError = issue.severity == ValidationIssue.Sev.Error;
                if (isError) ErrorCount++;
                else WarnCount++;
                if (issue.target != GraphValidator.GraphIssueTarget)
                {
                    var bucket = isError ? m_ProblemInstances : problemWarns;
                    if (!bucket.Contains(issue.target)) bucket.Add(issue.target);
                }
                if (issue.target == GraphValidator.GraphIssueTarget)
                {
                    // 图级别的问题（例如没有入口）：没有节点可标记——把它收集到画布内的 banner 里，
                    // 而不是用 Debug.LogWarning，这样正常编写一个新图时不会刷屏控制台。
                    graphLevel.Add(issue.message);
                    continue;
                }
                if (m_ByInstance.TryGetValue(issue.target, out var view))
                    view.MarkValidation(issue.severity == ValidationIssue.Sev.Error
                        ? ValidationSeverity.Error : ValidationSeverity.Warn);   // 把 4c 的 Sev 映射为 5a 本地枚举
            }
            // 警告接在错误后面：巡回时先把挡住运行的错误走完，再看不致命的警告。
            foreach (var warn in problemWarns)
                if (!m_ProblemInstances.Contains(warn)) m_ProblemInstances.Add(warn);
            m_Canvas.SetBanner(graphLevel);
            OnValidated?.Invoke();
        }

        // --- 为 #1-#5 接入 play 模式 ---
        public void AttachRuntime(IRuntimeGraph runtime)
        {
            m_Runtime = runtime;
            m_Trace = runtime as IRuntimeGraphTrace;   // 可选契约：老执行器不实现就退回纯快照着色
            m_VisitCounts.Clear();
            if (!m_Hooked) { EditorApplication.update += OnEditorUpdate; m_Hooked = true; }
        }
        public void DetachRuntime()
        {
            m_Runtime = null;
            m_Trace = null;
            m_VisitCounts.Clear();
            if (m_Hooked) { EditorApplication.update -= OnEditorUpdate; m_Hooked = false; }
            foreach (var v in m_ByInstance.Values) v.SetStatusClass(null);
        }

        // 某个节点在本次运行中被走到过几次（0 = 没走到过）。
        // 公开是为了给检视/提示留一个接缝：轨迹能回答「这个节点被绕了几圈」，而 StatusOf 回答不了。
        public int VisitCountOf(string instanceId) =>
            instanceId != null && m_VisitCounts.TryGetValue(instanceId, out var n) ? n : 0;

        // 从轨迹重建「走到过几次」的计数表。graphId 非空时只收该图的访问——三个执行器都会下钻子图，
        // 画布上显示的是其中一张，把子图的节点排除在外。
        // 静态纯函数：着色决策要能在 EditMode 里直接测，而 OnEditorUpdate 只在 play 模式跑。
        public static void BuildVisitCounts(IRuntimeGraphTrace trace, string graphId, Dictionary<string, int> into)
        {
            if (into == null) return;
            into.Clear();
            if (trace?.Trace == null) return;
            foreach (var visit in trace.Trace)
            {
                if (string.IsNullOrEmpty(visit.InstanceId)) continue;
                if (!string.IsNullOrEmpty(graphId) && visit.GraphId != graphId) continue;
                into.TryGetValue(visit.InstanceId, out var n);
                into[visit.InstanceId] = n + 1;
            }
        }

        // 一个节点这一帧该挂哪个状态 class。
        // 关键的一条是 None + 走过 -> "status-visited"：快照说「现在不在这儿」，轨迹说「来过」。
        // 状态机尤其需要——它的 StatusOf 只把当前活动路径报成 Running，已退出的状态全部是 None，
        // 没有轨迹的话整段历史在画布上是不可见的。
        // 刻意**不**把走过的节点标成 status-success：走过不等于成功，状态机的历史状态既非成功也非失败。
        public static string StatusClassOf(Status status, bool visited) => status switch
        {
            Status.Success => "status-success",
            Status.Failure => "status-failure",
            Status.Running => "status-running",
            Status.None    => visited ? "status-visited" : "status-inactive",
            _              => visited ? "status-visited" : "status-inactive"
        };

        void OnEditorUpdate()
        {
            if (!Application.isPlaying || m_Runtime == null) return;

            BuildVisitCounts(m_Trace, m_Canvas.Asset != null ? m_Canvas.Asset.graphId : null, m_VisitCounts);

            foreach (var kv in m_ByInstance)
            {
                var view = kv.Value;
                var status = m_Runtime.StatusOf(kv.Key);          // 第 4 层暴露每个节点的状态

                // #1 状态着色 + #2 运行中高亮（running class 在 USS 里承载脉冲动画）。
                // status-visited 由轨迹补齐：快照报 None、但本次运行走到过的节点不该和从未到达的节点同色。
                view.SetStatusClass(StatusClassOf(status, m_VisitCounts.ContainsKey(kv.Key)));

                // #5 每个节点的运行时视图（例如 Wait 进度条）
                if (view.AttachedControl != null)
                {
                    var runtimeNode = m_Runtime.RuntimeNodeOf(kv.Key);
                    if (runtimeNode != null) view.AttachedControl.OnRuntimeUpdate(runtimeNode);
                }

                // #4 断点：当被标记的节点变为 Running 时暂停
                if (status == Status.Running && BreakpointStore.Has(kv.Key))
                    Debug.Break();
            }
            // #3 监视值由 inspector/变量面板轮询实时 blackboard 来刷新。
        }
    }


    // 断点按 instance id 存储（不放进 NodeInstance 的序列化数据；仅编辑器使用）。
    // 由 SessionState 支撑，使其能在进入 play 模式触发的 domain reload 后存活——否则
    // 在编辑模式下设置的断点会恰好在 play 模式（其唯一消费者）开始时被清掉。
    //（SessionState 会在编辑器重启时清空，这正是临时调试断点合适的生命周期范围。）
    public static class BreakpointStore
    {
        const string k_Key = "NodeEditor.Breakpoints";
        static HashSet<string> s_Set;
        static HashSet<string> Set => s_Set ??= Load();
        static HashSet<string> Load()
        {
            var raw = UnityEditor.SessionState.GetString(k_Key, "");
            return raw.Length == 0 ? new HashSet<string>() : new HashSet<string>(raw.Split(';'));
        }
        static void Save() => UnityEditor.SessionState.SetString(k_Key, string.Join(";", Set));
        public static bool Has(string instanceId) => Set.Contains(instanceId);
        public static void Toggle(string instanceId)
        { if (!Set.Add(instanceId)) Set.Remove(instanceId); Save(); }
    }
}
