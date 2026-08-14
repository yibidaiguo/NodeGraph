// Breadcrumb.cs — 面包屑路径条控件（breadcrumb-crumb/-sep/--current，扁平导航 chrome）。
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

    // ---- 面包屑栏（显示嵌套路径；点击某一节即可退出到该层） ----
    public class Breadcrumb : VisualElement
    {
        readonly Action<int> m_OnClick;
        public Breadcrumb(Action<int> onClick)
        {
            m_OnClick = onClick;
            AddToClassList("breadcrumb");
            // 这条显示的是「最近访问过的图」（导航历史栈），不是层级路径。
            tooltip = Localizer.UI("ui.recentGraphs", "Recently visited graphs");
        }
        public void SetPath(IEnumerable<string> titles)
        {
            Clear();
            // 访问历史条：crumb 间插中点分隔（并列关系，不暗示层级）。
            var list = titles?.ToList() ?? new List<string>();
            // 末位是当前图 —— 顶栏胶囊上已经写着它了，这里再列一遍就是同一行出现两次同名。
            // 只列"之前去过哪"；一个都没有（刚打开、只到过一张图）就整条隐藏，不在顶栏留半截空 chrome。
            int shown = list.Count - 1;
            style.display = shown <= 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (shown <= 0) return;

            var lead = new Label(Localizer.UI("ui.recent", "Recent"));
            lead.AddToClassList("breadcrumb-lead");
            Add(lead);

            for (int i = 0; i < shown; i++)
            {
                if (i > 0)
                {
                    var sep = new Label("·");
                    sep.AddToClassList("breadcrumb-sep");
                    Add(sep);
                }
                int captured = i;
                var crumb = new Button(() => m_OnClick(captured)) { text = list[i] };
                crumb.AddToClassList("breadcrumb-crumb");
                Add(crumb);
            }
        }
    }
}
