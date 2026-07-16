using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public static class NodeEditorAssetPathsLocator
    {
        const string ModuleName = "NodeEditor";
        static NodeEditorAssetPaths s_Cached;
        static bool s_Resolved;

        static NodeEditorAssetPathsLocator()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        public static NodeEditorAssetPaths FindOrCreate()
        {
            if (s_Resolved) return s_Cached;
            s_Resolved = true;
            s_Cached = ProjectAssetPaths.FindOrCreate<NodeEditorAssetPaths>("NodeEditor", ApplyDefaults);
            return s_Cached;
        }

        static void Invalidate()
        {
            s_Cached = null;
            s_Resolved = false;
            EditorLocalizationLocator.Invalidate();
        }

        public static void OpenAssetPaths() =>
            ProjectAssetPaths.Open<NodeEditorAssetPaths>("NodeEditor", ApplyDefaults);

        internal static string DefaultLanguageOptionsPathForCurrentRoot()
        {
            return DefaultLanguageOptionsPathForRoot(ProjectAssetPaths.ContentRoot(ModuleName));
        }

        // Kept as a test seam: installation path must never influence the project-owned bootstrap location.
        static string DefaultBootstrapPathForScriptPath(string _)
        {
            return ProjectAssetPaths.BootstrapPath<NodeEditorAssetPaths>();
        }

        // Kept as a test seam: caller/package roots are intentionally ignored.
        internal static void ApplyDefaults(NodeEditorAssetPaths cfg)
        {
            var root = ProjectAssetPaths.ContentRoot(ModuleName);
            cfg.nodeDefinitionsRootDir = $"{root}/Nodes/Definitions";
            cfg.registryPath = $"{root}/Nodes/NodeRegistry.asset";
            cfg.globalBlackboardPath = $"{root}/Blackboards/GlobalBlackboard.asset";
            cfg.localizationTablePath = $"{root}/Localization/LocalizationTable.asset";
            cfg.editorLocalizationConfigPath = $"{root}/Config/EditorLocalizationConfig.asset";
            cfg.runtimeLocalizationConfigPath = $"{root}/Config/RuntimeLocalizationConfig.asset";
            cfg.languageOptionsPath = DefaultLanguageOptionsPathForRoot(root);
        }

        static string DefaultLanguageOptionsPathForRoot(string root) =>
            $"{ProjectAssetPaths.NormalizeAssetPath(root)}/Config/LanguageOptions.asset";

        internal static void EnsureFolderForSharedLocator(string path)
        {
            ProjectAssetPaths.EnsureFolder(path);
        }
    }

    public static class LanguageOptionsLocator
    {
        public static LanguageOptions Find()
        {
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (paths == null) return null;
            return ProjectAssetPaths.FindConfigured<LanguageOptions>("NodeEditor", paths.languageOptionsPath);
        }

        public static LanguageOptions FindOrCreate()
        {
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (paths == null) return null;
            var path = Normalize(paths?.languageOptionsPath);
            if (string.IsNullOrEmpty(path)) path = NodeEditorAssetPathsLocator.DefaultLanguageOptionsPathForCurrentRoot();

            if (!ProjectAssetPaths.IsProjectAssetPath(path))
            {
                Debug.LogError($"NodeEditor: LanguageOptions path must be under Assets/: '{path}'.");
                return null;
            }

            var configured = AssetDatabase.LoadAssetAtPath<LanguageOptions>(path);
            if (configured != null) return configured;

            var candidates = ProjectCandidates();
            if (candidates.Length > 0)
            {
                if (candidates.Length > 1)
                    Debug.LogError("NodeEditor: multiple project-owned LanguageOptions assets found while the configured path is missing. " +
                                   "Resolve the candidates before creating another:\n- " + string.Join("\n- ", candidates));
                else
                    Debug.LogError($"NodeEditor: configured LanguageOptions asset is missing at '{path}', but one exists at '{candidates[0]}'. " +
                                   "Move it or update NodeEditorAssetPaths explicitly.");
                return null;
            }

            var options = ScriptableObject.CreateInstance<LanguageOptions>();
            var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            NodeEditorAssetPathsLocator.EnsureFolderForSharedLocator(dir);
            AssetDatabase.CreateAsset(options, path);
            Undo.RegisterCreatedObjectUndo(options, "Create LanguageOptions");
            EditorUtility.SetDirty(options);
            return options;
        }

        public static List<string> Codes()
        {
            var options = Find();
            return options != null ? options.Codes().ToList() : new List<string> { Language.English.Code(), Language.Chinese.Code() };
        }

        public static string DisplayName(string code)
        {
            var options = Find();
            return options != null ? options.DisplayName(code) : code ?? string.Empty;
        }

        static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/').Trim();

        static string[] ProjectCandidates() => AssetDatabase.FindAssets("t:LanguageOptions")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(ProjectAssetPaths.IsProjectAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .ToArray();
    }
}
