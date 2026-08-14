// OptionNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Option")]
    [NodeDoc(Language.English, "Option", "One selectable branch under a Choice; visibility gated by an optional condition unit.")]
    [NodeDoc(Language.Chinese, "选项", "Choice 下的一个可选分支；可见性由一个可选的条件单元门控。")]
    [ParamDoc(Language.English, "optionKey", "Option Key", "Localization key for the option's display text.")]
    [ParamDoc(Language.Chinese, "optionKey", "选项键", "选项显示文案的本地化键。")]
    [ParamDoc(Language.English, "gate", "Gate", "Condition unit gating visibility; empty = always visible. Pick/compose from the condition registry.")]
    [ParamDoc(Language.Chinese, "gate", "门控", "控制可见性的条件单元；留空=总是可见。从条件注册表里下拉选择/装饰组合。")]
    public class OptionNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Option;
        protected override void Define()
        {
            Meta("Option", NodeRole.Control);
            AddParam("optionKey", TypeRef.String, "dialogue.optionKeys");   // 只列用途=选项/通用 的库 key，避免混进台词键
            // 可见性门控：gate 为空 => 始终可见；否则仅当 gate.Evaluate 成立时可见。
            // 与 Condition 节点共用同一套可组合条件单元（全局通用 + 领域），不再自带 gateKey/gateOp/gateValue。
            AddUnitParam("gate", "Condition");
            AddIn("in", Arity.ExactlyOne);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
