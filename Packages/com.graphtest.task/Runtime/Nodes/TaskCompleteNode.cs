using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Complete")]
    [NodeDoc(Language.English, "Complete", "Marks the active task complete.")]
    [NodeDoc(Language.Chinese, "完成", "将当前任务标记为完成。")]
    public class TaskCompleteNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Complete;

        protected override void Define()
        {
            Meta("Complete", NodeRole.Action);
            AddIn("in", Arity.Many);
        }
    }
}
