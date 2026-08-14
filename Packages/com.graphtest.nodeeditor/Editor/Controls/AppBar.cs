// AppBar.cs — 第 5 层（连线图编辑器），共享控件。
// 窗口顶栏：一行三区 —— 左「在哪张图 / 怎么走」，中留白，右「看什么 / 状态」。
// 取代原先那条把导航、命令、视图开关、全局设置平铺成同一权重的 Toolbar（UI-STANDARD §3.1）。
// Editor/ 程序集。

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class AppBar : VisualElement
    {
        public const string RootClass = "ne-appbar";
        public const string NavButtonClass = "ne-appbar-nav";
        public const string CommandClass = "ne-appbar-cmd";
        public const string CommandGlyphClass = "ne-appbar-cmd-glyph";
        public const string SpacerClass = "ne-appbar-spacer";

        public AppBar()
        {
            AddToClassList(RootClass);
        }

        // 弹性留白：把右侧那一组推到窗口右缘，让「在哪」和「看什么」自然分成两端。
        public VisualElement AddSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList(SpacerClass);
            Add(spacer);
            return spacer;
        }

        // 导航钮（后退/前进）：纯字形、扁平，属导航 chrome，不给按钮质感（UI-STANDARD §2.3）。
        public static Button NavButton(string glyph, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { text = glyph, tooltip = tooltip };
            button.AddToClassList(NavButtonClass);
            return button;
        }

        // 命令钮（数据 / 查找 / 溢出菜单）：会执行动作，走 premium button tokens。
        public static Button CommandButton(string glyph, string label, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { tooltip = tooltip };
            button.AddToClassList(CommandClass);

            if (!string.IsNullOrEmpty(glyph))
            {
                var icon = new Label(glyph);
                icon.AddToClassList(CommandGlyphClass);
                button.Add(icon);
            }
            if (!string.IsNullOrEmpty(label)) button.Add(new Label(label));
            return button;
        }
    }
}
