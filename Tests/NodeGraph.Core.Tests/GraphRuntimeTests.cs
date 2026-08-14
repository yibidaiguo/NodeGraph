// GraphRuntimeTests.cs —— 纯 C# 门禁：不装 Unity、不开编辑器，直接加载一张图并 tick 出结果。
//
// 这套测试存在的意义不是"覆盖率"，而是证明一件事：执行器真的脱离了 Unity。
// 它引用的是 NodeEditor.Runtime / Dialogue.Runtime 等纯层程序集 + 只含特性的 shim，
// 没有 UnityEngine.dll、没有 Editor、没有 PlayMode。跑得起来就说明改造成立。
//
// 覆盖（对应改造简报的交付项 4）：加载图 / tick 到 Success / tick 到 Failure /
// Running 状态保持 / 黑板读写 / 单元树嵌套求值。

using System.Collections.Generic;
using System.Linq;
using NodeEditor;
using Xunit;

namespace NodeGraph.Core.Tests
{
    public class GraphRuntimeTests
    {
        // ---- 夹具：全部用纯 C# 构造，没有一个 ScriptableObject ----

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

        // ---- 加载图 ----

        [Fact]
        public void LoadsGraphAndIndexesInstances()
        {
            var a = Node("a", "d.Start");
            var b = Node("b", "d.End");
            Wire(a, "next", b);
            var g = new GraphData { graphId = "g1", instances = { a, b }, entryInstanceIds = { "a" } };

            Assert.Same(a, g.Find("a"));
            Assert.Same(b, g.Next(a, "next"));
            Assert.Same(a, g.Entry());
            Assert.Null(g.Find("nope"));
        }

        [Fact]
        public void GraphSetResolvesSubGraphsById()
        {
            var sub = new GraphData { graphId = "sub" };
            var set = new GraphSet(new[] { sub });
            Assert.Same(sub, set.FindGraph("sub"));
            Assert.Null(set.FindGraph("missing"));
        }

        // ---- 黑板读写 ----

        [Fact]
        public void BlackboardSeedsFromDeclarationsAndRoundTrips()
        {
            var decl = new BlackboardDecl("", "")
                .Add("hp", TypeRef.Float, "100")
                .Add("dead", TypeRef.Bool, "false");
            var bb = new RuntimeBlackboard(new BlackboardSet(decl));

            Assert.Equal(100f, bb.GetF("hp"), 3);          // 声明的默认值被播种
            Assert.Equal(false, bb.Get("dead"));

            bb.Set("hp", 42f);
            Assert.Equal(42f, bb.GetF("hp"), 3);
            Assert.Null(bb.Get("未声明的键"));                // 未写入过 => null，不抛
        }

        [Fact]
        public void BlackboardLayersOverrideNearest()
        {
            var global = new BlackboardDecl("", "").Add("hp", TypeRef.Float, "1");
            var module = new BlackboardDecl("dialogue", "").Add("hp", TypeRef.Float, "2");
            // 由外到内：全局在前、模块在后 —— 更专的一档胜出。
            var set = new BlackboardSet(global, module);
            Assert.Equal("2", set.Find("hp").defaultJson);
            Assert.Equal(2f, new RuntimeBlackboard(set).GetF("hp"), 3);
        }

        // ---- 单元树嵌套求值 ----

        static NodeContext Ctx(IScopedBlackboard bb) => new NodeContext { blackboard = bb, dt = 0.016f };

        [Fact]
        public void NestedUnitTreeEvaluates()
        {
            var bb = new RuntimeBlackboard(new BlackboardSet(
                new BlackboardDecl("", "").Add("hp", TypeRef.Float, "50")));
            var ctx = Ctx(bb);

            // And( Not(false), hp >= 50 )
            var cond = new AndCondition
            {
                items = new List<ConditionUnit>
                {
                    new NotCondition { inner = new AlwaysCondition { value = false } },
                    new CompareCondition
                    {
                        left = new BlackboardProvider { key = "hp" },
                        op = CompareOp.Gte,
                        right = new ConstProvider { type = PrimitiveType.Float, value = "50" }
                    }
                }
            };
            Assert.True(cond.Evaluate(ctx));

            bb.Set("hp", 10f);
            Assert.False(cond.Evaluate(ctx));   // 同一棵树随黑板变化改判
        }

        [Fact]
        public void ActionUnitWritesBackWithSourceTyping()
        {
            var bb = new RuntimeBlackboard(new BlackboardSet(
                new BlackboardDecl("", "").Add("count", TypeRef.Int, "1")));
            var ctx = Ctx(bb);

            new SequenceAction
            {
                items = new List<ActionUnit>
                {
                    new SetVariableAction
                    {
                        key = "count",
                        value = new ArithmeticProvider
                        {
                            a = new BlackboardProvider { key = "count" },
                            op = ArithOp.Add,
                            b = new ConstProvider { type = PrimitiveType.Int, value = "41" }
                        }
                    }
                }
            }.Execute(ctx);

            // 写回按黑板当前值的类型强转：int + int 仍是 int，不是 double。
            Assert.Equal(42, bb.Get("count"));
            Assert.IsType<int>(bb.Get("count"));
        }

