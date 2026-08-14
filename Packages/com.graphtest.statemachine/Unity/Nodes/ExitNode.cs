// ExitNode.cs —— 状态机节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StateMachineNodes.cs）。Runtime 程序集。

using NodeEditor;

namespace StateMachine
{
    [NodeMenu("State Machine/Core/Exit")]
    [NodeDoc(Language.English, "Exit", "Return point of a sub machine: reaching it pops back to the parent layer. In a top-level graph it stops the machine. Has no output.")]
    [NodeDoc(Language.Chinese, "出口", "子状态机的返回点：转移到它即结束子机、回到父层继续。在顶层图中出现 = 状态机结束/停机。无出边。")]
    public class ExitNode : StateMachineNodeDefinition
    {
        public override StateMachineNodeKind Kind => StateMachineNodeKind.Exit;
        protected override void Define()
        {
            // 只收不发：入边来自 Transition（转移到 Exit = 结束）；无出端口即天然无出边。
            Meta("Exit", NodeRole.Control);
            AddIn("in", Arity.Many);
        }
    }
}
