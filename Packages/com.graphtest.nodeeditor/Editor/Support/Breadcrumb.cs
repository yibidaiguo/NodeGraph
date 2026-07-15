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
        }
        public void SetPath(IEnumerable<string> titles)
        {
            Clear();
            // 扁平路径条：crumb 间插「›」分隔，末位（当前图）挂 --current 提亮成 chip。
            var list = titles?.ToList() ?? new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    var sep = new Label("›");
                    sep.AddToClassList("breadcrumb-sep");
                    Add(sep);
                }
                int captured = i;
                var crumb = new Button(() => m_OnClick(captured)) { text = list[i] };
                crumb.AddToClassList("breadcrumb-crumb");
                if (i == list.Count - 1) crumb.AddToClassList("breadcrumb-crumb--current");
                Add(crumb);
            }
        }
    }
}
