// FindWindow.cs — 查找节点弹窗（具体 EditorWindow——一类一文件）。
// 拆自 EditorSupport.cs（B3 内聚拆分：一类型一文件；类型代码逐字未改）。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{

    // 小型浮动窗口：一个搜索框，随输入实时选中并取景（frame）匹配的节点。
    class FindWindow : EditorWindow
    {
        GraphCanvas m_Canvas;

        public static void Show(GraphCanvas canvas)
        {
            var w = CreateInstance<FindWindow>();
            w.m_Canvas = canvas;
            w.titleContent = new GUIContent(Localizer.UI("ui.findNode", "Find Node"));
            w.minSize = w.maxSize = new Vector2(300, 64);
            w.ShowUtility();
        }

        void CreateGUI()
        {
            EditorUi.ConfigureWindow(rootVisualElement);
            var root = rootVisualElement;
            root.style.paddingTop = 8; root.style.paddingBottom = 8; root.style.paddingLeft = 8; root.style.paddingRight = 8;

            var field = new TextField(Localizer.UI("ui.find", "Find"));
            var info = new Label(Localizer.UI("ui.typeToMatch", "type to match node titles"));
            info.AddToClassList(EditorUi.FormHelpClass);
            field.RegisterValueChangedCallback(e =>
            {
                int n = SelectMatches(e.newValue);
                info.text = string.IsNullOrWhiteSpace(e.newValue) ? Localizer.UI("ui.typeToMatch", "type to match node titles") : $"{n} {Localizer.UI("ui.matchCount", "match(es)")}";
            });
            root.Add(field);
            root.Add(info);
            field.Focus();
        }

        int SelectMatches(string query)
        {
            m_Canvas.ClearSelection();
            var matches = m_Canvas.nodes.ToList().OfType<NodeView>()
                .Where(n => FindDialog.TitleMatches(n.title, query)).ToList();
            foreach (var n in matches) m_Canvas.AddToSelection(n);
            if (matches.Count > 0) m_Canvas.FrameSelection();
            return matches.Count;
        }
    }
}
