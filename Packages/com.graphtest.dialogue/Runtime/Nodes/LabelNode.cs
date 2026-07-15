// LabelNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Flow/Label")]
    [NodeDoc(Language.English, "Label", "A named flow target; a Jump with matching name continues from here.")]
    [NodeDoc(Language.Chinese, "标签", "一个具名的流程目标；同名 Jump 会从这里继续。")]
    [ParamDoc(Language.English, "labelName", "Label Name", "Name other Jumps target.")]
    [ParamDoc(Language.Chinese, "labelName", "标签名", "其它 Jump 用来定位的名称。")]
    public class LabelNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Label;
        protected override void Define()
        {
            // 一个具名的流程目标：targetLabel 匹配的 Jump 会从此 Label 的 next 继续执行。
            Meta("Label", NodeRole.Control);
            AddParam("labelName", TypeRef.String);
            AddIn("in", Arity.Many);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
