// GraphAuthoringUnitCodec.cs —— Unit 树的内部反射编解码与严格 shape 校验。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace NodeEditor
{
    internal static class GraphAuthoringUnitCodec
    {
        internal static void EncodeSlots(
            List<UnitOverride> source,
            List<GraphAuthoringUnitSlot> target,
            IUnitAuthoringCatalog catalog,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null) { Add(diagnostics, "authoring.collection.missing", path, "Unit 槽集合不能为空。"); return; }
            for (int i = 0; i < source.Count; i++)
            {
                var slot = source[i];
                if (slot == null) { Add(diagnostics, "authoring.unit-slot.missing", $"{path}[{i}]", "Unit 槽不能为空。"); continue; }
                target.Add(new GraphAuthoringUnitSlot
                {
                    paramName = slot.paramName,
                    unit = Encode(slot.value, catalog, $"{path}[{i}].unit", diagnostics,
                        new HashSet<Unit>(ReferenceComparer<Unit>.Instance))
                });
            }
        }

        internal static void DecodeSlots(
            List<GraphAuthoringUnitSlot> source,
            List<UnitOverride> target,
            IUnitAuthoringCatalog catalog,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null) { Add(diagnostics, "authoring.collection.missing", path, "Unit 槽集合不能为空。"); return; }
            for (int i = 0; i < source.Count; i++)
            {
                var slot = source[i];
                if (slot == null) { Add(diagnostics, "authoring.unit-slot.missing", $"{path}[{i}]", "Unit 槽不能为空。"); continue; }
                target.Add(new UnitOverride
                {
                    paramName = slot.paramName,
                    value = Decode(slot.unit, typeof(Unit), catalog, $"{path}[{i}].unit", diagnostics,
                        new HashSet<GraphAuthoringUnit>(ReferenceComparer<GraphAuthoringUnit>.Instance))
                });
            }
        }

        static GraphAuthoringUnit Encode(
            Unit source,
            IUnitAuthoringCatalog catalog,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics,
            HashSet<Unit> active)
        {
            if (source == null) return null;
            if (catalog == null || !catalog.TryGetStableId(source.GetType(), out var typeId))
            {
                Add(diagnostics, "authoring.unit.unknown-type", path + ".typeId", $"Unit 类型 '{source.GetType().FullName}' 未注册稳定 id。");
                return null;
            }
            if (!active.Add(source))
            {
                Add(diagnostics, "authoring.unit.cycle", path, "Unit 图包含循环引用；创作模型只接受树。");
                return null;
            }

            var result = new GraphAuthoringUnit { typeId = typeId };
            foreach (var field in GraphAuthoringUnitReflection.SerializableFields(source.GetType(), path, diagnostics))
            {
                var fieldPath = path + ".fields." + field.Name;
                var value = field.GetValue(source);
                var encoded = new GraphAuthoringUnitField { name = field.Name };
                if (typeof(Unit).IsAssignableFrom(field.FieldType))
                {
                    encoded.kind = GraphAuthoringUnitFieldKind.Unit;
                    encoded.isNull = value == null;
                    encoded.unit = Encode(value as Unit, catalog, fieldPath, diagnostics, active);
                }
                else if (GraphAuthoringUnitReflection.TryGetUnitElementType(field.FieldType, out _))
                {
                    encoded.kind = GraphAuthoringUnitFieldKind.UnitList;
                    encoded.isNull = value == null;
                    if (value != null)
                    {
                        encoded.units = new List<GraphAuthoringUnit>();
                        foreach (var item in (IEnumerable)value)
                            encoded.units.Add(Encode(item as Unit, catalog, fieldPath, diagnostics, active));
                    }
                }
                else if (GraphAuthoringUnitReflection.IsScalar(field.FieldType))
                {
                    encoded.kind = GraphAuthoringUnitFieldKind.Scalar;
                    encoded.isNull = value == null;
                    encoded.value = EncodeScalar(value, field.FieldType);
                }
                else
                {
                    Add(diagnostics, "authoring.unit.unsupported-field", fieldPath,
                        $"字段类型 '{field.FieldType.FullName}' 不属于稳定 scalar/Unit/UnitList 词汇。");
                    continue;
                }
                result.fields.Add(encoded);
            }
            active.Remove(source);
            return result;
        }

        static Unit Decode(
            GraphAuthoringUnit source,
            Type expectedType,
            IUnitAuthoringCatalog catalog,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics,
            HashSet<GraphAuthoringUnit> active)
        {
            if (source == null) return null;
            if (!active.Add(source))
            {
                Add(diagnostics, "authoring.unit.cycle", path, "Unit 文档包含循环引用；创作模型只接受树。");
                return null;
            }
            if (catalog == null || !catalog.TryResolve(source.typeId, out var type) ||
                type == null || type.IsAbstract || !typeof(Unit).IsAssignableFrom(type))
            {
                Add(diagnostics, "authoring.unit.unknown-type", path + ".typeId", $"未知 Unit 类型 id '{source.typeId}'。");
                active.Remove(source);
                return null;
            }
            if (!expectedType.IsAssignableFrom(type))
            {
                Add(diagnostics, "authoring.unit.not-assignable", path + ".typeId",
                    $"Unit '{source.typeId}' 不能赋给 '{expectedType.FullName}'。");
                active.Remove(source);
                return null;
            }
            if (source.fields == null)
            {
                Add(diagnostics, "authoring.collection.missing", path + ".fields", "Unit 字段集合不能为空。");
                active.Remove(source);
                return null;
            }

            Unit result;
            try { result = (Unit)Activator.CreateInstance(type, true); }
            catch (Exception ex)
            {
                Add(diagnostics, "authoring.unit.construct-failed", path, $"无法构建 Unit '{source.typeId}': {ex.GetType().Name}。");
                active.Remove(source);
                return null;
            }

            var serializableFields = GraphAuthoringUnitReflection.SerializableFields(type, path, diagnostics);
            var fieldsByName = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (var field in serializableFields)
                if (!fieldsByName.ContainsKey(field.Name)) fieldsByName.Add(field.Name, field);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.fields.Count; i++)
            {
                var fieldSource = source.fields[i];
                var fieldPath = path + $".fields[{i}]";
                if (fieldSource == null)
                {
                    Add(diagnostics, "authoring.unit.field.missing", fieldPath, "Unit 字段不能为空。");
                    continue;
                }
                if (!seen.Add(fieldSource.name ?? ""))
                {
                    Add(diagnostics, "authoring.unit.field.duplicate", fieldPath + ".name", "Unit 字段名不能重复。");
                    continue;
                }
                if (!fieldsByName.TryGetValue(fieldSource.name ?? "", out var field))
                {
                    Add(diagnostics, "authoring.unit.unknown-field", fieldPath + ".name",
                        $"Unit '{source.typeId}' 没有字段 '{fieldSource.name}'。");
                    continue;
                }
                DecodeField(result, field, fieldSource, catalog, fieldPath, diagnostics, active);
            }
            foreach (var field in serializableFields)
                if (!seen.Contains(field.Name))
                    Add(diagnostics, "authoring.unit.missing-field", path + ".fields",
                        $"Unit '{source.typeId}' 缺少字段 '{field.Name}'。");

            active.Remove(source);
            return result;
        }

        static void DecodeField(
            Unit owner,
            FieldInfo field,
            GraphAuthoringUnitField source,
            IUnitAuthoringCatalog catalog,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics,
            HashSet<GraphAuthoringUnit> active)
        {
            if (!ValidateShape(source, field.FieldType, path, diagnostics)) return;

            object decoded = null;
            if (typeof(Unit).IsAssignableFrom(field.FieldType))
            {
                decoded = Decode(source.unit, field.FieldType, catalog, path + ".unit", diagnostics, active);
            }
            else if (GraphAuthoringUnitReflection.TryGetUnitElementType(field.FieldType, out var elementType))
            {
                if (!source.isNull)
                {
                    var items = new List<Unit>();
                    for (int i = 0; i < source.units.Count; i++)
                        items.Add(Decode(source.units[i], elementType, catalog, path + $".units[{i}]", diagnostics, active));
                    decoded = CreateUnitCollection(field.FieldType, elementType, items, path, diagnostics);
                }
            }
            else if (!source.isNull && !TryDecodeScalar(source.value, field.FieldType, out decoded))
            {
                Add(diagnostics, "authoring.unit.invalid-scalar", path + ".value",
                    $"'{source.value}' 不是有效的 {field.FieldType.Name} 值。");
                return;
            }

            try { field.SetValue(owner, decoded); }
            catch (Exception ex)
            {
                Add(diagnostics, "authoring.unit.field-write-failed", path,
                    $"无法写入字段 '{field.Name}': {ex.GetType().Name}。");
            }
        }

        static bool ValidateShape(
            GraphAuthoringUnitField source,
            Type fieldType,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var valid = true;
            if (!Enum.IsDefined(typeof(GraphAuthoringUnitFieldKind), source.kind))
            {
                Add(diagnostics, "authoring.unit.field-kind", path + ".kind", "未知 Unit 字段 kind。");
                return false;
            }

            var expected = typeof(Unit).IsAssignableFrom(fieldType)
                ? GraphAuthoringUnitFieldKind.Unit
                : GraphAuthoringUnitReflection.TryGetUnitElementType(fieldType, out _)
                    ? GraphAuthoringUnitFieldKind.UnitList
                    : GraphAuthoringUnitFieldKind.Scalar;
            if (source.kind != expected)
            {
                Add(diagnostics, "authoring.unit.field-kind", path + ".kind", "Unit 字段 kind 与声明类型不匹配。");
                valid = false;
            }

            switch (source.kind)
            {
                case GraphAuthoringUnitFieldKind.Scalar:
                    if (source.unit != null || source.units != null)
                    {
                        Add(diagnostics, "authoring.unit.field-shape", path, "Scalar 字段不得携带 unit/units。");
                        valid = false;
                    }
                    if (source.isNull)
                    {
                        if (source.value != null)
                        {
                            Add(diagnostics, "authoring.unit.field-shape", path + ".value", "null Scalar 的 value 必须为 null。");
                            valid = false;
                        }
                        if (fieldType != typeof(string))
                        {
                            Add(diagnostics, "authoring.unit.field-shape", path, "仅 string Scalar 可为 null。");
                            valid = false;
                        }
                    }
                    else if (source.value == null)
                    {
                        Add(diagnostics, "authoring.unit.field-shape", path + ".value", "非 null Scalar 必须携带 value。");
                        valid = false;
                    }
                    break;

                case GraphAuthoringUnitFieldKind.Unit:
                    if (source.value != null || source.units != null)
                    {
                        Add(diagnostics, "authoring.unit.field-shape", path, "Unit 字段不得携带 value/units。");
                        valid = false;
                    }
                    if (source.isNull != (source.unit == null))
                    {
                        Add(diagnostics, "authoring.unit.field-shape", path + ".unit", "Unit 的 isNull 必须与 unit 是否存在一致。");
                        valid = false;
                    }
                    break;

                case GraphAuthoringUnitFieldKind.UnitList:
                    if (source.value != null || source.unit != null)
                    {
                        Add(diagnostics, "authoring.unit.field-shape", path, "UnitList 字段不得携带 value/unit。");
                        valid = false;
                    }
                    if (source.isNull != (source.units == null))
                    {
                        Add(diagnostics, "authoring.unit.field-shape", path + ".units", "UnitList 的 isNull 必须与 units 是否存在一致。");
                        valid = false;
                    }
                    break;
            }
            return valid;
        }

        static object CreateUnitCollection(
            Type collectionType,
            Type elementType,
            List<Unit> items,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (collectionType.IsArray)
            {
                var array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
                return array;
            }
            try
            {
                var list = (IList)Activator.CreateInstance(collectionType, true);
                foreach (var item in items) list.Add(item);
                return list;
            }
            catch (Exception ex)
            {
                Add(diagnostics, "authoring.unit.collection-construct-failed", path,
                    $"无法构建 Unit 列表 '{collectionType.FullName}': {ex.GetType().Name}。");
                return null;
            }
        }

        static string EncodeScalar(object value, Type type)
        {
            if (value == null) return null;
            if (type == typeof(bool)) return (bool)value ? "true" : "false";
            if (type == typeof(float)) return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            if (type == typeof(double)) return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            if (type == typeof(decimal)) return ((decimal)value).ToString(CultureInfo.InvariantCulture);
            return type.IsEnum ? Enum.GetName(type, value) : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        static bool TryDecodeScalar(string value, Type type, out object decoded)
        {
            decoded = null;
            if (type == typeof(string)) { decoded = value; return true; }
            if (type == typeof(char)) { if (value?.Length == 1) { decoded = value[0]; return true; } return false; }
            if (type.IsEnum)
            {
                if (string.IsNullOrEmpty(value) || !Enum.TryParse(type, value, false, out decoded) || !Enum.IsDefined(type, decoded))
                { decoded = null; return false; }
                return true;
            }
            if (type == typeof(bool)) { if (bool.TryParse(value, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(byte)) { if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(sbyte)) { if (sbyte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(short)) { if (short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(ushort)) { if (ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(int)) { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(uint)) { if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(long)) { if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(ulong)) { if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(float)) { if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(double)) { if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            if (type == typeof(decimal)) { if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var x)) { decoded = x; return true; } return false; }
            return false;
        }

        static void Add(List<GraphAuthoringDiagnostic> diagnostics, string code, string path, string message) =>
            diagnostics.Add(new GraphAuthoringDiagnostic(code, path, message));

        sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    // 编解码与能力目录共用这一套反射规则；任何字段可见性变化只允许在此处发生。
    internal static class GraphAuthoringUnitReflection
    {
        internal static List<FieldInfo> SerializableFields(
            Type type,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var result = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(Unit); current = current.BaseType)
            {
                result.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(field => !field.IsStatic && !field.IsLiteral && !field.IsInitOnly &&
                                    !field.IsDefined(typeof(NonSerializedAttribute), false) &&
                                    (field.IsPublic || HasSerializationAttribute(field))));
            }
            result = result.OrderBy(field => field.Name, StringComparer.Ordinal).ToList();
            if (result.GroupBy(field => field.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
                diagnostics.Add(new GraphAuthoringDiagnostic(
                    "authoring.unit.field-name-collision",
                    path,
                    $"Unit '{type.FullName}' 含有被隐藏的同名序列化字段，无法稳定寻址。"));
            return result;
        }

        internal static bool TryGetUnitElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (type.IsArray && typeof(Unit).IsAssignableFrom(type.GetElementType()))
            {
                elementType = type.GetElementType();
                return true;
            }
            if (!type.IsGenericType || !typeof(IList).IsAssignableFrom(type)) return false;
            var arguments = type.GetGenericArguments();
            if (arguments.Length != 1 || !typeof(Unit).IsAssignableFrom(arguments[0])) return false;
            elementType = arguments[0];
            return true;
        }

        internal static bool IsScalar(Type type) =>
            type == typeof(string) || type == typeof(bool) || type == typeof(char) || type.IsEnum ||
            type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal);

        static bool HasSerializationAttribute(FieldInfo field) =>
            field.GetCustomAttributes(false).Any(attribute =>
            {
                var name = attribute.GetType().FullName;
                return name == "UnityEngine.SerializeField" || name == "UnityEngine.SerializeReference";
            });
    }
}
