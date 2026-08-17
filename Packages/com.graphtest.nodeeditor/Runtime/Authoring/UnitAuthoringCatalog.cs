// UnitAuthoringCatalog.cs —— Unit 稳定 id 与 CLR 类型之间的显式边界。
// 输出只使用稳定 id；FullName/Name 仅用于读取旧文档，绝不回写。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NodeEditor
{
    public sealed class UnitAuthoringCatalogEntry
    {
        public UnitAuthoringCatalogEntry(string stableId, Type unitType)
        {
            StableId = stableId;
            UnitType = unitType;
        }

        public string StableId { get; }
        public Type UnitType { get; }
    }

    public interface IUnitAuthoringCatalog
    {
        bool TryGetStableId(Type unitType, out string stableId);
        bool TryResolve(string stableIdOrLegacyAlias, out Type unitType);
    }

    public sealed class UnitAuthoringCatalog : IUnitAuthoringCatalog
    {
        readonly Dictionary<Type, string> m_IdsByType = new();
        readonly Dictionary<string, Type> m_TypesById = new(StringComparer.Ordinal);
        readonly Dictionary<string, Type> m_TypesByAlias = new(StringComparer.Ordinal);
        readonly HashSet<string> m_AmbiguousAliases = new(StringComparer.Ordinal);
        readonly IReadOnlyList<UnitAuthoringCatalogEntry> m_Entries;

        public static UnitAuthoringCatalog Empty { get; } = new UnitAuthoringCatalog(Array.Empty<Type>());

        public UnitAuthoringCatalog(IEnumerable<Type> unitTypes)
        {
            if (unitTypes == null) throw new ArgumentNullException(nameof(unitTypes));

            foreach (var type in unitTypes.Where(t => t != null).Distinct().OrderBy(t => t.FullName, StringComparer.Ordinal))
                Register(type);

            m_Entries = Array.AsReadOnly(m_TypesById
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new UnitAuthoringCatalogEntry(pair.Key, pair.Value))
                .ToArray());
        }

        public UnitAuthoringCatalog(params Type[] unitTypes) : this((IEnumerable<Type>)unitTypes) { }

        public static UnitAuthoringCatalog FromAssemblies(params Assembly[] assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            var types = new List<Type>();
            foreach (var assembly in assemblies.Where(a => a != null).Distinct())
            {
                try
                {
                    types.AddRange(assembly.GetTypes().Where(t =>
                        !t.IsAbstract && typeof(Unit).IsAssignableFrom(t) &&
                        t.GetCustomAttribute<UnitAuthoringIdAttribute>(false) != null));
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types.AddRange(ex.Types.Where(t =>
                        t != null && !t.IsAbstract && typeof(Unit).IsAssignableFrom(t) &&
                        t.GetCustomAttribute<UnitAuthoringIdAttribute>(false) != null));
                }
            }
            return new UnitAuthoringCatalog(types);
        }

        public bool TryGetStableId(Type unitType, out string stableId)
        {
            stableId = null;
            return unitType != null && m_IdsByType.TryGetValue(unitType, out stableId);
        }

        public bool TryResolve(string stableIdOrLegacyAlias, out Type unitType)
        {
            unitType = null;
            if (string.IsNullOrEmpty(stableIdOrLegacyAlias)) return false;
            if (m_TypesById.TryGetValue(stableIdOrLegacyAlias, out unitType)) return true;
            return !m_AmbiguousAliases.Contains(stableIdOrLegacyAlias) &&
                   m_TypesByAlias.TryGetValue(stableIdOrLegacyAlias, out unitType);
        }

        // 只读稳定快照，供目录导出使用；不会暴露内部字典。
        public IReadOnlyList<UnitAuthoringCatalogEntry> Entries => m_Entries;

        void Register(Type type)
        {
            if (type.IsAbstract || !typeof(Unit).IsAssignableFrom(type))
                throw new ArgumentException($"'{type.FullName}' 不是可实例化的 Unit 类型。", nameof(type));

            var attribute = type.GetCustomAttribute<UnitAuthoringIdAttribute>(false);
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id) || attribute.Id != attribute.Id.Trim())
                throw new ArgumentException($"Unit '{type.FullName}' 缺少有效的 UnitAuthoringIdAttribute。", nameof(type));
            if (m_TypesById.TryGetValue(attribute.Id, out var existing) && existing != type)
                throw new ArgumentException($"Unit authoring id '{attribute.Id}' 被重复注册。", nameof(type));

            m_IdsByType.Add(type, attribute.Id);
            m_TypesById.Add(attribute.Id, type);
            AddAlias(type.FullName, type);
            AddAlias(type.Name, type);
        }

        void AddAlias(string alias, Type type)
        {
            if (string.IsNullOrEmpty(alias) || m_TypesById.ContainsKey(alias)) return;
            if (m_TypesByAlias.TryGetValue(alias, out var existing) && existing != type)
            {
                m_TypesByAlias.Remove(alias);
                m_AmbiguousAliases.Add(alias);
                return;
            }
            if (!m_AmbiguousAliases.Contains(alias)) m_TypesByAlias[alias] = type;
        }
    }
}