        // ---- tick 到 Success / Failure / Running 保持 ----

        // 一个可控的叶子单元：按脚本给定的序列逐次返回状态，用来断言编排语义。
        sealed class ScriptedControl : ControlUnit
        {
            readonly Queue<Status> m_Script;
            public int Ticks { get; private set; }
            public ScriptedControl(params Status[] script) { m_Script = new Queue<Status>(script); }
            public override Status Tick(NodeContext ctx)
            {
                Ticks++;
                return m_Script.Count > 1 ? m_Script.Dequeue() : m_Script.Peek();
            }
        }

        [Fact]
        public void SequenceTicksToSuccess()
        {
            var seq = new SequenceControl
            {
                children = new List<ControlUnit>
                {
                    new ConditionControl { condition = new AlwaysCondition { value = true } },
                    new ConditionControl { condition = new AlwaysCondition { value = true } },
                }
            };
            Assert.Equal(Status.Success, seq.Tick(Ctx(null)));
        }

        [Fact]
        public void SequenceTicksToFailure()
        {
            var second = new ScriptedControl(Status.Failure);
            var seq = new SequenceControl
            {
                children = new List<ControlUnit>
                {
                    new ConditionControl { condition = new AlwaysCondition { value = true } },
                    second,
                }
            };
            Assert.Equal(Status.Failure, seq.Tick(Ctx(null)));
            Assert.Equal(1, second.Ticks);
        }

        [Fact]
        public void SequenceShortCircuitsOnFailure()
        {
            var never = new ScriptedControl(Status.Success);
            var seq = new SequenceControl
            {
                children = new List<ControlUnit> { new ScriptedControl(Status.Failure), never }
            };
            Assert.Equal(Status.Failure, seq.Tick(Ctx(null)));
            Assert.Equal(0, never.Ticks);   // 首个非 Success 即返回，后续子节点不该被 tick
        }

        [Fact]
        public void RunningStatePersistsAcrossTicks()
        {
            // 第一拍 Running、第二拍仍 Running、第三拍 Success —— 断言 Running 会被原样透传，
            // 不会被塌缩成 bool（NodeRuntime.cs 开头那条"永远不要塌缩成 bool"的契约）。
            var leaf = new ScriptedControl(Status.Running, Status.Running, Status.Success);
            var seq = new SequenceControl { children = new List<ControlUnit> { leaf } };
            var ctx = Ctx(null);

            Assert.Equal(Status.Running, seq.Tick(ctx));
            Assert.Equal(Status.Running, seq.Tick(ctx));
            Assert.Equal(Status.Success, seq.Tick(ctx));
            Assert.Equal(3, leaf.Ticks);
        }

        [Fact]
        public void ParallelReportsRunningUntilAllSettle()
        {
            var par = new ParallelControl
            {
                requireAll = true,
                children = new List<ControlUnit>
                {
                    new ScriptedControl(Status.Success),
                    new ScriptedControl(Status.Running, Status.Success),
                }
            };
            var ctx = Ctx(null);
            Assert.Equal(Status.Running, par.Tick(ctx));   // 有 Running 且无 Failure => Running
            Assert.Equal(Status.Success, par.Tick(ctx));   // 全部成功 => Success
        }

        [Fact]
        public void InverterPassesRunningThroughUnchanged()
        {
            var inv = new InverterControl { inner = new ScriptedControl(Status.Running) };
            Assert.Equal(Status.Running, inv.Tick(Ctx(null)));   // 只翻转 Success/Failure
        }

        // ---- 参数解析（版本回填契约）----

        [Fact]
        public void ParamResolverPrefersOverrideThenSchemaDefault()
        {
            var schema = Schema("d.Line", "Line", "lineKey");
            schema.Param("lineKey").defaultJson = "默认值";
            var inst = Node("n", "d.Line");

            Assert.Equal("默认值", ParamResolver.Resolve(inst, schema, "lineKey"));   // 回填

            inst.parameterOverrides.Add(new ParamOverride { paramName = "lineKey", valueJson = "覆盖值" });
            Assert.Equal("覆盖值", ParamResolver.Resolve(inst, schema, "lineKey"));   // 覆盖优先
        }

        [Fact]
        public void SchemaSetResolvesAndReportsDuplicates()
        {
            var set = new SchemaSet(new[] { Schema("a", "A"), Schema("b", "B"), Schema("a", "A") });
            Assert.NotNull(set.FindSchema("a"));
            Assert.Null(set.FindSchema("zzz"));
            Assert.Contains("a", set.Duplicates);
        }

        // ---- 日志接缝 ----

        [Fact]
        public void GraphLogIsCollectableForAssertions()
        {
            var log = new CollectingGraphLog();
            log.Error("boom");
            Assert.Single(log.Errors);
            Assert.Empty(log.Warnings);
        }
    }
}
