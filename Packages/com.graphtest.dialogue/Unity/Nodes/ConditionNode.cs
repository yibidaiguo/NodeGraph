// ConditionNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Logic/Condition")]
    [NodeDoc(Language.English, "Condition", "Routes flow to its true / false outputs by evaluating a composable condition unit.")]
    [NodeDoc(Language.Chinese, "条件", "按一个可组合条件单元的求值，把流程分到 true / false 两个出口。")]
    [ParamDoc(Language.English, "predicate", "Predicate", "Condition unit to evaluate; pick/compose from the global + dialogue condition registry.")]
    [ParamDoc(Language.Chinese, "predicate", "谓词", "要求值的条件单元；从「全局通用 + 对话」条件注册表里下拉选择/装饰组合。")]
    public class ConditionNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Condition;
        protected override void Define()
        {
            // Control 角色：通过 true/false 两个出口路由流程；判定本身委托给可组合条件单元（predicate 槽），
            // 不再自带 variableKey/op/value 参数（旧设计已废除）。
            Meta("Condition", NodeRole.Control);
            AddUnitParam("predicate", "Condition");
            AddIn("in", Arity.Many);
            AddOut("true", Arity.ExactlyOne);
            AddOut("false", Arity.ExactlyOne);
        }
    }
}
