// GraphAuthoringBlackboardCodecTests.cs —— 黑板完整快照的纯层无损/失败关闭门禁。

using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor;
using Xunit;

namespace NodeGraph.Core.Tests
{
    public class GraphAuthoringBlackboardCodecTests
    {
        [Fact]
        public void DocumentAlwaysStartsWithANonNullFullSnapshotList()
        {
            Assert.NotNull(new GraphAuthoringDocument().blackboards);
            Assert.Empty(new GraphAuthoringDocument().blackboards);
        }

        [Fact]
        public void RoundTripPreservesLayerAndVariableOrderAndEveryTypeRefField()
        {
            var originalType = new TypeRef
            {
                kind = TypeKind.List,
                primitive = PrimitiveType.Color,
                enumOrObjectName = null,
                element = new TypeRef
                {
                    kind = TypeKind.Enum,
                    primitive = PrimitiveType.Float,
                    enumOrObjectName = "Game.QuestState"
                }
            };
            var sources = new[]
            {
                Owner("Assets/NodeGraph/GlobalBlackboard.asset", "", "",
                    Variable("ordered.first", originalType, "[\"Ready\"]"),
                    Variable("primitive", new TypeRef
                    {
                        kind = TypeKind.Primitive,
                        primitive = PrimitiveType.Vector3,
                        enumOrObjectName = ""
                    }, "{\"x\":1}"),
                    Variable("object", TypeRef.Object("Game.Actor"), "null"),
                    Variable("blackboard-key", TypeRef.BBKey(), null),
                    Variable("any", TypeRef.Any, "42"),
                    Variable("blackboard-value", TypeRef.BBValue("keyParam"), "true"),
                    Variable("unit", TypeRef.Unit("Condition"), null)),
                Owner("Assets/Dialogue/DialogueBlackboard.asset", "dialogue", "",
                    Variable("module.value", TypeRef.String, "\"module\"")),
                Owner("Assets/Dialogue/Chapter1Blackboard.asset", "dialogue", "chapter-1",
                    Variable("group.value", TypeRef.Int, "7"))
            };

            var exported = GraphAuthoringBlackboardCodec.Export(sources);

            Assert.True(exported.Succeeded, DiagnosticText(exported.Diagnostics));
            Assert.Equal(new[] { "", "dialogue", "dialogue" }, exported.Layers.Select(layer => layer.module));
            Assert.Equal(new[] { "ordered.first", "primitive", "object", "blackboard-key", "any", "blackboard-value", "unit" },
                exported.Layers[0].variables.Select(variable => variable.key));
            var encoded = exported.Layers[0].variables[0].type;
            Assert.Equal((TypeKind.List, PrimitiveType.Color, null),
                (encoded.kind, encoded.primitive, encoded.enumOrObjectName));
            Assert.Equal((TypeKind.Enum, PrimitiveType.Float, "Game.QuestState"),
                (encoded.element.kind, encoded.element.primitive, encoded.element.enumOrObjectName));
            Assert.Equal("", exported.Layers[0].variables[1].type.enumOrObjectName);
            string expectedFingerprint = Fingerprint(exported.Layers);

            originalType.element.enumOrObjectName = "mutated-after-export";
            Assert.Equal("Game.QuestState", encoded.element.enumOrObjectName);

            var imported = GraphAuthoringBlackboardCodec.Import(exported.Layers);

            Assert.True(imported.Succeeded, DiagnosticText(imported.Diagnostics));
            Assert.Equal(sources.Select(source => source.OwnerPath), imported.Owners.Select(owner => owner.OwnerPath));
            Assert.Equal(new[] { "ordered.first", "primitive", "object", "blackboard-key", "any", "blackboard-value", "unit" },
                imported.Owners[0].Data.Variables.Select(variable => variable.key));
            var decoded = imported.Owners[0].Data.Variables[0].type;
            Assert.Equal((TypeKind.List, PrimitiveType.Color, null),
                (decoded.kind, decoded.primitive, decoded.enumOrObjectName));
            Assert.Equal((TypeKind.Enum, PrimitiveType.Float, "Game.QuestState"),
                (decoded.element.kind, decoded.element.primitive, decoded.element.enumOrObjectName));

            encoded.element.enumOrObjectName = "mutated-after-import";
            Assert.Equal("Game.QuestState", decoded.element.enumOrObjectName);

            var reExported = GraphAuthoringBlackboardCodec.Export(imported.Owners);
            Assert.True(reExported.Succeeded, DiagnosticText(reExported.Diagnostics));
            Assert.Equal(expectedFingerprint, Fingerprint(reExported.Layers));
        }

