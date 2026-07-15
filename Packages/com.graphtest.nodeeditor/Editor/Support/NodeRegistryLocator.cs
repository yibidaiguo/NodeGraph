// NodeRegistryLocator.cs — 节点注册表定位器（读 NodeEditorAssetPaths 配置路径，歧义失败关闭）。
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

    // ---- 桥接到第 4 层 asset（项目特定；轻量 locator） ----
    public static class NodeRegistryLocator
    {
        public static NodeRegistry Find()
        {
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            return paths == null
                ? null
                : ProjectAssetPaths.FindConfigured<NodeRegistry>("NodeEditor", paths.registryPath);
        }
    }
}
