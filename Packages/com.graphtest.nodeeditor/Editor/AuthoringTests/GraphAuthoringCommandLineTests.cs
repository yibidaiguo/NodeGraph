using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace NodeEditor.EditorUI.Tests
{
    public sealed class GraphAuthoringCommandLineTests
    {
        [TestCase("list")]
        [TestCase("describe")]
        public void ReadOnlyDiscoveryCommandsAcceptOnlyTheirDeclaredShape(string command)
        {
            var before = AssetDatabase.GetAllAssetPaths().OrderBy(path => path, StringComparer.Ordinal).ToArray();

            int exitCode = GraphAuthoringCommandLine.Execute(new[]
            {
                "Unity", "-graphAuthoringCommand", command, "-graphAuthoringModule", "task"
            }, out var output, out var outputPath);

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(outputPath, Is.Null);
            StringAssert.Contains("\"command\": \"" + command + "\"", output);
            StringAssert.Contains("\"succeeded\": true", output);
            Assert.That(
                AssetDatabase.GetAllAssetPaths().OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(before));
        }

        [TestCase("read", "command.argument.missing")]
        [TestCase("write", "command.argument.missing")]
        [TestCase("validate", "command.argument.missing")]
        public void AssetCommandsRejectIncompleteArgumentShapesWithoutMutation(
            string command,
            string expectedCode)
        {
            var before = AssetDatabase.GetAllAssetPaths().OrderBy(path => path, StringComparer.Ordinal).ToArray();

            int exitCode = GraphAuthoringCommandLine.Execute(new[]
            {
                "Unity", "-graphAuthoringCommand", command
            }, out var output, out _);

            Assert.That(exitCode, Is.EqualTo(1));
            StringAssert.Contains("\"code\": \"" + expectedCode + "\"", output);
            StringAssert.Contains("\"succeeded\": false", output);
            Assert.That(
                AssetDatabase.GetAllAssetPaths().OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(before));
        }

        [Test]
        public void UnknownAuthoringFlagIsRejected()
        {
            int exitCode = GraphAuthoringCommandLine.Execute(new[]
            {
                "Unity", "-graphAuthoringCommand", "list", "-graphAuthoringMystery", "value"
            }, out var output, out _);

            Assert.That(exitCode, Is.EqualTo(1));
            StringAssert.Contains("\"code\": \"command.argument.unknown\"", output);
        }

        [Test]
        public void KnownButIrrelevantFlagIsRejectedBeforeAssetAccess()
        {
            int exitCode = GraphAuthoringCommandLine.Execute(new[]
            {
                "Unity", "-graphAuthoringCommand", "read",
                "-graphAuthoringAsset", "Assets/DoesNotExist.asset",
                "-graphAuthoringModule", "task"
            }, out var output, out _);

            Assert.That(exitCode, Is.EqualTo(1));
            StringAssert.Contains("\"code\": \"command.argument.not-applicable\"", output);
            StringAssert.DoesNotContain("asset.graph.missing", output);
        }

        [Test]
        public void DraftRejectsNonExactGraphTypeName()
        {
            int exitCode = GraphAuthoringCommandLine.Execute(new[]
            {
                "Unity", "-graphAuthoringCommand", "draft",
                "-graphAuthoringAsset", "Assets/DoesNotExist.asset",
                "-graphAuthoringModule", "task",
                "-graphAuthoringGraphType", "controlflow"
            }, out var output, out _);

            Assert.That(exitCode, Is.EqualTo(1));
            StringAssert.Contains("\"code\": \"command.argument.invalid\"", output);
        }
    }
}
