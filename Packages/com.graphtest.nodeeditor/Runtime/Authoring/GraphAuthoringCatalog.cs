// GraphAuthoringCatalog.cs —— 面向 AI/工具的纯只读创作能力目录。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NodeEditor
{
    public enum GraphAuthoringBlackboardScope { Global, Module, Group }

    public sealed class GraphAuthoringCatalog
    {
        internal GraphAuthoringCatalog(
            string module,
            IReadOnlyList<GraphAuthoringDefinition> definitions,
            IReadOnlyList<GraphAuthoringUnitDefinition> units,
            IReadOnlyList<string> unitIds,
            IReadOnlyList<GraphAuthoringBlackboardVariable> blackboardVariables)
        {
            Module = module;
            Definitions = definitions;
            Units = units;
            UnitIds = unitIds;
            BlackboardVariables = blackboardVariables;
        }

        public string Module { get; }
        public IReadOnlyList<GraphAuthoringDefinition> Definitions { get; }
        public IReadOnlyList<GraphAuthoringUnitDefinition> Units { get; }
        // 兼容既有只读调用方；完整能力以 Units 为准。
        public IReadOnlyList<string> UnitIds { get; }
        public IReadOnlyList<GraphAuthoringBlackboardVariable> BlackboardVariables { get; }
    }

    public sealed class GraphAuthoringUnitDefinition
    {
        internal GraphAuthoringUnitDefinition(
            string stableId,
            NodeRole role,
            IReadOnlyList<GraphAuthoringUnitFieldDefinition> fields)
        {
            StableId = stableId;
            Role = role;
            Family = role.ToString();
            Fields = fields;
        }

        public string StableId { get; }
        public NodeRole Role { get; }
        public string Family { get; }
        public IReadOnlyList<GraphAuthoringUnitFieldDefinition> Fields { get; }
    }

    public sealed class GraphAuthoringUnitFieldDefinition
    {
        internal GraphAuthoringUnitFieldDefinition(
            string name,
            GraphAuthoringUnitFieldKind kind,
            string scalarType,
            IReadOnlyList<string> enumValues,
            string expectedUnitFamily,
            string expectedUnitTypeId,
            bool nullable,
            string payload)
        {
            Name = name;
            Kind = kind;
            ScalarType = scalarType;
            EnumValues = enumValues;
            ExpectedUnitFamily = expectedUnitFamily;
            ExpectedUnitTypeId = expectedUnitTypeId;
            Nullable = nullable;
            Payload = payload;
        }

        public string Name { get; }
        public GraphAuthoringUnitFieldKind Kind { get; }
        public string ScalarType { get; }
        public IReadOnlyList<string> EnumValues { get; }
        public string ExpectedUnitFamily { get; }
        // null 表示可接受该 family 的任意已注册具体 Unit；非空则还需满足该稳定类型 id。
        public string ExpectedUnitTypeId { get; }
        // Codec 要求每个已发现字段均出现；非 null 时只允许指定的 payload 成员存在。
        public bool Required => true;
        public bool Nullable { get; }
        public string Payload { get; }
        public bool PayloadRequiredWhenNonNull => true;
        public bool PayloadForbiddenWhenNull => true;
    }

    public sealed class GraphAuthoringDefinition
    {
        internal GraphAuthoringDefinition(
            string id,
            string name,
            string module,
            string kind,
            NodeRole role,
            RuntimeKind runtime,
            int version,
            IReadOnlyList<GraphAuthoringPort> inputs,
            IReadOnlyList<GraphAuthoringPort> outputs,
            IReadOnlyList<GraphAuthoringParameter> parameters)
        {
            Id = id;
            Name = name;
            Module = module;
            Kind = kind;
            Role = role;
            Runtime = runtime;
            Version = version;
            Inputs = inputs;
            Outputs = outputs;
            Parameters = parameters;
        }

        public string Id { get; }
        public string Name { get; }
        public string Module { get; }
        public string Kind { get; }
        public NodeRole Role { get; }
        public RuntimeKind Runtime { get; }
        public int Version { get; }
        public IReadOnlyList<GraphAuthoringPort> Inputs { get; }
        public IReadOnlyList<GraphAuthoringPort> Outputs { get; }
        public IReadOnlyList<GraphAuthoringParameter> Parameters { get; }
    }

    public sealed class GraphAuthoringPort
    {
        internal GraphAuthoringPort(string name, GraphAuthoringType type, Arity arity)
        {
            Name = name;
            Type = type;
            Arity = arity;
        }

        public string Name { get; }
        public GraphAuthoringType Type { get; }
        public Arity Arity { get; }
    }

    public sealed class GraphAuthoringParameter
    {
        internal GraphAuthoringParameter(ParamDef source)
        {
            Name = source.name;
            Type = GraphAuthoringCatalogBuilder.CopyType(source.type);
            DefaultJson = source.defaultJson;
            HasBounds = source.hasBounds;
            BoundsMin = source.boundsMin;
            BoundsMax = source.boundsMax;
            ChoiceSource = source.choiceSource;
        }

        public string Name { get; }
        public GraphAuthoringType Type { get; }
        public string DefaultJson { get; }
        public bool HasBounds { get; }
        public float BoundsMin { get; }
        public float BoundsMax { get; }
        public string ChoiceSource { get; }
    }

    public sealed class GraphAuthoringType
    {
        internal GraphAuthoringType(TypeKind kind, PrimitiveType? primitive, string name, GraphAuthoringType element)
        {
            Kind = kind;
            Primitive = primitive;
            Name = name;
            Element = element;
        }

        public TypeKind Kind { get; }
        public PrimitiveType? Primitive { get; }
        // Enum/Object 的类型名、Unit 的族名、BlackboardValueRef 的兄弟键参数名均保留在此。
        public string Name { get; }
        public GraphAuthoringType Element { get; }
    }

    public sealed class GraphAuthoringBlackboardVariable
    {
        internal GraphAuthoringBlackboardVariable(
            GraphAuthoringBlackboardScope scope,
            string module,
            string group,
            string key,
            GraphAuthoringType type,
            string defaultJson)
        {
            Scope = scope;
            Module = module;
            Group = group;
            Key = key;
            Type = type;
            DefaultJson = defaultJson;
        }

        public GraphAuthoringBlackboardScope Scope { get; }
        public string Module { get; }
        public string Group { get; }
        public string Key { get; }
        public GraphAuthoringType Type { get; }
        public string DefaultJson { get; }
    }

    public static class GraphAuthoringCatalogBuilder
    {
        public static GraphAuthoringCatalog Build(
            string module,
            IEnumerable<NodeSchema> definitions,
            IEnumerable<Type> unitTypes,
            IEnumerable<IBlackboardDecl> blackboards)
        {
            if (unitTypes == null) throw new ArgumentNullException(nameof(unitTypes));
            var types = unitTypes.ToArray();
            if (types.Any(type => type == null))
                throw new ArgumentException("Unit 类型列表不能包含 null。", nameof(unitTypes));
            return Build(module, definitions, new UnitAuthoringCatalog(types), blackboards);
        }

        public static GraphAuthoringCatalog Build(
            string module,
            IEnumerable<NodeSchema> definitions,
            UnitAuthoringCatalog unitCatalog,
            IEnumerable<IBlackboardDecl> blackboards)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (unitCatalog == null) throw new ArgumentNullException(nameof(unitCatalog));
            if (blackboards == null) throw new ArgumentNullException(nameof(blackboards));

            module ??= string.Empty;
            var schemas = definitions.ToArray();
            if (schemas.Any(schema => schema == null))
                throw new ArgumentException("节点定义列表不能包含 null。", nameof(definitions));

            var selectedSchemas = schemas
                .Where(schema => string.IsNullOrEmpty(schema.module) ||
                                 string.Equals(schema.module, module, StringComparison.Ordinal))
                .ToArray();
            EnsureStableIds(selectedSchemas.Select(schema => schema.id), "节点定义");

            var definitionDtos = selectedSchemas
                .OrderBy(schema => schema.id, StringComparer.Ordinal)
                .Select(BuildDefinition)
                .ToArray();
            var unitDtos = BuildUnits(unitCatalog);
            var unitIds = unitDtos.Select(unit => unit.StableId).ToArray();
            var variableDtos = BuildBlackboardVariables(module, blackboards);

            return new GraphAuthoringCatalog(
                module,
                Array.AsReadOnly(definitionDtos),
                Array.AsReadOnly(unitDtos),
                Array.AsReadOnly(unitIds),
                Array.AsReadOnly(variableDtos));
        }

        static GraphAuthoringUnitDefinition[] BuildUnits(UnitAuthoringCatalog unitCatalog) =>
            unitCatalog.Entries
                .Select(entry => BuildUnit(entry, unitCatalog))
                .ToArray();

        static GraphAuthoringUnitDefinition BuildUnit(
            UnitAuthoringCatalogEntry entry,
            UnitAuthoringCatalog unitCatalog)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            var fields = GraphAuthoringUnitReflection.SerializableFields(
                entry.UnitType,
                $"units.{entry.StableId}.fields",
                diagnostics);
            if (diagnostics.Count != 0)
                throw new ArgumentException($"{diagnostics[0].code}: {diagnostics[0].message}", nameof(unitCatalog));

            var fieldDtos = fields
                .Select(field => BuildUnitField(field, unitCatalog))
                .ToArray();
            return new GraphAuthoringUnitDefinition(
                entry.StableId,
                UnitRole(entry.UnitType),
                Array.AsReadOnly(fieldDtos));
        }

        static GraphAuthoringUnitFieldDefinition BuildUnitField(
            FieldInfo field,
            UnitAuthoringCatalog unitCatalog)
        {
            if (typeof(Unit).IsAssignableFrom(field.FieldType))
                return BuildNestedUnitField(field, field.FieldType, GraphAuthoringUnitFieldKind.Unit, "unit", unitCatalog);
            if (GraphAuthoringUnitReflection.TryGetUnitElementType(field.FieldType, out var elementType))
                return BuildNestedUnitField(field, elementType, GraphAuthoringUnitFieldKind.UnitList, "units", unitCatalog);
            if (!GraphAuthoringUnitReflection.IsScalar(field.FieldType))
                throw new ArgumentException(
                    $"Unit 字段 '{field.DeclaringType?.FullName}.{field.Name}' 不属于稳定 scalar/Unit/UnitList 词汇。",
                    nameof(unitCatalog));

            var enumValues = field.FieldType.IsEnum
                ? Enum.GetNames(field.FieldType).OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            return new GraphAuthoringUnitFieldDefinition(
                field.Name,
                GraphAuthoringUnitFieldKind.Scalar,
                StableScalarType(field.FieldType),
                Array.AsReadOnly(enumValues),
                null,
                null,
                field.FieldType == typeof(string),
                "value");
        }

        static GraphAuthoringUnitFieldDefinition BuildNestedUnitField(
            FieldInfo field,
            Type expectedType,
            GraphAuthoringUnitFieldKind kind,
            string payload,
            UnitAuthoringCatalog unitCatalog)
        {
            unitCatalog.TryGetStableId(expectedType, out var expectedTypeId);
            return new GraphAuthoringUnitFieldDefinition(
                field.Name,
                kind,
                null,
                Array.AsReadOnly(Array.Empty<string>()),
                UnitFamily(expectedType),
                expectedTypeId,
                true,
                payload);
        }

        static NodeRole UnitRole(Type unitType)
        {
            if (typeof(ActionUnit).IsAssignableFrom(unitType)) return NodeRole.Action;
            if (typeof(ConditionUnit).IsAssignableFrom(unitType)) return NodeRole.Condition;
            if (typeof(ProviderUnit).IsAssignableFrom(unitType)) return NodeRole.Provider;
            if (typeof(ControlUnit).IsAssignableFrom(unitType)) return NodeRole.Control;
            throw new ArgumentException($"Unit '{unitType.FullName}' 不属于四个稳定角色族。", nameof(unitType));
        }

        static string UnitFamily(Type unitType)
        {
            if (typeof(ActionUnit).IsAssignableFrom(unitType)) return "Action";
            if (typeof(ConditionUnit).IsAssignableFrom(unitType)) return "Condition";
            if (typeof(ProviderUnit).IsAssignableFrom(unitType)) return "Provider";
            if (typeof(ControlUnit).IsAssignableFrom(unitType)) return "Control";
            return typeof(Unit).IsAssignableFrom(unitType) ? "Any" : null;
        }

        static string StableScalarType(Type type)
        {
            if (type.IsEnum) return "enum";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(char)) return "char";
            if (type == typeof(byte)) return "uint8";
            if (type == typeof(sbyte)) return "int8";
            if (type == typeof(short)) return "int16";
            if (type == typeof(ushort)) return "uint16";
            if (type == typeof(int)) return "int32";
            if (type == typeof(uint)) return "uint32";
            if (type == typeof(long)) return "int64";
            if (type == typeof(ulong)) return "uint64";
            if (type == typeof(float)) return "float32";
            if (type == typeof(double)) return "float64";
            if (type == typeof(decimal)) return "decimal";
            throw new ArgumentException($"'{type.FullName}' 不是稳定 scalar 类型。", nameof(type));
        }

        static GraphAuthoringDefinition BuildDefinition(NodeSchema schema)
        {
            var inputs = BuildPorts(schema.id, "输入", schema.inputPorts);
            var outputs = BuildPorts(schema.id, "输出", schema.outputPorts);
            var parameters = BuildParameters(schema.id, schema.parameters);
            return new GraphAuthoringDefinition(
                schema.id,
                schema.displayName,
                schema.module,
                schema.kind,
                schema.role,
                schema.runtimeKind,
                schema.version,
                Array.AsReadOnly(inputs),
                Array.AsReadOnly(outputs),
                Array.AsReadOnly(parameters));
        }

        static GraphAuthoringPort[] BuildPorts(string definitionId, string direction, IEnumerable<PortDef> ports)
        {
            var items = (ports ?? Enumerable.Empty<PortDef>()).ToArray();
            if (items.Any(port => port == null))
                throw new ArgumentException($"节点 '{definitionId}' 的{direction}端口包含 null。");
            EnsureStableIds(items.Select(port => port.name), $"节点 '{definitionId}' 的{direction}端口");
            return items
                .OrderBy(port => port.name, StringComparer.Ordinal)
                .Select(port => new GraphAuthoringPort(port.name, CopyType(port.type), port.arity))
                .ToArray();
        }

        static GraphAuthoringParameter[] BuildParameters(string definitionId, IEnumerable<ParamDef> parameters)
        {
            var items = (parameters ?? Enumerable.Empty<ParamDef>()).ToArray();
            if (items.Any(parameter => parameter == null))
                throw new ArgumentException($"节点 '{definitionId}' 的参数包含 null。");
            EnsureStableIds(items.Select(parameter => parameter.name), $"节点 '{definitionId}' 的参数");
            return items
                .OrderBy(parameter => parameter.name, StringComparer.Ordinal)
                .Select(parameter => new GraphAuthoringParameter(parameter))
                .ToArray();
        }

        static GraphAuthoringBlackboardVariable[] BuildBlackboardVariables(
            string catalogModule,
            IEnumerable<IBlackboardDecl> blackboards)
        {
            var layers = blackboards.ToArray();
            if (layers.Any(layer => layer == null))
                throw new ArgumentException("黑板声明列表不能包含 null。", nameof(blackboards));

            var selected = layers
                .Where(layer => IsGlobal(layer) ||
                                string.Equals(layer.Module ?? string.Empty, catalogModule, StringComparison.Ordinal))
                .ToArray();
            var layerIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<GraphAuthoringBlackboardVariable>();
            foreach (var layer in selected)
            {
                string layerModule = layer.Module ?? string.Empty;
                string layerGroup = layer.Group ?? string.Empty;
                if (layerModule.Length == 0 && layerGroup.Length != 0)
                    throw new ArgumentException($"黑板组 '{layerGroup}' 缺少模块 id。", nameof(blackboards));
                if (!layerIds.Add(layerModule + "\0" + layerGroup))
                    throw new ArgumentException($"黑板作用域 '{layerModule}/{layerGroup}' 重复。", nameof(blackboards));

                var variables = (layer.Variables ?? Array.Empty<VariableDef>()).ToArray();
                if (variables.Any(variable => variable == null))
                    throw new ArgumentException($"黑板作用域 '{layerModule}/{layerGroup}' 包含 null 变量。", nameof(blackboards));
                EnsureStableIds(variables.Select(variable => variable.key), $"黑板作用域 '{layerModule}/{layerGroup}' 的变量");

                var scope = layerGroup.Length != 0
                    ? GraphAuthoringBlackboardScope.Group
                    : layerModule.Length != 0
                        ? GraphAuthoringBlackboardScope.Module
                        : GraphAuthoringBlackboardScope.Global;
                result.AddRange(variables.Select(variable => new GraphAuthoringBlackboardVariable(
                    scope,
                    layerModule,
                    layerGroup,
                    variable.key,
                    CopyType(variable.type),
                    variable.defaultJson)));
            }

            return result
                .OrderBy(variable => variable.Scope)
                .ThenBy(variable => variable.Module, StringComparer.Ordinal)
                .ThenBy(variable => variable.Group, StringComparer.Ordinal)
                .ThenBy(variable => variable.Key, StringComparer.Ordinal)
                .ToArray();
        }

        static bool IsGlobal(IBlackboardDecl layer) =>
            string.IsNullOrEmpty(layer.Module) && string.IsNullOrEmpty(layer.Group);

        internal static GraphAuthoringType CopyType(TypeRef type)
        {
            if (type == null) return null;
            return new GraphAuthoringType(
                type.kind,
                type.kind == TypeKind.Primitive ? type.primitive : (PrimitiveType?)null,
                type.enumOrObjectName,
                CopyType(type.element));
        }

        static void EnsureStableIds(IEnumerable<string> ids, string owner)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
                    throw new ArgumentException($"{owner}包含无效稳定 id。", owner);
                if (!seen.Add(id))
                    throw new ArgumentException($"{owner}的稳定 id '{id}' 重复。", owner);
            }
        }
    }
}
