// BlackboardLocator.cs — 分层黑板定位缝（全局/模块/组档按标签解析；框架↔领域公共缝，准则#15）。
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


    // ---- 小弹窗（UI 主体从略；仅展示结构） ----

    // 定位分层的 BlackboardAsset。作用域 = asset 的 module/group 标签所在的层级（见 BlackboardAsset）：
    // 全局（标签皆空，每项目一块）→ 模块（带 module）→ 组（带 module+group）。Resolve 据图的 module/group
    // 把适用各档按「全局→模块→组」合并成一个 BlackboardSet（运行播种 / 校验 / 检视面板的「键」下拉都读它）。
    public static class BlackboardLocator
    {
        static List<BlackboardAsset> s_All;

        static BlackboardLocator()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        static IEnumerable<BlackboardAsset> All()
        {
            if (s_All != null) return s_All;
            s_All = AssetDatabase.FindAssets("t:BlackboardAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<BlackboardAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();
            return s_All;
        }

        static void Invalidate() => s_All = null;

        // 全局黑板（标签皆空）：每项目假设一块；发现多个时告警而非静默取第一个（那会掩盖歧义）。
        public static BlackboardAsset FindGlobal()
        {
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            var configured = paths == null
                ? null
                : ProjectAssetPaths.FindConfigured<BlackboardAsset>("NodeEditor", paths.globalBlackboardPath);
            if (configured != null && (!string.IsNullOrEmpty(configured.Module) || !string.IsNullOrEmpty(configured.Group)))
            {
                Debug.LogError($"NodeEditor: configured global BlackboardAsset at '{AssetDatabase.GetAssetPath(configured)}' " +
                               $"has module='{configured.Module}' group='{configured.Group}'. The global layer requires both tags empty.");
                return null;
            }
            return configured;
        }

        // 某一档的黑板：按 module/group 标签精确匹配（空 group = 模块档）。同一档发现多块时报错并失败关闭，不选择任何候选。
        public static BlackboardAsset FindLayer(string module, string group)
        {
            if (!ValidLayerTags(module, group)) return null;
            var matches = LayerMatches(module, group);
            if (matches.Count == 0) return null;
            if (matches.Count > 1)
            {
                var candidates = matches.Select(AssetDatabase.GetAssetPath).OrderBy(path => path).ToArray();
                Debug.LogError($"NodeEditor: multiple BlackboardAssets found for module='{module}' group='{group}'. " +
                               "Keep exactly one for this layer before continuing:\n- " + string.Join("\n- ", candidates));
                return null;
            }
            return matches[0];
        }

        static List<BlackboardAsset> LayerMatches(string module, string group) =>
            All().Where(a => a.Module == (module ?? "") && a.Group == (group ?? "")).ToList();

        static bool ValidLayerTags(string module, string group)
        {
            if (!string.IsNullOrEmpty(module) || string.IsNullOrEmpty(group)) return true;
            Debug.LogError("NodeEditor: a BlackboardAsset group requires a non-empty module.");
            return false;
        }

        // 合并某 module/group 的有效黑板：全局 → 模块 → 组（由外到内）。缺某档则跳过该档。
        public static BlackboardSet Resolve(string module, string group)
        {
            if (!ValidLayerTags(module, group)) return new BlackboardSet();
            var layers = new List<BlackboardAsset>();
            layers.Add(FindGlobal());
            if (!string.IsNullOrEmpty(module))
            {
                layers.Add(FindLayer(module, ""));
                if (!string.IsNullOrEmpty(group)) layers.Add(FindLayer(module, group));
            }
            return new BlackboardSet(layers);   // BlackboardSet 自身剔除 null 档
        }

        // 某张图的有效黑板（按图的 module/group 标签）。图为 null 时退化为「仅全局」。
        public static BlackboardSet ResolveFor(NodeGraphAsset graph) =>
            graph == null ? new BlackboardSet(FindGlobal()) : Resolve(graph.module, graph.group);

        // 某一档黑板该落在哪个文件夹（分层原则：资产位置跟随其作用域/受众）：
        //  · 全局档（module 空）→ NodeEditorAssetPaths.globalBlackboardPath 所在文件夹；
        //  · 模块/组档 → 该模块自己的资产区——取「该模块某张图所在的文件夹」（与其图同处），
        //    而不是框架目录。找不到该模块的图时才退回全局档文件夹（兜底）。
        public static string LayerFolder(string module)
        {
            if (!string.IsNullOrEmpty(module))
            {
                var graphFolders = AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(path => new { path, graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path) })
                    .Where(item => item.graph != null && item.graph.module == module)
                    .Select(item => System.IO.Path.GetDirectoryName(item.path).Replace('\\', '/'))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (graphFolders.Count > 1)
                {
                    Debug.LogError($"NodeEditor: multiple graph folders exist for module='{module}'. " +
                                   "Pass the intended BlackboardAsset folder explicitly instead of choosing by discovery:\n- " +
                                   string.Join("\n- ", graphFolders));
                    return null;
                }
                if (graphFolders.Count == 1) return graphFolders[0];
            }
            var g = FindGlobal();
            var gp = g != null ? AssetDatabase.GetAssetPath(g) : null;
            return string.IsNullOrEmpty(gp) ? "Assets" : System.IO.Path.GetDirectoryName(gp).Replace('\\', '/');
        }

        // 新建并打标某一档黑板（之后在数据窗口 / 默认 Inspector 里编辑其变量）。folder 显式给出落盘目录
        //（调用方按分层原则传该模块的资产区，见 LayerFolder / 准则 #15）；为空则由 LayerFolder(module) 推断。
        // 解析按 module/group 标签进行、与路径无关，但位置必须落在所属作用域的目录里（别统一丢进框架目录）。
        public static BlackboardAsset CreateLayer(string module, string group, string folder = null)
        {
            if (!ValidLayerTags(module, group)) return null;
            var isGlobal = string.IsNullOrEmpty(module) && string.IsNullOrEmpty(group);
            string exactGlobalPath = null;
            if (isGlobal)
            {
                var paths = NodeEditorAssetPathsLocator.FindOrCreate();
                if (paths == null) return null;
                exactGlobalPath = (paths.globalBlackboardPath ?? string.Empty).Replace('\\', '/').Trim();
                if (!ProjectAssetPaths.IsProjectAssetPath(exactGlobalPath))
                {
                    Debug.LogError($"NodeEditor: global BlackboardAsset path must be under Assets/: '{exactGlobalPath}'.");
                    return null;
                }

                var configured = AssetDatabase.LoadAssetAtPath<BlackboardAsset>(exactGlobalPath);
                if (configured != null)
                {
                    if (!string.IsNullOrEmpty(configured.Module) || !string.IsNullOrEmpty(configured.Group))
                    {
                        Debug.LogError($"NodeEditor: configured global BlackboardAsset at '{exactGlobalPath}' " +
                                       "must have empty module/group tags.");
                        return null;
                    }
                    return configured;
                }

                var occupied = AssetDatabase.LoadMainAssetAtPath(exactGlobalPath);
                if (occupied != null)
                {
                    Debug.LogError($"NodeEditor: cannot create global BlackboardAsset; '{exactGlobalPath}' " +
                                   $"is occupied by {occupied.GetType().Name}.");
                    return null;
                }

                var alternateGlobals = LayerMatches("", "");
                if (alternateGlobals.Count > 0)
                {
                    Debug.LogError($"NodeEditor: configured global BlackboardAsset is missing at '{exactGlobalPath}', " +
                                   "but alternate global assets exist. Move the intended asset or update the path explicitly:\n- " +
                                   string.Join("\n- ", alternateGlobals.Select(AssetDatabase.GetAssetPath)));
                    return null;
                }

                folder = System.IO.Path.GetDirectoryName(exactGlobalPath)?.Replace('\\', '/');
            }
            else
            {
                var matches = LayerMatches(module, group);
                if (matches.Count == 1) return matches[0];
                if (matches.Count > 1)
                {
                    var candidates = matches.Select(AssetDatabase.GetAssetPath).OrderBy(path => path).ToArray();
                    Debug.LogError($"NodeEditor: multiple BlackboardAssets found for module='{module}' group='{group}'. " +
                                   "Keep exactly one for this layer before continuing:\n- " + string.Join("\n- ", candidates));
                    return null;
                }

                if (string.IsNullOrWhiteSpace(folder))
                {
                    Debug.LogError($"NodeEditor: a configured BlackboardAsset folder is required for module='{module}'.");
                    return null;
                }
                folder = folder.Replace('\\', '/').Trim().TrimEnd('/');
            }
            if (string.IsNullOrEmpty(folder)) return null;
            if (!ProjectAssetPaths.IsProjectAssetPath(folder))
            {
                Debug.LogError($"NodeEditor: BlackboardAsset folder must be under Assets/: '{folder}'.");
                return null;
            }
            ProjectAssetPaths.EnsureFolder(folder);
            var asset = ScriptableObject.CreateInstance<BlackboardAsset>();
            var baseName = string.IsNullOrEmpty(group) ? $"Blackboard_{module}" : $"Blackboard_{module}_{group}";
            var path = isGlobal ? exactGlobalPath : AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            // 标签是 private [SerializeField]：用 SerializedObject 按字段名写入，无需给 Runtime 类加编辑器专用 setter。
            var so = new SerializedObject(asset);
            so.FindProperty("module").stringValue = module ?? "";
            so.FindProperty("group").stringValue = group ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Blackboard Layer");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
