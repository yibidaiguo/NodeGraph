// NodeViewSilhouette.cs — NodeView 的圆角矩形表面绘制（Painter2D）。
// partial 拆分纯为内聚（拆自 GraphCanvasView.cs）；绘制入口 OnGenerateVisualContent 与
// 命中入口 ContainsPoint 留在 GraphCanvasView.cs 的 NodeView 本体。

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;   // 仅限于此适配器文件使用
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public partial class NodeView
    {
        const float BezierCircle = 0.55228475f;

        void DrawNodeSurface(MeshGenerationContext context)
        {
            var bounds = NodeSurfaceBounds(contentRect);
            if (bounds.width <= 0f || bounds.height <= 0f) return;

            var painter = context.painter2D;

            // 1. 辉光（运行态）
            if (m_ShapeGlow.a > 0f)
            {
                painter.strokeColor = m_ShapeGlow;
                painter.lineWidth = 7f;
                BeginRoundedRectPath(painter, bounds, m_CornerRadius);
                painter.Stroke();
                painter.lineWidth = 4f;
                BeginRoundedRectPath(painter, bounds, m_CornerRadius);
                painter.Stroke();
            }

            // 2. 纯色填充
            painter.fillColor = m_ShapeFill;
            BeginRoundedRectPath(painter, bounds, m_CornerRadius);
            painter.Fill();

            // 3. 标题色带：上圆下方
            if (m_TitleFill.a > 0f && m_TitleHeight > 0f)
            {
                float titleH = Mathf.Min(m_TitleHeight, bounds.height);
                painter.fillColor = m_TitleFill;
                BeginTitleBarPath(painter, bounds, m_CornerRadius, titleH);
                painter.Fill();
            }

            DrawRunningFlow(context, bounds);

            // 4. 主描边
            painter.strokeColor = m_ShapeOutline;
            painter.lineWidth = Mathf.Max(1f, m_ShapeOutlineWidth);
            BeginRoundedRectPath(painter, bounds, m_CornerRadius);
            painter.Stroke();

            // 5. 校验轮廓（内缩 3px）
            if (m_ValidationOutline.a > 0f)
            {
                painter.strokeColor = m_ValidationOutline;
                painter.lineWidth = 2f;
                var vBounds = ValidationSilhouetteBounds(bounds);
                var vRadius = Mathf.Max(0f, m_CornerRadius - 3f);
                BeginRoundedRectPath(painter, vBounds, vRadius);
                painter.Stroke();
            }

            // 6. 选中轮廓（外扩 3px）
            if (selected && m_SelectionOutline.a > 0f)
            {
                painter.strokeColor = m_SelectionOutline;
                painter.lineWidth = 2.5f;
                var sBounds = SelectionSilhouetteBounds(bounds);
                var sRadius = m_CornerRadius + 3f;
                BeginRoundedRectPath(painter, sBounds, sRadius);
                painter.Stroke();
            }
        }

        static void BeginRoundedRectPath(Painter2D painter, Rect bounds, float radius)
        {
            float r = Mathf.Min(radius, Mathf.Min(bounds.width, bounds.height) * 0.5f);
            float k = BezierCircle * r;
            float x0 = bounds.xMin, x1 = bounds.xMax;
            float y0 = bounds.yMin, y1 = bounds.yMax;

            painter.BeginPath();
            painter.MoveTo(new Vector2(x0 + r, y0));
            // 顶边
            painter.LineTo(new Vector2(x1 - r, y0));
            // 右上角
            painter.BezierCurveTo(
                new Vector2(x1 - r + k, y0),
                new Vector2(x1, y0 + r - k),
                new Vector2(x1, y0 + r));
            // 右边
            painter.LineTo(new Vector2(x1, y1 - r));
            // 右下角
            painter.BezierCurveTo(
                new Vector2(x1, y1 - r + k),
                new Vector2(x1 - r + k, y1),
                new Vector2(x1 - r, y1));
            // 底边
            painter.LineTo(new Vector2(x0 + r, y1));
            // 左下角
            painter.BezierCurveTo(
                new Vector2(x0 + r - k, y1),
                new Vector2(x0, y1 - r + k),
                new Vector2(x0, y1 - r));
            // 左边
            painter.LineTo(new Vector2(x0, y0 + r));
            // 左上角
            painter.BezierCurveTo(
                new Vector2(x0, y0 + r - k),
                new Vector2(x0 + r - k, y0),
                new Vector2(x0 + r, y0));
            painter.ClosePath();
        }

        static void BeginTitleBarPath(Painter2D painter, Rect bounds, float radius, float titleHeight)
        {
            float r = Mathf.Min(radius, Mathf.Min(bounds.width, bounds.height) * 0.5f);
            float k = BezierCircle * r;
            float x0 = bounds.xMin, x1 = bounds.xMax;
            float y0 = bounds.yMin;
            float yTitle = y0 + titleHeight;

            painter.BeginPath();
            painter.MoveTo(new Vector2(x0 + r, y0));
            // 顶边
            painter.LineTo(new Vector2(x1 - r, y0));
            // 右上角
            painter.BezierCurveTo(
                new Vector2(x1 - r + k, y0),
                new Vector2(x1, y0 + r - k),
                new Vector2(x1, y0 + r));
            // 右边直下到 titleHeight
            painter.LineTo(new Vector2(x1, yTitle));
            // 底边直线到左
            painter.LineTo(new Vector2(x0, yTitle));
            // 左边直上
            painter.LineTo(new Vector2(x0, y0 + r));
            // 左上角
            painter.BezierCurveTo(
                new Vector2(x0, y0 + r - k),
                new Vector2(x0 + r - k, y0),
                new Vector2(x0 + r, y0));
            painter.ClosePath();
        }

        static Rect NodeSurfaceBounds(Rect contentBounds)
            => Rect.MinMaxRect(contentBounds.xMin + 1f, contentBounds.yMin + 1f,
                contentBounds.xMax - 1f, contentBounds.yMax - 1f);

        static Rect ValidationSilhouetteBounds(Rect shapeBounds)
            => Rect.MinMaxRect(shapeBounds.xMin + 3f, shapeBounds.yMin + 3f,
                shapeBounds.xMax - 3f, shapeBounds.yMax - 3f);

        static Rect SelectionSilhouetteBounds(Rect shapeBounds)
            => Rect.MinMaxRect(shapeBounds.xMin - 3f, shapeBounds.yMin - 3f,
                shapeBounds.xMax + 3f, shapeBounds.yMax + 3f);

        // 采样圆角矩形顶点（供运行态流光使用）
        static void BuildRoundedRectSamples(Rect bounds, float radius, List<Vector2> samples,
            float bezierCircle = BezierCircle)
        {
            const int curveSteps = 5;
            float r = Mathf.Min(radius, Mathf.Min(bounds.width, bounds.height) * 0.5f);
            float k = bezierCircle * r;
            float x0 = bounds.xMin, x1 = bounds.xMax;
            float y0 = bounds.yMin, y1 = bounds.yMax;

            samples.Clear();

            // top-right corner entry
            var entry = new Vector2(x1 - r, y0);
            samples.Add(entry);
            AppendCubicSamples(samples, entry,
                new Vector2(x1 - r + k, y0),
                new Vector2(x1, y0 + r - k),
                new Vector2(x1, y0 + r), curveSteps);

            // bottom-right corner
            entry = new Vector2(x1, y1 - r);
            samples.Add(entry);
            AppendCubicSamples(samples, entry,
                new Vector2(x1, y1 - r + k),
                new Vector2(x1 - r + k, y1),
                new Vector2(x1 - r, y1), curveSteps);

            // bottom-left corner
            entry = new Vector2(x0 + r, y1);
            samples.Add(entry);
            AppendCubicSamples(samples, entry,
                new Vector2(x0 + r - k, y1),
                new Vector2(x0, y1 - r + k),
                new Vector2(x0, y1 - r), curveSteps);

            // top-left corner
            entry = new Vector2(x0, y0 + r);
            samples.Add(entry);
            AppendCubicSamples(samples, entry,
                new Vector2(x0, y0 + r - k),
                new Vector2(x0 + r - k, y0),
                new Vector2(x0 + r, y0), curveSteps);
        }
    }
}
