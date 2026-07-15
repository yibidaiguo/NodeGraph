// NodeHoverTooltip.cs — 悬停节点满 1 秒后在其旁弹出的本地化"功能 tooltip"：节点功能说明 + 各参数（本地化名 +
// 说明 + 当前值）。全局只维持一个 tooltip 元素，挂在面板根上（不随画布缩放/平移）。文案全部走 Localizer
// （按编辑器语言：属性 → 表 → 英文回退）。Editor 程序集。

using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;   // ParamResolver / NodeDefinition / NodeInstance（Runtime 数据类型）

namespace NodeEditor.EditorUI
{
    public static class NodeHoverTooltip
    {
        static VisualElement s_Tip;

        public static void Show(NodeView node)
        {
            Hide();
            if (node?.panel == null || node.Definition == null) return;
            // 挂到 GraphCanvas（与 banner/minimap 同级）——那一层主题/字体已就绪，Label 能正常渲染文字；
            // 而 panel.visualTree（编辑器宿主根）那层未必继承到字体，会出现"框在、字不显示"。
            var canvas = node.GetFirstAncestorOfType<GraphCanvas>();
            if (canvas == null) return;
            var def = node.Definition; var inst = node.Instance;

            var tip = new VisualElement { name = "node-hover-tip" };
            tip.AddToClassList("node-hover-tip");
            tip.pickingMode = PickingMode.Ignore;   // 不挡鼠标
            tip.style.position = Position.Absolute;
            // 固定宽度：中文（CJK）可在任意字之间换行，自动宽度 + whiteSpace:Normal 会把容器塌缩成 1 字宽
            // （表现为"一个小黑框、里面什么都没有"），必须给定宽度，文字才在框内正常换行。
            tip.style.width = 320;
            tip.style.flexShrink = 0;

            var title = new Label(Localizer.NodeName(def));
            title.AddToClassList(EditorUi.NodeHoverTitleClass);
            tip.Add(title);

            var desc = Localizer.NodeDesc(def);
            if (!string.IsNullOrEmpty(desc))
            {
                var d = new Label(desc);
                d.AddToClassList(EditorUi.NodeHoverDescClass);
                tip.Add(d);
            }

            foreach (var pd in def.Parameters)
            {
                string pn = Localizer.ParamName(def, pd.name);
                string val = ParamResolver.Resolve(inst, def, pd.name);
                string pdesc = Localizer.ParamDesc(def, pd.name);
                string text = "• " + pn + (string.IsNullOrEmpty(val) ? "" : " = " + val);
                if (!string.IsNullOrEmpty(pdesc)) text += "  — " + pdesc;
                var row = new Label(text);
                row.AddToClassList(EditorUi.NodeHoverParamClass);
                tip.Add(row);
            }

            // 坐标用 WorldToLocal 把节点的面板坐标换算到 canvas 本地坐标：默认放节点右侧，越过右边界则放左侧。
            var wb = node.worldBound;
            Vector2 tl = canvas.WorldToLocal(new Vector2(wb.xMax + 8f, wb.yMin));
            float x = tl.x;
            float top = Mathf.Max(4f, tl.y);
            if (x + 320f > canvas.contentRect.width)
            {
                float leftX = canvas.WorldToLocal(new Vector2(wb.xMin - 8f, wb.yMin)).x;
                x = Mathf.Max(4f, leftX - 320f);
            }
            tip.style.left = x;
            tip.style.top = top;
            canvas.Add(tip);
            s_Tip = tip;
        }

        public static void Hide()
        {
            if (s_Tip != null) { s_Tip.RemoveFromHierarchy(); s_Tip = null; }
        }
    }
}
