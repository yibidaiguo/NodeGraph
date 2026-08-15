// RuntimeTraceTests.cs —— IRuntimeGraphTrace 的纯 C# 门禁：三个执行器各两条，
// 一条断言真实执行顺序，一条断言重复访问在轨迹里照样出现、且重跑会清空。
//
// 这套测试守的是下游对拍的那一半：在 IRuntimeGraphTrace 之前，IRuntimeGraph 只有快照式的
// StatusOf，下游拿它跟自带执行器比只能按集合比——顺序错了照样绿，循环走了三圈也看不出来。

using System;
using System.Collections.Generic;
using System.Linq;
using Dialogue;
using NodeEditor;
using StateMachine;
using TaskEditor;
using Xunit;

namespace NodeGraph.Core.Tests
{
    public class RuntimeTraceTests
    {
        // ---- 共用夹具：全部纯 C# 构造，没有一个 ScriptableObject（照 GraphRuntimeTests） ----

        static NodeSchema Schema(string id, string kind, params string[] paramNames) => new NodeSchema
        {
            id = id,
            kind = kind,
            displayName = id,
            parameters = paramNames.Select(n => new ParamDef { name = n, type = TypeRef.String }).ToList(),
        };

        static NodeInstance Node(string id, string defId) =>
            new NodeInstance { instanceId = id, definitionId = defId, position = new Vec2(0, 0) };

        static void Wire(NodeInstance from, string port, NodeInstance to) =>
            from.connections.Add(new Connection { fromPort = port, toInstanceId = to.instanceId, toPort = "in" });

        static void Param(NodeInstance inst, string name, string value) =>
            inst.parameterOverrides.Add(new ParamOverride { paramName = name, valueJson = value });

        static void UnitSlot(NodeInstance inst, string slot, Unit unit) =>
            inst.unitOverrides.Add(new UnitOverride { paramName = slot, value = unit });

        static List<string> Ids(IRuntimeGraphTrace runtime) =>
            runtime.Trace.Select(v => v.InstanceId).ToList();

        static BlackboardSet EmptyBlackboard() => new BlackboardSet(new BlackboardDecl("", ""));

        // 由测试从外部翻转的条件单元（照 GraphRuntimeTests 里 ScriptedControl 的做法）。
        sealed class ScriptedCondition : ConditionUnit
        {
            public Func<bool> fn;
            public override bool Evaluate(NodeContext ctx) => fn();
        }

        // 第 trueFromCall 次求值起返回 true。用于「第一圈走假分支、第二圈走真分支」的收敛型循环。
        sealed class CountingCondition : ConditionUnit
        {
            public int trueFromCall = 1;
            public int calls;
            public override bool Evaluate(NodeContext ctx) => ++calls >= trueFromCall;
        }

        // 状态机测试要跨 tick 控制两条转移，用一个可变的持有者传进闭包。
        sealed class Flags { public bool first; public bool second; }

        // ================================ DialogueRunner ================================

        // start -> L1(Line) -> lbl(Label "loop") -> L2(Line) -> jmp(Jump "loop")
        // Jump 解析回 lbl 的 next（也就是 L2 本身），于是 L2 在一次播放里被走到两次。
        static DialogueRunner NewDialogue(out GraphData graph)
        {
            var schemas = new SchemaSet(new[]
            {
                Schema("d.Start", "Start"),
                Schema("d.Line",  "Line",  "lineKey"),
                Schema("d.Label", "Label", "labelName"),
                Schema("d.Jump",  "Jump",  "targetLabel"),
            });

            var start = Node("start", "d.Start");
            var l1    = Node("L1",    "d.Line");
            var lbl   = Node("lbl",   "d.Label");
            var l2    = Node("L2",    "d.Line");
            var jmp   = Node("jmp",   "d.Jump");

            Param(l1,  "lineKey",     "line.one");
            Param(l2,  "lineKey",     "line.two");
            Param(lbl, "labelName",   "loop");
            Param(jmp, "targetLabel", "loop");

            Wire(start, "next", l1);
            Wire(l1,    "next", lbl);
            Wire(lbl,   "next", l2);
            Wire(l2,    "next", jmp);

            graph = new GraphData
            {
                graphId = "dlg",
                instances = { start, l1, lbl, l2, jmp },
                entryInstanceIds = { "start" },
            };
            return new DialogueRunner(schemas, EmptyBlackboard(), null, "zh");
        }

