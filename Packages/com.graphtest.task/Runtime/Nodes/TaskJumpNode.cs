using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Jump")]
    [NodeDoc(Language.English, "Jump", "Redirects step flow to a label by name.")]
    [NodeDoc(Language.Chinese, "跳转", "按标签名重定向步骤流程。")]
    [ParamDoc(Language.English, "targetLabel", "Target Label", "Name of the label to jump to.")]
    [ParamDoc(Language.Chinese, "targetLabel", "目标标签", "要跳转到的标签名。")]
    public class TaskJumpNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Jump;

        protected override void Define()
        {
            Meta("Jump", NodeRole.Control);
            AddParam("targetLabel", TypeRef.String, "task.labels");
            AddIn("in", Arity.Many);
        }
    }
}
