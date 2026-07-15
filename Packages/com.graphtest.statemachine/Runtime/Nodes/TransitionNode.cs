// TransitionNode.cs —— 状态机节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StateMachineNodes.cs）。Runtime 程序集。

using NodeEditor;

namespace StateMachine
{
    [NodeMenu("State Machine/Flow/Transition")]
    [NodeDoc(Language.English, "Transition", "Explicit transition between states: fires when its condition unit evaluates true. Edges carry no logic — condition and priority live here.")]
    [NodeDoc(Language.Chinese, "转移", "状态间的显式转移节点：条件单元求值为真即触发切换。边不带逻辑——条件与优先级全部住在本节点上（State→Transition→State 三段式）。")]
    [ParamDoc(Language.English, "condition", "Condition", "Condition unit gating this transition; empty = always true (unconditional transition). Compose from the global + state-machine condition registry.")]
    [ParamDoc(Language.Chinese, "condition", "条件", "门控本转移的条件单元；留空 = 恒真（无条件转移）。从「全局通用 + 状态机」条件注册表里下拉选择/装饰组合。")]
    [ParamDoc(Language.English, "priority", "Priority", "Evaluation order among transitions from the same source: smaller value first; the first true condition wins and short-circuits. Ties break by connection order.")]
    [ParamDoc(Language.Chinese, "priority", "优先级", "同一源节点多条转移的求值次序：数值小者先；首个条件为真的转移生效并短路后续。同优先级按连接顺序决胜。默认 0。")]
    public class TransitionNode : StateMachineNodeDefinition
    {
        public override StateMachineNodeKind Kind => StateMachineNodeKind.Transition;
        protected override void Define()
        {
            // Condition 角色：转移即「带条件的路由」。入端来自 State/AnyState/SubMachine（可多源共用），
            // 出端恰好一条指向目标（State/SubMachine/Exit；转移到 Exit = 结束子机/停机）。
            Meta("Transition", NodeRole.Condition);
            AddIn("in", Arity.AtLeastOne);
            AddOut("to", Arity.ExactlyOne);
            AddUnitParam("condition", "Condition");
            AddParam("priority", TypeRef.Int);
        }
    }
}