        [Theory]
        [InlineData("backslash", "authoring.blackboard.owner-path.not-normalized")]
        [InlineData("absolute", "authoring.blackboard.owner-path.invalid")]
        [InlineData("parent", "authoring.blackboard.owner-path.invalid")]
        [InlineData("dot", "authoring.blackboard.owner-path.invalid")]
        [InlineData("empty-segment", "authoring.blackboard.owner-path.invalid")]
        [InlineData("assets-root", "authoring.blackboard.owner-path.invalid")]
        public void ExportRejectsNonCanonicalOrNonProjectOwnerPaths(string mutation, string expectedCode)
        {
            string ownerPath = mutation switch
            {
                "backslash" => "Assets\\NodeGraph\\Blackboard.asset",
                "absolute" => "C:/Project/Assets/Blackboard.asset",
                "parent" => "Assets/NodeGraph/../Blackboard.asset",
                "dot" => "Assets/./Blackboard.asset",
                "empty-segment" => "Assets//Blackboard.asset",
                "assets-root" => "Assets",
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };

            var result = GraphAuthoringBlackboardCodec.Export(new[]
            {
                Owner(ownerPath, "", "", Variable("valid", TypeRef.Bool, "true"))
            });

            Assert.False(result.Succeeded);
            Assert.Null(result.Layers);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.code == expectedCode);
        }

