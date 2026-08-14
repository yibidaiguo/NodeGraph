// AnyStateNode.cs —— 状态机节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StateMachineNodes.cs）。Runtime 程序集。

using NodeEditor;

namespace StateMachine
{
    [NodeMenu("State Machine/Flow/Any State")]
    [NodeDoc(Language.English, "Any State", "Transition source from any active state (e.g. -> Death). Has no input; its transitions are evaluated before the current node's at the same layer. Targets already on the active path are skipped (anti-jitter).")]
    [NodeDoc(Language.Chinese, "任意状态", "任意状态转移源：无论当前处于哪个状态，其转移都参与求值（如 →死亡）。无入边（有入边 = 校验 ERROR）；同层内先于当前节点的转移求值。目标若已在当前活动路径上则跳过不触发（防每 tick 抖动）。")]
    public class AnyStateNode : StateMachineNodeDefinition
    {
        public override StateMachineNodeKind Kind => StateMachineNodeKind.AnyState;
        protected override void Define()
        {
            // source-only 伪节点：无入边，天然不被 entry 播种的 BFS 覆盖——声明 ReachabilitySeed
            // 约束，把自己并进 CheckReachability 的播种集，避免被误标「dead content」WARN。
            Meta("Any State", NodeRole.Control);
            AddOut("transitions", Arity.Many);
            AddConstraint(new ReachabilitySeed());
        }
    }
}
