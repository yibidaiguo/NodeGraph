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
        const float RoleCornerRadius = 13f;

        void DrawRoleSilhouette(MeshGenerationContext context)
        {
            var bounds = RoleSilhouetteBounds(contentRect);
            if (bounds.width <= 0f || bounds.height <= 0f) return;

            var painter = context.painter2D;

            // A small offset remains behind the face as elevation; the face itself is a
            // true vertex-colour gradient matching the approved 145-degree material.
            DrawOffsetFill(painter, bounds, new Vector2(0f, 2f), m_ShapeShadow);

            if (m_ShapeGlow.a > 0f)
            {
                painter.strokeColor = m_ShapeGlow;
                painter.lineWidth = 7f;
                BeginRoleSilhouettePath(painter, m_VisualRole, bounds);
                painter.Stroke();
                painter.lineWidth = 4f;
                BeginRoleSilhouettePath(painter, m_VisualRole, bounds);
                painter.Stroke();
            }

            DrawGradientFace(context, m_VisualRole, bounds,
                m_ShapeHighlight, m_ShapeFill, m_ShapeShadow);
            DrawRunningFlow(context, m_VisualRole, bounds);

            painter.strokeColor = m_ShapeOutline;
            painter.lineWidth = Mathf.Max(1f, m_ShapeOutlineWidth);
            BeginRoleSilhouettePath(painter, m_VisualRole, bounds);
            painter.Stroke();

            if (m_ValidationOutline.a > 0f)
            {
                painter.strokeColor = m_ValidationOutline;
                painter.lineWidth = 2f;
                BeginRoleSilhouettePath(painter, m_VisualRole, ValidationSilhouetteBounds(bounds));
                painter.Stroke();
            }

            if (selected && m_SelectionOutline.a > 0f)
            {
                painter.strokeColor = m_SelectionOutline;
                painter.lineWidth = 2.5f;
                BeginRoleSilhouettePath(painter, m_VisualRole, SelectionSilhouetteBounds(bounds));
                painter.Stroke();
            }
        }

        void DrawOffsetFill(Painter2D painter, Rect bounds, Vector2 offset, Color color)
        {
            if (color.a <= 0f) return;
            bounds.position += offset;
            painter.fillColor = color;
            BeginRoleSilhouettePath(painter, m_VisualRole, bounds);
            painter.Fill();
        }

        static void DrawGradientFace(MeshGenerationContext context, NodeRole role,
            Rect bounds, Color top, Color middle, Color bottom)
        {
            var perimeter = s_RoundedSampleScratch ??= new List<Vector2>(64);
            BuildRoleSilhouetteSamples(role, bounds, perimeter);
            if (perimeter.Count < 3) return;

            var upper = s_GradientUpperScratch ??= new List<Vector2>(40);
            var lower = s_GradientLowerScratch ??= new List<Vector2>(40);
            ClipGradientRegion(perimeter, bounds, true, upper);
            ClipGradientRegion(perimeter, bounds, false, lower);
            DrawGradientRegion(context, upper, bounds, top, middle, bottom);
            DrawGradientRegion(context, lower, bounds, top, middle, bottom);
        }

        static void DrawGradientRegion(MeshGenerationContext context,
            IReadOnlyList<Vector2> region, Rect bounds, Color top, Color middle, Color bottom)
        {
            if (region.Count < 3) return;
            var center = Vector2.zero;
            for (var i = 0; i < region.Count; i++) center += region[i];
            center /= region.Count;

            var mesh = context.Allocate(region.Count + 1, region.Count * 3);
            mesh.SetNextVertex(GradientVertex(center, bounds, top, middle, bottom));
            for (var i = 0; i < region.Count; i++)
                mesh.SetNextVertex(GradientVertex(region[i], bounds, top, middle, bottom));

            for (var i = 0; i < region.Count; i++)
            {
                mesh.SetNextIndex(0);
                mesh.SetNextIndex((ushort)(i + 1));
                mesh.SetNextIndex((ushort)(((i + 1) % region.Count) + 1));
            }
        }

        static void ClipGradientRegion(IReadOnlyList<Vector2> perimeter, Rect bounds,
            bool keepUpper, List<Vector2> output)
        {
            const float middleStop = 0.55f;
            output.Clear();
            if (perimeter.Count == 0) return;

            var previous = perimeter[perimeter.Count - 1];
            var previousT = GradientPosition(previous, bounds);
            var previousInside = keepUpper ? previousT <= middleStop : previousT >= middleStop;
            for (var i = 0; i < perimeter.Count; i++)
            {
                var current = perimeter[i];
                var currentT = GradientPosition(current, bounds);
                var currentInside = keepUpper ? currentT <= middleStop : currentT >= middleStop;
                if (currentInside != previousInside)
                {
                    var denominator = currentT - previousT;
                    var amount = Mathf.Abs(denominator) > 0.00001f
                        ? Mathf.Clamp01((middleStop - previousT) / denominator)
                        : 0f;
                    AppendDistinct(output, Vector2.Lerp(previous, current, amount));
                }
                if (currentInside) AppendDistinct(output, current);
                previous = current;
                previousT = currentT;
                previousInside = currentInside;
            }
            RemoveDuplicateClosure(output);
        }

        static float GradientPosition(Vector2 point, Rect bounds)
        {
            var normalizedX = Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x);
            var normalizedY = Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y);
            return 0.22f * normalizedX + 0.78f * normalizedY;
        }

        static void AppendDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 0.000001f)
                points.Add(point);
        }

        static void RemoveDuplicateClosure(List<Vector2> points)
        {
            if (points.Count > 1
                && (points[points.Count - 1] - points[0]).sqrMagnitude <= 0.000001f)
                points.RemoveAt(points.Count - 1);
        }

        static Vertex GradientVertex(Vector2 point, Rect bounds,
            Color top, Color middle, Color bottom)
        {
            var t = GradientPosition(point, bounds);
            return new Vertex
            {
                position = new Vector3(point.x, point.y, Vertex.nearZ),
                tint = GradientColorAt(top, middle, bottom, t),
                uv = Vector2.zero
            };
        }

        static Color GradientColorAt(Color top, Color middle, Color bottom, float t)
        {
            var topStop = CompositeGradientEdge(top, middle);
            var bottomStop = CompositeGradientEdge(bottom, middle);
            return t <= 0.55f
                ? Color.Lerp(topStop, middle, Mathf.Clamp01(t / 0.55f))
                : Color.Lerp(middle, bottomStop, Mathf.Clamp01((t - 0.55f) / 0.45f));
        }

        static Color CompositeGradientEdge(Color edge, Color face)
        {
            var color = Color.Lerp(face, edge, edge.a);
            color.a = edge.a + face.a * (1f - edge.a);
            return color;
        }

        static Rect RoleSilhouetteBounds(Rect contentBounds)
            => Rect.MinMaxRect(contentBounds.xMin + 1f, contentBounds.yMin + 1f,
                contentBounds.xMax - 1f, contentBounds.yMax - 1f);

        static Rect ValidationSilhouetteBounds(Rect shapeBounds)
            => Rect.MinMaxRect(shapeBounds.xMin + 3f, shapeBounds.yMin + 3f,
                shapeBounds.xMax - 3f, shapeBounds.yMax - 3f);

        static Rect SelectionSilhouetteBounds(Rect shapeBounds)
            => Rect.MinMaxRect(shapeBounds.xMin - 3f, shapeBounds.yMin - 3f,
                shapeBounds.xMax + 3f, shapeBounds.yMax + 3f);

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
                BeginRoundedPolygonPath(painter, vertices, RoleCornerRadius);
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
            => Mathf.Max(0f, Mathf.Min(requested, Mathf.Min(incomingLength, outgoingLength) * 0.42f));

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

        static void BuildRoleSilhouetteSamples(NodeRole role, Rect bounds, List<Vector2> samples)
        {
            if (role != NodeRole.Provider)
            {
                var vertices = s_RolePolygonScratch ??= new List<Vector2>(8);
                BuildRolePolygon(role, bounds, vertices);
                BuildRoundedPolygonSamples(vertices, RoleCornerRadius, samples);
                return;
            }

            const int curveSteps = 8;
            const float k = 0.55228475f;
            var x0 = bounds.xMin;
            var x1 = bounds.xMax;
            var y0 = bounds.yMin;
            var y1 = bounds.yMax;
            var midY = bounds.center.y;
            var radiusX = Mathf.Min(32f, bounds.width * 0.2f);
            var radiusY = bounds.height * 0.5f;
            var topLeft = new Vector2(x0 + radiusX, y0);
            var topRight = new Vector2(x1 - radiusX, y0);
            var rightMiddle = new Vector2(x1, midY);
            var bottomRight = new Vector2(x1 - radiusX, y1);
            var bottomLeft = new Vector2(x0 + radiusX, y1);
            var leftMiddle = new Vector2(x0, midY);

            samples.Clear();
            samples.Add(topLeft);
            samples.Add(topRight);
            AppendCubicSamples(samples, topRight,
                new Vector2(x1 - radiusX + k * radiusX, y0),
                new Vector2(x1, midY - k * radiusY), rightMiddle, curveSteps);
            AppendCubicSamples(samples, rightMiddle,
                new Vector2(x1, midY + k * radiusY),
                new Vector2(x1 - radiusX + k * radiusX, y1), bottomRight, curveSteps);
            samples.Add(bottomLeft);
            AppendCubicSamples(samples, bottomLeft,
                new Vector2(x0 + radiusX - k * radiusX, y1),
                new Vector2(x0, midY + k * radiusY), leftMiddle, curveSteps);
            AppendCubicSamples(samples, leftMiddle,
                new Vector2(x0, midY - k * radiusY),
                new Vector2(x0 + radiusX - k * radiusX, y0), topLeft, curveSteps);
            RemoveDuplicateClosure(samples);
        }

        static void AppendCubicSamples(List<Vector2> samples, Vector2 start,
            Vector2 firstControl, Vector2 secondControl, Vector2 end, int steps)
        {
            for (var step = 1; step <= steps; step++)
            {
                var t = step / (float)steps;
                var oneMinusT = 1f - t;
                samples.Add(oneMinusT * oneMinusT * oneMinusT * start
                    + 3f * oneMinusT * oneMinusT * t * firstControl
                    + 3f * oneMinusT * t * t * secondControl
                    + t * t * t * end);
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
            BuildRoundedPolygonSamples(vertices, RoleCornerRadius, samples);
            return PointInPolygon(samples, point);
        }
    }
}
