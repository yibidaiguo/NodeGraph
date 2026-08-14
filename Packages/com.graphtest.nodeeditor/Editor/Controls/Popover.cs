// Popover.cs — 第 5 层（连线图编辑器），共享控件。
// 自绘弹出层：贴着锚点元素落下，点外面或按 Esc 即关。用来承载「对话切换器」「溢出菜单」这类
// 从工具栏长出来的临时面板 —— Unity 原生 GenericMenu / SearchWindow 是编辑器铬，跟不了本编辑器主题
// （与 StringSearchWindow 同一条理由，UI-STANDARD §3.4）。
// Editor/ 程序集。

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class Popover : VisualElement
    {
        public const string RootClass = "ne-popover";
        public const string SectionTitleClass = "ne-popover-section";
        public const string RowClass = "ne-popover-row";
        public const string RowLabelClass = "ne-popover-row-label";
        public const string RowCheckClass = "ne-popover-row-check";
        public const string RowShortcutClass = "ne-popover-row-shortcut";
        public const string SeparatorClass = "ne-popover-sep";

        static Popover s_Open;

        readonly VisualElement m_Root;
        readonly VisualElement m_Anchor;
        readonly EventCallback<PointerDownEvent> m_OutsideDown;
        readonly EventCallback<KeyDownEvent> m_KeyDown;

        public Action OnClosed;

        Popover(VisualElement root, VisualElement anchor, float width)
        {
            m_Root = root;
            m_Anchor = anchor;
            AddToClassList(RootClass);
            style.position = Position.Absolute;
            style.width = width;

            m_OutsideDown = evt =>
            {
                var target = evt.target as VisualElement;
                if (Contains(target)) return;
                // 锚点自己的点击留给它的开合处理器，否则「点 pill 关闭」会先关再开、看起来点不动。
                if (m_Anchor != null && m_Anchor.Contains(target)) return;
                Close();
            };
            m_KeyDown = evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                Close();
                evt.StopPropagation();
            };
        }

        // 在锚点下方打开一个弹层。build 往里填内容；同一时刻只允许一个弹层存在。
        public static Popover Open(VisualElement anchor, float width, Action<VisualElement> build)
        {
            CloseAll();
            if (anchor == null) return null;
            var root = WindowRootOf(anchor);
            if (root == null) return null;

            var popover = new Popover(root, anchor, width);
            build?.Invoke(popover);
            root.Add(popover);
            popover.BringToFront();
            s_Open = popover;

            root.RegisterCallback(popover.m_OutsideDown, TrickleDown.TrickleDown);
            root.RegisterCallback(popover.m_KeyDown, TrickleDown.TrickleDown);

            // 弹层自身的宽高要等一次布局才知道，钳边界因此推迟到下一帧。
            popover.RegisterCallback<GeometryChangedEvent>(_ => popover.PlaceUnderAnchor());
            popover.PlaceUnderAnchor();
            return popover;
        }

        public static bool IsOpenFor(VisualElement anchor) => s_Open != null && s_Open.m_Anchor == anchor;

        public static void CloseAll() => s_Open?.Close();

        public void Close()
        {
            if (s_Open == this) s_Open = null;
            m_Root?.UnregisterCallback(m_OutsideDown, TrickleDown.TrickleDown);
            m_Root?.UnregisterCallback(m_KeyDown, TrickleDown.TrickleDown);
            RemoveFromHierarchy();
            OnClosed?.Invoke();
        }

        void PlaceUnderAnchor()
        {
            if (m_Root == null || m_Anchor == null) return;
            var anchorRect = m_Root.WorldToLocal(m_Anchor.worldBound);
            var host = m_Root.layout.size;
            var size = layout.size;
            if (host.x <= 0f) return;

            float width = size.x > 0f ? size.x : resolvedStyle.width;
            // 优先左缘对齐锚点；右边放不下就改成右缘对齐，别把弹层挤出窗口。
            float x = anchorRect.xMin;
            if (x + width > host.x - 4f) x = anchorRect.xMax - width;
            style.left = Mathf.Clamp(x, 4f, Mathf.Max(4f, host.x - width - 4f));

            float y = anchorRect.yMax + 4f;
            float height = size.y > 0f ? size.y : resolvedStyle.height;
            // 下方装不下就翻到锚点上方（顶栏在窗口顶部时几乎不会走到，但停靠布局下会）。
            if (height > 0f && y + height > host.y - 4f && anchorRect.yMin - height - 4f > 0f)
                y = anchorRect.yMin - height - 4f;
            style.top = Mathf.Max(4f, y);
            style.maxHeight = Mathf.Max(120f, host.y - y - 8f);
        }

        static VisualElement WindowRootOf(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
                if (e.ClassListContains(EditorUi.WindowRootClass)) return e;
            return element?.panel?.visualTree;
        }

        // ---- 内容拼装小工具：菜单/列表两种弹层都用这几件，避免各调用方自造行 ----

        public static Label Section(string title)
        {
            var label = new Label(title ?? "");
            label.AddToClassList(SectionTitleClass);
            return label;
        }

        public static VisualElement Separator()
        {
            var sep = new VisualElement();
            sep.AddToClassList(SeparatorClass);
            return sep;
        }

        // 菜单行：左侧勾选位常驻占位（选中只换字形，不改尺寸，UI-STANDARD §2.2 规则 1）。
        public static Button MenuRow(string label, bool check, Action action, string shortcut = null)
        {
            var row = new Button(action);
            row.AddToClassList(RowClass);

            var tick = new Label(check ? "✓" : "");
            tick.AddToClassList(RowCheckClass);
            row.Add(tick);

            var text = new Label(label ?? "");
            text.AddToClassList(RowLabelClass);
            row.Add(text);

            if (!string.IsNullOrEmpty(shortcut))
            {
                var key = new Label(shortcut);
                key.AddToClassList(RowShortcutClass);
                row.Add(key);
            }
            return row;
        }
    }
}
