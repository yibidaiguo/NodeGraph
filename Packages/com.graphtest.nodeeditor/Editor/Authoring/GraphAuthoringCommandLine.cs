// GraphAuthoringCommandLine.cs —— Unity batchmode 的稳定机器入口。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NodeEditor;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public sealed class GraphAuthoringCommandOutput
    {
        public string command;
        public object data;
        public IReadOnlyList<GraphAuthoringDiagnostic> diagnostics;
        public bool succeeded;
    }

    public static class GraphAuthoringCommandLine
    {
        const string CommandFlag = "-graphAuthoringCommand";
        const string ModuleFlag = "-graphAuthoringModule";
        const string AssetFlag = "-graphAuthoringAsset";
        const string InputFlag = "-graphAuthoringInput";
        const string OutputFlag = "-graphAuthoringOutput";
        const string GroupFlag = "-graphAuthoringGroup";
        const string GraphTypeFlag = "-graphAuthoringGraphType";
        static readonly Encoding s_Utf8 = new UTF8Encoding(false, true);

        public static void Run()
        {
            int exitCode;
            string json;
            string outputPath;
            try
            {
                exitCode = Execute(Environment.GetCommandLineArgs(), out json, out outputPath);
            }
            catch (Exception exception)
            {
                var diagnostic = new GraphAuthoringDiagnostic(
                    "command.unhandled", "$", $"命令执行失败：{exception.GetType().Name}。");
                json = Output("", null, new[] { diagnostic }, false);
                outputPath = null;
                exitCode = 1;
            }

            try
            {
                if (string.IsNullOrEmpty(outputPath)) Debug.Log(json);
                else File.WriteAllText(outputPath, json, s_Utf8);
            }
            catch (Exception exception)
            {
                Debug.LogError($"NodeEditor graph authoring output failed: {exception.GetType().Name}.\n{json}");
                exitCode = 1;
            }

            Environment.ExitCode = exitCode;
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        internal static int Execute(
            IReadOnlyList<string> arguments,
            out string outputJson,
            out string outputPath)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            var options = ParseOptions(arguments, diagnostics);
            options.TryGetValue(OutputFlag, out outputPath);
            options.TryGetValue(CommandFlag, out var command);
            if (diagnostics.Count != 0 || string.IsNullOrEmpty(command))
            {
                if (string.IsNullOrEmpty(command)) Add(diagnostics, "command.missing", "$args.graphAuthoringCommand", "缺少创作命令。");
                outputJson = Output(command, null, diagnostics, false);
                return 1;
            }
            ValidateApplicableOptions(command, options, diagnostics);
            if (diagnostics.Count != 0)
            {
                outputJson = Output(command, null, diagnostics, false);
                return 1;
            }

            try
            {
                return ExecuteCommand(command, options, diagnostics, out outputJson);
            }
            catch (Exception exception)
            {
                Add(diagnostics, "command.failed", "$", $"命令执行失败：{exception.GetType().Name}。");
                outputJson = Output(command, null, diagnostics, false);
                return 1;
            }
        }

        static int ExecuteCommand(
            string command,
            IReadOnlyDictionary<string, string> options,
            List<GraphAuthoringDiagnostic> diagnostics,
            out string outputJson)
        {
            switch (command)
            {
                case "list":
                {
                    options.TryGetValue(ModuleFlag, out var module);
                    module ??= string.Empty;
                    var result = GraphAuthoringAssetAccess.List(module);
                    return Finish(command, result.Graphs, result.Diagnostics, result.Succeeded, out outputJson);
                }
                case "describe":
                {
                    options.TryGetValue(ModuleFlag, out var module);
                    module ??= string.Empty;
                    var result = GraphAuthoringAssetAccess.Describe(module);
                    return Finish(command, result.Catalog, result.Diagnostics, result.Succeeded, out outputJson);
                }
                case "read":
                {
                    if (!Require(options, AssetFlag, diagnostics, out var assetPath))
                        return Finish(command, null, diagnostics, out outputJson);
                    var result = GraphAuthoringAssetAccess.Read(assetPath);
                    return Finish(command, result.Document, result.Diagnostics, result.Succeeded, out outputJson);
                }
                case "draft":
                    return Draft(options, diagnostics, out outputJson);
                case "write":
                    return Write(options, diagnostics, out outputJson);
                case "validate":
                    return Validate(options, diagnostics, out outputJson);
                default:
                    Add(diagnostics, "command.unknown", "$args.graphAuthoringCommand",
                        $"未知创作命令 '{command}'。可用值：list, describe, read, draft, write, validate。");
                    return Finish(command, null, diagnostics, out outputJson);
            }
        }

        static int Draft(
            IReadOnlyDictionary<string, string> options,
            List<GraphAuthoringDiagnostic> diagnostics,
            out string outputJson)
        {
            var valid = Require(options, AssetFlag, diagnostics, out var assetPath);
            valid &= Require(options, ModuleFlag, diagnostics, out var module);
            if (!valid) return Finish("draft", null, diagnostics, out outputJson);
            options.TryGetValue(GroupFlag, out var group);
            group ??= string.Empty;

            var graphType = GraphType.ControlFlow;
            if (options.TryGetValue(GraphTypeFlag, out var graphTypeName) &&
                (!Enum.GetNames(typeof(GraphType)).Contains(graphTypeName, StringComparer.Ordinal) ||
                 !Enum.TryParse(graphTypeName, false, out graphType)))
            {
                Add(diagnostics, "command.argument.invalid", "$args.graphAuthoringGraphType",
                    $"参数 '{GraphTypeFlag}' 必须是 GraphType 的精确枚举名。");
                return Finish("draft", null, diagnostics, out outputJson);
            }

            var result = GraphAuthoringAssetAccess.CreateDraft(assetPath, module, group, graphType);
            return Finish("draft", result.Document, result.Diagnostics, result.Succeeded, out outputJson);
        }

        static int Write(
            IReadOnlyDictionary<string, string> options,
            List<GraphAuthoringDiagnostic> diagnostics,
            out string outputJson)
        {
            var valid = Require(options, AssetFlag, diagnostics, out var assetPath);
            valid &= Require(options, InputFlag, diagnostics, out var inputPath);
            if (!valid) return Finish("write", null, diagnostics, out outputJson);
            if (!TryReadDocument(inputPath, diagnostics, out var document))
                return Finish("write", null, diagnostics, out outputJson);

            var result = GraphAuthoringAssetAccess.Write(assetPath, document);
            return Finish("write", result.Document, result.Diagnostics, result.Succeeded, out outputJson);
        }

        static int Validate(
            IReadOnlyDictionary<string, string> options,
            List<GraphAuthoringDiagnostic> diagnostics,
            out string outputJson)
        {
            if (!Require(options, AssetFlag, diagnostics, out var assetPath))
                return Finish("validate", null, diagnostics, out outputJson);

            GraphAuthoringDocument document = null;
            if (options.TryGetValue(InputFlag, out var inputPath))
                TryReadDocument(inputPath, diagnostics, out document);
            else
            {
                var read = GraphAuthoringAssetAccess.Read(assetPath);
                diagnostics.AddRange(read.Diagnostics);
                document = read.Document;
            }

            if (document == null)
                return Finish("validate", null, diagnostics, out outputJson);

            var result = GraphAuthoringAssetAccess.Validate(assetPath, document);
            return Finish("validate", null, result.Diagnostics, result.Succeeded, out outputJson);
        }

        static bool TryReadDocument(
            string path,
            List<GraphAuthoringDiagnostic> diagnostics,
            out GraphAuthoringDocument document)
        {
            document = null;
            try
            {
                var parsed = GraphAuthoringJson.DeserializeDocument(File.ReadAllText(path, s_Utf8));
                diagnostics.AddRange(parsed.Diagnostics);
                document = parsed.Value;
                return parsed.Succeeded;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException || exception is DecoderFallbackException)
            {
                Add(diagnostics, "command.input.read-failed", "$args.graphAuthoringInput",
                    $"无法按 UTF-8 读取输入文件：{exception.GetType().Name}。");
                return false;
            }
        }

        static Dictionary<string, string> ParseOptions(
            IReadOnlyList<string> arguments,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var known = new HashSet<string>(new[]
            {
                CommandFlag, ModuleFlag, AssetFlag, InputFlag, OutputFlag, GroupFlag, GraphTypeFlag
            }, StringComparer.Ordinal);
            for (int i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (!known.Contains(argument))
                {
                    if (IsAuthoringFlag(argument))
                        Add(diagnostics, "command.argument.unknown", "$args", $"未知创作参数 '{argument}'。");
                    continue;
                }
                if (result.ContainsKey(argument))
                {
                    Add(diagnostics, "command.argument.duplicate", "$args", $"参数 '{argument}' 重复。");
                    continue;
                }
                if (i + 1 >= arguments.Count || IsAuthoringFlag(arguments[i + 1]) || string.IsNullOrWhiteSpace(arguments[i + 1]))
                {
                    Add(diagnostics, "command.argument.value-missing", "$args", $"参数 '{argument}' 缺少值。");
                    continue;
                }
                result.Add(argument, arguments[++i]);
            }
            return result;
        }

        static bool IsAuthoringFlag(string argument) =>
            argument != null && argument.StartsWith("-graphAuthoring", StringComparison.Ordinal);

        static void ValidateApplicableOptions(
            string command,
            IReadOnlyDictionary<string, string> options,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var allowed = new HashSet<string>(new[] { CommandFlag, OutputFlag }, StringComparer.Ordinal);
            switch (command)
            {
                case "list":
                case "describe":
                    allowed.Add(ModuleFlag);
                    break;
                case "read":
                    allowed.Add(AssetFlag);
                    break;
                case "draft":
                    allowed.Add(AssetFlag);
                    allowed.Add(ModuleFlag);
                    allowed.Add(GroupFlag);
                    allowed.Add(GraphTypeFlag);
                    break;
                case "write":
                    allowed.Add(AssetFlag);
                    allowed.Add(InputFlag);
                    break;
                case "validate":
                    allowed.Add(AssetFlag);
                    allowed.Add(InputFlag);
                    break;
                default:
                    return;
            }
            foreach (string option in options.Keys.Where(option => !allowed.Contains(option)))
                Add(diagnostics, "command.argument.not-applicable", "$args",
                    $"参数 '{option}' 不适用于命令 '{command}'。");
        }

        static bool Require(
            IReadOnlyDictionary<string, string> options,
            string option,
            List<GraphAuthoringDiagnostic> diagnostics,
            out string value)
        {
            if (options.TryGetValue(option, out value)) return true;
            Add(diagnostics, "command.argument.missing", "$args", $"缺少参数 '{option}'。");
            return false;
        }

        static int Finish(
            string command,
            object data,
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics,
            bool succeeded,
            out string outputJson)
        {
            var allDiagnostics = diagnostics ?? Array.Empty<GraphAuthoringDiagnostic>();
            succeeded &= allDiagnostics.Count == 0;
            outputJson = Output(command, data, allDiagnostics, succeeded);
            return succeeded ? 0 : 1;
        }

        static int Finish(
            string command,
            object data,
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics,
            out string outputJson) =>
            Finish(command, data, diagnostics, false, out outputJson);

        static string Output(
            string command,
            object data,
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics,
            bool succeeded) =>
            GraphAuthoringJson.SerializeResult(new GraphAuthoringCommandOutput
            {
                command = command ?? string.Empty,
                data = data,
                diagnostics = diagnostics ?? Array.Empty<GraphAuthoringDiagnostic>(),
                succeeded = succeeded
            });

        static void Add(List<GraphAuthoringDiagnostic> diagnostics, string code, string path, string message) =>
            diagnostics.Add(new GraphAuthoringDiagnostic(code, path, message));
    }
}
