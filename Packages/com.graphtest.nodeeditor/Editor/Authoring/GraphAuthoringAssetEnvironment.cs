using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace NodeEditor.EditorUI
{
    internal static class GraphAuthoringAssetEnvironment
    {
        public static string NormalizeExplicitPath(string path, string diagnosticPath, List<GraphAuthoringDiagnostic> diagnostics)
        {
            string normalized = ProjectAssetPaths.NormalizeAssetPath(path);
            if (!string.Equals(path, normalized, StringComparison.Ordinal) ||
                !ProjectAssetPaths.IsProjectAssetPath(normalized) ||
                !string.Equals(Path.GetExtension(normalized), ".asset", StringComparison.OrdinalIgnoreCase))
            {
                Add(diagnostics, "asset.path.invalid", diagnosticPath,
                    "资产路径必须是已规范化、位于 Assets/... 下且以 .asset 结尾的显式路径。");
                return null;
            }
            return normalized;
        }

        public static UnitAuthoringCatalog Units(string module, List<GraphAuthoringDiagnostic> diagnostics)
        {
            try
            {
                if (string.IsNullOrEmpty(module)) return GraphAuthoringModuleRegistry.CreateCoreUnitCatalog();
                if (!GraphAuthoringModuleRegistry.TryGet(module, out _))
                {
                    Add(diagnostics, "asset.module.unregistered", "$.module", $"模块 '{module}' 未注册创作扩展。");
                    return null;
                }
                return GraphAuthoringModuleRegistry.CreateUnitCatalog(module);
            }
            catch (Exception ex)
            {
                Add(diagnostics, "asset.unit-catalog.invalid", "$catalog.units", ex.Message);
                return null;
            }
        }

        public static NodeRegistry Registry(List<GraphAuthoringDiagnostic> diagnostics)
        {
            var paths = NodeEditorAssetPathsLocator.Find();
            if (paths == null)
            {
                Add(diagnostics, "asset.paths.missing", "$project.nodeEditorAssetPaths",
                    "项目必须且只能存在一份 NodeEditorAssetPaths；读取不会自动创建。");
                return null;
            }
            string path = ProjectAssetPaths.NormalizeAssetPath(paths.registryPath);
            if (!ProjectAssetPaths.IsProjectAssetPath(path))
            {
                Add(diagnostics, "asset.registry.path.invalid", "$project.registryPath", "NodeRegistry 配置路径无效。");
                return null;
            }
            var registry = AssetDatabase.LoadAssetAtPath<NodeRegistry>(path);
            if (registry == null)
                Add(diagnostics, "asset.registry.missing", "$project.registryPath", $"配置路径 '{path}' 没有 NodeRegistry。");
            return registry;
        }

        public static IReadOnlyList<BlackboardAsset> EffectiveBlackboards(
            string module,
            string group,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var result = new List<BlackboardAsset>();
            var paths = NodeEditorAssetPathsLocator.Find();
            if (paths == null)
            {
                Add(diagnostics, "asset.paths.missing", "$project.nodeEditorAssetPaths",
                    "项目必须且只能存在一份 NodeEditorAssetPaths；读取不会自动创建。");
                return result;
            }

            string globalPath = ProjectAssetPaths.NormalizeAssetPath(paths.globalBlackboardPath);
            if (!ProjectAssetPaths.IsProjectAssetPath(globalPath))
                Add(diagnostics, "asset.blackboard.global-path.invalid", "$project.globalBlackboardPath", "全局黑板配置路径无效。");
            else
            {
                var global = AssetDatabase.LoadAssetAtPath<BlackboardAsset>(globalPath);
                if (global != null)
                {
                    if (global.Module.Length != 0 || global.Group.Length != 0)
                        Add(diagnostics, "asset.blackboard.global-scope.invalid", globalPath, "配置的全局黑板必须具有空 module/group。");
                    else result.Add(global);
                }
            }

            if (!string.IsNullOrEmpty(module))
            {
                AddUniqueLayer(module, string.Empty, result, diagnostics);
                if (!string.IsNullOrEmpty(group)) AddUniqueLayer(module, group, result, diagnostics);
            }
            return result;
        }

        public static GraphAuthoringCatalog Catalog(
            string module,
            NodeRegistry registry,
            UnitAuthoringCatalog units,
            IEnumerable<IBlackboardDecl> blackboards,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (registry == null || units == null) return null;
            try
            {
                return GraphAuthoringCatalogBuilder.Build(
                    module ?? string.Empty,
                    registry.universal.Concat(registry.projectDomain)
                        .Select(definition => definition?.ToSchema()),
                    units,
                    blackboards ?? Array.Empty<IBlackboardDecl>());
            }
            catch (Exception ex)
            {
                Add(diagnostics, "asset.catalog.invalid", "$catalog", ex.Message);
                return null;
            }
        }

        public static bool IsUnderRegisteredRoot(string module, string assetPath)
        {
            if (string.IsNullOrEmpty(module)) return false;
            return GraphAuthoringModuleRegistry.ResolveGraphRoots(module).Any(root =>
                ProjectAssetPaths.IsProjectAssetPath(root) &&
                assetPath.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal));
        }

        public static IReadOnlyList<BlackboardAsset> AllBlackboards() =>
            Array.AsReadOnly(AssetDatabase.FindAssets("t:BlackboardAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(ProjectAssetPaths.IsProjectAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<BlackboardAsset>)
                .Where(asset => asset != null)
                .ToArray());

        public static void Add(
            List<GraphAuthoringDiagnostic> diagnostics,
            string code,
            string path,
            string message) => diagnostics.Add(new GraphAuthoringDiagnostic(code, path, message));

        static void AddUniqueLayer(
            string module,
            string group,
            List<BlackboardAsset> result,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var matches = AllBlackboards()
                .Where(asset => string.Equals(asset.Module, module, StringComparison.Ordinal) &&
                                string.Equals(asset.Group, group, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length > 1)
            {
                Add(diagnostics, "asset.blackboard.scope.ambiguous", "$.blackboards",
                    $"黑板作用域 '{module}/{group}' 存在多个资产，必须先消除歧义。");
                return;
            }
            if (matches.Length == 1) result.Add(matches[0]);
        }
    }
}
