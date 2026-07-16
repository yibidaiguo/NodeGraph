using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public readonly struct NodeCatalogEntry
    {
        public NodeCatalogEntry(Type definitionType, NodeDefinition definition, string displayPath)
        {
            DefinitionType = definitionType;
            Definition = definition;
            DisplayPath = displayPath;
        }

        public Type DefinitionType { get; }
        public NodeDefinition Definition { get; }
        public string DisplayPath { get; }
    }

    public static class NodeCatalog
    {
        public static IReadOnlyList<NodeCatalogEntry> Query(NodeGraphAsset graph)
        {
            var entries = new List<NodeCatalogEntry>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<NodeDefinition>())
            {
                if (type.IsAbstract) continue;
                var menu = type.GetCustomAttribute<NodeMenuAttribute>();
                if (menu == null) continue;

                var definition = NodeDefinitionLocator.ForType(type);
                if (!NodeAdmission.Evaluate(graph, definition).allowed) continue;

                var parts = menu.Path.Split('/');
                var leafLabel = Localizer.NodeName(definition);
                var displayPath = parts.Length > 1
                    ? string.Join("/", parts.Take(parts.Length - 1)) + "/" + leafLabel
                    : leafLabel;
                entries.Add(new NodeCatalogEntry(type, definition, displayPath));
            }

            return entries.OrderBy(entry => entry.DisplayPath, StringComparer.Ordinal).ToArray();
        }
    }
}
