// NodeViewSilhouette.cs — NodeView 的角色轮廓几何（Painter2D rounded-polygon 构建/采样/命中）。
// partial 拆分纯为内聚（拆自 GraphCanvasView.cs，方法逐字未改，不改任何语义）；
// 绘制入口 OnGenerateVisualContent 与命中入口 ContainsPoint 留在 GraphCanvasView.cs 的 NodeView 本体。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;   // 仅限于此适配器文件使用
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset、……）

namespace NodeEditor.EditorUI
{
    public partial class NodeView
    {

        void DrawRoleSilhouette(MeshGenerationContext context)
        {
            var bounds = RoleSilhouetteBounds(contentRect);
            if (bounds.width <= 0f || bounds.height <= 0f) return;

            var painter = context.painter2D;

            // Two offset fills expose only a narrow top highlight and bottom shadow once
            // the opaque face is drawn, matching the pressed-metal controls elsewhere.
            DrawOffsetFill(painter, bounds, new Vector2(0f, 2f), m_ShapeShadow);
            DrawOffsetFill(painter, bounds, new Vector2(0f, -1f), m_ShapeHighlight);

            if (m_ShapeGlow.a > 0f)
            {
                painter.strokeColor = m_ShapeGlow;
                painter.lineWidth = 7f;
                BeginRoleSilhouettePath(painter, Definition.Role, bounds);
                painter.Stroke();
                painter.lineWidth = 4f;
                BeginRoleSilhouettePath(painter, Definition.Role, bounds);
                painter.Stroke();
            }

            painter.fillColor = m_ShapeFill;
            painter.strokeColor = m_ShapeOutline;
            painter.lineWidth = Mathf.Max(1f, m_ShapeOutlineWidth);
            BeginRoleSilhouettePath(painter, Definition.Role, bounds);
            painter.Fill();
            painter.Stroke();

            if (selected && m_SelectionOutline.a > 0f)
            {
                painter.strokeColor = m_SelectionOutline;
                painter.lineWidth = 2.5f;
                BeginRoleSilhouettePath(painter, Definition.Role, bounds);
                painter.Stroke();
            }
        }

        void DrawOffsetFill(Painter2D painter, Rect bounds, Vector2 offset, Color color)
        {
            if (color.a <= 0f) return;
            bounds.position += offset;
            painter.fillColor = color;
            BeginRoleSilhouettePath(painter, Definition.Role, bounds);
            painter.Fill();
        }

        static Rect RoleSilhouetteBounds(Rect contentBounds)
            => Rect.MinMaxRect(contentBounds.xMin + 1f, contentBounds.yMin + 1f,
                contentBounds.xMax - 1f, contentBounds.yMax - 1f);

