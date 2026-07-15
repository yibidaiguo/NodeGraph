using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/Steps/Label")]
    [NodeDoc(Language.English, "Label", "Named target for Jump nodes.")]
    [NodeDoc(Language.Chinese, "标签", "供跳转节点定位的命名目标。")]
    [ParamDoc(Language.English, "labelName", "Label Name", "Unique name inside this step graph.")]
    [ParamDoc(Language.Chinese, "labelName", "标签名", "本步骤图内唯一的标签名。")]
    public class TaskLabelNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Label;

        protected override void Define()
        {
            Meta("Label", NodeRole.Control);
            AddParam("labelName", TypeRef.String);
            AddIn("in", Arity.Many);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
