// GraphAuthoringSemanticValidatorTests.cs —— Catalog 语义门禁的纯 C# 回归测试。

using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor;
using Xunit;

namespace NodeGraph.Core.Tests
{
    public class GraphAuthoringSemanticValidatorTests
    {
        [Fact]
        public void ValidDialogueDocumentPassesWithoutMutation()
        {
            var document = ValidDocument();
            var firstNode = document.nodes[0];
            var unit = firstNode.unitSlots[0].unit;

            var diagnostics = GraphAuthoringSemanticValidator.Validate(document, Catalog());

            Assert.Empty(diagnostics);
            Assert.Same(firstNode, document.nodes[0]);
            Assert.Same(unit, document.nodes[0].unitSlots[0].unit);
            Assert.Equal("dialogue.line", document.nodes[0].definitionId);
            Assert.Equal("core.always", unit.typeId);
        }

        [Theory]
        [InlineData("definition", "semantic.node.definition.unknown", "$.nodes[0].definitionId")]
        [InlineData("from-port", "semantic.edge.port.unknown", "$.edges[0].fromPort")]
        [InlineData("to-port", "semantic.edge.port.unknown", "$.edges[0].toPort")]
        [InlineData("parameter", "semantic.param.unknown", "$.nodes[0].parameters[0].paramName")]
        public void UnknownCatalogMembersFailClosed(string mutation, string code, string path)
        {
            var document = ValidDocument();
            switch (mutation)
            {
                case "definition": document.nodes[0].definitionId = "dialogue.removed"; break;
                case "from-port": document.edges[0].fromPort = "removed"; break;
                case "to-port": document.edges[0].toPort = "removed"; break;
                case "parameter": document.nodes[0].parameters[0].paramName = "removed"; break;
            }

            var diagnostics = GraphAuthoringSemanticValidator.Validate(document, Catalog());

            Assert.Contains(diagnostics, diagnostic => diagnostic.code == code && diagnostic.path == path);
        }

        [Fact]
        public void WrongParameterRepresentationFailsClosed()
        {
            var document = ValidDocument();
            document.nodes[0].parameters.Add(new GraphAuthoringParam
            {
                paramName = "condition",
                valueJson = "{}"
            });
            document.nodes[0].unitSlots.Clear();

            var diagnostic = Assert.Single(
                GraphAuthoringSemanticValidator.Validate(document, Catalog()),
                item => item.code == "semantic.param.representation");

            Assert.Equal("$.nodes[0].parameters[1].paramName", diagnostic.path);
        }

        [Fact]
        public void DuplicateUnitSlotsFailClosedAcrossOneRepresentation()
        {
            var document = ValidDocument();
            document.nodes[0].unitSlots.Add(new GraphAuthoringUnitSlot
            {
                paramName = "condition",
                unit = new GraphAuthoringUnit { typeId = "core.always" }
            });

            var diagnostic = Assert.Single(
                GraphAuthoringSemanticValidator.Validate(document, Catalog()),
                item => item.code == "semantic.param.duplicate");

            Assert.Equal("$.nodes[0].unitSlots[1].paramName", diagnostic.path);
        }

        [Fact]
        public void TopLevelUnitMustSatisfyDeclaredFamily()
        {
            var document = ValidDocument();
            document.nodes[0].unitSlots[0].unit = new GraphAuthoringUnit
            {
                typeId = "dialogue.fire-event",
                fields = new List<GraphAuthoringUnitField>
                {
                    NullableString("arg"),
                    NullableString("eventId")
                }
            };

            var diagnostic = Assert.Single(
                GraphAuthoringSemanticValidator.Validate(document, Catalog()),
                item => item.code == "semantic.unit.family");

            Assert.Equal("$.nodes[0].unitSlots[0].unit.typeId", diagnostic.path);
        }

        [Fact]
        public void EmptyGraphReferenceFailsClosed()
        {
            var document = ValidDocument();
            document.nodes[0].graphRefs[0].graphId = "";

            var diagnostic = Assert.Single(
                GraphAuthoringSemanticValidator.Validate(document, Catalog()),
                item => item.code == "semantic.graph-ref.graph-id.missing");

            Assert.Equal("$.nodes[0].graphRefs[0].graphId", diagnostic.path);
        }

