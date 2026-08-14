// OverlayPanel.cs — 第 5 层（连线图编辑器），共享控件。
// 画布浮层：贴角停靠、可拖、可折叠、可关闭，位置/折叠/显隐按 id 存 EditorPrefs 跨会话保留。
// 外壳把「变量」「检视」这类随需panel 挂成浮层，画布因此不再被固定栏切走宽度（UI-STANDARD §3.1）。
// 贴角语义借鉴 Shader Graph 的 Blackboard：记的是「离最近那个角的距离」，窗口缩放时浮层不会飘出可视区。
// Editor/ 程序集。

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class OverlayPanel : VisualElement
    {
        public const string RootClass = "ne-overlay";
        public const string HeadClass = "ne-overlay-head";
        public const string TitleClass = "ne-overlay-title";
        public const string HeadButtonClass = "ne-overlay-headbtn";
        public const string BodyClass = "ne-overlay-body";
        public const string CollapsedClass = "ne-overlay--collapsed";

        // 浮层贴哪个角：拖动时按落点就近改写，因此窗口变宽变高都不会把浮层甩到屏幕外。
        public enum Corner { TopLeft, TopRight }

        readonly string m_PrefKey;
        readonly VisualElement m_Body;
        readonly Label m_Title;
        readonly Button m_CollapseButton;

        Corner m_Corner;
        Vector2 m_Offset = new(12f, 12f);   // 离所贴角的距离（像素）
        bool m_Dragging;
        bool m_DragMoved;
        Vector2 m_DragStartPointer;
        Vector2 m_DragStartOffset;
        // 手抖阈值：没真的挪动就不算拖，免得标题条上一次点击就把存下来的位置改写掉。
        const float DragThreshold = 3f;

        // 关闭按钮：外壳据此把顶栏对应的面板开关一并熄掉（浮层自己不认识工具栏）。
        public Action OnCloseRequested;

        public VisualElement Body => m_Body;

        public OverlayPanel(string prefId, string title, Corner corner, float width)
        {
            m_PrefKey = "NodeEditor.Overlay." + (prefId ?? "");
            m_Corner = corner;
            AddToClassList(RootClass);
            // 纯布局：浮层浮在画布之上，宽度固定、高度由内容撑（上限见 ClampToParent）。
            style.position = Position.Absolute;
            style.width = width;

            var head = new VisualElement();
            head.AddToClassList(HeadClass);

            // 整条标题条就是拖拽把手（提示进 tooltip）。不放 ⠿ 之类的握把字形 ——
            // 编辑器字体里没有那个码位，画出来是个豆腐块。
            head.tooltip = Localizer.UI("ui.overlayDragTip", "Drag to move");

            m_Title = new Label(title ?? "");
            m_Title.AddToClassList(TitleClass);
            head.Add(m_Title);

            m_CollapseButton = new Button(() => Collapsed = !Collapsed);
            m_CollapseButton.AddToClassList(HeadButtonClass);
            head.Add(m_CollapseButton);

            var close = new Button(() => OnCloseRequested?.Invoke())
            {
                text = "✕",
                tooltip = Localizer.UI("ui.close", "Close")
            };
            close.AddToClassList(HeadButtonClass);
            head.Add(close);

            head.RegisterCallback<PointerDownEvent>(OnHeadPointerDown);
            head.RegisterCallback<PointerMoveEvent>(OnHeadPointerMove);
            head.RegisterCallback<PointerUpEvent>(OnHeadPointerUp);
            Add(head);

            m_Body = new VisualElement();
            m_Body.AddToClassList(BodyClass);
            m_Body.style.flexGrow = 1;
            Add(m_Body);

            // 浮层是画布的子元素，事件会一路冒泡回 GraphView 的 manipulator ——
            // 不掐断的话，在浮层里点一下就顺手起了框选、滚一下就把画布缩了。
            RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            RegisterCallback<WheelEvent>(e => e.StopPropagation());

            m_Corner = (Corner)EditorPrefs.GetInt(m_PrefKey + ".corner", (int)corner);
            m_Offset = new Vector2(
                EditorPrefs.GetFloat(m_PrefKey + ".dx", m_Offset.x),
                EditorPrefs.GetFloat(m_PrefKey + ".dy", m_Offset.y));
            SyncCollapsed(EditorPrefs.GetBool(m_PrefKey + ".collapsed", false));

            // 浮层与宿主的尺寸都可能后到：两边的 geometry 事件都要重新钳一次位置。
            RegisterCallback<GeometryChangedEvent>(_ => ClampToParent());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                parent?.RegisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
                ClampToParent();
            });
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                (evt.destinationPanel == null ? parent : null)?.UnregisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
            });
        }

        void OnParentGeometryChanged(GeometryChangedEvent _) => ClampToParent();

        public string Title
        {
            get => m_Title.text;
            set => m_Title.text = value ?? "";
        }

        public bool Collapsed
        {
            get => ClassListContains(CollapsedClass);
            set
            {
                if (Collapsed == value) return;
                SyncCollapsed(value);
                EditorPrefs.SetBool(m_PrefKey + ".collapsed", value);
            }
        }

        void SyncCollapsed(bool collapsed)
        {
            EnableInClassList(CollapsedClass, collapsed);
            m_Body.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            // 折叠钮的字形就是它的功能提示：展开时给「收起」，收起时给「展开」。
            m_CollapseButton.text = collapsed ? "▸" : "▾";
            m_CollapseButton.tooltip = collapsed
                ? Localizer.UI("ui.expand", "Expand")
                : Localizer.UI("ui.collapse", "Collapse");
        }

        // 用户意图的显隐：写回 EditorPrefs，下次开窗照此还原。
        public bool Visible
        {
            get => style.display != DisplayStyle.None;
            set
            {
                SetDisplayed(value);
                EditorPrefs.SetBool(m_PrefKey + ".visible", value);
            }
        }

        // 只改这一刻的显隐、不写 pref。给"跟随选中自动出没"这类临时收起用 ——
        // 走 Visible 的话，取消选中一次就会把"启用检视"这个用户意图也一并存成 false。
        public void SetDisplayed(bool displayed)
        {
            style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
            if (displayed) ClampToParent();
        }

        // 上次会话是否开着这个浮层。外壳在建面板开关时读它，让开关与浮层同一状态。
        public bool RestoreVisible(bool fallback)
        {
            var visible = EditorPrefs.GetBool(m_PrefKey + ".visible", fallback);
            SetDisplayed(visible);
            return visible;
        }

        void OnHeadPointerDown(PointerDownEvent evt)
        {
            // 标题条右侧的折叠/关闭钮不参与拖动，否则点一下就会被当成 0 像素的拖拽吃掉点击。
            if (evt.target is Button) return;
            m_Dragging = true;
            m_DragMoved = false;
            m_DragStartPointer = evt.position;
            m_DragStartOffset = ResolvedTopLeft();
            (evt.currentTarget as VisualElement)?.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnHeadPointerMove(PointerMoveEvent evt)
        {
            if (!m_Dragging) return;
            var delta = (Vector2)evt.position - m_DragStartPointer;
            if (!m_DragMoved && delta.magnitude < DragThreshold) return;
            m_DragMoved = true;
            SetTopLeft(m_DragStartOffset + delta);
            evt.StopPropagation();
        }

        void OnHeadPointerUp(PointerUpEvent evt)
        {
            if (!m_Dragging) return;
            m_Dragging = false;
            (evt.currentTarget as VisualElement)?.ReleasePointer(evt.pointerId);
            if (m_DragMoved) PersistPosition();
            evt.StopPropagation();
        }

        // 当前左上角在宿主坐标里的位置（贴右角时由宿主宽度反算）。
        Vector2 ResolvedTopLeft()
        {
            var host = parent != null ? parent.layout.size : Vector2.zero;
            float x = m_Corner == Corner.TopLeft ? m_Offset.x : Mathf.Max(0f, host.x - m_Offset.x - layout.width);
            return new Vector2(x, m_Offset.y);
        }

        // 拖动落点 → 就近改贴角 + 记距离。左半边贴左角，右半边贴右角。
        void SetTopLeft(Vector2 topLeft)
        {
            var host = parent != null ? parent.layout.size : Vector2.zero;
            var width = layout.width;
            float maxX = Mathf.Max(0f, host.x - width);
            float x = Mathf.Clamp(topLeft.x, 0f, maxX);
            // 标题条本身要留在窗口里，否则拖出去就再也抓不回来。
            float y = Mathf.Clamp(topLeft.y, 0f, Mathf.Max(0f, host.y - 28f));

            m_Corner = (x + width * 0.5f) <= host.x * 0.5f ? Corner.TopLeft : Corner.TopRight;
            m_Offset = new Vector2(m_Corner == Corner.TopLeft ? x : Mathf.Max(0f, host.x - x - width), y);
            ApplyPosition();
        }

        void PersistPosition()
        {
            EditorPrefs.SetInt(m_PrefKey + ".corner", (int)m_Corner);
            EditorPrefs.SetFloat(m_PrefKey + ".dx", m_Offset.x);
            EditorPrefs.SetFloat(m_PrefKey + ".dy", m_Offset.y);
        }

        // 宿主尺寸变了（窗口缩放 / 首帧布局）：保持离角距离，并把高度上限收进宿主，避免浮层顶到画布外。
        void ClampToParent()
        {
            if (parent == null) return;
            var host = parent.layout.size;
            if (host.x <= 0f || host.y <= 0f) return;

            m_Offset = new Vector2(
                Mathf.Clamp(m_Offset.x, 0f, Mathf.Max(0f, host.x - layout.width)),
                Mathf.Clamp(m_Offset.y, 0f, Mathf.Max(0f, host.y - 28f)));
            style.maxHeight = Mathf.Max(120f, host.y - m_Offset.y - 12f);
            ApplyPosition();
        }

        void ApplyPosition()
        {
            style.top = m_Offset.y;
            if (m_Corner == Corner.TopLeft)
            {
                style.left = m_Offset.x;
                style.right = StyleKeyword.Auto;
            }
            else
            {
                style.left = StyleKeyword.Auto;
                style.right = m_Offset.x;
            }
        }
    }
}
