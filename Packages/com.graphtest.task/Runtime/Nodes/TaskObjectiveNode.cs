using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Objective")]
    [NodeDoc(Language.English, "Objective", "Waits until an objective reaches the required progress.")]
    [NodeDoc(Language.Chinese, "目标", "等待某个目标进度达到要求。")]
    [ParamDoc(Language.English, "objectiveId", "Objective Id", "Stable objective id reported by gameplay systems.")]
    [ParamDoc(Language.Chinese, "objectiveId", "目标 ID", "游戏系统上报的稳定目标 ID。")]
    [ParamDoc(Language.English, "requiredCount", "Required Count", "Progress count required before continuing.")]
    [ParamDoc(Language.Chinese, "requiredCount", "所需数量", "继续之前需要达到的进度数量。")]
    public class TaskObjectiveNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Objective;

        protected override void Define()
        {
            Meta("Objective", NodeRole.Control);
            AddParam("objectiveId", TypeRef.String);
            AddParam("requiredCount", TypeRef.Int);
            AddIn("in", Arity.Many);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
