using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Condition")]
    [NodeDoc(Language.English, "Condition", "Branches by evaluating a composable condition unit.")]
    [NodeDoc(Language.Chinese, "条件", "通过可组合条件单元求值后分支。")]
    [ParamDoc(Language.English, "predicate", "Predicate", "Condition unit used for branching.")]
    [ParamDoc(Language.Chinese, "predicate", "谓词", "用于分支判断的条件单元。")]
    public class TaskConditionNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Condition;

        protected override void Define()
        {
            Meta("Condition", NodeRole.Control);
            AddUnitParam("predicate", "Condition");
            AddIn("in", Arity.Many);
            AddOut("true", Arity.ExactlyOne);
            AddOut("false", Arity.ExactlyOne);
        }
    }
}
