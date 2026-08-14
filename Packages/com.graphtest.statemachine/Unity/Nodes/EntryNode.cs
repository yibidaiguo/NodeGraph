// EntryNode.cs —— 状态机节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StateMachineNodes.cs）。Runtime 程序集。

using NodeEditor;

namespace StateMachine
{
    [NodeMenu("State Machine/Core/Entry")]
    [NodeDoc(Language.English, "Entry", "Pinned machine entry; its single output designates the initial state (State or Sub Machine).")]
    [NodeDoc(Language.Chinese, "入口", "钉住的状态机唯一入口；其唯一出口指向初始状态（State 或 Sub Machine），启动即进入该状态。")]
    public class EntryNode : StateMachineNodeDefinition
    {
        public override StateMachineNodeKind Kind => StateMachineNodeKind.Entry;
        protected override void Define()
        {
            // 唯一入口：恰好一条出边指向初始状态（连接矩阵限定目标为 State/SubMachine，SM2 落地）。
            Meta("Entry", NodeRole.Control);
            AddOut("out", Arity.ExactlyOne);
        }
    }
}
