// AddNodeSearchWindow.cs — 第 5 层（wire-graph editor，连线图编辑器），模板级别。
// 带分类的空格键"添加节点"搜索：节点类型通过 [NodeMenu] 自动注册（TypeCache）。
// 弹窗复用主题化 StringSearchWindow（与字段下拉同一控件）——原生 SearchWindow 是编辑器铬
// 深色皮肤，与编辑器主题打架；分类树以 '/' 路径分组表达。Unity 6。Editor/ 程序集。

using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;   // VisualElementExtensions：WorldToLocal

namespace NodeEditor.EditorUI
{
    public static class AddNodeSearchWindow
    {
        public static void Open(Vector2 screenPos, EditorWindow window, GraphCanvas canvas)
        {
            var entries = NodeCatalog.Query(canvas.Asset)
                .ToDictionary(entry => entry.DefinitionType.AssemblyQualifiedName);

            StringSearchWindow.Open(screenPos, 300f, entries.Keys,
                Localizer.UI("ui.addNode", "Add Node"), '/',
                id => entries[id].DisplayPath,
                id =>
                {
                    var def = entries[id].Definition;
                    if (def == null) return;
                    // 屏幕 -> 窗口面板空间（减去窗口的屏幕原点）-> 图局部坐标。
                    var panelMousePos = screenPos - window.position.position;
                    var graphPos = canvas.contentViewContainer.WorldToLocal(panelMousePos);
                    canvas.CreateNode(def, graphPos);
                });
        }

    }
}
