// GraphAuthoringCodecTests.cs —— 纯层 GraphData ↔ AI 创作文档无损门禁。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NodeEditor;
using Xunit;

namespace NodeGraph.Core.Tests
{
    public class GraphAuthoringCodecTests
    {
        static readonly UnitAuthoringCatalog Catalog = new UnitAuthoringCatalog(
            typeof(ConditionalAction), typeof(NotCondition), typeof(AlwaysCondition),
            typeof(SequenceAction), typeof(SetVariableLiteralAction));

        [Theory]
        [InlineData(GraphType.ControlFlow)]
        [InlineData(GraphType.TickTree)]
        [InlineData(GraphType.Dataflow)]
        [InlineData(GraphType.DependencyDag)]
        public void RoundTripPreservesEveryNodeFieldAndAllGraphTypes(GraphType graphType)
        {
            var source = MakeGraph(graphType);
            var revision = new GraphAuthoringRevisionVector
            {
                owners = new List<GraphAuthoringRevisionOwner>
                {
                    new GraphAuthoringRevisionOwner
                    {
                        ownerId = "owner-guid", ownerPath = "Assets/Graph.asset", contentHash = "sha256",
                        expectedState = GraphAuthoringExpectedState.Exists
                    }
                }
            };

            var exported = GraphAuthoringCodec.Export(source, Catalog, revision);
            Assert.True(exported.Succeeded, DiagnosticText(exported.Diagnostics));
            Assert.True(exported.Document.authoringKeysPersisted);
            Assert.Equal(new[] { "root" }, exported.Document.entries);
            Assert.Equal(2, exported.Document.edges.Count);
            Assert.Equal(new[] { "left", "right" }, exported.Document.edges.Select(e => e.toPort));

            var imported = GraphAuthoringCodec.Import(exported.Document, Catalog);
            Assert.True(imported.Succeeded, DiagnosticText(imported.Diagnostics));
            AssertGraph(source, imported.Data);

            var reExported = GraphAuthoringCodec.Export(imported.Data, Catalog, exported.Document.revisionVector);
            Assert.True(reExported.Succeeded, DiagnosticText(reExported.Diagnostics));
            Assert.Equal(Fingerprint(exported.Document), Fingerprint(reExported.Document));
        }

        [Fact]
        public void LegacyKeysAreDeterministicAndExportDoesNotMutateSource()
        {
            var first = MakeGraph(GraphType.ControlFlow, persistedKeys: false);
            var second = MakeGraph(GraphType.ControlFlow, persistedKeys: false);
            second.instances.Reverse();

            var a = GraphAuthoringCodec.Export(first, Catalog);
            var b = GraphAuthoringCodec.Export(second, Catalog);
            Assert.True(a.Succeeded, DiagnosticText(a.Diagnostics));
            Assert.True(b.Succeeded, DiagnosticText(b.Diagnostics));
            Assert.False(a.Document.authoringKeysPersisted);
            Assert.All(a.Document.nodes, node => Assert.StartsWith("n-", node.authoringKey));
            Assert.Equal(
                a.Document.nodes.ToDictionary(n => n.instanceId, n => n.authoringKey),
                b.Document.nodes.ToDictionary(n => n.instanceId, n => n.authoringKey));

            Assert.All(first.instances, node => Assert.True(string.IsNullOrEmpty(node.authoringKey)));
            Assert.Equal("node-2", first.instances[0].connections[0].toInstanceId);
        }

        [Fact]
        public void LegacyKeyMatchesFixedRfc4648Sha256Vector()
        {
            var result = GraphAuthoringCodec.Export(MakeGraph(GraphType.ControlFlow, persistedKeys: false), Catalog);

            Assert.True(result.Succeeded, DiagnosticText(result.Diagnostics));
            Assert.Equal("n-gwlrxzxjxmbe",
                result.Document.nodes.Single(node => node.instanceId == "node-1").authoringKey);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MissingEdgePortsFailClosed(bool missingFromPort)
        {
            var document = ExportDocument();
            if (missingFromPort) document.edges[0].fromPort = null;
            else document.edges[0].toPort = "";

            var result = GraphAuthoringCodec.Import(document, Catalog);

            Assert.Null(result.Data);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.code == "authoring.edge.port.missing");
        }

