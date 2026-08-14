// StartNode.cs —— 对话节点定义。每个 NodeDefinition 子类都必须放在与类同名的独立文件中，
// 这样 Unity 才会为其绑定 MonoScript；否则它生成的 Def .asset 会序列化出一个损坏的
// m_Script（fileID 0），编辑器的 NodeDefinitionLocator/registry 将无法找到它。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Flow/Start")]
    [NodeDoc(Language.English, "Start", "Entry point of the dialogue; each graph has exactly one.")]
    [NodeDoc(Language.Chinese, "开始", "对话的入口，每张图有且仅有一个。")]
    public class StartNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Start;
        protected override void Define()
        {
            Meta("Start", NodeRole.Control);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
