// RuntimeGraphLocator.cs — 按当前 NodeGraphAsset 从全部 live runner 匹配 IRuntimeGraphSource。
// 拆自 EditorSupport.cs（B3 内聚拆分：一类型一文件；类型代码逐字未改）。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{

    // 定位项目的 IRuntimeGraph 实现，供调试器在播放模式下挂接。
    // 由项目提供（例如一个持有运行中 graph 的 MonoBehaviour）。占位 locator 的形态：
    public static class RuntimeGraphLocator
    {
        public static IRuntimeGraph Find()
        {
            // 项目特定：查找当前活动的运行时 graph（例如通过已注册的服务或场景对象）。
            // 没有任何东西在运行时返回 null；此时调试器只是不显示任何实时状态。
            return RuntimeGraphRegistry.Current;
        }

        public static IRuntimeGraph Find(NodeGraphAsset graph)
        {
            if (graph == null) return null;
            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                if (RuntimeGraphRegistry.Live[i] is IActiveRuntimeGraphSource source &&
                    source.ActiveGraph == graph && source.OwnsGraph(graph))
                    return RuntimeGraphRegistry.Live[i];
            }
            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                if (RuntimeGraphRegistry.Live[i] is IActiveRuntimeGraphSource source &&
                    source.ActiveGraph != null && source.OwnsGraph(graph))
                    return RuntimeGraphRegistry.Live[i];
            }
            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                var runtime = RuntimeGraphRegistry.Live[i];
                if (runtime is IRuntimeGraphSource source && source.OwnsGraph(graph) &&
                    HasRunningNode(runtime, graph))
                    return runtime;
            }
            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                var runtime = RuntimeGraphRegistry.Live[i];
                if (runtime is IRuntimeGraphSource source && source.OwnsGraph(graph))
                    return runtime;
            }
            return null;
        }

        // Fast path for runtimes that can name their current graph directly. A null report
        // means the runtime owns graphs but is not currently executing one.
        public static NodeGraphAsset FindReportedActiveGraph(
            string module,
            NodeGraphAsset preferred = null)
        {
            if (string.IsNullOrEmpty(module)) return null;
            if (preferred != null)
            {
                for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
                    if (TryReportedActiveGraph(RuntimeGraphRegistry.Live[i], module, out _, out var active) &&
                        active == preferred)
                        return active;

                for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
                    if (TryReportedActiveGraph(RuntimeGraphRegistry.Live[i], module, out var source, out var active) &&
                        source.OwnsGraph(preferred))
                        return active;
            }

            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                if (TryReportedActiveGraph(RuntimeGraphRegistry.Live[i], module, out _, out var active))
                    return active;
            }
            return null;
        }

        public static NodeGraphAsset FindReportedActiveGraph(
            IRuntimeGraph runtime,
            string module)
        {
            if (runtime == null || string.IsNullOrEmpty(module) ||
                !RuntimeGraphRegistry.Live.Contains(runtime)) return null;
            return TryReportedActiveGraph(runtime, module, out _, out var active)
                ? active
                : null;
        }

        static bool TryReportedActiveGraph(
            IRuntimeGraph runtime,
            string module,
            out IActiveRuntimeGraphSource source,
            out NodeGraphAsset active)
        {
            source = runtime as IActiveRuntimeGraphSource;
            active = source?.ActiveGraph;
            return active != null && source.OwnsGraph(active) &&
                string.Equals(active.module, module, StringComparison.Ordinal);
        }

        // 模块入口只提供模块名和兜底图；框架统一从所有同模块资产中定位当前运行图。
        public static NodeGraphAsset FindActiveGraph(string module, NodeGraphAsset preferred = null)
        {
            if (string.IsNullOrEmpty(module)) return preferred;
            var reported = FindReportedActiveGraph(module, preferred);
            if (reported != null) return reported;
            return FindActiveGraph(FindModuleGraphs(module, preferred), preferred);
        }

        internal static List<NodeGraphAsset> FindModuleGraphs(
            string module,
            NodeGraphAsset preferred = null)
        {
            var candidates = AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                .Select(guid => AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(graph => graph != null && string.Equals(graph.module, module, StringComparison.Ordinal))
                .Concat(Resources.FindObjectsOfTypeAll<NodeGraphAsset>().Where(graph =>
                    graph != null && string.Equals(graph.module, module, StringComparison.Ordinal)))
                .Distinct()
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ThenBy(graph => graph.name, StringComparer.Ordinal)
                .ToList();
            if (preferred != null && !candidates.Contains(preferred)) candidates.Insert(0, preferred);
            return candidates;
        }

        // 先选确有 Running 节点的图；若运行器暂时没有 Running 节点，再按所有权安全回退。
        public static NodeGraphAsset FindActiveGraph(
            IEnumerable<NodeGraphAsset> candidates,
            NodeGraphAsset preferred = null)
        {
            var graphs = (candidates ?? Enumerable.Empty<NodeGraphAsset>())
                .Where(graph => graph != null)
                .Distinct()
                .ToList();
            if (preferred != null && !graphs.Contains(preferred)) graphs.Insert(0, preferred);

            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                if (RuntimeGraphRegistry.Live[i] is not IActiveRuntimeGraphSource source) continue;
                var active = source.ActiveGraph;
                if (active != null && graphs.Contains(active) && source.OwnsGraph(active)) return active;
            }

            var preferredRuntime = Find(preferred);
            if (HasRunningNode(preferredRuntime, preferred)) return preferred;

            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                var runtime = RuntimeGraphRegistry.Live[i];
                if (runtime is not IRuntimeGraphSource source) continue;
                var running = graphs.FirstOrDefault(graph =>
                    source.OwnsGraph(graph) && HasRunningNode(runtime, graph));
                if (running != null) return running;
            }

            if (preferredRuntime != null) return preferred;
            for (int i = RuntimeGraphRegistry.Live.Count - 1; i >= 0; i--)
            {
                if (RuntimeGraphRegistry.Live[i] is not IRuntimeGraphSource source) continue;
                var owned = graphs.FirstOrDefault(source.OwnsGraph);
                if (owned != null) return owned;
            }
            return preferred;
        }

        static bool HasRunningNode(IRuntimeGraph runtime, NodeGraphAsset graph) =>
            runtime != null && graph != null && graph.instances.Any(instance =>
                instance != null && runtime.StatusOf(instance.instanceId) == Status.Running);
    }
}