        [Theory]
        [InlineData("scalar-unit")]
        [InlineData("scalar-units")]
        [InlineData("scalar-null-with-value")]
        [InlineData("scalar-value-missing")]
        [InlineData("unit-value")]
        [InlineData("unit-units")]
        [InlineData("unit-null-with-value")]
        [InlineData("unit-value-missing")]
        [InlineData("list-value")]
        [InlineData("list-unit")]
        [InlineData("list-null-with-items")]
        [InlineData("list-items-missing")]
        public void ContradictoryUnitFieldShapesFailClosed(string mutation)
        {
            var document = ExportDocument();
            var root = document.nodes[0].unitSlots[0].unit;
            var unitField = root.fields.Single(field => field.name == "condition");
            var listField = root.fields.Single(field => field.name == "action").unit.fields
                .Single(field => field.name == "items");
            var scalarField = listField.units[0].fields.Single(field => field.name == "key");

            switch (mutation)
            {
                case "scalar-unit": scalarField.unit = unitField.unit; break;
                case "scalar-units": scalarField.units = new List<GraphAuthoringUnit>(); break;
                case "scalar-null-with-value": scalarField.isNull = true; break;
                case "scalar-value-missing": scalarField.value = null; break;
                case "unit-value": unitField.value = "extra"; break;
                case "unit-units": unitField.units = new List<GraphAuthoringUnit>(); break;
                case "unit-null-with-value": unitField.isNull = true; break;
                case "unit-value-missing": unitField.unit = null; break;
                case "list-value": listField.value = "extra"; break;
                case "list-unit": listField.unit = unitField.unit; break;
                case "list-null-with-items": listField.isNull = true; break;
                case "list-items-missing": listField.units = null; break;
            }

            var result = GraphAuthoringCodec.Import(document, Catalog);

            Assert.Null(result.Data);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.code == "authoring.unit.field-shape");
        }

        [Fact]
        public void RevisionVectorClonesAndRoundTripsEveryField()
        {
            var revision = new GraphAuthoringRevisionVector
            {
                owners = new List<GraphAuthoringRevisionOwner>
                {
                    new GraphAuthoringRevisionOwner
                    {
                        ownerId = "graph-guid", ownerPath = "Assets/Graph.asset", contentHash = "graph-hash",
                        expectedState = GraphAuthoringExpectedState.Exists
                    },
                    new GraphAuthoringRevisionOwner
                    {
                        ownerId = null, ownerPath = "Assets/NewBlackboard.asset", contentHash = null,
                        expectedState = GraphAuthoringExpectedState.MustNotExist
                    }
                }
            };
            var exported = GraphAuthoringCodec.Export(MakeGraph(GraphType.ControlFlow), Catalog, revision);
            Assert.True(exported.Succeeded, DiagnosticText(exported.Diagnostics));
            Assert.NotSame(revision, exported.Document.revisionVector);
            Assert.NotSame(revision.owners[0], exported.Document.revisionVector.owners[0]);

            revision.owners[0].ownerId = "mutated";
            var imported = GraphAuthoringCodec.Import(exported.Document, Catalog);
            Assert.True(imported.Succeeded, DiagnosticText(imported.Diagnostics));
            var roundTrip = GraphAuthoringCodec.Export(imported.Data, Catalog, exported.Document.revisionVector);
            Assert.True(roundTrip.Succeeded, DiagnosticText(roundTrip.Diagnostics));

            var owners = roundTrip.Document.revisionVector.owners;
            Assert.Equal(2, owners.Count);
            Assert.Equal(("graph-guid", "Assets/Graph.asset", "graph-hash", GraphAuthoringExpectedState.Exists),
                (owners[0].ownerId, owners[0].ownerPath, owners[0].contentHash, owners[0].expectedState));
            Assert.Equal((null, "Assets/NewBlackboard.asset", null, GraphAuthoringExpectedState.MustNotExist),
                (owners[1].ownerId, owners[1].ownerPath, owners[1].contentHash, owners[1].expectedState));
        }

