using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    /// <summary>Editor-only singleton locator for project-owned path configuration assets.</summary>
    public static class ProjectAssetPaths
    {
        public const string SettingsRoot = "Assets/NodeEditorSettings";

        public static T FindOrCreate<T>(string owner, Action<T> applyDefaults) where T : ScriptableObject
        {
            var candidates = ConfigurationCandidates<T>();

            if (candidates.Length > 1)
            {
                Debug.LogError($"{owner}: multiple project-owned {typeof(T).Name} assets found. " +
                               "Keep exactly one before continuing:\n- " + string.Join("\n- ", candidates));
                return null;
            }

            if (candidates.Length == 1)
                return AssetDatabase.LoadAssetAtPath<T>(candidates[0]);

            var path = BootstrapPath<T>();
            var occupied = AssetDatabase.LoadMainAssetAtPath(path);
            if (occupied != null)
            {
                Debug.LogError($"{owner}: cannot create {typeof(T).Name}; '{path}' is occupied by {occupied.GetType().Name}.");
                return null;
            }

            EnsureFolder(SettingsRoot);
            var config = ScriptableObject.CreateInstance<T>();
            applyDefaults?.Invoke(config);
            AssetDatabase.CreateAsset(config, path);
            Undo.RegisterCreatedObjectUndo(config, $"Create {typeof(T).Name}");
            EditorUtility.SetDirty(config);
            return config;
        }

        public static void Open<T>(string owner, Action<T> applyDefaults) where T : ScriptableObject
        {
            var config = FindOrCreate(owner, applyDefaults);
            if (config == null) return;
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        public static string BootstrapPath<T>() where T : ScriptableObject =>
            $"{SettingsRoot}/{typeof(T).Name}.asset";

        public static NodeGraphInstallSetupDescriptor CreateInstallSetupDescriptor<T>(
            string moduleId,
            string displayName,
            int order,
            string owner,
            Action<T> applyDefaults,
            Action generate) where T : ScriptableObject
        {
            return new NodeGraphInstallSetupDescriptor(
                moduleId,
                displayName,
                order,
                () => ConfigurationCandidates<T>().Length > 0,
                () =>
                {
                    var draft = ScriptableObject.CreateInstance<T>();
                    applyDefaults?.Invoke(draft);
                    return draft;
                },
                ValidateConfigurationPaths,
                draft => SaveInstallConfiguration(owner, draft as T),
                generate);
        }

        public static string ValidateConfigurationPaths(ScriptableObject configuration)
        {
            if (configuration == null) return "路径配置草稿不存在。 / The path configuration draft is missing.";

            var serialized = new SerializedObject(configuration);
            var property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.String || property.name == "m_Script") continue;
                string normalized = NormalizeAssetPath(property.stringValue);
                property.stringValue = normalized;
                if (!IsProjectAssetPath(normalized))
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    return $"“{property.displayName}”必须是 Assets/ 下的工程路径：{normalized}\n" +
                           $"'{property.displayName}' must be a project path under Assets/: {normalized}";
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }

        static string SaveInstallConfiguration<T>(string owner, T configuration) where T : ScriptableObject
        {
            if (configuration == null)
                return $"{owner}: 路径配置类型不匹配。 / The path configuration type is invalid.";

            var candidates = ConfigurationCandidates<T>();
            if (candidates.Length > 0)
                return $"{owner}: 已存在 {typeof(T).Name}，不会覆盖。 / An existing {typeof(T).Name} will not be overwritten.";

            string path = BootstrapPath<T>();
            var occupied = AssetDatabase.LoadMainAssetAtPath(path);
            if (occupied != null)
                return $"{owner}: “{path}”已被 {occupied.GetType().Name} 占用。 / '{path}' is occupied by {occupied.GetType().Name}.";

            EnsureFolder(SettingsRoot);
            AssetDatabase.CreateAsset(configuration, path);
            Undo.RegisterCreatedObjectUndo(configuration, $"Create {typeof(T).Name}");
            EditorUtility.SetDirty(configuration);
            AssetDatabase.SaveAssets();
            return null;
        }

        static string[] ConfigurationCandidates<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsProjectAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        public static string ContentRoot(string moduleName) =>
            $"Assets/{moduleName}Content";

        public static bool IsProjectAssetPath(string path) =>
            IsNormalizedProjectAssetPath(path);

        static bool IsNormalizedProjectAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path != path.Trim()) return false;
            var normalized = path.Replace('\\', '/');
            if (normalized.Contains("//")) return false;
            normalized = normalized.TrimEnd('/');
            if (normalized == "Assets") return true;
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            var segments = normalized.Split('/');
            return segments.Length > 1 && segments[0] == "Assets" &&
                   segments.Skip(1).All(segment => !string.IsNullOrEmpty(segment) && segment != "." && segment != ".." && !segment.Contains(':'));
        }

        public static bool ValidateWritablePaths(string owner, params string[] paths)
        {
            var invalid = (paths ?? Array.Empty<string>())
                .Where(path => !IsProjectAssetPath(path))
                .Select(path => string.IsNullOrWhiteSpace(path) ? "<empty>" : path)
                .Distinct()
                .ToArray();
            if (invalid.Length == 0) return true;
            Debug.LogError($"{owner}: generated asset paths must be project-owned paths under Assets/: " +
                           string.Join(", ", invalid));
            return false;
        }

        public static bool PrepareWritableDirectories(string owner, params string[] directories)
        {
            if (!ValidateWritablePaths(owner, directories)) return false;
            foreach (string directory in directories ?? Array.Empty<string>())
                EnsureFolder(NormalizeAssetPath(directory));
            return true;
        }

        public static T FindConfigured<T>(string owner, string configuredPath) where T : UnityEngine.Object
        {
            configuredPath = (configuredPath ?? string.Empty).Replace('\\', '/').Trim();
            var typeName = typeof(T).Name;
            if (!IsProjectAssetPath(configuredPath))
            {
                Debug.LogError($"{owner}: {typeName} path must be under Assets/: '{configuredPath}'.");
                return null;
            }

            var configured = AssetDatabase.LoadAssetAtPath<T>(configuredPath);
            if (configured != null) return configured;

            var candidates = AssetDatabase.FindAssets($"t:{typeName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsProjectAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length > 1)
            {
                Debug.LogError($"{owner}: multiple project-owned {typeName} assets found while the configured path is missing. " +
                               $"Move the intended asset to '{configuredPath}' or update the path explicitly:\n- " +
                               string.Join("\n- ", candidates));
            }
            else if (candidates.Length == 1)
            {
                Debug.LogError($"{owner}: configured {typeName} asset is missing at '{configuredPath}'. " +
                               $"A different asset exists at '{candidates[0]}'; update the path explicitly instead of relying on discovery.");
            }
            return null;
        }

        // 资产路径规范化（全工程唯一实现——各领域 Setup/Locator 过去各自复制且漂移：仅 Task 版带 Trim）：
        // 反斜杠转正斜杠、去首尾空白、去尾部斜杠。null 安全。
        public static string NormalizeAssetPath(string path) =>
            (path ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');

        public static void EnsureFolder(string path)
        {
            path = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            if (!IsProjectAssetPath(path))
            {
                Debug.LogError($"NodeEditor: refusing to create non-project folder '{path}'. Use a normalized path under Assets/.");
                return;
            }
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
