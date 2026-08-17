// GraphAuthoringCatalogTests.cs —— AI 创作能力目录的纯 C# 契约门禁。

using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor;
using Xunit;

namespace NodeGraph.Core.Tests
{
    public class GraphAuthoringCatalogTests
    {
        [Fact]
        public void BuildSortsOrdinalCopiesFieldsAndDoesNotMutateInputs()
        {
            var listType = new TypeRef
            {
                kind = TypeKind.List,
                element = TypeRef.Enum("Game.Tag")
            };
            var domain = new NodeSchema
            {
                id = "dialogue.z-line",
                displayName = "Line",
                module = "dialogue",
                kind = "Line",
                role = NodeRole.Action,
                runtimeKind = RuntimeKind.EnterExecuteExit,
                version = 7,
                inputPorts = new List<PortDef>
                {
                    new PortDef { name = "z-flow", type = TypeRef.Unit("Action"), arity = new Arity { kind = ArityKind.Range, min = 1, max = 3 } },
                    new PortDef { name = "a-tags", type = listType, arity = Arity.Optional },
                },
                outputPorts = new List<PortDef>
                {
                    new PortDef { name = "next", type = TypeRef.String, arity = Arity.AtLeastOne },
                },
                parameters = new List<ParamDef>
                {
                    new ParamDef { name = "z-speaker", type = TypeRef.Object("Dialogue.Speaker"), defaultJson = "null", choiceSource = "dialogue.speakers" },
                    new ParamDef { name = "a-speed", type = TypeRef.Float, defaultJson = "1.0", hasBounds = true, boundsMin = 0.25f, boundsMax = 3f },
                }
            };
            var otherModule = new NodeSchema { id = "task.hidden", module = "task" };
            var core = new NodeSchema { id = "core.a", displayName = "Common", role = NodeRole.Provider, runtimeKind = RuntimeKind.Evaluate };
            var schemas = new List<NodeSchema> { domain, otherModule, core };
            var originalSchemas = schemas.ToArray();
            var originalInputs = domain.inputPorts.ToArray();
            var originalParameters = domain.parameters.ToArray();

            var catalog = GraphAuthoringCatalogBuilder.Build(
                "dialogue",
                schemas,
                new UnitAuthoringCatalog(Array.Empty<Type>()),
                Array.Empty<IBlackboardDecl>());

            Assert.Equal("dialogue", catalog.Module);
            Assert.Equal(new[] { "core.a", "dialogue.z-line" }, catalog.Definitions.Select(definition => definition.Id));
            Assert.Equal(originalSchemas, schemas);
            Assert.Equal(originalInputs, domain.inputPorts);
            Assert.Equal(originalParameters, domain.parameters);
            Assert.Same(listType, domain.inputPorts[1].type);

            var dto = catalog.Definitions[1];
            Assert.Equal("Line", dto.Name);
            Assert.Equal("dialogue", dto.Module);
            Assert.Equal("Line", dto.Kind);
            Assert.Equal(NodeRole.Action, dto.Role);
            Assert.Equal(RuntimeKind.EnterExecuteExit, dto.Runtime);
            Assert.Equal(7, dto.Version);

            Assert.Equal(new[] { "a-tags", "z-flow" }, dto.Inputs.Select(port => port.Name));
            Assert.Equal(TypeKind.List, dto.Inputs[0].Type.Kind);
            Assert.Equal(TypeKind.Enum, dto.Inputs[0].Type.Element.Kind);
            Assert.Equal("Game.Tag", dto.Inputs[0].Type.Element.Name);
            Assert.Equal(ArityKind.Optional, dto.Inputs[0].Arity.kind);
            Assert.Equal(TypeKind.Unit, dto.Inputs[1].Type.Kind);
            Assert.Equal("Action", dto.Inputs[1].Type.Name);
            Assert.Equal((1, 3), (dto.Inputs[1].Arity.min, dto.Inputs[1].Arity.max));
            Assert.Equal("next", Assert.Single(dto.Outputs).Name);

            Assert.Equal(new[] { "a-speed", "z-speaker" }, dto.Parameters.Select(parameter => parameter.Name));
            var bounded = dto.Parameters[0];
            Assert.Equal(TypeKind.Primitive, bounded.Type.Kind);
            Assert.Equal(PrimitiveType.Float, bounded.Type.Primitive);
            Assert.Equal("1.0", bounded.DefaultJson);
            Assert.True(bounded.HasBounds);
            Assert.Equal(0.25f, bounded.BoundsMin);
            Assert.Equal(3f, bounded.BoundsMax);
            var choice = dto.Parameters[1];
            Assert.Equal(TypeKind.Object, choice.Type.Kind);
            Assert.Equal("Dialogue.Speaker", choice.Type.Name);
            Assert.Equal("null", choice.DefaultJson);
            Assert.Equal("dialogue.speakers", choice.ChoiceSource);
        }

