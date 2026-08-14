// PickerPill.cs — 第 5 层（连线图编辑器），共享控件。
// 顶栏左侧那颗「当前在编辑哪张图」的胶囊：色点 + 名字 + ▾，点开是带搜索的切换弹层。
// 它一个人顶掉了旧外壳的三件套（左侧图列表面板 + 工具栏对象框 + 面包屑），
// 让整条左栏可以删掉（UI-STANDARD §3.1）。Editor/ 程序集。

using System;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class PickerPill : Button
    {
        public const string RootClass = "ne-picker-pill";
        public const string DotClass = "ne-picker-pill-dot";
        public const string NameClass = "ne-picker-pill-name";
        public const string CaretClass = "ne-picker-pill-caret";
        public const string OpenClass = "is-open";

        readonly Label m_Name;

        public PickerPill(Func<VisualElement, Popover> openPopover)
        {
            AddToClassList(RootClass);

            var dot = new VisualElement();
            dot.AddToClassList(DotClass);
            Add(dot);

            m_Name = new Label();
            m_Name.AddToClassList(NameClass);
            Add(m_Name);

            var caret = new Label("▾");
            caret.AddToClassList(CaretClass);
            Add(caret);

            clicked += () =>
            {
                // 已开着就是「再点一下收起」；弹层的点外关闭会主动跳过锚点，把开合权留在这里。
                if (Popover.IsOpenFor(this)) { Popover.CloseAll(); return; }
                var popover = openPopover?.Invoke(this);
                if (popover == null) return;
                AddToClassList(OpenClass);
                popover.OnClosed += () => RemoveFromClassList(OpenClass);
            };
        }

        public string Text
        {
            get => m_Name.text;
            set => m_Name.text = value ?? "";
        }
    }
}