        [Fact]
        public void DialogueTraceRecordsRealVisitOrder()
        {
            var runner = NewDialogue(out var graph);

            runner.Run(graph);   // start -> L1，停在 Line 上
            runner.Advance();    // lbl -> L2，再停
            runner.Advance();    // jmp -> 跳回 L2

            Assert.Equal(new[] { "start", "L1", "lbl", "L2", "jmp", "L2" }, Ids(runner));
            Assert.All(runner.Trace, v => Assert.Equal("dlg", v.GraphId));
        }

        [Fact]
        public void DialogueTraceKeepsRevisitsAndResetsOnRerun()
        {
            var runner = NewDialogue(out var graph);
            runner.Run(graph);
            runner.Advance();
            runner.Advance();

            var ids = Ids(runner);
            Assert.Equal(2, ids.Count(id => id == "L2"));                  // 重复访问没有被去重压掉
            Assert.Equal("jmp", ids[ids.LastIndexOf("L2") - 1]);           // 第二次是跳转带回来的

            runner.ClearTrace();
            Assert.Empty(runner.Trace);

            runner.Run(graph);                                             // 重跑：轨迹从头开始
            Assert.Equal(new[] { "start", "L1" }, Ids(runner));
        }

        // ============================== StateMachineRunner ==============================

        // entry -out-> A -transitions-> T1 -to-> B -transitions-> T2 -to-> A
        // 两条转移的 condition 槽由测试翻转，于是 A -> B -> A 三步进入序里 A 出现两次。
        static StateMachineRunner NewMachine(Flags flags)
        {
            var schemas = new SchemaSet(new[]
            {
                Schema("sm.Entry",      "Entry"),
                Schema("sm.State",      "State"),
                Schema("sm.Transition", "Transition", "priority"),
            });

            var entry = Node("entry", "sm.Entry");
            var a     = Node("A",     "sm.State");
            var b     = Node("B",     "sm.State");
            var t1    = Node("T1",    "sm.Transition");
            var t2    = Node("T2",    "sm.Transition");

            UnitSlot(t1, "condition", new ScriptedCondition { fn = () => flags.first });
            UnitSlot(t2, "condition", new ScriptedCondition { fn = () => flags.second });

            Wire(entry, "out",         a);
            Wire(a,     "transitions", t1);
            Wire(t1,    "to",          b);
            Wire(b,     "transitions", t2);
            Wire(t2,    "to",          a);

            var graph = new GraphData
            {
                graphId  = "sm",
                instances = { entry, a, b, t1, t2 },
            };
            var ctx = new StateMachineRunContext(new StateMachineBlackboard(EmptyBlackboard()));
            return new StateMachineRunner(graph, schemas, ctx);
        }

        [Fact]
        public void StateMachineTraceRecordsEnterOrder()
        {
            var flags = new Flags();
            var machine = NewMachine(flags);

            machine.Start();                                       // 进入初始态 A
            flags.first = true;
            machine.Tick(0.016f);                                  // A -> B
            flags.first = false; flags.second = true;
            machine.Tick(0.016f);                                  // B -> A

            Assert.Equal(new[] { "A", "B", "A" }, Ids(machine));
            Assert.All(machine.Trace, v => Assert.Equal("sm", v.GraphId));
        }

