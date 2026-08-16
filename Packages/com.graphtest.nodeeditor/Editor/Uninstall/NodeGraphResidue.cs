// NodeGraphResidue.cs — 「卸载一个模块，工程里会剩下什么」的唯一答案。
//
// 过去卸载只有一步 Client.Remove(packageId)：包没了，Setup 生成的那批工程资产、
// 空掉的目录、EditorPrefs 里的界面状态原地不动。装一次留一堆，这不叫可插拔。
//
// 残留面不另开清单，直接读模块自己的 *AssetPaths 配置——「这个模块生成什么」本来就只声明在那里。
// 另写一份就会漂移：加一个落点忘了同步，卸载后就少清一处，而且是沉默的。
//
// 引用检查是刻意的：图资产可能已经被业务预制体/场景引用。删掉那种资产就是把工程弄坏，
// 所以被外部引用的条目单独列出来、默认不删，由人决定。
//
// 仅 Editor/ 程序集。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    /// <summary>一条残留：一个资产或一个目录。</summary>
    public sealed class NodeGraphResidueItem
    {
        public NodeGraphResidueItem(string path, bool isFolder, IReadOnlyList<string> externalReferences)
        {
            Path = path;
            IsFolder = isFolder;
            ExternalReferences = externalReferences ?? Array.Empty<string>();
        }

        public string Path { get; }
        public bool IsFolder { get; }

        /// <summary>工程里引用了它、但自己不在这次残留集里的资产。非空即「删了会弄坏别处」。</summary>
        public IReadOnlyList<string> ExternalReferences { get; }

        public bool IsReferenced => ExternalReferences.Count > 0;
    }

    /// <summary>一个模块的完整残留面。</summary>
    public sealed class NodeGraphResidue
    {
        public NodeGraphResidue(
            string moduleId,
            string displayName,
            IReadOnlyList<NodeGraphResidueItem> assets,
            IReadOnlyList<NodeGraphResidueItem> folders,
            IReadOnlyList<string> editorPrefKeys,
            IReadOnlyList<string> projectSettingsFiles,
            bool referenceScanCompleted)
        {
            ModuleId = moduleId;
            DisplayName = displayName;
            Assets = assets ?? Array.Empty<NodeGraphResidueItem>();
            Folders = folders ?? Array.Empty<NodeGraphResidueItem>();
            EditorPrefKeys = editorPrefKeys ?? Array.Empty<string>();
            ProjectSettingsFiles = projectSettingsFiles ?? Array.Empty<string>();
            ReferenceScanCompleted = referenceScanCompleted;
        }

        public string ModuleId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<NodeGraphResidueItem> Assets { get; }
        public IReadOnlyList<NodeGraphResidueItem> Folders { get; }
        public IReadOnlyList<string> EditorPrefKeys { get; }
        public IReadOnlyList<string> ProjectSettingsFiles { get; }

        /// <summary>false = 引用扫描被中断，「无人引用」这个结论不成立，删除前必须当作未知处理。</summary>
        public bool ReferenceScanCompleted { get; }

        public IEnumerable<NodeGraphResidueItem> Removable =>
            Assets.Where(item => !item.IsReferenced);

        public IEnumerable<NodeGraphResidueItem> Blocked =>
            Assets.Where(item => item.IsReferenced);

        public bool IsEmpty =>
            Assets.Count == 0 && Folders.Count == 0 &&
            EditorPrefKeys.Count == 0 && ProjectSettingsFiles.Count == 0;
    }

    public static class NodeGraphResidueScanner
    {
        // 框架的编辑器界面状态。EditorPrefs 没有枚举 API，只能按名单删——
        // 新增一个 pref 键就要在这里补一行，否则它会活过卸载。
        static readonly string[] FrameworkEditorPrefKeys =
        {
            "NodeEditor.DarkTheme",
            "NodeEditor.EdgeStyle",
            "NodeEditor.MiniMap",
            "NodeEditor.InspectorEnabled",
            "NodeEditor.Overlay.variables.corner",
            "NodeEditor.Overlay.variables.dx",
            "NodeEditor.Overlay.variables.dy",
            "NodeEditor.Overlay.variables.collapsed",
            "NodeEditor.Overlay.variables.visible",
            "NodeEditor.Overlay.inspector.corner",
            "NodeEditor.Overlay.inspector.dx",
            "NodeEditor.Overlay.inspector.dy",
            "NodeEditor.Overlay.inspector.collapsed",
            "NodeEditor.Overlay.inspector.visible",
        };

        // 引用扫描要读工程里每一个可能持有引用的资产。这些扩展名不可能引用别的资产，跳过它们
        // 能把一次卸载扫描从「整个 Assets」缩到「真正可能连边的那部分」。
        static readonly HashSet<string> NonReferencingExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".js", ".dll", ".md", ".txt", ".json", ".xml", ".csv", ".shader", ".cginc", ".hlsl",
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".gif", ".bmp", ".tif", ".tiff",
            ".wav", ".mp3", ".ogg", ".aiff", ".ttf", ".otf", ".fbx", ".obj", ".meta", ".asmdef", ".asmref",
        };

        /// <summary>算出一个模块的残留面。framework = true 时连安装根配置与 EditorPrefs 一起算进去。</summary>
        public static NodeGraphResidue Scan(
            NodeGraphInstallSetupDescriptor descriptor,
            bool isFramework,
            bool showProgress = true)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            var declared = descriptor.OwnedPaths();
            var assetPaths = new HashSet<string>(StringComparer.Ordinal);
            var folderPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var declaredPath in declared)
            {
                if (AssetDatabase.IsValidFolder(declaredPath))
                {
                    CollectFolder(declaredPath, assetPaths, folderPaths);
                    continue;
                }
                if (AssetDatabase.LoadMainAssetAtPath(declaredPath) != null) assetPaths.Add(declaredPath);
            }

            // 声明的落点是叶子目录（.../Nodes/Definitions），它们清空之后中间目录也就空了。
            // 一路上溯到安装根，把因此变空的目录一并收进来——留一串空壳同样是遗留。
            foreach (var folder in folderPaths.ToArray()) CollectEmptiedAncestors(folder, folderPaths);
            foreach (var asset in assetPaths)
            {
                var parent = ParentFolder(asset);
                if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent))
                {
                    folderPaths.Add(parent);
                    CollectEmptiedAncestors(parent, folderPaths);
                }
            }

            var references = FindExternalReferences(assetPaths, showProgress, out bool completed);


            var assets = assetPaths
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new NodeGraphResidueItem(
                    path, false, references.TryGetValue(path, out var refs) ? refs : Array.Empty<string>()))
                .ToArray();

            // 深的先删，否则父目录先没了、子目录的删除就落空。
            var folders = folderPaths
                .OrderByDescending(path => path.Count(c => c == '/'))
                .ThenBy(path => path, StringComparer.Ordinal)
                .Select(path => new NodeGraphResidueItem(path, true, Array.Empty<string>()))
                .ToArray();

            var prefKeys = isFramework
                ? FrameworkEditorPrefKeys.Where(EditorPrefs.HasKey).ToArray()
                : Array.Empty<string>();

            var settingsFiles = isFramework && NodeGraphInstallRoot.IsConfigured
                ? new[] { NodeGraphInstallRoot.ConfigFilePath }
                : Array.Empty<string>();

            return new NodeGraphResidue(
                descriptor.ModuleId, descriptor.DisplayName, assets, folders, prefKeys, settingsFiles, completed);
        }

        static void CollectFolder(string folder, HashSet<string> assets, HashSet<string> folders)
        {
            folders.Add(folder);
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path)) folders.Add(path);
                else assets.Add(path);
            }
        }

        // 从 folder 往上走，把「除了这次要删的东西以外空无一物」的祖先目录也收进来，止步于安装根之外。
        static void CollectEmptiedAncestors(string folder, HashSet<string> folders)
        {
            string root = ProjectAssetPaths.NormalizeAssetPath(NodeGraphInstallRoot.Root);
            if (NodeGraphInstallRoot.IsLegacyLayout) return;

            var current = ParentFolder(folder);
            while (!string.IsNullOrEmpty(current) &&
                   current.StartsWith(root, StringComparison.Ordinal) &&
                   current != "Assets")
            {
                folders.Add(current);
                if (current == root) return;
                current = ParentFolder(current);
            }
        }

        static string ParentFolder(string path)
        {
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(parent) ? null : parent;
        }

        /// <summary>
        /// 节点图自己的地盘：所有已注册模块声明的落点，非 legacy 时再加上安装根本身。
        /// 引用检查要拿它把「框架自己的账本」摘出去——共享注册表当然引用着各模块的节点定义，
        /// 把那算成外部引用，卸对话就会一个定义都删不掉，残留照旧。
        /// </summary>
        static string[] NodeGraphOwnedPrefixes()
        {
            var prefixes = new List<string>();
            if (!NodeGraphInstallRoot.IsLegacyLayout)
                prefixes.Add(ProjectAssetPaths.NormalizeAssetPath(NodeGraphInstallRoot.Root));

            foreach (var descriptor in NodeGraphInstallSetupCoordinator.RegisteredDescriptors)
                prefixes.AddRange(descriptor.OwnedPaths());

            return prefixes
                .Select(ProjectAssetPaths.NormalizeAssetPath)
                .Where(ProjectAssetPaths.IsProjectAssetPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        // 按路径段比较，别让 Assets/NodeGraph/Node 顺手匹配上 Assets/NodeGraph/NodeXyz。
        static bool IsUnderAny(string path, string[] prefixes) =>
            prefixes.Any(prefix =>
                string.Equals(path, prefix, StringComparison.Ordinal) ||
                path.StartsWith(prefix + "/", StringComparison.Ordinal));

        /// <summary>
        /// 反查引用：Unity 没有公开的反向依赖索引，只能把工程里可能持有引用的资产过一遍。
        /// 用户取消时 completed 置 false——此时「没人引用」是没验过的话，调用方不许拿它当放行依据。
        /// </summary>
        static Dictionary<string, List<string>> FindExternalReferences(
            HashSet<string> residue, bool showProgress, out bool completed)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            completed = true;
            if (residue.Count == 0) return result;

            var owned = NodeGraphOwnedPrefixes();
            var candidates = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => !residue.Contains(path))
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(path => !NonReferencingExtensions.Contains(Path.GetExtension(path)))
                .Where(path => !IsUnderAny(path, owned))
                .ToArray();

            try
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (showProgress && (i % 64 == 0) && EditorUtility.DisplayCancelableProgressBar(
                            "NodeGraph", $"检查引用 / Checking references ({i + 1}/{candidates.Length})",
                            (float)i / candidates.Length))
                    {
                        completed = false;
                        return result;
                    }

                    foreach (var dependency in AssetDatabase.GetDependencies(candidates[i], false))
                    {
                        if (!residue.Contains(dependency)) continue;
                        if (!result.TryGetValue(dependency, out var referrers))
                            result[dependency] = referrers = new List<string>();
                        if (!referrers.Contains(candidates[i])) referrers.Add(candidates[i]);
                    }
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }

            return result;
        }
    }
}
