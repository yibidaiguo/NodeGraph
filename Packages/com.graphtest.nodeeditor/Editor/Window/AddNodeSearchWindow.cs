// AddNodeSearchWindow.cs — 第 5 层（wire-graph editor，连线图编辑器），模板级别。
// 带分类的空格键"添加节点"搜索：节点类型通过 [NodeMenu] 自动注册（TypeCache）。
// 弹窗复用主题化 StringSearchWindow（与字段下拉同一控件）——原生 SearchWindow 是编辑器铬
// 深色皮肤，与编辑器主题打架；分类树以 '/' 路径分组表达。Unity 6。Editor/ 程序集。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;   // VisualElementExtensions：WorldToLocal
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset、...）

namespace NodeEditor.EditorUI
{
    public static class AddNodeSearchWindow
    {
        public static void Open(Vector2 screenPos, EditorWindow window, GraphCanvas canvas)
        {
            var entries = new Dictionary<string, (Type type, string display)>();
            foreach (var (type, path) in CollectNodeMenu().OrderBy(x => x.path))
            {
                var parts = path.Split('/');
                // 先解析定义并走当前图的可用性策略；缺失或被领域规则拒绝的定义不进入搜索结果。
                // 可用条目的显示名再走 Localizer.NodeName。
                var leafDef = NodeDefinitionLocator.ForType(type);
                if (!NodeDefinitionAvailability.Evaluate(canvas.Asset, leafDef).allowed) continue;
                var leafLabel = leafDef != null ? Localizer.NodeName(leafDef) : parts[^1];
                var display = parts.Length > 1
                    ? string.Join("/", parts.Take(parts.Length - 1)) + "/" + leafLabel
                    : leafLabel;
                entries[type.AssemblyQualifiedName] = (type, display);
            }

            StringSearchWindow.Open(screenPos, 300f, entries.Keys,
                Localizer.UI("ui.addNode", "Add Node"), '/',
                id => entries[id].display,
                id =>
                {
                    var def = NodeDefinitionLocator.ForType(entries[id].type);
                    if (def == null) return;
                    // 屏幕 -> 窗口面板空间（减去窗口的屏幕原点）-> 图局部坐标。
                    var panelMousePos = screenPos - window.position.position;
                    var graphPos = canvas.contentViewContainer.WorldToLocal(panelMousePos);
                    canvas.CreateNode(def, graphPos);
                });
        }

        // TypeCache 让新编写的子类自动出现——无需手动配置菜单。
        static IEnumerable<(Type type, string path)> CollectNodeMenu()
        {
            return TypeCache.GetTypesDerivedFrom<NodeDefinition>()
                .Where(t => !t.IsAbstract && t.GetCustomAttribute<NodeMenuAttribute>() != null)
                .Select(t => (t, t.GetCustomAttribute<NodeMenuAttribute>().Path));
        }
    }
}
