// NodeEditorMainObjectNameSync.cs — 资产移动/改名后同步 SO 主对象名的 AssetPostprocessor。
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

    class NodeEditorMainObjectNameSync : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var paths = imported.Concat(moved).ToArray();
            if (paths.Length == 0) return;
            EditorApplication.delayCall += () =>
            {
                bool changed = false;
                foreach (var path in paths)
                    changed |= Sync(path);
                if (changed) AssetDatabase.SaveAssets();
            };
        }

        static bool Sync(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return false;

            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!ShouldSync(asset)) return false;

            var expected = System.IO.Path.GetFileNameWithoutExtension(path);
            if (asset.name == expected) return false;

            Undo.RegisterCompleteObjectUndo(asset, "Synchronize Asset Name");
            asset.name = expected;
            EditorUtility.SetDirty(asset);
            return true;
        }

        static bool ShouldSync(UnityEngine.Object asset) =>
            asset is NodeGraphAsset
            || asset is BlackboardAsset
            || asset is NodeRegistry
            || asset is NodeDefinition
            || asset is NodeEditorAssetPaths
            || asset is LocalizationTable
            || asset is EditorLocalizationConfig
            || asset is RuntimeLocalizationConfig
            || asset is LanguageOptions;
    }
}
