// SubDialogueNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Flow/Sub Dialogue")]
    [NodeDoc(Language.English, "Sub Dialogue", "Enters another dialogue graph, then returns and continues.")]
    [NodeDoc(Language.Chinese, "子对话", "进入另一张对话图，返回后继续。")]
    [ParamDoc(Language.English, "subGraph", "Sub Graph", "The dialogue graph asset to run.")]
    [ParamDoc(Language.Chinese, "subGraph", "子图", "要运行的对话图资产。")]
    public class SubDialogueNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.SubDialogue;
        protected override void Define()
        {
            Meta("Sub Dialogue", NodeRole.Control);
            AddParam("subGraph", TypeRef.Object(typeof(NodeGraphAsset).FullName));
            AddIn("in", Arity.Many);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
