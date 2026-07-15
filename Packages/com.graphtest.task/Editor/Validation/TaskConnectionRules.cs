// TaskConnectionRules.cs — 任务族的「连接规则」集中矩阵（include/exclude，双向），通过
// ConnectionRules.RegisterRule 钩子注册进框架。外层任务线（DependencyDag）与内层步骤图（ControlFlow）
// 的连接约定都显式化在 s_Matrix 里：拖拽期实时拦截 + 校验兜底共享同一份规则源。
// 判定引擎（出向/入向、端口专属优先、本地化拒绝消息 val.conn*）在框架 ConnectionRuleMatrix<TDef,TKind>——
// 本文件只持有「任务矩阵数据」这份领域策略。仅 Editor 程序集，无运行时依赖。

using UnityEditor;
using NodeEditor;
using TaskEditor;

namespace TaskEditor.EditorUI
{
    [InitializeOnLoad]
    public static class TaskConnectionRules
    {
        static readonly TaskNodeKind[] s_DagKinds =
        {
            TaskNodeKind.Task,
            TaskNodeKind.Gate
        };

        static readonly TaskNodeKind[] s_StepSourceKinds =
        {
            TaskNodeKind.Start,
            TaskNodeKind.Objective,
            TaskNodeKind.Condition,
            TaskNodeKind.Action,
            TaskNodeKind.WaitEvent,
            TaskNodeKind.Label
        };

        static readonly TaskNodeKind[] s_StepTargetKinds =
        {
            TaskNodeKind.Objective,
            TaskNodeKind.Condition,
            TaskNodeKind.Action,
            TaskNodeKind.WaitEvent,
            TaskNodeKind.Jump,
            TaskNodeKind.Label,
            TaskNodeKind.Complete,
            TaskNodeKind.Fail
        };

        static readonly ConnectionRuleMatrix<TaskNodeDefinition, TaskNodeKind> s_Matrix =
            new ConnectionRuleMatrix<TaskNodeDefinition, TaskNodeKind>(new[]
            {
                // —— 外层任务线（DependencyDag）：Task/Gate 经 unlocks -> prerequisite 互连 ——
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Task, port = "unlocks", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_DagKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Gate, port = "unlocks", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_DagKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Task, port = "prerequisite", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_DagKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Gate, port = "prerequisite", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_DagKinds },

                // —— 内层步骤图（ControlFlow）：出向按步骤目标集，入向按步骤来源集 ——
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Start, port = "next", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_StepTargetKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Objective, port = "next", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_StepTargetKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Condition, port = "true", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_StepTargetKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Condition, port = "false", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_StepTargetKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Action, port = "next", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_StepTargetKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.WaitEvent, port = "received", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_StepTargetKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Label, port = "next", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = s_StepTargetKinds },

                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Objective, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Condition, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Action, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.WaitEvent, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Jump, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Label, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Complete, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
                new ConnectionRuleEntry<TaskNodeKind> { node = TaskNodeKind.Fail, port = "in", side = ConnectSide.In, mode = ConnectMode.Include, kinds = s_StepSourceKinds },
            }, def => def.Kind);

        static TaskConnectionRules() => ConnectionRules.RegisterRule("task", s_Matrix.Evaluate);

        // 直接判定入口（EditMode 测试逐格断言用；与注册进框架的是同一个矩阵实例）。
        public static ConnectionVerdict Evaluate(ConnectionContext ctx) => s_Matrix.Evaluate(ctx);
    }
}
