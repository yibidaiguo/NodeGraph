// StateMachineNodeDefinition.cs —— 状态机节点定义基类（ScriptableObject）。StateMachine.Unity 程序集。
// 枚举 StateMachineNodeKind 是纯数据，留在 StateMachine.Runtime；本基类继承 NodeDefinition，属 Unity 侧。
using NodeEditor;

namespace StateMachine
{
    public abstract class StateMachineNodeDefinition : NodeDefinition
    {
        public override string Module => "statemachine";
        public abstract StateMachineNodeKind Kind { get; }
        protected override string StableId => "statemachine." + Kind;
        // 烘进 NodeSchema.kind，供纯层执行器经 StateMachineKinds.Parse 解析回枚举。
        public override string KindTag => Kind.ToString();
    }
}
