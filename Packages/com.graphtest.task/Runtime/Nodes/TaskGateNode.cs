using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/DAG/Gate")]
    [NodeDoc(Language.English, "Gate", "Combines prerequisites before unlocking downstream tasks.")]
    [NodeDoc(Language.Chinese, "门控", "组合多个前置条件后解锁后续任务。")]
    [ParamDoc(Language.English, "mode", "Mode", "All means every incoming prerequisite; Any means one incoming prerequisite.")]
    [ParamDoc(Language.Chinese, "mode", "模式", "全部表示所有入边都满足；任一表示满足任一入边即可。")]
    public class TaskGateNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Gate;

        protected override void Define()
        {
            Meta("Gate", NodeRole.Control);
            AddParam("mode", TypeRef.Enum(typeof(TaskGateMode).FullName));
            AddIn("prerequisite", Arity.AtLeastOne);
            AddOut("unlocks", Arity.Many);
        }
    }
}
