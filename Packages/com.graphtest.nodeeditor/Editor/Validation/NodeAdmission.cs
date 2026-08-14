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

        // 准入作用域：节点若被接纳，会落进哪个模块。有图时就是图的 module；没有图时（模块模式外壳还没
        // 建出第一张图）是外壳锁定的模块。领域规则一律读它，不要只看 graph —— 只看 graph 会在
        // graph==null 时放行全部模块的节点（"状态机编辑器右键弹出对话节点"就是这么来的）。
        public readonly string moduleScope;

        public NodeAvailabilityContext(NodeGraphAsset graph, NodeDefinition definition)
            : this(graph, graph != null ? graph.module : null, definition) { }

        public NodeAvailabilityContext(NodeGraphAsset graph, string moduleScope, NodeDefinition definition)
        {
            this.graph = graph;
            this.definition = definition;
            this.moduleScope = graph != null ? graph.module : moduleScope;
        }

        // 作用域是否已确定。未确定（自由模式且没打开图）时不做模块裁剪。
        public bool IsScoped => graph != null || !string.IsNullOrEmpty(moduleScope);
    }

    public static class NodeAdmission
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
            => Evaluate(graph, null, definition);

        // moduleScope：模块模式外壳锁定的模块（自由模式传 null）。graph 非空时以图的 module 为准，
        // graph 为空时才用它 —— 这样"编辑器已锁到某模块、但还没有图"也照样只接纳本模块的节点。
        public static NodeAvailabilityVerdict Evaluate(NodeGraphAsset graph, string moduleScope, NodeDefinition definition)
        {
            if (definition == null)
                return NodeAvailabilityVerdict.Deny(
                    Localizer.UI("val.definitionMissing", "The node definition could not be resolved."));
            var context = new NodeAvailabilityContext(graph, moduleScope, definition);
            if (context.IsScoped
                && !string.IsNullOrEmpty(definition.Module)
                && !string.Equals(context.moduleScope, definition.Module, StringComparison.Ordinal))
                return NodeAvailabilityVerdict.Deny(
                    Localizer.UI("val.definitionWrongModule", "This node belongs to another graph module."));
            foreach (var entry in s_Rules)
            {
                var verdict = entry.rule(context);
                if (!verdict.allowed) return verdict;
            }
            return NodeAvailabilityVerdict.Allow;
        }
    }
}