        [Fact]
        public void BuildExportsCompleteCoreAndDomainUnitDefinitions()
        {
            var catalog = GraphAuthoringCatalogBuilder.Build(
                "dialogue",
                Array.Empty<NodeSchema>(),
                new[]
                {
                    typeof(ConditionalAction),
                    typeof(SequenceAction),
                    typeof(ArithmeticProvider),
                    typeof(Dialogue.FireEventAction),
                    typeof(StateMachine.FireMachineEventAction),
                },
                Array.Empty<IBlackboardDecl>());

            Assert.Equal(
                new[]
                {
                    "core.arithmetic",
                    "core.conditional-action",
                    "core.sequence-action",
                    "dialogue.fire-event",
                    "statemachine.fire-event",
                },
                catalog.UnitIds);
            Assert.Equal(catalog.UnitIds, catalog.Units.Select(unit => unit.StableId).ToArray());

            var conditional = Unit(catalog, "core.conditional-action");
            Assert.Equal(NodeRole.Action, conditional.Role);
            Assert.Equal("Action", conditional.Family);
            Assert.Equal(new[] { "action", "condition" }, conditional.Fields.Select(field => field.Name));
            AssertNestedField(conditional.Fields[0], GraphAuthoringUnitFieldKind.Unit, "Action", "unit");
            AssertNestedField(conditional.Fields[1], GraphAuthoringUnitFieldKind.Unit, "Condition", "unit");

            var sequence = Unit(catalog, "core.sequence-action");
            var items = Assert.Single(sequence.Fields);
            AssertNestedField(items, GraphAuthoringUnitFieldKind.UnitList, "Action", "units");

            var arithmetic = Unit(catalog, "core.arithmetic");
            Assert.Equal(new[] { "a", "b", "op" }, arithmetic.Fields.Select(field => field.Name));
            AssertNestedField(arithmetic.Fields[0], GraphAuthoringUnitFieldKind.Unit, "Provider", "unit");
            AssertNestedField(arithmetic.Fields[1], GraphAuthoringUnitFieldKind.Unit, "Provider", "unit");
            var op = arithmetic.Fields[2];
            Assert.Equal(GraphAuthoringUnitFieldKind.Scalar, op.Kind);
            Assert.Equal("enum", op.ScalarType);
            Assert.Equal(new[] { "Add", "Div", "Mul", "Sub" }, op.EnumValues);
            Assert.False(op.Nullable);
            Assert.Equal("value", op.Payload);

            var dialogue = Unit(catalog, "dialogue.fire-event");
            Assert.Equal(NodeRole.Action, dialogue.Role);
            Assert.Equal(new[] { "arg", "eventId" }, dialogue.Fields.Select(field => field.Name));
            foreach (var field in dialogue.Fields) AssertNullableStringField(field);

            var machine = Unit(catalog, "statemachine.fire-event");
            Assert.Equal("eventName", Assert.Single(machine.Fields).Name);
            AssertNullableStringField(machine.Fields[0]);
        }

