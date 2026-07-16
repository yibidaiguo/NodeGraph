using System;

namespace NodeEditor
{
    // Compatibility adapter for integrations built against v0.0.4 and earlier.
    public static class NodeDefinitionAvailability
    {
        public static void Register(string id, Func<NodeAvailabilityContext, NodeAvailabilityVerdict> rule)
            => NodeAdmission.Register(id, rule);

        public static void Unregister(string id)
            => NodeAdmission.Unregister(id);

        public static NodeAvailabilityVerdict Evaluate(NodeGraphAsset graph, NodeDefinition definition)
            => NodeAdmission.Evaluate(graph, definition);
    }
}