        [Fact]
        public void ExportRejectsInvalidSourceScopeAndCollections()
        {
            var nullModule = GraphAuthoringBlackboardCodec.Export(new[]
            {
                Owner("Assets/NodeGraph/Blackboard.asset", null, "", Variable("valid", TypeRef.Bool, "true"))
            });
            Assert.Null(nullModule.Layers);
            Assert.Contains(nullModule.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.module.invalid");

            var nullVariables = GraphAuthoringBlackboardCodec.Export(new[]
            {
                new GraphAuthoringBlackboardOwner(
                    "Assets/NodeGraph/Blackboard.asset",
                    new TestBlackboardDecl("", "", null))
            });
            Assert.Null(nullVariables.Layers);
            Assert.Contains(nullVariables.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.collection.missing");
        }

        [Fact]
        public void ExportRejectsNullLayerDeclarationAndVariableItems()
        {
            var nullLayer = GraphAuthoringBlackboardCodec.Export(new GraphAuthoringBlackboardOwner[] { null });
            Assert.Null(nullLayer.Layers);
            Assert.Contains(nullLayer.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.layer.missing");

            var nullDeclaration = GraphAuthoringBlackboardCodec.Export(new[]
            {
                new GraphAuthoringBlackboardOwner("Assets/NodeGraph/Blackboard.asset", null)
            });
            Assert.Null(nullDeclaration.Layers);
            Assert.Contains(nullDeclaration.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.source.missing");

            var nullVariable = GraphAuthoringBlackboardCodec.Export(new[]
            {
                Owner("Assets/NodeGraph/Blackboard.asset", "", "", (VariableDef)null)
            });
            Assert.Null(nullVariable.Layers);
            Assert.Contains(nullVariable.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.variable.missing");
        }

        [Theory]
        [InlineData("null-layer", "authoring.blackboard.layer.missing")]
        [InlineData("non-normalized-path", "authoring.blackboard.owner-path.not-normalized")]
        [InlineData("duplicate-owner", "authoring.blackboard.owner-path.duplicate")]
        [InlineData("duplicate-scope", "authoring.blackboard.scope.duplicate")]
        [InlineData("null-module", "authoring.blackboard.module.invalid")]
        [InlineData("trimmed-module", "authoring.blackboard.module.invalid")]
        [InlineData("group-without-module", "authoring.blackboard.scope.invalid")]
        [InlineData("null-variables", "authoring.blackboard.collection.missing")]
        [InlineData("null-variable", "authoring.blackboard.variable.missing")]
        [InlineData("invalid-key", "authoring.blackboard.key.invalid")]
        [InlineData("duplicate-key", "authoring.blackboard.key.duplicate")]
        [InlineData("null-type", "authoring.blackboard.type.missing")]
        [InlineData("invalid-kind", "authoring.blackboard.type.enum-invalid")]
        [InlineData("invalid-primitive", "authoring.blackboard.type.enum-invalid")]
        [InlineData("enum-without-name", "authoring.blackboard.type.shape")]
        [InlineData("primitive-with-name", "authoring.blackboard.type.shape")]
        [InlineData("list-without-element", "authoring.blackboard.type.shape")]
        [InlineData("non-list-with-element", "authoring.blackboard.type.shape")]
        public void InvalidSnapshotsFailClosed(string mutation, string expectedCode)
        {
            var layers = ValidLayers();
            switch (mutation)
            {
                case "null-layer": layers[0] = null; break;
                case "non-normalized-path": layers[0].ownerPath = "Assets\\NodeGraph\\Blackboard.asset"; break;
                case "duplicate-owner": layers.Add(NewLayer("Assets/NodeGraph/Blackboard.asset", "other", "")); break;
                case "duplicate-scope": layers.Add(NewLayer("Assets/Other.asset", "", "")); break;
                case "null-module": layers[0].module = null; break;
                case "trimmed-module": layers[0].module = " dialogue "; break;
                case "group-without-module": layers[0].group = "chapter-1"; break;
                case "null-variables": layers[0].variables = null; break;
                case "null-variable": layers[0].variables[0] = null; break;
                case "invalid-key": layers[0].variables[0].key = " bad "; break;
                case "duplicate-key": layers[0].variables.Add(new GraphAuthoringBlackboardVariableData
                {
                    key = "valid", type = Encoded(TypeRef.Int)
                }); break;
                case "null-type": layers[0].variables[0].type = null; break;
                case "invalid-kind": layers[0].variables[0].type.kind = (TypeKind)999; break;
                case "invalid-primitive": layers[0].variables[0].type.primitive = (PrimitiveType)999; break;
                case "enum-without-name": layers[0].variables[0].type = new GraphAuthoringTypeRef
                {
                    kind = TypeKind.Enum, primitive = PrimitiveType.Bool
                }; break;
                case "primitive-with-name": layers[0].variables[0].type.enumOrObjectName = "not-allowed"; break;
                case "list-without-element": layers[0].variables[0].type = new GraphAuthoringTypeRef
                {
                    kind = TypeKind.List, primitive = PrimitiveType.Bool
                }; break;
                case "non-list-with-element": layers[0].variables[0].type.element = new GraphAuthoringTypeRef
                {
                    kind = TypeKind.Any, primitive = PrimitiveType.Bool
                }; break;
            }

            var result = GraphAuthoringBlackboardCodec.Import(layers);

            Assert.False(result.Succeeded);
            Assert.Null(result.Owners);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.code == expectedCode);
        }

        [Fact]
        public void NullCollectionsAndCyclicTypesFailClosedWithoutPartialData()
        {
            var missingExport = GraphAuthoringBlackboardCodec.Export(null);
            Assert.Null(missingExport.Layers);
            Assert.Contains(missingExport.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.collection.missing");

            var missingImport = GraphAuthoringBlackboardCodec.Import((IReadOnlyList<GraphAuthoringBlackboardLayer>)null);
            Assert.Null(missingImport.Owners);
            Assert.Contains(missingImport.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.collection.missing");

            var cyclic = new TypeRef { kind = TypeKind.List, primitive = PrimitiveType.Bool };
            cyclic.element = cyclic;
            var cycleExport = GraphAuthoringBlackboardCodec.Export(new[]
            {
                Owner("Assets/NodeGraph/Blackboard.asset", "", "", Variable("cycle", cyclic, null))
            });
            Assert.Null(cycleExport.Layers);
            Assert.Contains(cycleExport.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.type.cycle");

            var cyclicDto = new GraphAuthoringTypeRef { kind = TypeKind.List, primitive = PrimitiveType.Bool };
            cyclicDto.element = cyclicDto;
            var layers = ValidLayers();
            layers[0].variables[0].type = cyclicDto;
            var cycleImport = GraphAuthoringBlackboardCodec.Import(layers);
            Assert.Null(cycleImport.Owners);
            Assert.Contains(cycleImport.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.type.cycle");
        }

        [Theory]
        [InlineData("reversed", "authoring.blackboard.effective-order.invalid")]
        [InlineData("foreign-module", "authoring.blackboard.effective-scope.invalid")]
        [InlineData("sibling-group", "authoring.blackboard.effective-scope.invalid")]
        [InlineData("group-without-current-group", "authoring.blackboard.effective-scope.invalid")]
        public void DocumentImportRejectsLayersOutsideTheEffectiveOrderedClosure(
            string mutation,
            string expectedCode)
        {
            var document = new GraphAuthoringDocument
            {
                module = "dialogue",
                group = "chapter-1",
                blackboards = new List<GraphAuthoringBlackboardLayer>
                {
                    NewLayer("Assets/Global.asset", "", ""),
                    NewLayer("Assets/Dialogue.asset", "dialogue", ""),
                    NewLayer("Assets/Chapter1.asset", "dialogue", "chapter-1")
                }
            };
            switch (mutation)
            {
                case "reversed":
                    document.blackboards.Reverse();
                    break;
                case "foreign-module":
                    document.blackboards[1].module = "task";
                    break;
                case "sibling-group":
                    document.blackboards[2].group = "chapter-2";
                    break;
                case "group-without-current-group":
                    document.group = "";
                    break;
            }

            var result = GraphAuthoringBlackboardCodec.Import(document);

            Assert.False(result.Succeeded);
            Assert.Null(result.Owners);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.code == expectedCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void DocumentImportAcceptsSparseEffectiveLayersInOuterToInnerOrder(int shape)
        {
            var global = NewLayer("Assets/Global.asset", "", "");
            var module = NewLayer("Assets/Dialogue.asset", "dialogue", "");
            var group = NewLayer("Assets/Chapter1.asset", "dialogue", "chapter-1");
            var layers = shape switch
            {
                0 => new List<GraphAuthoringBlackboardLayer>(),
                1 => new List<GraphAuthoringBlackboardLayer> { global },
                2 => new List<GraphAuthoringBlackboardLayer> { module },
                3 => new List<GraphAuthoringBlackboardLayer> { group },
                4 => new List<GraphAuthoringBlackboardLayer> { global, group },
                5 => new List<GraphAuthoringBlackboardLayer> { module, group },
                6 => new List<GraphAuthoringBlackboardLayer> { global, module, group },
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
            var document = new GraphAuthoringDocument
            {
                module = "dialogue",
                group = "chapter-1",
                blackboards = layers
            };

            var result = GraphAuthoringBlackboardCodec.Import(document);

            Assert.True(result.Succeeded, DiagnosticText(result.Diagnostics));
            Assert.Equal(layers.Select(layer => layer.ownerPath), result.Owners.Select(owner => owner.OwnerPath));
        }

        [Fact]
        public void EmptyDocumentModuleAllowsOnlyGlobalLayer()
        {
            var globalDocument = new GraphAuthoringDocument
            {
                module = "",
                group = "",
                blackboards = new List<GraphAuthoringBlackboardLayer>
                {
                    NewLayer("Assets/Global.asset", "", "")
                }
            };
            Assert.True(GraphAuthoringBlackboardCodec.Import(globalDocument).Succeeded);

            globalDocument.blackboards.Add(NewLayer("Assets/Dialogue.asset", "dialogue", ""));
            var invalid = GraphAuthoringBlackboardCodec.Import(globalDocument);
            Assert.Null(invalid.Owners);
            Assert.Contains(invalid.Diagnostics,
                diagnostic => diagnostic.code == "authoring.blackboard.effective-scope.invalid");
        }

        static GraphAuthoringBlackboardOwner Owner(
            string ownerPath,
            string module,
            string group,
            params VariableDef[] variables) =>
            new GraphAuthoringBlackboardOwner(ownerPath, new TestBlackboardDecl(module, group, variables));

        static VariableDef Variable(string key, TypeRef type, string defaultJson) =>
            new VariableDef { key = key, type = type, defaultJson = defaultJson };

        static List<GraphAuthoringBlackboardLayer> ValidLayers() => new()
        {
            NewLayer("Assets/NodeGraph/Blackboard.asset", "", "")
        };

        static GraphAuthoringBlackboardLayer NewLayer(string ownerPath, string module, string group) => new()
        {
            ownerPath = ownerPath,
            module = module,
            group = group,
            variables = new List<GraphAuthoringBlackboardVariableData>
            {
                new GraphAuthoringBlackboardVariableData
                {
                    key = "valid",
                    type = Encoded(TypeRef.Bool),
                    defaultJson = "true"
                }
            }
        };

        static GraphAuthoringTypeRef Encoded(TypeRef source) => source == null
            ? null
            : new GraphAuthoringTypeRef
            {
                kind = source.kind,
                primitive = source.primitive,
                enumOrObjectName = source.enumOrObjectName,
                element = Encoded(source.element)
            };

        static string Fingerprint(IReadOnlyList<GraphAuthoringBlackboardLayer> layers) =>
            string.Join("|", layers.Select(layer =>
                $"{layer.ownerPath}:{layer.module}:{layer.group}:" +
                string.Join(",", layer.variables.Select(variable =>
                    $"{variable.key}={TypeFingerprint(variable.type)}={variable.defaultJson}"))));

        static string TypeFingerprint(GraphAuthoringTypeRef type) => type == null
            ? "<null>"
            : $"{type.kind}:{type.primitive}:{type.enumOrObjectName}<{TypeFingerprint(type.element)}>";

        static string DiagnosticText(IReadOnlyList<GraphAuthoringDiagnostic> diagnostics) =>
            string.Join(Environment.NewLine,
                diagnostics.Select(diagnostic => $"{diagnostic.code} {diagnostic.path}: {diagnostic.message}"));

        sealed class TestBlackboardDecl : IBlackboardDecl
        {
            public TestBlackboardDecl(string module, string group, IReadOnlyList<VariableDef> variables)
            {
                Module = module;
                Group = group;
                Variables = variables;
            }

            public string Module { get; }
            public string Group { get; }
            public IReadOnlyList<VariableDef> Variables { get; }
            public VariableDef Find(string key) => Variables?.FirstOrDefault(variable => variable?.key == key);
        }
    }
}
