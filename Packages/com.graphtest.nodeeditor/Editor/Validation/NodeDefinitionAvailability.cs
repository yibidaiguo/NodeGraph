using System;
using System.Collections.Generic;
using UnityEngine;
using NodeEditor.EditorUI;

namespace NodeEditor
{
    public readonly struct NodeAvailabilityVerdict
    {
        public readonly bool allowed;
        public readonly string reason;
        NodeAvailabilityVerdict(bool allowed, string reason) { this.allowed = allowed; this.reason = reason; }
        public static NodeAvailabilityVerdict Allow => new(true, null);
        public static NodeAvailabilityVerdict Deny(string reason) => new(false, reason);
    }

    public readonly struct NodeAvailabilityContext
    {
        public readonly NodeGraphAsset graph;
        public readonly NodeDefinition definition;
        public NodeAvailabilityContext(NodeGraphAsset graph, NodeDefinition definition)
        { this.graph = graph; this.definition = definition; }
    }

    public static class NodeDefinitionAvailability
    {
        static readonly List<(string id, Func<NodeAvailabilityContext, NodeAvailabilityVerdict> rule)> s_Rules = new();

        public static void Register(string id, Func<NodeAvailabilityContext, NodeAvailabilityVerdict> rule)
        {
            if (string.IsNullOrEmpty(id) || rule == null) return;
            int at = s_Rules.FindIndex(entry => entry.id == id);
            if (at >= 0)
            {
                Debug.LogWarning($"NodeEditor: definition availability rule '{id}' already registered; overwriting.");
                s_Rules[at] = (id, rule);
                return;
            }
            s_Rules.Add((id, rule));
        }

        public static void Unregister(string id)
        {
            int at = s_Rules.FindIndex(entry => entry.id == id);
            if (at >= 0) s_Rules.RemoveAt(at);
        }

        public static NodeAvailabilityVerdict Evaluate(NodeGraphAsset graph, NodeDefinition definition)
        {
            if (definition == null)
                return NodeAvailabilityVerdict.Deny(
                    Localizer.UI("val.definitionMissing", "The node definition could not be resolved."));
            var context = new NodeAvailabilityContext(graph, definition);
            foreach (var entry in s_Rules)
            {
                var verdict = entry.rule(context);
                if (!verdict.allowed) return verdict;
            }
            return NodeAvailabilityVerdict.Allow;
        }
    }
}
