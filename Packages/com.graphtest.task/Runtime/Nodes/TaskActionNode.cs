using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Action")]
    [NodeDoc(Language.English, "Action", "Runs a composable action unit and continues.")]
    [NodeDoc(Language.Chinese, "动作", "执行一个可组合动作单元后继续。")]
    [ParamDoc(Language.English, "actions", "Actions", "Action unit to run.")]
    [ParamDoc(Language.Chinese, "actions", "动作", "要执行的动作单元。")]
    public class TaskActionNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Action;

        protected override void Define()
        {
            Meta("Action", NodeRole.Action);
            AddUnitParam("actions", "Action");
            AddIn("in", Arity.Many);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
