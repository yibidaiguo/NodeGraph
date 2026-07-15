using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    // 标题图标只负责绘制语义 glyph；金属底座与主题色全部由共享 USS 提供。
    public sealed class NodeIconControl : VisualElement
    {
        static readonly CustomStyleProperty<Color> s_IconColor =
            new("--ne-node-icon-color");
        const float BezierCircle = 0.55228475f;

        Color m_IconColor;

        public NodeIconKind Kind { get; }

        public NodeIconControl(NodeIconKind kind)
        {
            Kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("node-icon");
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
            generateVisualContent += DrawIcon;
        }

        void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            evt.customStyle.TryGetValue(s_IconColor, out m_IconColor);
            MarkDirtyRepaint();
        }

        void DrawIcon(MeshGenerationContext context)
        {
            var bounds = contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f || m_IconColor.a <= 0f) return;

            var painter = context.painter2D;
            painter.strokeColor = m_IconColor;
            painter.lineWidth = 1.5f;

            switch (Kind)
            {
                case NodeIconKind.RoleProvider:
                    Circle(painter, bounds, 10f, 10f, 5.5f);
                    Circle(painter, bounds, 10f, 10f, 1.2f);
                    break;
                case NodeIconKind.RoleCondition:
                    Closed(painter, bounds, V(10, 3), V(17, 10), V(10, 17), V(3, 10));
                    Stroke(painter, bounds, V(7, 10), V(13, 10));
                    break;
                case NodeIconKind.RoleAction:
                    Stroke(painter, bounds, V(3, 10), V(16, 10), V(11, 5));
                    Stroke(painter, bounds, V(16, 10), V(11, 15));
                    break;
                case NodeIconKind.RoleControl:
                    Stroke(painter, bounds, V(10, 17), V(10, 10), V(5, 5));
                    Stroke(painter, bounds, V(10, 10), V(15, 5));
                    break;
                case NodeIconKind.Entry:
                    Stroke(painter, bounds, V(4, 3), V(4, 17));
                    Stroke(painter, bounds, V(6, 10), V(16, 10), V(12, 6));
                    Stroke(painter, bounds, V(16, 10), V(12, 14));
                    break;
                case NodeIconKind.Terminal:
                    RoundedRect(painter, bounds, 4, 4, 12, 12, 2);
                    Stroke(painter, bounds, V(7, 7), V(13, 13));
                    Stroke(painter, bounds, V(13, 7), V(7, 13));
                    break;
                case NodeIconKind.Dialogue:
                    RoundedRect(painter, bounds, 3, 4, 14, 10, 2);
                    Stroke(painter, bounds, V(7, 14), V(5, 17), V(5, 14));
                    Stroke(painter, bounds, V(6, 8), V(14, 8));
                    Stroke(painter, bounds, V(6, 11), V(11, 11));
                    break;
                case NodeIconKind.Choice:
                    Circle(painter, bounds, 4, 10, 1.5f);
                    Circle(painter, bounds, 16, 5, 1.5f);
                    Circle(painter, bounds, 16, 15, 1.5f);
                    Stroke(painter, bounds, V(5.5f, 10), V(9, 10), V(13.5f, 5));
                    Stroke(painter, bounds, V(9, 10), V(13.5f, 15));
                    break;
                case NodeIconKind.Option:
                    Circle(painter, bounds, 5, 10, 1.5f);
                    Stroke(painter, bounds, V(6.5f, 10), V(15, 10), V(12, 7));
                    Stroke(painter, bounds, V(15, 10), V(12, 13));
                    break;
                case NodeIconKind.Condition:
                    Closed(painter, bounds, V(10, 3), V(17, 10), V(10, 17), V(3, 10));
                    Stroke(painter, bounds, V(7, 10), V(13, 10));
                    break;
                case NodeIconKind.Action:
                    Closed(painter, bounds, V(3, 4), V(11, 4), V(11, 8), V(17, 8), V(17, 17), V(3, 17));
                    Stroke(painter, bounds, V(6, 11), V(9, 14), V(15, 7));
                    break;
                case NodeIconKind.Jump:
                    Stroke(painter, bounds, V(4, 4), V(4, 16));
                    Stroke(painter, bounds, V(6, 10), V(16, 10), V(12, 6));
                    Stroke(painter, bounds, V(16, 10), V(12, 14));
                    break;
                case NodeIconKind.Label:
                    Closed(painter, bounds, V(6, 3), V(14, 3), V(14, 17), V(10, 14), V(6, 17));
                    Stroke(painter, bounds, V(8, 7), V(12, 7));
                    break;
                case NodeIconKind.SubGraph:
                    RoundedRect(painter, bounds, 3, 4, 11, 11, 2);
                    RoundedRect(painter, bounds, 7, 7, 10, 10, 2);
                    Stroke(painter, bounds, V(10, 12), V(14, 12), V(12, 10));
                    Stroke(painter, bounds, V(14, 12), V(12, 14));
                    break;
                case NodeIconKind.Task:
                    RoundedRect(painter, bounds, 4, 5, 12, 12, 2);
                    RoundedRect(painter, bounds, 7, 3, 6, 4, 1.5f);
                    Stroke(painter, bounds, V(7, 10), V(13, 10));
                    Stroke(painter, bounds, V(7, 13), V(11, 13));
                    break;
                case NodeIconKind.Gate:
                    Stroke(painter, bounds, V(5, 4), V(5, 16));
                    Stroke(painter, bounds, V(15, 4), V(15, 16));
                    Stroke(painter, bounds, V(5, 7), V(15, 7), V(5, 13), V(15, 13));
                    break;
                case NodeIconKind.Objective:
                    Circle(painter, bounds, 10, 10, 7);
                    Circle(painter, bounds, 10, 10, 3);
                    Stroke(painter, bounds, V(10, 3), V(10, 6));
                    break;
                case NodeIconKind.WaitEvent:
                    Stroke(painter, bounds, V(5, 3), V(15, 3));
                    Stroke(painter, bounds, V(5, 17), V(15, 17));
                    Closed(painter, bounds, V(6, 4), V(14, 4), V(12, 8), V(8, 12), V(6, 16), V(14, 16), V(12, 12), V(8, 8));
                    break;
                case NodeIconKind.Complete:
                    Circle(painter, bounds, 10, 10, 7);
                    Stroke(painter, bounds, V(6, 10), V(9, 13), V(15, 6));
                    break;
                case NodeIconKind.Failure:
                    Circle(painter, bounds, 10, 10, 7);
                    Stroke(painter, bounds, V(7, 7), V(13, 13));
                    Stroke(painter, bounds, V(13, 7), V(7, 13));
                    break;
                case NodeIconKind.State:
                    Circle(painter, bounds, 10, 10, 7);
                    Circle(painter, bounds, 10, 10, 3);
                    break;
                case NodeIconKind.Transition:
                    Circle(painter, bounds, 4.5f, 10, 2);
                    Circle(painter, bounds, 15.5f, 10, 2);
                    Stroke(painter, bounds, V(6.5f, 10), V(13.5f, 10), V(11, 7.5f));
                    Stroke(painter, bounds, V(13.5f, 10), V(11, 12.5f));
                    break;
                case NodeIconKind.AnyState:
                    Circle(painter, bounds, 10, 10, 7);
                    Stroke(painter, bounds, V(10, 6), V(10, 14));
                    Stroke(painter, bounds, V(6.5f, 8), V(13.5f, 12));
                    Stroke(painter, bounds, V(13.5f, 8), V(6.5f, 12));
                    break;
            }
        }

        static Vector2 V(float x, float y) => new(x, y);

        static Vector2 Point(Rect bounds, Vector2 value) => new(
            bounds.xMin + value.x / 20f * bounds.width,
            bounds.yMin + value.y / 20f * bounds.height);

        static void Stroke(Painter2D painter, Rect bounds, params Vector2[] points)
        {
            if (points.Length < 2) return;
            painter.BeginPath();
            painter.MoveTo(Point(bounds, points[0]));
            for (var i = 1; i < points.Length; i++) painter.LineTo(Point(bounds, points[i]));
            painter.Stroke();
        }

        static void Closed(Painter2D painter, Rect bounds, params Vector2[] points)
        {
            if (points.Length < 3) return;
            painter.BeginPath();
            painter.MoveTo(Point(bounds, points[0]));
            for (var i = 1; i < points.Length; i++) painter.LineTo(Point(bounds, points[i]));
            painter.ClosePath();
            painter.Stroke();
        }

        static void Circle(Painter2D painter, Rect bounds, float x, float y, float radius)
        {
            var center = Point(bounds, V(x, y));
            var rx = radius / 20f * bounds.width;
            var ry = radius / 20f * bounds.height;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x + rx, center.y));
            painter.BezierCurveTo(new Vector2(center.x + rx, center.y + BezierCircle * ry),
                new Vector2(center.x + BezierCircle * rx, center.y + ry), new Vector2(center.x, center.y + ry));
            painter.BezierCurveTo(new Vector2(center.x - BezierCircle * rx, center.y + ry),
                new Vector2(center.x - rx, center.y + BezierCircle * ry), new Vector2(center.x - rx, center.y));
            painter.BezierCurveTo(new Vector2(center.x - rx, center.y - BezierCircle * ry),
                new Vector2(center.x - BezierCircle * rx, center.y - ry), new Vector2(center.x, center.y - ry));
            painter.BezierCurveTo(new Vector2(center.x + BezierCircle * rx, center.y - ry),
                new Vector2(center.x + rx, center.y - BezierCircle * ry), new Vector2(center.x + rx, center.y));
            painter.ClosePath();
            painter.Stroke();
        }

        static void RoundedRect(Painter2D painter, Rect bounds,
            float x, float y, float width, float height, float radius)
        {
            var x0 = x;
            var x1 = x + width;
            var y0 = y;
            var y1 = y + height;
            var r = Mathf.Min(radius, Mathf.Min(width, height) * 0.5f);
            var k = BezierCircle * r;
            painter.BeginPath();
            painter.MoveTo(Point(bounds, V(x0 + r, y0)));
            painter.LineTo(Point(bounds, V(x1 - r, y0)));
            painter.BezierCurveTo(Point(bounds, V(x1 - r + k, y0)), Point(bounds, V(x1, y0 + r - k)), Point(bounds, V(x1, y0 + r)));
            painter.LineTo(Point(bounds, V(x1, y1 - r)));
            painter.BezierCurveTo(Point(bounds, V(x1, y1 - r + k)), Point(bounds, V(x1 - r + k, y1)), Point(bounds, V(x1 - r, y1)));
            painter.LineTo(Point(bounds, V(x0 + r, y1)));
            painter.BezierCurveTo(Point(bounds, V(x0 + r - k, y1)), Point(bounds, V(x0, y1 - r + k)), Point(bounds, V(x0, y1 - r)));
            painter.LineTo(Point(bounds, V(x0, y0 + r)));
            painter.BezierCurveTo(Point(bounds, V(x0, y0 + r - k)), Point(bounds, V(x0 + r - k, y0)), Point(bounds, V(x0 + r, y0)));
            painter.ClosePath();
            painter.Stroke();
        }
    }
}
