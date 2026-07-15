// LocalizationTableLocator.cs — 本地化表定位器（服从项目路径配置，失败关闭）。
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

    // 定位项目唯一的 LocalizationTable（兜底本地化表）。与 BlackboardLocator 同款假设：
    // 每项目一个；发现多个时告警而非静默取第一个。供数据窗口的「本地化」源编辑用。
    public static class LocalizationTableLocator
    {
        public static LocalizationTable Find()
        {
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            return paths == null
                ? null
                : ProjectAssetPaths.FindConfigured<LocalizationTable>("NodeEditor", paths.localizationTablePath);
        }
    }
}
