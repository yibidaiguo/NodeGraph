using System;

namespace NodeEditor.EditorUI
{
    // 一个领域注册类可声明多个节点类型映射，避免运行时程序集依赖编辑器 UI。
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class NodeIconAttribute : Attribute
    {
        public Type NodeType { get; }
        public NodeIconKind Kind { get; }

        public NodeIconAttribute(Type nodeType, NodeIconKind kind)
        {
            NodeType = nodeType;
            Kind = kind;
        }
    }
}
