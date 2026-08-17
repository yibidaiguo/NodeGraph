// GraphAuthoringJson.cs —— Editor-only 的严格、确定性 JSON 边界。
// Runtime 创作模型不依赖 JSON 库；文件与命令行入口统一经过这里，避免两套序列化规则。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public sealed class GraphAuthoringJsonReadResult<T>
    {
        internal GraphAuthoringJsonReadResult(T value, IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        {
            Value = value;
            Diagnostics = diagnostics;
        }

        public T Value { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Value != null && Diagnostics.Count == 0;
    }

    public static class GraphAuthoringJson
    {
        static readonly JsonSerializerSettings s_Settings = CreateSettings();
        static readonly Regex s_RfcNumber = new(
            @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$",
            RegexOptions.CultureInvariant);

        public static string Serialize(object value) =>
            JsonConvert.SerializeObject(value, Formatting.Indented, s_Settings);

        public static string SerializeDocument(GraphAuthoringDocument document) => Serialize(document);
        public static string SerializeCatalog(GraphAuthoringCatalog catalog) => Serialize(catalog);
        public static string SerializeList<T>(IReadOnlyList<T> values) => Serialize(values);
        public static string SerializeResult(object result) => Serialize(result);

        public static GraphAuthoringJsonReadResult<GraphAuthoringDocument> DeserializeDocument(string json) =>
            Deserialize<GraphAuthoringDocument>(json);

        public static GraphAuthoringJsonReadResult<T> Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return Failure<T>("json.empty", "$", "JSON 内容不能为空。");

            try
            {
                RejectNonStandardSyntax(json);
                RejectNonRfcNumbers(json);
                RejectReaderExtensions(json);
                RejectComments(json);
                var token = ParseSingleToken(json);
                var serializer = JsonSerializer.Create(s_Settings);
                if (TryFindPropertyCaseMismatch(token, typeof(T), serializer.ContractResolver, "$", out var mismatchPath))
                    return Failure<T>(
                        "json.property-case-mismatch",
                        mismatchPath,
                        "JSON 属性名大小写必须与当前 schema 完全一致。");
                var value = token.ToObject<T>(serializer);
                if (value == null)
                    return Failure<T>("json.null", "$", "JSON 根值不能为 null。");
                return new GraphAuthoringJsonReadResult<T>(value, Array.Empty<GraphAuthoringDiagnostic>());
            }
            catch (JsonReaderException exception)
            {
                var duplicate = exception.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0;
                return Failure<T>(
                    duplicate ? "json.duplicate-property" : "json.syntax",
                    NormalizePath(exception.Path),
                    duplicate ? "JSON 对象包含重复属性。" : "JSON 语法无效。");
            }
            catch (JsonSerializationException exception)
            {
                var missing = exception.Message.IndexOf("Required property", StringComparison.Ordinal) >= 0 &&
                              exception.Message.IndexOf("not found", StringComparison.Ordinal) >= 0;
                var unknown = exception.Message.IndexOf("Could not find member", StringComparison.Ordinal) >= 0;
                var path = NormalizePath(exception.Path);
                if (missing) path = AppendMissingMember(path, MissingMemberName(exception.Message));
                return Failure<T>(
                    missing ? "json.missing-member" : unknown ? "json.unknown-member" : "json.invalid-value",
                    path,
                    missing
                        ? "JSON 缺少当前 schema 要求的属性。"
                        : unknown
                            ? "JSON 包含当前 schema 未声明的属性。"
                            : "JSON 值与当前 schema 不兼容。");
            }
        }

        static JToken ParseSingleToken(string json)
        {
            using var stringReader = new StringReader(json);
            using var reader = CreateReader(stringReader);
            var token = JToken.ReadFrom(reader, new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                LineInfoHandling = LineInfoHandling.Load
            });
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.Comment) continue;
                throw new JsonReaderException("JSON 根值之后存在额外内容。");
            }
            return token;
        }

        static void RejectComments(string json)
        {
            using var stringReader = new StringReader(json);
            using var reader = CreateReader(stringReader);
            while (reader.Read())
                if (reader.TokenType == JsonToken.Comment)
                    throw new JsonReaderException("JSON 注释不属于创作格式。", reader.Path, reader.LineNumber, reader.LinePosition, null);
        }

        static void RejectNonStandardSyntax(string json)
        {
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped) { escaped = false; continue; }
                    if (current == '\\') { escaped = true; continue; }
                    if (current == '"') inString = false;
                    continue;
                }

                if (current == '"') { inString = true; continue; }
                if (current == '\'')
                    throw new JsonReaderException("JSON 字符串和属性名必须使用双引号。");
                if (current != ',') continue;

                int next = i + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next < json.Length && (json[next] == '}' || json[next] == ']'))
                    throw new JsonReaderException("JSON 不允许尾随逗号。");
            }
        }

        static void RejectReaderExtensions(string json)
        {
            using var stringReader = new StringReader(json);
            using var reader = CreateReader(stringReader);
            while (reader.Read())
            {
                if ((reader.TokenType == JsonToken.PropertyName || reader.TokenType == JsonToken.String) &&
                    reader.QuoteChar != '"')
                    throw new JsonReaderException("JSON 属性名和字符串必须使用双引号。");
                if (reader.TokenType == JsonToken.Undefined || reader.TokenType == JsonToken.StartConstructor)
                    throw new JsonReaderException("JSON 不允许 JavaScript 扩展值。");
                if (reader.TokenType == JsonToken.Float && reader.Value is IConvertible number)
                {
                    double value = number.ToDouble(CultureInfo.InvariantCulture);
                    if (double.IsNaN(value) || double.IsInfinity(value))
                        throw new JsonReaderException("JSON 数字必须是有限值。");
                }
            }
        }

        static void RejectNonRfcNumbers(string json)
        {
            bool inString = false;
            bool escaped = false;
            char previousSignificant = '\0';
            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped) { escaped = false; continue; }
                    if (current == '\\') { escaped = true; continue; }
                    if (current == '"')
                    {
                        inString = false;
                        previousSignificant = '"';
                    }
                    continue;
                }

                if (current == '"') { inString = true; continue; }
                if (char.IsWhiteSpace(current)) continue;

                bool valuePosition = previousSignificant == '\0' || previousSignificant == ':' ||
                                     previousSignificant == '[' || previousSignificant == ',';
                bool numberStart = current == '-' || current == '+' || current == '.' ||
                                   (current >= '0' && current <= '9');
                if (valuePosition && numberStart)
                {
                    int end = i + 1;
                    while (end < json.Length && !char.IsWhiteSpace(json[end]) &&
                           json[end] != ',' && json[end] != ']' && json[end] != '}')
                        end++;
                    string lexeme = json.Substring(i, end - i);
                    if (!s_RfcNumber.IsMatch(lexeme))
                        throw new JsonReaderException($"JSON 数字 '{lexeme}' 不符合 RFC 8259。");
                    i = end - 1;
                    previousSignificant = '#';
                    continue;
                }

                previousSignificant = current;
            }
        }

        static bool TryFindPropertyCaseMismatch(
            JToken token,
            Type targetType,
            IContractResolver resolver,
            string path,
            out string mismatchPath)
        {
            mismatchPath = null;
            if (token == null || targetType == null || targetType == typeof(object)) return false;
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var contract = resolver.ResolveContract(targetType);
            if (token is JObject value && contract is JsonObjectContract objectContract)
            {
                var properties = objectContract.Properties.Where(property => !property.Ignored).ToArray();
                foreach (var member in value.Properties())
                {
                    var exact = properties.FirstOrDefault(property =>
                        string.Equals(property.PropertyName, member.Name, StringComparison.Ordinal));
                    if (exact == null)
                    {
                        if (properties.Any(property =>
                                string.Equals(property.PropertyName, member.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            mismatchPath = path + "." + member.Name;
                            return true;
                        }
                        continue;
                    }
                    if (TryFindPropertyCaseMismatch(
                            member.Value, exact.PropertyType, resolver, path + "." + member.Name, out mismatchPath))
                        return true;
                }
            }
            else if (token is JArray array && contract is JsonArrayContract arrayContract)
            {
                for (int i = 0; i < array.Count; i++)
                    if (TryFindPropertyCaseMismatch(
                            array[i], arrayContract.CollectionItemType, resolver, $"{path}[{i}]", out mismatchPath))
                        return true;
            }
            return false;
        }

        static JsonTextReader CreateReader(TextReader reader) => new(reader)
        {
            Culture = CultureInfo.InvariantCulture,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };

        static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new OrdinalContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None
            };
            settings.Converters.Add(new StringEnumConverter { AllowIntegerValues = false });
            return settings;
        }

        static GraphAuthoringJsonReadResult<T> Failure<T>(string code, string path, string message) where T : class =>
            new(null, Array.AsReadOnly(new[] { new GraphAuthoringDiagnostic(code, path, message) }));

        static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "$";
            return path[0] == '[' ? "$" + path : "$." + path;
        }

        static string MissingMemberName(string message)
        {
            const string marker = "Required property '";
            int start = message.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            int end = message.IndexOf('\'', start);
            return end > start ? message.Substring(start, end - start) : null;
        }

        static string AppendMissingMember(string path, string member) =>
            string.IsNullOrEmpty(member) ? path : path + "." + member;

        sealed class OrdinalContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var properties = base.CreateProperties(type, memberSerialization)
                    .OrderBy(property => property.PropertyName, StringComparer.Ordinal)
                    .ToList();
                if (IsDocumentType(type))
                    foreach (var property in properties)
                        property.Required = Required.AllowNull;
                return properties;
            }

            static bool IsDocumentType(Type type) =>
                type == typeof(GraphAuthoringDocument) ||
                type == typeof(GraphAuthoringRevisionVector) ||
                type == typeof(GraphAuthoringRevisionOwner) ||
                type == typeof(GraphAuthoringNode) ||
                type == typeof(GraphAuthoringEdge) ||
                type == typeof(GraphAuthoringParam) ||
                type == typeof(GraphAuthoringGraphRef) ||
                type == typeof(GraphAuthoringUnitSlot) ||
                type == typeof(GraphAuthoringUnit) ||
                type == typeof(GraphAuthoringUnitField) ||
                type == typeof(GraphAuthoringBlackboardLayer) ||
                type == typeof(GraphAuthoringBlackboardVariableData) ||
                type == typeof(GraphAuthoringTypeRef);
        }
    }
}
