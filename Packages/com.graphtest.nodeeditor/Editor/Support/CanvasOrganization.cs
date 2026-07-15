// CanvasOrganization.cs — 画布组织/连线风格偏好（EdgeStyle 随类使用，同文件）。
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

    // ---- 画布上的连接与组织辅助 ----
    public enum EdgeStyle { Orthogonal, Bezier, Straight }

    public static class CanvasOrganization
    {
        public static void SetEdgeStyle(GraphCanvas canvas, EdgeStyle style)
        {
            // GraphView 的 edge 暴露控制点；切换渲染方式并持久化。
            EditorPrefs.SetInt("NodeEditor.EdgeStyle", (int)style);
            // （逐条 edge 的重渲染会在下次刷新时应用。）
        }

        public static VisualElement CreateGroup(GraphCanvas canvas, string title, IEnumerable<NodeView> nodes)
            => canvas.CreateGroup(title, nodes);

        public static void InstallHoverBar(NodeView node, Action onAddChild, Action onComment, Action onToggleBreakpoint)
        {
            VisualElement bar = null;
            node.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (bar != null) return;   // 已显示——不要叠加第二个 bar（离开时只会移除最后一个）
                bar = new VisualElement(); bar.AddToClassList("hover-bar");
                bar.Add(new Button(onAddChild) { text = "+" });
                bar.Add(new Button(onComment) { text = "💬" });
                bar.Add(new Button(onToggleBreakpoint) { text = "●" });
                node.titleContainer.Add(bar);
            });
            node.RegisterCallback<MouseLeaveEvent>(_ => { if (bar != null) { bar.RemoveFromHierarchy(); bar = null; } });
        }
    }
}
