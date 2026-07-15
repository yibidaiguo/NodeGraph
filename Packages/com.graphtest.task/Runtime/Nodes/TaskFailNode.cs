using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Fail")]
    [NodeDoc(Language.English, "Fail", "Marks the active task failed or cancelled.")]
    [NodeDoc(Language.Chinese, "失败", "将当前任务标记为失败或取消。")]
    [ParamDoc(Language.English, "reasonKey", "Reason Key", "Optional localization key explaining the failure.")]
    [ParamDoc(Language.Chinese, "reasonKey", "原因键", "可选：解释失败原因的本地化键。")]
    public class TaskFailNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Fail;

        protected override void Define()
        {
            Meta("Fail", NodeRole.Action);
            AddParam("reasonKey", TypeRef.String, "task.localizationKeys");
            AddIn("in", Arity.Many);
        }
    }
}
