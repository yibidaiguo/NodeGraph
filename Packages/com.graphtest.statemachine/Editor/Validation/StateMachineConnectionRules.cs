// StateMachineConnectionRules.cs — 状态机族的「连接规则」集中矩阵（include/exclude，双向），通过
// ConnectionRules.RegisterRule 钩子注册进框架。矩阵按 SM0 规格「连接矩阵」节（准则#10），用实际端口名：
//   Entry.out→{State,SubMachine}；State/AnyState/SubMachine.transitions→{Transition}；
//   Transition.to→{State,SubMachine,Exit}（转移到 Exit = 结束子机/停机）；
//   入向：Transition.in←{State,AnyState,SubMachine}；State/SubMachine.in←{Entry,Transition}；Exit.in←{Transition}。
// sm.entry.target（Entry 不可直指 Transition/Exit/AnyState）即由 Entry.out 行强制。
// 判定引擎（出向/入向、端口专属优先、本地化拒绝消息 val.conn*）在框架 ConnectionRuleMatrix<TDef,TKind>——
// 本文件只持有「状态机矩阵数据」这份领域策略。非本域节点对不受影响。仅 Editor 程序集，无运行时依赖。

using UnityEditor;
using NodeEditor;

namespace StateMachine.EditorUI
{
    [InitializeOnLoad]
    public static class StateMachineConnectionRules
    {
        // —— 状态机连接矩阵：State→Transition→State 三段式的完整约定（边不带逻辑）——
        static readonly ConnectionRuleMatrix<StateMachineNodeDefinition, StateMachineNodeKind> s_Matrix =
            new ConnectionRuleMatrix<StateMachineNodeDefinition, StateMachineNodeKind>(new[]
            {
                // 出向：谁能连到谁。
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.Entry,      port = "out",         side = ConnectSide.Out, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.State, StateMachineNodeKind.SubMachine } },
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.State,      port = "transitions", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.Transition } },
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.AnyState,   port = "transitions", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.Transition } },
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.SubMachine, port = "transitions", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.Transition } },
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.Transition, port = "to",          side = ConnectSide.Out, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.State, StateMachineNodeKind.SubMachine, StateMachineNodeKind.Exit } },
                // 入向：谁的输入只接受谁（AnyState/Entry 无入端口，天然无规则）。
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.Transition, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.State, StateMachineNodeKind.AnyState, StateMachineNodeKind.SubMachine } },
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.State,      port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.Entry, StateMachineNodeKind.Transition } },
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.SubMachine, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.Entry, StateMachineNodeKind.Transition } },
                new ConnectionRuleEntry<StateMachineNodeKind> { node = StateMachineNodeKind.Exit,       port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = new[] { StateMachineNodeKind.Transition } },
            }, def => def.Kind);

        static StateMachineConnectionRules() => ConnectionRules.RegisterRule("statemachine", s_Matrix.Evaluate);

        // 直接判定入口（EditMode 测试逐格断言用；与注册进框架的是同一个矩阵实例）。
        public static ConnectionVerdict Evaluate(ConnectionContext ctx) => s_Matrix.Evaluate(ctx);
    }
}
