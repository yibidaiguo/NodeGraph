// NodeViewControl.cs — 第 5 层（连线图编辑器），模板级别。
// 每个节点的自定义视图扩展，通过 [NodeViewControl(typeof(SomeDef))] 绑定。
// 既支撑静态个性化（styling.md），也支撑实时运行时显示（debug-mode.md #5），
// 改编自 Behavior Designer 中由 [ControlType] 绑定的 TaskNodeViewControl。Editor/ 程序集。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层的数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeViewControlAttribute : Attribute
    {
        public Type NodeType { get; }
        public NodeViewControlAttribute(Type nodeType) => NodeType = nodeType;
    }

    // 每个节点自定义视图的基类。子类化 + 加特性即可绑定到某个定义类型。
    public abstract class NodeViewControl
    {
        // 向节点添加额外的 UIElements（图标、内联字段、进度条）。
        public virtual void OnAttach(NodeView view, VisualElement extraContent) { }
        // 在 play 模式下每次编辑器更新时调用；读取运行中节点的状态。
        public virtual void OnRuntimeUpdate(object runtimeNode) { }
    }

    public abstract class NodeCueControl : NodeViewControl
    {
        const long DefaultRefreshMs = 300;
        Label m_Label;
        NodeView m_View;

        protected virtual long RefreshIntervalMs => DefaultRefreshMs;
        protected virtual string CueName => "node-cue";
        protected virtual string UnsetText => Localizer.UI("ui.cue.unset", "(unset)");

        public override void OnAttach(NodeView view, VisualElement extraContent)
        {
            m_View = view;
            m_Label = new Label { name = CueName };
            m_Label.AddToClassList(EditorUi.NodeCueClass);
            m_Label.AddToClassList(EditorUi.FormHelpClass);
            extraContent.Add(m_Label);
            SetText();
            extraContent.schedule.Execute(Refresh).Every(RefreshIntervalMs);
        }

        void Refresh()
        {
            if (m_Label.panel == null) return;
            SetText();
        }

        void SetText()
        {
            m_Label.text = NormalizeText(Describe(m_View.Instance, m_View.Definition));
        }

        protected abstract string Describe(NodeInstance inst, NodeDefinition def);

        protected static string Param(NodeInstance inst, NodeDefinition def, string name) =>
            ParamResolver.Resolve(inst, def, name);

        protected string UnitDesc(NodeInstance inst, string name)
        {
            var unit = ParamResolver.ResolveUnit(inst, name);
            if (unit == null) return UnsetText;
            var attr = unit.GetType().GetCustomAttribute<UnitAttribute>();
            return attr != null ? Localizer.UI(attr.NameKey, attr.NameFallback) : unit.GetType().Name;
        }

        protected string Clip(string value, int max = 36)
        {
            if (string.IsNullOrEmpty(value)) return UnsetText;
            return value.Length <= max ? value : value.Substring(0, Math.Max(0, max - 3)) + "...";
        }

        public static string NormalizeText(string text, int maxLines = 2)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return string.Join("\n", lines.Take(Math.Max(1, maxLines)));
        }
    }

    // 发现带 [NodeViewControl] 特性的控件，并把匹配的那个附加到 NodeView 上。
    public static class NodeViewControlRegistry
    {
        static Dictionary<Type, Type> s_Map;   // 定义类型 -> 控件类型

        static void EnsureBuilt()
        {
            if (s_Map != null) return;
            s_Map = new Dictionary<Type, Type>();
            foreach (var t in TypeCache.GetTypesWithAttribute<NodeViewControlAttribute>())
            {
                if (t.IsAbstract) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    // 下面的 Activator.CreateInstance 需要一个 public 无参构造函数；跳过 + 警告，而不是抛异常。
                    UnityEngine.Debug.LogWarning($"[NodeViewControl] '{t.Name}' has no public parameterless constructor; skipping.");
                    continue;
                }
                var attr = t.GetCustomAttribute<NodeViewControlAttribute>();
                s_Map[attr.NodeType] = t;
            }
        }

        public static void AttachIfAny(NodeView view, VisualElement extraContent)
        {
            EnsureBuilt();
            var defType = view.Definition.GetType();
            if (s_Map.TryGetValue(defType, out var controlType))
            {
                var control = (NodeViewControl)Activator.CreateInstance(controlType);
                control.OnAttach(view, extraContent);
                view.AttachedControl = control;   // 保留下来，以便调试器可以调用 OnRuntimeUpdate
            }
        }
    }
}

// 自定义视图示例：一个显示进度条的 Wait 节点（对齐 Behavior Designer 的 WaitNodeViewControl）。
// [NodeViewControl(typeof(WaitDef))]
// public class WaitNodeView : NodeViewControl {
//     ProgressBar m_Bar;
//     public override void OnAttach(NodeView v, VisualElement extra) { m_Bar = new ProgressBar(); extra.Add(m_Bar); }
//     public override void OnRuntimeUpdate(object runtimeNode) {
//         var w = (WaitRuntime)runtimeNode;
//         m_Bar.highValue = w.Duration; m_Bar.value = w.Elapsed;
//     }
// }
