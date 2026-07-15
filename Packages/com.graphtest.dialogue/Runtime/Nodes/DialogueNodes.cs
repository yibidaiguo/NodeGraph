// DialogueNodes.cs — 在已冻结的 NodeEditor 核心之上，为对话节点家族（control-flow wire-graph，
// 控制流连线图）提供共享词汇。包含若干枚举 + 抽象基类 DialogueNodeDefinition。每个具体节点
// （StartNode、LineNode、…、EndNode）都放在以类名命名的独立文件中——Unity 只会把 MonoScript
// 绑定到与文件名同名的类型，而一个没有绑定 MonoScript 的 NodeDefinition 子类，其生成的 Def .asset
// 会被序列化为带有损坏 m_Script（fileID 0）的状态，导致编辑器的添加对话框 / registry 找不到它。
// 抽象类型从不被实例化为资产，因此基类留在这里。
// 运行时程序集——不依赖任何仅编辑器的内容（red line §6）。

using NodeEditor;

namespace Dialogue
{
    // 十种对话节点类型。Action 是通用副作用节点（持一个动作单元；旧的 SetVariable/Event 已并入为动作单元）。
    // Label 位于 Jump 和 SubDialogue 之间：Jump 会把流程重新定向到 labelName 匹配的那个 Label。
    public enum DialogueNodeKind { Start, Line, Choice, Option, Condition, Action, Jump, Label, SubDialogue, End }

    // CompareOp 等比较语义已上提到框架 NodeEditor（全局通用，供可组合单元复用）；此处不再定义。

    // 每个对话节点的公共基类：钉死一个由 Kind 推导出的确定性 StableId，使得一个定义在任何机器上/
    // 任何重新生成后都解析到相同的 id，从而让已有的图继续可用。
    public abstract class DialogueNodeDefinition : NodeDefinition
    {
        public abstract DialogueNodeKind Kind { get; }
        protected override string StableId => "dialogue." + Kind;
    }
}
