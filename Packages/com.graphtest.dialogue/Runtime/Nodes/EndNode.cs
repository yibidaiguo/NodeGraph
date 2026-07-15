// EndNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Flow/End")]
    [NodeDoc(Language.English, "End", "Terminates the current dialogue (or returns from a sub-dialogue).")]
    [NodeDoc(Language.Chinese, "结束", "结束当前对话（或从子对话返回）。")]
    public class EndNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.End;
        protected override void Define()
        {
            Meta("End", NodeRole.Control);
            AddIn("in", Arity.Many);
        }
    }
}
