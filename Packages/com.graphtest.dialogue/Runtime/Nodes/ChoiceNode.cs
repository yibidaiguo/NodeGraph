// ChoiceNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Choice")]
    [NodeDoc(Language.English, "Choice", "Presents the player with options; the options port wires to Option nodes, fallback is taken when none are visible.")]
    [NodeDoc(Language.Chinese, "选择", "给玩家若干选项；options 出口接 Option，fallback 在没有可见选项时兜底。")]
    public class ChoiceNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Choice;
        protected override void Define()
        {
            Meta("Choice", NodeRole.Control);
            AddIn("in", Arity.Many);
            AddOut("options", Arity.AtLeastOne);   // 每个都连接到一个 Option
            AddOut("fallback", Arity.Optional);    // 当没有任何 option 可见时走此分支 —— 避免死锁
        }
    }
}