        [Fact]
        public void NullInputsFailClosedWithStableDiagnostics()
        {
            var missingDocument = GraphAuthoringSemanticValidator.Validate(null, Catalog());
            var missingCatalog = GraphAuthoringSemanticValidator.Validate(ValidDocument(), null);

            Assert.Equal(("semantic.document.missing", "$"),
                (Assert.Single(missingDocument).code, missingDocument[0].path));
            Assert.Equal(("semantic.catalog.missing", "$catalog"),
                (Assert.Single(missingCatalog).code, missingCatalog[0].path));
        }

        [Fact]
        public void DiagnosticsHaveDeterministicCodePathAndOrder()
        {
            var document = ValidDocument();
            document.module = "task";
            document.nodes[0].parameters[0].paramName = "removed";
            document.nodes[0].graphRefs[0].graphId = null;
            document.edges[0].fromPort = "removed";

            var diagnostics = GraphAuthoringSemanticValidator.Validate(document, Catalog());

            Assert.Equal(
                new[]
                {
                    "semantic.module.mismatch|$.module",
                    "semantic.param.unknown|$.nodes[0].parameters[0].paramName",
                    "semantic.graph-ref.graph-id.missing|$.nodes[0].graphRefs[0].graphId",
                    "semantic.edge.port.unknown|$.edges[0].fromPort",
                },
                diagnostics.Select(item => item.code + "|" + item.path));
        }

        static GraphAuthoringCatalog Catalog()
        {
            var schema = new NodeSchema
            {
                id = "dialogue.line",
                module = "dialogue",
                inputPorts = new List<PortDef>
                {
                    new PortDef { name = "in", type = TypeRef.Any, arity = Arity.Many }
                },
                outputPorts = new List<PortDef>
                {
                    new PortDef { name = "next", type = TypeRef.Any, arity = Arity.Many }
                },
                parameters = new List<ParamDef>
                {
                    new ParamDef { name = "text", type = TypeRef.String },
                    new ParamDef { name = "condition", type = TypeRef.Unit("Condition") },
                    new ParamDef { name = "subGraph", type = TypeRef.Object("NodeEditor.NodeGraphAsset") },
                }
            };
            return GraphAuthoringCatalogBuilder.Build(
                "dialogue",
                new[] { schema },
                new[] { typeof(AlwaysCondition), typeof(Dialogue.FireEventAction) },
                Array.Empty<IBlackboardDecl>());
        }

        static GraphAuthoringDocument ValidDocument() => new GraphAuthoringDocument
        {
            module = "dialogue",
            nodes = new List<GraphAuthoringNode>
            {
                Node("line-1", "node-1", withOverrides: true),
                Node("line-2", "node-2", withOverrides: false),
            },
            edges = new List<GraphAuthoringEdge>
            {
                new GraphAuthoringEdge { from = "line-1", fromPort = "next", to = "line-2", toPort = "in" }
            }
        };

        static GraphAuthoringNode Node(string key, string instanceId, bool withOverrides)
        {
            var node = new GraphAuthoringNode
            {
                authoringKey = key,
                instanceId = instanceId,
                definitionId = "dialogue.line"
            };
            if (!withOverrides) return node;
            node.parameters.Add(new GraphAuthoringParam { paramName = "text", valueJson = "\"Hello\"" });
            node.graphRefs.Add(new GraphAuthoringGraphRef { paramName = "subGraph", graphId = "dialogue.child" });
            node.unitSlots.Add(new GraphAuthoringUnitSlot
            {
                paramName = "condition",
                unit = new GraphAuthoringUnit { typeId = "core.always" }
            });
            return node;
        }

        static GraphAuthoringUnitField NullableString(string name) => new GraphAuthoringUnitField
        {
            name = name,
            kind = GraphAuthoringUnitFieldKind.Scalar,
            isNull = true
        };
    }
}
