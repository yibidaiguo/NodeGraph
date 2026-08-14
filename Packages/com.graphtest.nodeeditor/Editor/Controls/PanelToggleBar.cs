// PanelToggleBar.cs — 第 5 层（连线图编辑器），共享控件。
// 顶栏右侧那组「看什么」开关：一段连体的扁平按钮，每个管一块浮层/窗口的显隐。
// 它是导航 chrome（回答"看哪"），所以不用按钮质感：透明底 + hover 软底 + 选中 accent-soft
//（UI-STANDARD §2.3）。与 ne-tabs 的区别是这组不互斥：各开关各自开合，不是"选中哪一档"。
// Editor/ 程序集。

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class PanelToggleBar : VisualElement
    {
        public const string RootClass = "ne-panel-seg";
        public const string ButtonClass = "ne-panel-seg-btn";

        readonly Dictionary<string, Button> m_Buttons = new();

        public PanelToggleBar()
        {
            AddToClassList(RootClass);
        }

        // pressed=false 的一次性命令（数据 / 查找）也放这组：它们同样是"去看别的东西"，
        // 与浮层开关同权重，分开摆反而让顶栏又碎成两堆。
        //
        // 只给文字不给图标：Unity 编辑器字体里没有 ◧/⛁/⌕ 这类几何字形，真机上全部渲染成同一个
        // 豆腐块 —— 四个开关配四个一模一样的方框，比不放图标更糟。
        public Button Add(string id, string label, string tooltip, bool on, Action<bool> onToggled)
        {
            var button = new Button { text = label ?? "", tooltip = tooltip };
            button.AddToClassList(ButtonClass);
            button.EnableInClassList("is-selected", on);
            button.clicked += () =>
            {
                var next = !button.ClassListContains("is-selected");
                onToggled?.Invoke(next);
            };
            m_Buttons[id] = button;
            base.Add(button);
            return button;
        }

        // 由外壳在浮层显隐真正变化后回调，让开关与浮层永远同一状态（含浮层自己的 ✕ 关闭）。
        public void SetOn(string id, bool on)
        {
            if (m_Buttons.TryGetValue(id, out var button)) button.EnableInClassList("is-selected", on);
        }

        public bool IsOn(string id) => m_Buttons.TryGetValue(id, out var button) && button.ClassListContains("is-selected");
    }
}
