// GraphAuthoringBlackboardCodec.cs —— 黑板资产与 AI 完整快照之间的纯层无损转换。
// 不依赖 JSON、Editor、文件系统或 GUID；导入/导出失败时不返回任何部分结果。

using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    public sealed class GraphAuthoringBlackboardOwner
    {
        public GraphAuthoringBlackboardOwner(string ownerPath, IBlackboardDecl data)
        {
            OwnerPath = ownerPath;
            Data = data;
        }

        public string OwnerPath { get; }
        public IBlackboardDecl Data { get; }
    }

    public sealed class GraphAuthoringBlackboardExportResult
    {
        public IReadOnlyList<GraphAuthoringBlackboardLayer> Layers { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Layers != null && Diagnostics.Count == 0;

        internal GraphAuthoringBlackboardExportResult(
            IReadOnlyList<GraphAuthoringBlackboardLayer> layers,
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        {
            Layers = layers;
            Diagnostics = diagnostics;
        }
    }

    public sealed class GraphAuthoringBlackboardImportResult
    {
        public IReadOnlyList<GraphAuthoringBlackboardOwner> Owners { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Owners != null && Diagnostics.Count == 0;

        internal GraphAuthoringBlackboardImportResult(
            IReadOnlyList<GraphAuthoringBlackboardOwner> owners,
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        {
            Owners = owners;
            Diagnostics = diagnostics;
        }
    }

    public static class GraphAuthoringBlackboardCodec
    {
        public static GraphAuthoringBlackboardExportResult Export(
            IEnumerable<GraphAuthoringBlackboardOwner> sources)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            if (sources == null)
            {
                Add(diagnostics, "authoring.blackboard.collection.missing", "$.blackboards",
                    "黑板层集合不能为空。");
                return ExportFailure(diagnostics);
            }

            var sourceList = sources.ToArray();
            var layers = new List<GraphAuthoringBlackboardLayer>(sourceList.Length);
            var ownerPaths = new HashSet<string>(StringComparer.Ordinal);
            var scopes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sourceList.Length; i++)
            {
                var path = $"$.blackboards[{i}]";
                var source = sourceList[i];
                if (source == null)
                {
                    Add(diagnostics, "authoring.blackboard.layer.missing", path, "黑板层不能为空。");
                    continue;
                }
                if (source.Data == null)
                {
                    Add(diagnostics, "authoring.blackboard.source.missing", path, "黑板声明不能为空。");
                    continue;
                }

                string ownerPath = NormalizeOwnerPath(source.OwnerPath);
                if (!string.Equals(source.OwnerPath, ownerPath, StringComparison.Ordinal))
                    Add(diagnostics, "authoring.blackboard.owner-path.not-normalized", path + ".ownerPath",
                        "ownerPath 必须使用规范化的 Assets/... 项目相对路径。");
                ValidateOwnerPath(ownerPath, path + ".ownerPath", ownerPaths, diagnostics);
                string module = source.Data.Module;
                string group = source.Data.Group;
                ValidateScope(module, group, path, scopes, diagnostics);

                var layer = new GraphAuthoringBlackboardLayer
                {
                    ownerPath = ownerPath,
                    module = module,
                    group = group
                };
                CopyVariables(source.Data.Variables, layer.variables, path + ".variables", diagnostics);
                layers.Add(layer);
            }

            return diagnostics.Count == 0
                ? new GraphAuthoringBlackboardExportResult(Array.AsReadOnly(layers.ToArray()), diagnostics)
                : ExportFailure(diagnostics);
        }

        public static GraphAuthoringBlackboardImportResult Import(
            IReadOnlyList<GraphAuthoringBlackboardLayer> layers)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            if (layers == null)
            {
                Add(diagnostics, "authoring.blackboard.collection.missing", "$.blackboards",
                    "黑板层集合不能为空。");
                return ImportFailure(diagnostics);
            }

            var owners = new List<GraphAuthoringBlackboardOwner>(layers.Count);
            var ownerPaths = new HashSet<string>(StringComparer.Ordinal);
            var scopes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layers.Count; i++)
            {
                var path = $"$.blackboards[{i}]";
                var layer = layers[i];
                if (layer == null)
                {
                    Add(diagnostics, "authoring.blackboard.layer.missing", path, "黑板层不能为空。");
                    continue;
                }

                string normalizedPath = NormalizeOwnerPath(layer.ownerPath);
                if (!string.Equals(layer.ownerPath, normalizedPath, StringComparison.Ordinal))
                    Add(diagnostics, "authoring.blackboard.owner-path.not-normalized", path + ".ownerPath",
                        "ownerPath 必须使用正斜杠，且不得有首尾空白或尾斜杠。");
                ValidateOwnerPath(normalizedPath, path + ".ownerPath", ownerPaths, diagnostics);
                ValidateScope(layer.module, layer.group, path, scopes, diagnostics);

                var variables = new List<VariableDef>();
                CopyVariables(layer.variables, variables, path + ".variables", diagnostics);
                owners.Add(new GraphAuthoringBlackboardOwner(
                    normalizedPath,
                    new BlackboardDecl(layer.module, layer.group, variables)));
            }

            return diagnostics.Count == 0
                ? new GraphAuthoringBlackboardImportResult(Array.AsReadOnly(owners.ToArray()), diagnostics)
                : ImportFailure(diagnostics);
        }

        public static GraphAuthoringBlackboardImportResult Import(GraphAuthoringDocument document)
        {
            if (document == null)
            {
                var missingDiagnostics = new List<GraphAuthoringDiagnostic>();
                Add(missingDiagnostics, "authoring.document.missing", "$", "创作文档不能为空。");
                return ImportFailure(missingDiagnostics);
            }

            var imported = Import(document.blackboards);
            if (!imported.Succeeded) return imported;

            var diagnostics = new List<GraphAuthoringDiagnostic>();
            ValidateEffectiveLayers(document.module, document.group, document.blackboards, diagnostics);
            return diagnostics.Count == 0
                ? imported
                : ImportFailure(diagnostics);
        }

        static void ValidateEffectiveLayers(
            string documentModule,
            string documentGroup,
            IReadOnlyList<GraphAuthoringBlackboardLayer> layers,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            bool contextValid = true;
            if (documentModule == null || (documentModule.Length != 0 && !IsStableText(documentModule)))
            {
                Add(diagnostics, "authoring.blackboard.effective-context.invalid", "$.module",
                    "图 module 必须是非 null、无首尾空白且不含控制字符的稳定值。");
                contextValid = false;
            }
            if (documentGroup == null || (documentGroup.Length != 0 && !IsStableText(documentGroup)))
            {
                Add(diagnostics, "authoring.blackboard.effective-context.invalid", "$.group",
                    "图 group 必须是非 null、无首尾空白且不含控制字符的稳定值。");
                contextValid = false;
            }
            if (string.IsNullOrEmpty(documentModule) && !string.IsNullOrEmpty(documentGroup))
            {
                Add(diagnostics, "authoring.blackboard.effective-context.invalid", "$.group",
                    "带 group 的图必须同时声明 module。");
                contextValid = false;
            }
            if (!contextValid) return;

            int previousRank = -1;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                int rank = EffectiveRank(documentModule, documentGroup, layer);
                if (rank < 0)
                {
                    Add(diagnostics, "authoring.blackboard.effective-scope.invalid", $"$.blackboards[{i}]",
                        "黑板层不属于当前图的 global/module/exact-group 有效作用域。");
                    continue;
                }
                if (rank < previousRank)
                    Add(diagnostics, "authoring.blackboard.effective-order.invalid", $"$.blackboards[{i}]",
                        "有效黑板层必须按 global、module、group 从外到内排列。");
                previousRank = rank;
            }
        }

        static int EffectiveRank(
            string documentModule,
            string documentGroup,
            GraphAuthoringBlackboardLayer layer)
        {
            if (layer.module.Length == 0 && layer.group.Length == 0) return 0;
            if (documentModule.Length == 0 ||
                !string.Equals(layer.module, documentModule, StringComparison.Ordinal))
                return -1;
            if (layer.group.Length == 0) return 1;
            return documentGroup.Length != 0 &&
                   string.Equals(layer.group, documentGroup, StringComparison.Ordinal)
                ? 2
                : -1;
        }

        static void CopyVariables(
            IReadOnlyList<VariableDef> sources,
            List<GraphAuthoringBlackboardVariableData> destination,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (sources == null)
            {
                Add(diagnostics, "authoring.blackboard.collection.missing", path, "变量集合不能为空。");
                return;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Count; i++)
            {
                var variablePath = $"{path}[{i}]";
                var source = sources[i];
                if (source == null)
                {
                    Add(diagnostics, "authoring.blackboard.variable.missing", variablePath, "变量不能为空。");
                    continue;
                }
                ValidateKey(source.key, variablePath + ".key", keys, diagnostics);
                destination.Add(new GraphAuthoringBlackboardVariableData
                {
                    key = source.key,
                    type = CopyType(source.type, variablePath + ".type", diagnostics,
                        new HashSet<TypeRef>(ReferenceComparer<TypeRef>.Instance)),
                    defaultJson = source.defaultJson
                });
            }
        }

        static void CopyVariables(
            IReadOnlyList<GraphAuthoringBlackboardVariableData> sources,
            List<VariableDef> destination,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (sources == null)
            {
                Add(diagnostics, "authoring.blackboard.collection.missing", path, "变量集合不能为空。");
                return;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Count; i++)
            {
                var variablePath = $"{path}[{i}]";
                var source = sources[i];
                if (source == null)
                {
                    Add(diagnostics, "authoring.blackboard.variable.missing", variablePath, "变量不能为空。");
                    continue;
                }
                ValidateKey(source.key, variablePath + ".key", keys, diagnostics);
                destination.Add(new VariableDef
                {
                    key = source.key,
                    type = CopyType(source.type, variablePath + ".type", diagnostics,
                        new HashSet<GraphAuthoringTypeRef>(ReferenceComparer<GraphAuthoringTypeRef>.Instance)),
                    defaultJson = source.defaultJson
                });
            }
        }

        static GraphAuthoringTypeRef CopyType(
            TypeRef source,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics,
            HashSet<TypeRef> ancestors)
        {
            if (source == null)
            {
                Add(diagnostics, "authoring.blackboard.type.missing", path, "变量类型不能为空。");
                return null;
            }
            if (!ancestors.Add(source))
            {
                Add(diagnostics, "authoring.blackboard.type.cycle", path, "变量类型不能形成循环。");
                return null;
            }

            ValidateTypeShape(
                source.kind, source.primitive, source.enumOrObjectName, source.element != null, path, diagnostics);
            var result = new GraphAuthoringTypeRef
            {
                kind = source.kind,
                primitive = source.primitive,
                enumOrObjectName = source.enumOrObjectName
            };
            if (source.element != null)
                result.element = CopyType(source.element, path + ".element", diagnostics, ancestors);
            ancestors.Remove(source);
            return result;
        }

        static TypeRef CopyType(
            GraphAuthoringTypeRef source,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics,
            HashSet<GraphAuthoringTypeRef> ancestors)
        {
            if (source == null)
            {
                Add(diagnostics, "authoring.blackboard.type.missing", path, "变量类型不能为空。");
                return null;
            }
            if (!ancestors.Add(source))
            {
                Add(diagnostics, "authoring.blackboard.type.cycle", path, "变量类型不能形成循环。");
                return null;
            }

            ValidateTypeShape(
                source.kind, source.primitive, source.enumOrObjectName, source.element != null, path, diagnostics);
            var result = new TypeRef
            {
                kind = source.kind,
                primitive = source.primitive,
                enumOrObjectName = source.enumOrObjectName
            };
            if (source.element != null)
                result.element = CopyType(source.element, path + ".element", diagnostics, ancestors);
            ancestors.Remove(source);
            return result;
        }

        static void ValidateTypeShape(
            TypeKind kind,
            PrimitiveType primitive,
            string name,
            bool hasElement,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (!Enum.IsDefined(typeof(TypeKind), kind))
            {
                Add(diagnostics, "authoring.blackboard.type.enum-invalid", path + ".kind", "TypeKind 值无效。");
                return;
            }
            if (!Enum.IsDefined(typeof(PrimitiveType), primitive))
                Add(diagnostics, "authoring.blackboard.type.enum-invalid", path + ".primitive", "PrimitiveType 值无效。");

            bool needsName = kind == TypeKind.Enum || kind == TypeKind.Object ||
                             kind == TypeKind.BlackboardValueRef || kind == TypeKind.Unit;
            if (needsName)
            {
                if (!IsStableText(name))
                {
                    Add(diagnostics, "authoring.blackboard.type.shape", path + ".enumOrObjectName",
                        "此类型需要非空、无首尾空白且不含控制字符的名称。");
                }
            }
            else if (!string.IsNullOrEmpty(name))
            {
                Add(diagnostics, "authoring.blackboard.type.shape", path + ".enumOrObjectName",
                    "此类型不得携带 enumOrObjectName。");
            }

            if (kind == TypeKind.List)
            {
                if (!hasElement)
                {
                    Add(diagnostics, "authoring.blackboard.type.shape", path + ".element",
                        "List 类型必须声明 element。");
                }
            }
            else if (hasElement)
            {
                Add(diagnostics, "authoring.blackboard.type.shape", path + ".element",
                    "只有 List 类型可以声明 element。");
            }
        }

        static void ValidateScope(
            string module,
            string group,
            string path,
            HashSet<string> scopes,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            bool valid = true;
            if (module == null || (module.Length != 0 && !IsStableText(module)))
            {
                Add(diagnostics, "authoring.blackboard.module.invalid", path + ".module",
                    "module 必须是非 null、无首尾空白且不含控制字符的稳定值。");
                valid = false;
            }
            if (group == null || (group.Length != 0 && !IsStableText(group)))
            {
                Add(diagnostics, "authoring.blackboard.group.invalid", path + ".group",
                    "group 必须是非 null、无首尾空白且不含控制字符的稳定值。");
                valid = false;
            }
            if (string.IsNullOrEmpty(module) && !string.IsNullOrEmpty(group))
            {
                Add(diagnostics, "authoring.blackboard.scope.invalid", path,
                    "组级黑板必须同时声明 module。");
                valid = false;
            }
            if (valid && !scopes.Add(module + "\0" + group))
                Add(diagnostics, "authoring.blackboard.scope.duplicate", path,
                    "同一份快照中 module/group 作用域不得重复。");
        }

        static void ValidateOwnerPath(
            string ownerPath,
            string path,
            HashSet<string> ownerPaths,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (!IsProjectAssetOwnerPath(ownerPath))
            {
                Add(diagnostics, "authoring.blackboard.owner-path.invalid", path,
                    "ownerPath 必须是 Assets/... 下的规范项目相对资产路径，且不得包含空、.、.. 段或冒号。");
                return;
            }
            if (!ownerPaths.Add(ownerPath))
                Add(diagnostics, "authoring.blackboard.owner-path.duplicate", path,
                    "同一份快照中的 ownerPath 必须唯一。");
        }

        static void ValidateKey(
            string key,
            string path,
            HashSet<string> keys,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (!IsStableText(key))
            {
                Add(diagnostics, "authoring.blackboard.key.invalid", path,
                    "变量 key 必须非空、无首尾空白且不含控制字符。");
                return;
            }
            if (!keys.Add(key))
                Add(diagnostics, "authoring.blackboard.key.duplicate", path,
                    "同一黑板层内的变量 key 必须唯一。");
        }

        static bool IsStableText(string value) =>
            !string.IsNullOrEmpty(value) && value == value.Trim() && !value.Any(char.IsControl);

        static string NormalizeOwnerPath(string path) =>
            (path ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');

        static bool IsProjectAssetOwnerPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Any(char.IsControl) || path.IndexOf(':') >= 0 ||
                !path.StartsWith("Assets/", StringComparison.Ordinal))
                return false;
            var segments = path.Split('/');
            return segments.Length > 1 && segments.All(segment =>
                segment.Length != 0 && segment != "." && segment != "..");
        }

        static GraphAuthoringBlackboardExportResult ExportFailure(
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics) =>
            new GraphAuthoringBlackboardExportResult(null, diagnostics);

        static GraphAuthoringBlackboardImportResult ImportFailure(
            IReadOnlyList<GraphAuthoringDiagnostic> diagnostics) =>
            new GraphAuthoringBlackboardImportResult(null, diagnostics);

        static void Add(
            List<GraphAuthoringDiagnostic> diagnostics,
            string code,
            string path,
            string message) => diagnostics.Add(new GraphAuthoringDiagnostic(code, path, message));

        sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly ReferenceComparer<T> Instance = new();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
