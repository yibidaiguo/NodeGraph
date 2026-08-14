// CanvasDock.cs — 第 5 层（连线图编辑器），共享控件。
// 画布左下角那条小坞：缩放读数 + 全览 + 整理 + 缩略图 + 添加节点。
// 旧外壳把这些全塞进顶部工具栏（或干脆没有），于是"画布上能做什么"要抬头去别处找；
// 放在画布自己身上，手不用离开工作区。Editor/ 程序集。

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class CanvasDock : VisualElement
    {
        public const string RootClass = "ne-canvas-dock";
        public const string ButtonClass = "ne-canvas-dock-btn";
        public const string GlyphClass = "ne-canvas-dock-glyph";
        public const string ZoomClass = "ne-canvas-dock-zoom";
        public const string SeparatorClass = "ne-canvas-dock-sep";

        readonly Label m_Zoom;
        readonly Button m_MiniMap;

        public CanvasDock(Action onFrameAll, Action onTidy, Action<bool> onMiniMap, Action onAddNode)
        {
            AddToClassList(RootClass);
            style.position = Position.Absolute;
            style.left = 12f;
            style.bottom = 12f;

            m_Zoom = new Label("100%") { tooltip = Localizer.UI("ui.zoomTip", "Current canvas zoom") };
            m_Zoom.AddToClassList(ZoomClass);
            Add(m_Zoom);
            Add(Separator());

            // 只有「＋」保留字形：⤢/⌗/▣ 这些几何符号不在编辑器字体里，真机上是清一色的豆腐块。
            Add(DockButton(null, Localizer.UI("ui.frameAll", "Frame all"),
                Localizer.UI("ui.frameAllTip", "Fit every node in view"), onFrameAll));
            Add(DockButton(null, Localizer.UI("ui.tidy", "Tidy"),
                Localizer.UI("ui.tidyTip", "Lay the graph out along its flow"), onTidy));

            m_MiniMap = DockButton(null, Localizer.UI("ui.minimap", "MiniMap"),
                Localizer.UI("ui.minimapTip", "Toggle minimap"), null);
            m_MiniMap.clicked += () =>
            {
                var next = !m_MiniMap.ClassListContains("is-selected");
                m_MiniMap.EnableInClassList("is-selected", next);
                onMiniMap?.Invoke(next);
            };
            Add(m_MiniMap);

            Add(Separator());
            Add(DockButton("＋", Localizer.UI("ui.addNode", "Add Node"),
                Localizer.UI("ui.addNodeTip", "Add a node (Space or right-click the canvas)"), onAddNode));

            // 与浮层同理：这条坞骑在 GraphView 上，事件不掐断就会被画布的框选/缩放接走。
            RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            RegisterCallback<WheelEvent>(e => e.StopPropagation());
        }

        public void SetZoom(float scale) => m_Zoom.text = Mathf.RoundToInt(scale * 100f) + "%";

        public void SetMiniMapOn(bool on) => m_MiniMap.EnableInClassList("is-selected", on);

        static Button DockButton(string glyph, string label, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { tooltip = tooltip };
            button.AddToClassList(ButtonClass);
            if (!string.IsNullOrEmpty(glyph))
            {
                var icon = new Label(glyph);
                icon.AddToClassList(GlyphClass);
                button.Add(icon);
            }
            if (!string.IsNullOrEmpty(label)) button.Add(new Label(label));
            return button;
        }

        static VisualElement Separator()
        {
            var sep = new VisualElement();
            sep.AddToClassList(SeparatorClass);
            return sep;
        }
    }
}
