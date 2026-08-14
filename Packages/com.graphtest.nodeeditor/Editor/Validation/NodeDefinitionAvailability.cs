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

        // 带作用域的重载：图还没建出来时，模块裁剪只能靠外壳锁定的模块。
        public static NodeAvailabilityVerdict Evaluate(NodeGraphAsset graph, string moduleScope, NodeDefinition definition)
            => NodeAdmission.Evaluate(graph, moduleScope, definition);

        public static NodeAvailabilityVerdict Evaluate(NodeGraphAsset graph, NodeDefinition definition)
            => NodeAdmission.Evaluate(graph, definition);
    }
}
