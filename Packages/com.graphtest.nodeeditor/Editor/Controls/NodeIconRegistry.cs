using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    // 只读发现表：领域经特性声明映射，框架统一解析并提供角色回退。
    public static class NodeIconRegistry
    {
        static Dictionary<Type, NodeIconKind> s_Map;

        public static NodeIconKind Resolve(Type nodeType, NodeRole fallbackRole)
        {
            EnsureBuilt();
            return nodeType != null && s_Map.TryGetValue(nodeType, out var kind)
                ? kind
                : Fallback(fallbackRole);
        }

        static void EnsureBuilt()
        {
            if (s_Map != null) return;
            s_Map = new Dictionary<Type, NodeIconKind>();
            foreach (var carrier in TypeCache.GetTypesWithAttribute<NodeIconAttribute>()
                         .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal))
            {
                foreach (NodeIconAttribute attr in carrier.GetCustomAttributes(typeof(NodeIconAttribute), false))
                {
                    if (attr.NodeType == null) continue;
                    if (s_Map.TryGetValue(attr.NodeType, out var existing) && existing != attr.Kind)
                    {
                        Debug.LogWarning(string.Format(Localizer.UI("ui.nodeIconConflict",
                            "Node icon for '{0}' is already {1}; ignored conflicting {2}."),
                            attr.NodeType.FullName, existing, attr.Kind));
                        continue;
                    }
                    s_Map.TryAdd(attr.NodeType, attr.Kind);
                }
            }
        }

        static NodeIconKind Fallback(NodeRole role) => role switch
        {
            NodeRole.Condition => NodeIconKind.RoleCondition,
            NodeRole.Action => NodeIconKind.RoleAction,
            NodeRole.Control => NodeIconKind.RoleControl,
            _ => NodeIconKind.RoleProvider
        };
    }
}