        [Theory]
        [InlineData("duplicate-key", "authoring.key.duplicate")]
        [InlineData("duplicate-instance", "authoring.instance-id.duplicate")]
        [InlineData("dangling-edge", "authoring.edge.dangling")]
        [InlineData("dangling-entry", "authoring.entry.dangling")]
        [InlineData("unknown-unit", "authoring.unit.unknown-type")]
        [InlineData("unknown-field", "authoring.unit.unknown-field")]
        public void InvalidDocumentsFailClosed(string mutation, string expectedCode)
        {
            var document = ExportDocument();
            switch (mutation)
            {
                case "duplicate-key": document.nodes[1].authoringKey = document.nodes[0].authoringKey; break;
                case "duplicate-instance": document.nodes[1].instanceId = document.nodes[0].instanceId; break;
                case "dangling-edge": document.edges[0].to = "missing"; break;
                case "dangling-entry": document.entries[0] = "missing"; break;
                case "unknown-unit": document.nodes[0].unitSlots[0].unit.typeId = "missing.unit"; break;
                case "unknown-field": document.nodes[0].unitSlots[0].unit.fields.Add(
                    new GraphAuthoringUnitField { name = "removedField", kind = GraphAuthoringUnitFieldKind.Scalar, value = "x" }); break;
            }

            var result = GraphAuthoringCodec.Import(document, Catalog);
            Assert.False(result.Succeeded);
            Assert.Null(result.Data);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.code == expectedCode);
        }

        [Fact]
        public void UnknownSchemaAndEnumsFailClosed()
        {
            var wrongSchema = ExportDocument();
            wrongSchema.schemaVersion++;
            var schemaResult = GraphAuthoringCodec.Import(wrongSchema, Catalog);
            Assert.Contains(schemaResult.Diagnostics, diagnostic => diagnostic.code == "authoring.schema.unsupported");

            var wrongEnum = ExportDocument();
            wrongEnum.graphType = (GraphType)999;
            var enumResult = GraphAuthoringCodec.Import(wrongEnum, Catalog);
            Assert.Null(enumResult.Data);
            Assert.Contains(enumResult.Diagnostics, diagnostic => diagnostic.code == "authoring.enum.invalid");
        }

        [Fact]
        public void LegacyClrUnitAliasIsInputOnly()
        {
            var document = ExportDocument();
            document.nodes[0].unitSlots[0].unit.typeId = typeof(ConditionalAction).FullName;

            var imported = GraphAuthoringCodec.Import(document, Catalog);
            Assert.True(imported.Succeeded, DiagnosticText(imported.Diagnostics));
            var exported = GraphAuthoringCodec.Export(imported.Data, Catalog);
            Assert.True(exported.Succeeded, DiagnosticText(exported.Diagnostics));
            Assert.Equal("core.conditional-action", exported.Document.nodes[0].unitSlots[0].unit.typeId);
        }

        [Fact]
        public void NestedUnitTreeAndOrderingAreStable()
        {
            var first = ExportDocument();
            var imported = GraphAuthoringCodec.Import(first, Catalog);
            var second = GraphAuthoringCodec.Export(imported.Data, Catalog).Document;

            Assert.Equal(Fingerprint(first), Fingerprint(second));
            var unit = second.nodes[0].unitSlots[0].unit;
            Assert.Equal(new[] { "action", "condition" }, unit.fields.Select(field => field.name));
            Assert.Equal("core.not", unit.fields.Single(field => field.name == "condition").unit.typeId);
        }

        static GraphAuthoringDocument ExportDocument()
        {
            var result = GraphAuthoringCodec.Export(MakeGraph(GraphType.ControlFlow), Catalog);
            Assert.True(result.Succeeded, DiagnosticText(result.Diagnostics));
            return result.Document;
        }

        static GraphData MakeGraph(GraphType graphType, bool persistedKeys = true)
        {
            var root = new NodeInstance
            {
                authoringKey = persistedKeys ? "root" : null,
                instanceId = "node-1",
                definitionId = "test.root",
                position = new Vec2(12.5f, -3.25f),
                displayName = "Root display",
                note = "Root note",
                pinned = true
            };
            root.parameterOverrides.Add(new ParamOverride { paramName = "count", valueJson = "3" });
            root.parameterOverrides.Add(new ParamOverride { paramName = "label", valueJson = "\"hello\"" });
            root.graphRefs.Add(new GraphRef { paramName = "subGraph", graphId = "child-graph" });
            root.unitOverrides.Add(new UnitOverride
            {
                paramName = "onEnter",
                value = new ConditionalAction
                {
                    condition = new NotCondition { inner = new AlwaysCondition { value = false } },
                    action = new SequenceAction
                    {
                        items = new List<ActionUnit>
                        {
                            new SetVariableLiteralAction { key = "quest.ready", value = "true" }
                        }
                    }
                }
            });

            var leaf = new NodeInstance
            {
                authoringKey = persistedKeys ? "leaf" : "",
                instanceId = "node-2",
                definitionId = "test.leaf",
                position = new Vec2(80, 40),
                displayName = "Leaf",
                note = null,
                pinned = false
            };
            root.connections.Add(new Connection { fromPort = "out", toInstanceId = leaf.instanceId, toPort = "left" });
            root.connections.Add(new Connection { fromPort = "out", toInstanceId = leaf.instanceId, toPort = "right" });

            return new GraphData
            {
                graphId = "graph-main",
                module = "dialogue",
                group = "chapter-1",
                graphType = graphType,
                orientation = GraphOrientation.Horizontal,
                instances = new List<NodeInstance> { root, leaf },
                entryInstanceIds = new List<string> { root.instanceId }
            };
        }

        static void AssertGraph(GraphData expected, GraphData actual)
        {
            Assert.Equal(expected.graphId, actual.graphId);
            Assert.Equal(expected.module, actual.module);
            Assert.Equal(expected.group, actual.group);
            Assert.Equal(expected.graphType, actual.graphType);
            Assert.Equal(expected.orientation, actual.orientation);
            Assert.Equal(expected.entryInstanceIds, actual.entryInstanceIds);
            Assert.Equal(expected.instances.Count, actual.instances.Count);
            for (int i = 0; i < expected.instances.Count; i++)
            {
                var a = expected.instances[i]; var b = actual.instances[i];
                Assert.Equal(a.authoringKey, b.authoringKey);
                Assert.Equal(a.instanceId, b.instanceId);
                Assert.Equal(a.definitionId, b.definitionId);
                Assert.Equal(a.position, b.position);
                Assert.Equal(a.displayName, b.displayName);
                Assert.Equal(a.note, b.note);
                Assert.Equal(a.pinned, b.pinned);
                Assert.Equal(a.connections.Select(c => (c.fromPort, c.toInstanceId, c.toPort)),
                    b.connections.Select(c => (c.fromPort, c.toInstanceId, c.toPort)));
                Assert.Equal(a.parameterOverrides.Select(p => (p.paramName, p.valueJson)),
                    b.parameterOverrides.Select(p => (p.paramName, p.valueJson)));
                Assert.Equal(a.graphRefs.Select(r => (r.paramName, r.graphId)),
                    b.graphRefs.Select(r => (r.paramName, r.graphId)));
            }
            var action = Assert.IsType<ConditionalAction>(actual.instances[0].unitOverrides[0].value);
            Assert.IsType<NotCondition>(action.condition);
            Assert.Single(Assert.IsType<SequenceAction>(action.action).items);
        }

        static string Fingerprint(GraphAuthoringDocument document)
        {
            var text = new StringBuilder();
            text.Append(document.schemaVersion).Append('|').Append(document.authoringKeysPersisted).Append('|')
                .Append(document.graphId).Append('|').Append(document.module).Append('|').Append(document.group).Append('|')
                .Append(document.graphType).Append('|').Append(document.orientation);
            foreach (var entry in document.entries) text.Append("|entry:").Append(entry);
            foreach (var node in document.nodes)
            {
                text.Append("|node:").Append(node.authoringKey).Append(':').Append(node.instanceId).Append(':')
                    .Append(node.definitionId).Append(':').Append(node.positionX).Append(':').Append(node.positionY)
                    .Append(':').Append(node.displayName).Append(':').Append(node.note).Append(':').Append(node.pinned);
                foreach (var value in node.parameters) text.Append("|p:").Append(value.paramName).Append(':').Append(value.valueJson);
                foreach (var value in node.graphRefs) text.Append("|g:").Append(value.paramName).Append(':').Append(value.graphId);
                foreach (var slot in node.unitSlots) { text.Append("|slot:").Append(slot.paramName); AppendUnit(text, slot.unit); }
            }
            foreach (var edge in document.edges)
                text.Append("|edge:").Append(edge.from).Append(':').Append(edge.fromPort).Append(':').Append(edge.to).Append(':').Append(edge.toPort);
            return text.ToString();
        }

        static void AppendUnit(StringBuilder text, GraphAuthoringUnit unit)
        {
            if (unit == null) { text.Append("<null>"); return; }
            text.Append('<').Append(unit.typeId);
            foreach (var field in unit.fields)
            {
                text.Append('|').Append(field.name).Append(':').Append(field.kind).Append(':').Append(field.isNull).Append(':').Append(field.value);
                AppendUnit(text, field.unit);
                if (field.units != null) foreach (var child in field.units) AppendUnit(text, child);
            }
            text.Append('>');
        }

        static string DiagnosticText(IReadOnlyList<GraphAuthoringDiagnostic> diagnostics) =>
            string.Join(Environment.NewLine, diagnostics.Select(diagnostic => $"{diagnostic.code} {diagnostic.path}: {diagnostic.message}"));
    }
}