        [Fact]
        public void BuildPreservesGlobalModuleAndGroupBlackboardScopes()
        {
            var layers = new IBlackboardDecl[]
            {
                new BlackboardDecl("dialogue", "chapter-1").Add("shared", TypeRef.String, "group"),
                new BlackboardDecl().Add("z-global", TypeRef.Bool, "false").Add("shared", TypeRef.String, "global"),
                new BlackboardDecl("dialogue").Add("shared", TypeRef.String, "module"),
                new BlackboardDecl("task").Add("task-only", TypeRef.Int, "0"),
            };

            var catalog = GraphAuthoringCatalogBuilder.Build(
                "dialogue",
                Array.Empty<NodeSchema>(),
                Array.Empty<Type>(),
                layers);

            Assert.Equal(4, catalog.BlackboardVariables.Count);
            Assert.Collection(
                catalog.BlackboardVariables,
                variable => AssertVariable(variable, GraphAuthoringBlackboardScope.Global, "", "", "shared", "global"),
                variable => AssertVariable(variable, GraphAuthoringBlackboardScope.Global, "", "", "z-global", "false"),
                variable => AssertVariable(variable, GraphAuthoringBlackboardScope.Module, "dialogue", "", "shared", "module"),
                variable => AssertVariable(variable, GraphAuthoringBlackboardScope.Group, "dialogue", "chapter-1", "shared", "group"));
        }

        [Fact]
        public void BuildFailsClosedOnDuplicateStableIds()
        {
            var duplicateDefinitions = new[]
            {
                new NodeSchema { id = "same" },
                new NodeSchema { id = "same", module = "dialogue" },
            };
            var definitionError = Assert.Throws<ArgumentException>(() => GraphAuthoringCatalogBuilder.Build(
                "dialogue",
                duplicateDefinitions,
                Array.Empty<Type>(),
                Array.Empty<IBlackboardDecl>()));
            Assert.Contains("same", definitionError.Message);

            var unitError = Assert.Throws<ArgumentException>(() =>
                new UnitAuthoringCatalog(typeof(DuplicateUnitA), typeof(DuplicateUnitB)));
            Assert.Contains("test.duplicate", unitError.Message);

            var duplicateVariables = new IBlackboardDecl[]
            {
                new BlackboardDecl("dialogue", variables: new[]
                {
                    new VariableDef { key = "same", type = TypeRef.Int },
                    new VariableDef { key = "same", type = TypeRef.Int },
                })
            };
            var variableError = Assert.Throws<ArgumentException>(() => GraphAuthoringCatalogBuilder.Build(
                "dialogue",
                Array.Empty<NodeSchema>(),
                Array.Empty<Type>(),
                duplicateVariables));
            Assert.Contains("same", variableError.Message);
        }

        static void AssertVariable(
            GraphAuthoringBlackboardVariable variable,
            GraphAuthoringBlackboardScope scope,
            string module,
            string group,
            string key,
            string defaultJson)
        {
            Assert.Equal(scope, variable.Scope);
            Assert.Equal(module, variable.Module);
            Assert.Equal(group, variable.Group);
            Assert.Equal(key, variable.Key);
            Assert.Equal(defaultJson, variable.DefaultJson);
        }

        static GraphAuthoringUnitDefinition Unit(GraphAuthoringCatalog catalog, string stableId) =>
            Assert.Single(catalog.Units, unit => unit.StableId == stableId);

        static void AssertNestedField(
            GraphAuthoringUnitFieldDefinition field,
            GraphAuthoringUnitFieldKind kind,
            string family,
            string payload)
        {
            Assert.Equal(kind, field.Kind);
            Assert.Equal(family, field.ExpectedUnitFamily);
            Assert.Null(field.ExpectedUnitTypeId);
            Assert.True(field.Required);
            Assert.True(field.Nullable);
            Assert.Equal(payload, field.Payload);
            Assert.True(field.PayloadRequiredWhenNonNull);
            Assert.True(field.PayloadForbiddenWhenNull);
        }

        static void AssertNullableStringField(GraphAuthoringUnitFieldDefinition field)
        {
            Assert.Equal(GraphAuthoringUnitFieldKind.Scalar, field.Kind);
            Assert.Equal("string", field.ScalarType);
            Assert.Empty(field.EnumValues);
            Assert.True(field.Required);
            Assert.True(field.Nullable);
            Assert.Equal("value", field.Payload);
            Assert.True(field.PayloadRequiredWhenNonNull);
            Assert.True(field.PayloadForbiddenWhenNull);
        }

        [Serializable]
        [UnitAuthoringId("test.duplicate")]
        sealed class DuplicateUnitA : ActionUnit
        {
            public override void Execute(NodeContext ctx) { }
        }

        [Serializable]
        [UnitAuthoringId("test.duplicate")]
        sealed class DuplicateUnitB : ActionUnit
        {
            public override void Execute(NodeContext ctx) { }
        }
    }
}
