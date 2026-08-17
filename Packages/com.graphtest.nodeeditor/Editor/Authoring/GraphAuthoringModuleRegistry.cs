// GraphAuthoringModuleRegistry.cs —— AI 创作入口的模块级注册缝。Editor 程序集。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NodeEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public sealed class GraphAuthoringModuleDescriptor
    {
        public GraphAuthoringModuleDescriptor(
            string moduleId,
            Func<IReadOnlyList<string>> graphRoots,
            params Type[] unitTypes)
        {
            if (string.IsNullOrWhiteSpace(moduleId) || moduleId != moduleId.Trim())
                throw new ArgumentException("模块 id 必须是非空、无首尾空白的稳定值。", nameof(moduleId));
            ModuleId = moduleId;
            GraphRoots = graphRoots ?? throw new ArgumentNullException(nameof(graphRoots));
            UnitTypes = Array.AsReadOnly((unitTypes ?? Array.Empty<Type>()).ToArray());
        }

        public string ModuleId { get; }
        public Func<IReadOnlyList<string>> GraphRoots { get; }
        public IReadOnlyList<Type> UnitTypes { get; }
    }

    public static class GraphAuthoringModuleRegistry
    {
        static readonly Dictionary<string, GraphAuthoringModuleDescriptor> s_Modules =
            new(StringComparer.Ordinal);

        public static void Register(GraphAuthoringModuleDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (s_Modules.ContainsKey(descriptor.ModuleId))
                Debug.LogWarning($"NodeEditor: authoring module '{descriptor.ModuleId}' already registered; overwriting.");
            s_Modules[descriptor.ModuleId] = descriptor;
        }

        public static void Unregister(string moduleId)
        {
            if (moduleId != null) s_Modules.Remove(moduleId);
        }

        public static bool TryGet(string moduleId, out GraphAuthoringModuleDescriptor descriptor)
        {
            descriptor = null;
            return moduleId != null && s_Modules.TryGetValue(moduleId, out descriptor);
        }

        public static IReadOnlyList<GraphAuthoringModuleDescriptor> All() =>
            Array.AsReadOnly(s_Modules.Values
                .OrderBy(descriptor => descriptor.ModuleId, StringComparer.Ordinal)
                .ToArray());

        public static IReadOnlyList<string> ResolveGraphRoots(string moduleId)
        {
            if (!TryGet(moduleId, out var descriptor)) return Array.Empty<string>();
            var roots = descriptor.GraphRoots() ?? Array.Empty<string>();
            return Array.AsReadOnly(roots
                .Select(ProjectAssetPaths.NormalizeAssetPath)
                .Where(path => path.Length != 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray());
        }

        public static UnitAuthoringCatalog CreateUnitCatalog(string moduleId)
        {
            if (!TryGet(moduleId, out var descriptor))
                throw new InvalidOperationException($"创作模块 '{moduleId}' 未注册。");

            var coreTypes = ConcreteCoreUnitTypes();
            var moduleTypes = descriptor.UnitTypes.ToArray();
            foreach (var type in moduleTypes)
            {
                if (type == null || type.IsAbstract || !typeof(Unit).IsAssignableFrom(type))
                    throw new InvalidOperationException($"模块 '{moduleId}' 注册了无效 Unit 类型。");
                var attribute = type.GetCustomAttribute<UnitAuthoringIdAttribute>(false);
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id) || attribute.Id != attribute.Id.Trim())
                    throw new InvalidOperationException($"模块 '{moduleId}' 的 Unit '{type.FullName}' 缺少有效 UnitAuthoringIdAttribute。");
            }

            var allTypes = coreTypes.Concat(moduleTypes).ToArray();
            var duplicateId = allTypes
                .Select(type => new { Type = type, Id = type.GetCustomAttribute<UnitAuthoringIdAttribute>(false).Id })
                .GroupBy(entry => entry.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (duplicateId != null)
                throw new InvalidOperationException($"Unit authoring id '{duplicateId.Key}' 被重复注册。");

            return new UnitAuthoringCatalog(allTypes);
        }

        // 空 module 表示只使用框架通用能力；它不是一个需要领域注册的伪模块。
        public static UnitAuthoringCatalog CreateCoreUnitCatalog() =>
            new UnitAuthoringCatalog(ConcreteCoreUnitTypes());

        static Type[] ConcreteCoreUnitTypes() => typeof(Unit).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Unit).IsAssignableFrom(type) &&
                           type.GetCustomAttribute<UnitAuthoringIdAttribute>(false) != null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }
}
