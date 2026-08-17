using System;
using System.Linq;
using NUnit.Framework;

namespace NodeEditor.EditorUI.Tests
{
    public sealed class GraphAuthoringJsonTests
    {
        public sealed class NumberEnvelope
        {
            public double value;
        }

        [Test]
        public void DocumentRoundTripsWithEnumNamesAndExplicitNulls()
        {
            var source = new GraphAuthoringDocument
            {
                graphId = "graph.dialogue.intro",
                module = "dialogue",
                graphType = GraphType.TickTree,
                orientation = GraphOrientation.Horizontal
            };
            source.nodes.Add(new GraphAuthoringNode
            {
                authoringKey = "start",
                instanceId = "instance-1",
                definitionId = "dialogue.line",
                displayName = null,
                positionX = 12.5f,
                positionY = -3f
            });
            source.entries.Add("start");

            var json = GraphAuthoringJson.SerializeDocument(source);
            var parsed = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(parsed.Succeeded, Is.True);
            Assert.That(parsed.Value.graphType, Is.EqualTo(GraphType.TickTree));
            Assert.That(parsed.Value.orientation, Is.EqualTo(GraphOrientation.Horizontal));
            Assert.That(parsed.Value.nodes.Single().displayName, Is.Null);
            StringAssert.Contains("\"graphType\": \"TickTree\"", json);
            StringAssert.Contains("\"displayName\": null", json);
        }

        [Test]
        public void SerializationIsDeterministicAcrossRoundTrip()
        {
            var source = new GraphAuthoringDocument
            {
                graphId = "g",
                module = "task",
                graphType = GraphType.ControlFlow
            };

            var first = GraphAuthoringJson.SerializeDocument(source);
            var parsed = GraphAuthoringJson.DeserializeDocument(first);
            var second = GraphAuthoringJson.SerializeDocument(parsed.Value);

            Assert.That(parsed.Succeeded, Is.True);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void UnknownMemberFailsClosedWithoutPartialDocument()
        {
            const string json = "{\"schemaVersion\":1,\"unexpected\":true}";

            var result = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.unknown-member"));
            Assert.That(result.Diagnostics.Single().path, Is.EqualTo("$.unexpected"));
        }

        [Test]
        public void DuplicateMemberFailsClosedWithoutPartialDocument()
        {
            const string json = "{\"schemaVersion\":1,\"schemaVersion\":1}";

            var result = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.duplicate-property"));
        }

        [TestCase("{/* comment */\"schemaVersion\":1}")]
        [TestCase("{} {}")]
        [TestCase("{'schemaVersion':1}")]
        [TestCase("{\"schemaVersion\":1,}")]
        [TestCase("{\"schemaVersion\":1,\"entries\":[],}")]
        [TestCase("{schemaVersion:1}")]
        [TestCase("{\"schemaVersion\":NaN}")]
        [TestCase("{\"schemaVersion\":undefined}")]
        public void NonCanonicalSyntaxFailsClosed(string json)
        {
            var result = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.syntax"));
        }

        [Test]
        public void InvalidEnumNameFailsClosedWithoutPartialDocument()
        {
            const string json = "{\"schemaVersion\":1,\"graphType\":\"NotAGraphType\"}";

            var result = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.invalid-value"));
            Assert.That(result.Diagnostics.Single().path, Is.EqualTo("$.graphType"));
        }

        [Test]
        public void OmittedSchemaVersionFailsClosed()
        {
            var json = GraphAuthoringJson.SerializeDocument(new GraphAuthoringDocument());
            json = json.Replace(Environment.NewLine + "  \"schemaVersion\": 1", string.Empty);

            var result = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.missing-member"));
            Assert.That(result.Diagnostics.Single().path, Is.EqualTo("$.schemaVersion"));
        }

        [Test]
        public void OmittedNestedCollectionFailsClosed()
        {
            var document = new GraphAuthoringDocument();
            document.nodes.Add(new GraphAuthoringNode());
            var json = GraphAuthoringJson.SerializeDocument(document);
            json = json.Replace(Environment.NewLine + "      \"parameters\": [],", string.Empty);

            var result = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.missing-member"));
            Assert.That(result.Diagnostics.Single().path, Is.EqualTo("$.nodes[0].parameters"));
        }

        [Test]
        public void PropertyNameCaseMismatchFailsClosed()
        {
            var json = GraphAuthoringJson.SerializeDocument(new GraphAuthoringDocument());
            json = json.Replace("\"schemaVersion\"", "\"SchemaVersion\"");

            var result = GraphAuthoringJson.DeserializeDocument(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.property-case-mismatch"));
            Assert.That(result.Diagnostics.Single().path, Is.EqualTo("$.SchemaVersion"));
        }

        [TestCase("0x10")]
        [TestCase("010")]
        [TestCase("01")]
        [TestCase(".5")]
        [TestCase("1.")]
        public void NonRfcNumberLexemesFailClosed(string lexeme)
        {
            var result = GraphAuthoringJson.Deserialize<NumberEnvelope>(
                "{\"value\":" + lexeme + "}");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Single().code, Is.EqualTo("json.syntax"));
        }

        [TestCase("0", 0d)]
        [TestCase("-12", -12d)]
        [TestCase("12.5", 12.5d)]
        [TestCase("6.022e23", 6.022e23)]
        [TestCase("-1.25E-3", -0.00125d)]
        public void RfcNumberLexemesAreAccepted(string lexeme, double expected)
        {
            var result = GraphAuthoringJson.Deserialize<NumberEnvelope>(
                "{\"value\":" + lexeme + "}");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.value, Is.EqualTo(expected));
        }
    }
}
