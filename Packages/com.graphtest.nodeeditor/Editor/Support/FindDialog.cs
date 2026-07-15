// FindDialog.cs — 查找节点的纯逻辑（TitleMatches 不依赖 panel，可单测）。
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

    public static class FindDialog
    {
        // 对节点标题做大小写不敏感的子串匹配。空/空白查询不匹配任何内容（Find 不能
        // 选中所有节点）。纯函数 + 可单元测试；下方的窗口会在整个画布上应用它。
        public static bool TitleMatches(string title, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            return (title ?? string.Empty).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Open(GraphCanvas canvas)
        {
            if (canvas == null) return;
            FindWindow.Show(canvas);
        }
    }
}
