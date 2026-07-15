using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Start")]
    [NodeDoc(Language.English, "Start", "Entry point of a task step graph.")]
    [NodeDoc(Language.Chinese, "开始", "任务步骤图的入口。")]
    public class TaskStartNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Start;

        protected override void Define()
        {
            Meta("Start", NodeRole.Control);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