        static void BeginRoleSilhouettePath(Painter2D painter, NodeRole role, Rect bounds)
        {
            var x0 = bounds.xMin;
            var x1 = bounds.xMax;
            var y0 = bounds.yMin;
            var y1 = bounds.yMax;
            var midY = bounds.center.y;
            if (role == NodeRole.Provider)
            {
                var radiusX = Mathf.Min(32f, bounds.width * 0.2f);
                var radiusY = bounds.height * 0.5f;
                const float k = 0.55228475f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x0 + radiusX, y0));
                painter.LineTo(new Vector2(x1 - radiusX, y0));
                painter.BezierCurveTo(new Vector2(x1 - radiusX + k * radiusX, y0),
                    new Vector2(x1, midY - k * radiusY), new Vector2(x1, midY));
                painter.BezierCurveTo(new Vector2(x1, midY + k * radiusY),
                    new Vector2(x1 - radiusX + k * radiusX, y1), new Vector2(x1 - radiusX, y1));
                painter.LineTo(new Vector2(x0 + radiusX, y1));
                painter.BezierCurveTo(new Vector2(x0 + radiusX - k * radiusX, y1),
                    new Vector2(x0, midY + k * radiusY), new Vector2(x0, midY));
                painter.BezierCurveTo(new Vector2(x0, midY - k * radiusY),
                    new Vector2(x0 + radiusX - k * radiusX, y0), new Vector2(x0 + radiusX, y0));
            }
            else
            {
                var vertices = s_RolePolygonScratch ??= new List<Vector2>(8);
                BuildRolePolygon(role, bounds, vertices);
                BeginRoundedPolygonPath(painter, vertices, 7f);
            }
            painter.ClosePath();
        }

        static void BuildRolePolygon(NodeRole role, Rect bounds, List<Vector2> vertices)
        {
            vertices.Clear();
            var x0 = bounds.xMin;
            var x1 = bounds.xMax;
            var y0 = bounds.yMin;
            var y1 = bounds.yMax;
            var midY = bounds.center.y;

            if (role == NodeRole.Condition)
            {
                var shoulder = Mathf.Min(16f, bounds.width * 0.15f);
                vertices.Add(new Vector2(x0 + shoulder, y0));
                vertices.Add(new Vector2(x1 - shoulder, y0));
                vertices.Add(new Vector2(x1, midY));
                vertices.Add(new Vector2(x1 - shoulder, y1));
                vertices.Add(new Vector2(x0 + shoulder, y1));
                vertices.Add(new Vector2(x0, midY));
            }
            else if (role == NodeRole.Action)
            {
                var arrow = Mathf.Min(22f, bounds.width * 0.18f);
                vertices.Add(new Vector2(x0, y0));
                vertices.Add(new Vector2(x1 - arrow, y0));
                vertices.Add(new Vector2(x1, midY));
                vertices.Add(new Vector2(x1 - arrow, y1));
                vertices.Add(new Vector2(x0, y1));
            }
            else
            {
                var chamfer = Mathf.Min(12f, Mathf.Min(bounds.width, bounds.height) * 0.2f);
                vertices.Add(new Vector2(x0 + chamfer, y0));
                vertices.Add(new Vector2(x1 - chamfer, y0));
                vertices.Add(new Vector2(x1, y0 + chamfer));
                vertices.Add(new Vector2(x1, y1 - chamfer));
                vertices.Add(new Vector2(x1 - chamfer, y1));
                vertices.Add(new Vector2(x0 + chamfer, y1));
                vertices.Add(new Vector2(x0, y1 - chamfer));
                vertices.Add(new Vector2(x0, y0 + chamfer));
            }
        }

        static float RoundedCornerCut(float requested, float incomingLength, float outgoingLength)
            => Mathf.Max(0f, Mathf.Min(requested, Mathf.Min(incomingLength, outgoingLength) * 0.25f));

        static void GetRoundedCorner(IReadOnlyList<Vector2> vertices, int index, float radius,
            out Vector2 entry, out Vector2 firstControl, out Vector2 secondControl, out Vector2 exit)
        {
            var vertex = vertices[index];
            var previous = vertices[(index + vertices.Count - 1) % vertices.Count];
            var next = vertices[(index + 1) % vertices.Count];
            var incoming = previous - vertex;
            var outgoing = next - vertex;
            var cut = RoundedCornerCut(radius, incoming.magnitude, outgoing.magnitude);
            entry = vertex + incoming.normalized * cut;
            exit = vertex + outgoing.normalized * cut;
            firstControl = entry + (vertex - entry) * (2f / 3f);
            secondControl = exit + (vertex - exit) * (2f / 3f);
        }

        static void BeginRoundedPolygonPath(Painter2D painter,
            IReadOnlyList<Vector2> vertices, float radius)
        {
            GetRoundedCorner(vertices, 0, radius,
                out var firstEntry, out _, out _, out _);
            painter.BeginPath();
            painter.MoveTo(firstEntry);
            for (var i = 0; i < vertices.Count; i++)
            {
                GetRoundedCorner(vertices, i, radius,
                    out var entry, out var firstControl, out var secondControl, out var exit);
                if (i > 0) painter.LineTo(entry);
                painter.BezierCurveTo(firstControl, secondControl, exit);
            }
        }

        static void BuildRoundedPolygonSamples(IReadOnlyList<Vector2> vertices,
            float radius, List<Vector2> samples)
        {
            const int curveSteps = 5;
            samples.Clear();
            for (var i = 0; i < vertices.Count; i++)
            {
                GetRoundedCorner(vertices, i, radius,
                    out var entry, out var firstControl, out var secondControl, out var exit);
                samples.Add(entry);
                for (var step = 1; step <= curveSteps; step++)
                {
                    var t = step / (float)curveSteps;
                    var oneMinusT = 1f - t;
                    samples.Add(oneMinusT * oneMinusT * oneMinusT * entry
                        + 3f * oneMinusT * oneMinusT * t * firstControl
                        + 3f * oneMinusT * t * t * secondControl
                        + t * t * t * exit);
                }
            }
        }

        static bool PointInPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[j];
                var b = polygon[i];
                var segment = b - a;
                var lengthSquared = segment.sqrMagnitude;
                var projection = lengthSquared > 0f
                    ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared)
                    : 0f;
                if ((point - (a + projection * segment)).sqrMagnitude <= 0.0001f) return true;

                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static bool ContainsRoleSilhouettePoint(NodeRole role, Rect bounds, Vector2 point)
        {
            if (point.x < bounds.xMin || point.x > bounds.xMax
                || point.y < bounds.yMin || point.y > bounds.yMax) return false;

            if (role == NodeRole.Provider)
            {
                var localX = point.x - bounds.xMin;
                var localY = point.y - bounds.yMin;
                var radiusX = Mathf.Min(32f, bounds.width * 0.2f);
                var radiusY = bounds.height * 0.5f;
                if (localX >= radiusX && localX <= bounds.width - radiusX) return true;
                var centerX = localX < radiusX ? radiusX : bounds.width - radiusX;
                var ellipseX = (localX - centerX) / Mathf.Max(1f, radiusX);
                var ellipseY = (localY - radiusY) / Mathf.Max(1f, radiusY);
                return ellipseX * ellipseX + ellipseY * ellipseY <= 1f;
            }

            var vertices = s_RolePolygonScratch ??= new List<Vector2>(8);
            var samples = s_RoundedSampleScratch ??= new List<Vector2>(48);
            BuildRolePolygon(role, bounds, vertices);
            BuildRoundedPolygonSamples(vertices, 7f, samples);
            return PointInPolygon(samples, point);
        }
    }
}
