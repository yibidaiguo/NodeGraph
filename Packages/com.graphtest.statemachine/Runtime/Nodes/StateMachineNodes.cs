// StateMachineNodes.cs — 在已冻结的 NodeEditor 核心之上，为状态机节点家族（control-flow wire-graph，
// FSM = 控制流的强运行时变体）提供共享词汇。包含节点种类枚举 + 抽象基类 StateMachineNodeDefinition。
// 每个具体节点（EntryNode、StateNode、…、ExitNode）都放在以类名命名的独立文件中——Unity 只会把
// MonoScript 绑定到与文件名同名的类型，而一个没有绑定 MonoScript 的 NodeDefinition 子类，其生成的
// Def .asset 会被序列化为带有损坏 m_Script（fileID 0）的状态（硬规则 A1；抽象基类与 enum 不在此限）。
// 运行时程序集——不依赖任何仅编辑器的内容。

using NodeEditor;

namespace StateMachine
{
    // 六种状态机节点类型。State→Transition→State 三段式：转移条件/优先级全部住在
    // Transition 节点上（边不带逻辑）；AnyState 是任意状态转移源；SubMachine 承载
    // 分层状态机（HSM）；Exit 是子机返回点（顶层出现 = 状态机结束/停机）。
    public enum StateMachineNodeKind { Entry, State, Transition, AnyState, SubMachine, Exit }

    // StateMachineNodeDefinition（ScriptableObject 基类）已移至 Unity/Nodes/StateMachineNodeDefinition.cs
    //（StateMachine.Unity 程序集）。执行器不再按类型强转定义，改读 NodeSchema.kind——见下。

    // 把 NodeSchema.kind 字符串解析回领域枚举（由 StateMachineNodeDefinition.KindTag 烘出）。
    // 解析失败返回 null，由调用方按语义兜底。
    public static class StateMachineKinds
    {
        public static StateMachineNodeKind? Parse(string kind) =>
            System.Enum.TryParse<StateMachineNodeKind>(kind, out var k) ? k : (StateMachineNodeKind?)null;
    }
}