        [Fact]
        public void StateMachineTraceKeepsRevisitsAndResetsOnRestart()
        {
            var flags = new Flags();
            var machine = NewMachine(flags);

            machine.Start();
            flags.first = true;
            machine.Tick(0.016f);
            flags.first = false; flags.second = true;
            machine.Tick(0.016f);

            var ids = Ids(machine);
            Assert.Equal(2, ids.Count(id => id == "A"));            // 回到 A 在轨迹里看得见
            Assert.Equal(0, ids.IndexOf("A"));
            Assert.Equal(2, ids.LastIndexOf("A"));

            machine.ClearTrace();
            Assert.Empty(machine.Trace);

            machine.Start();                                       // 重启：轨迹只剩新一轮的初始态
            Assert.Equal(new[] { "A" }, Ids(machine));
        }

        // ================================== TaskRunner ==================================

        // 步骤图：s -> lbl("loop") -> cond -false-> jmp("loop") -> （跳回 lbl 的 next，即 cond）
        //                                 -true--> comp
        // cond 第一次求值为假、第二次为真，于是 cond 在一次运行里被走到两次并收敛到 Complete。
        static TaskRunner NewTask()
        {
            var schemas = new SchemaSet(new[]
            {
                Schema("t.Task",      "Task",      "taskId", "repeatable"),
                Schema("t.Start",     "Start"),
                Schema("t.Label",     "Label",     "labelName"),
                Schema("t.Condition", "Condition"),
                Schema("t.Jump",      "Jump",      "targetLabel"),
                Schema("t.Complete",  "Complete"),
            });

            var s    = Node("s",    "t.Start");
            var lbl  = Node("lbl",  "t.Label");
            var cond = Node("cond", "t.Condition");
            var jmp  = Node("jmp",  "t.Jump");
            var comp = Node("comp", "t.Complete");

            Param(lbl, "labelName",   "loop");
            Param(jmp, "targetLabel", "loop");
            UnitSlot(cond, "predicate", new CountingCondition { trueFromCall = 2 });

            Wire(s,    "next",  lbl);
            Wire(lbl,  "next",  cond);
            Wire(cond, "true",  comp);
            Wire(cond, "false", jmp);

            var steps = new GraphData
            {
                graphId  = "steps",
                instances = { s, lbl, cond, jmp, comp },
                entryInstanceIds = { "s" },
            };

            // 任务 DAG：一个 Task 节点，taskId = "quest"，stepGraph 指向上面的步骤图。
            // repeatable = true 让第二次 StartTask 能再跑一遍（用来断言轨迹会重置）。
            var task = Node("t1", "t.Task");
            Param(task, "taskId",     "quest");
            Param(task, "repeatable", "true");
            task.graphRefs.Add(new GraphRef { paramName = "stepGraph", graphId = "steps" });

            var taskGraph = new GraphData { graphId = "tasks", instances = { task } };

            return new TaskRunner(schemas, taskGraph, EmptyBlackboard(), new GraphSet(new[] { steps }));
        }

        [Fact]
        public void TaskTraceRecordsRealVisitOrder()
        {
            var runner = NewTask();

            Assert.True(runner.StartTask("quest"));

            Assert.Equal(new[] { "s", "lbl", "cond", "jmp", "cond", "comp" }, Ids(runner));
            Assert.All(runner.Trace, v => Assert.Equal("steps", v.GraphId));
        }

        [Fact]
        public void TaskTraceKeepsRevisitsAndResetsOnRestart()
        {
            var runner = NewTask();
            runner.StartTask("quest");

            var ids = Ids(runner);
            Assert.Equal(2, ids.Count(id => id == "cond"));                // 绕了一圈的 cond 出现两次
            Assert.Equal("jmp", ids[ids.LastIndexOf("cond") - 1]);         // 第二次是跳转带回来的

            runner.ClearTrace();
            Assert.Empty(runner.Trace);

            Assert.True(runner.StartTask("quest"));                        // 可重复任务再跑一遍
            Assert.Equal(new[] { "s", "lbl", "cond", "comp" }, Ids(runner));// 条件已为真，这轮不绕圈
        }
    }
}
