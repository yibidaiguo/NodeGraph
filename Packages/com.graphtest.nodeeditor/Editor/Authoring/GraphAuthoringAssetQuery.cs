using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace NodeEditor.EditorUI
{
    internal static class GraphAuthoringAssetQuery
    {
        public static GraphAuthoringCatalogResult Describe(string module)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            if (module == null)
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.module.missing", "$.module", "module 不能为 null；通用目录请传空串。");
            var units = module == null ? null : GraphAuthoringAssetEnvironment.Units(module, diagnostics);
            var registry = GraphAuthoringAssetEnvironment.Registry(diagnostics);
            var catalog = GraphAuthoringAssetEnvironment.Catalog(
                module, registry, units, GraphAuthoringAssetEnvironment.AllBlackboards(), diagnostics);
            return new GraphAuthoringCatalogResult(
                diagnostics.Count == 0 ? catalog : null,
                Array.AsReadOnly(diagnostics.ToArray()));
        }

        public static GraphAuthoringListResult List(string module)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            if (module == null)
            {
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.module.missing", "$.module", "module 不能为 null。");
                return Failure(diagnostics);
            }
            if (module.Length == 0)
                return new GraphAuthoringListResult(
                    Array.AsReadOnly(Array.Empty<GraphAuthoringGraphInfo>()),
                    Array.AsReadOnly(diagnostics.ToArray()));
            if (!GraphAuthoringModuleRegistry.TryGet(module, out _))
            {
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.module.unregistered", "$.module", $"模块 '{module}' 未注册创作扩展。");
                return Failure(diagnostics);
            }

            var roots = GraphAuthoringModuleRegistry.ResolveGraphRoots(module)
                .Where(ProjectAssetPaths.IsProjectAssetPath)
                .Where(AssetDatabase.IsValidFolder)
                .ToArray();
            if (roots.Length == 0)
                return new GraphAuthoringListResult(
                    Array.AsReadOnly(Array.Empty<GraphAuthoringGraphInfo>()),
                    Array.AsReadOnly(diagnostics.ToArray()));

            var graphs = AssetDatabase.FindAssets("t:NodeGraphAsset", roots)
                .Select(guid => new { Guid = guid, Path = AssetDatabase.GUIDToAssetPath(guid) })
                .Where(item => GraphAuthoringAssetEnvironment.IsUnderRegisteredRoot(module, item.Path))
                .Select(item => new { item.Guid, item.Path, Graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(item.Path) })
                .Where(item => item.Graph != null && string.Equals(item.Graph.module, module, StringComparison.Ordinal))
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .Select(item => new GraphAuthoringGraphInfo(item.Path, item.Guid, item.Graph))
                .ToArray();

            var duplicate = graphs.GroupBy(graph => graph.GraphId, StringComparer.Ordinal)
                .FirstOrDefault(group => string.IsNullOrEmpty(group.Key) || group.Count() > 1);
            if (duplicate != null)
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph-id.duplicate", "$.graphs",
                    $"模块 '{module}' 存在重复或空 graphId。请先修复资产身份。");
            return diagnostics.Count == 0
                ? new GraphAuthoringListResult(Array.AsReadOnly(graphs), Array.AsReadOnly(diagnostics.ToArray()))
                : Failure(diagnostics);
        }

        static GraphAuthoringListResult Failure(List<GraphAuthoringDiagnostic> diagnostics) =>
            new(null, Array.AsReadOnly(diagnostics.ToArray()));
    }
}
