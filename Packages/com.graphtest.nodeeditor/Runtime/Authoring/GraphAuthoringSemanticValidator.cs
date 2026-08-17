// GraphAuthoringSemanticValidator.cs —— AI 创作文档与能力目录之间的纯语义门禁。
//
// 结构形状与 Unit 树字段由 GraphAuthoringCodec 负责；这里仅解释 Catalog 声明，
// 不访问 Unity、资产、文件系统，也不修改输入文档。

using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    public static class GraphAuthoringSemanticValidator
    {
        const string GraphAssetTypeName = "NodeEditor.NodeGraphAsset";

        public static IReadOnlyList<GraphAuthoringDiagnostic> Validate(
            GraphAuthoringDocument document,
            GraphAuthoringCatalog catalog)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            if (document == null)
            {
                Add(diagnostics, "semantic.document.missing", "$", "创作文档不能为空。");
                return ReadOnly(diagnostics);
            }
            if (catalog == null)
            {
                Add(diagnostics, "semantic.catalog.missing", "$catalog", "创作能力目录不能为空。");
                return ReadOnly(diagnostics);
            }

            var definitions = IndexDefinitions(catalog.Definitions, diagnostics);
            var units = IndexUnits(catalog.Units, diagnostics);
            if (!string.Equals(document.module, catalog.Module, StringComparison.Ordinal))
                Add(diagnostics, "semantic.module.mismatch", "$.module",
                    $"文档模块 '{document.module}' 与目录模块 '{catalog.Module}' 不一致。");

            var nodesByKey = new Dictionary<string, GraphAuthoringNode>(StringComparer.Ordinal);
            var definitionsByNode = new Dictionary<GraphAuthoringNode, GraphAuthoringDefinition>(
                ReferenceComparer<GraphAuthoringNode>.Instance);
            if (document.nodes == null)
            {
                Add(diagnostics, "semantic.collection.missing", "$.nodes", "节点集合不能为空。");
            }
            else
            {
                for (int i = 0; i < document.nodes.Count; i++)
                    ValidateNode(document.nodes[i], i, definitions, units, nodesByKey, definitionsByNode, diagnostics);
            }

            ValidateEdges(document.edges, nodesByKey, definitionsByNode, diagnostics);
            return ReadOnly(diagnostics);
        }

        static Dictionary<string, GraphAuthoringDefinition> IndexDefinitions(
            IReadOnlyList<GraphAuthoringDefinition> source,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var result = new Dictionary<string, GraphAuthoringDefinition>(StringComparer.Ordinal);
            if (source == null)
            {
                Add(diagnostics, "semantic.catalog.definitions.missing", "$catalog.definitions",
                    "目录的节点定义集合不能为空。");
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                var definition = source[i];
                var path = $"$catalog.definitions[{i}]";
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                {
                    Add(diagnostics, "semantic.catalog.definition.invalid", path, "目录包含无效节点定义。");
                    continue;
                }
                if (!result.TryAdd(definition.Id, definition))
                    Add(diagnostics, "semantic.catalog.definition.duplicate", path + ".id",
                        $"目录中的节点定义 id '{definition.Id}' 重复。");
            }
            return result;
        }

        static Dictionary<string, GraphAuthoringUnitDefinition> IndexUnits(
            IReadOnlyList<GraphAuthoringUnitDefinition> source,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var result = new Dictionary<string, GraphAuthoringUnitDefinition>(StringComparer.Ordinal);
            if (source == null)
            {
                Add(diagnostics, "semantic.catalog.units.missing", "$catalog.units",
                    "目录的 Unit 定义集合不能为空。");
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                var unit = source[i];
                var path = $"$catalog.units[{i}]";
                if (unit == null || string.IsNullOrEmpty(unit.StableId))
                {
                    Add(diagnostics, "semantic.catalog.unit.invalid", path, "目录包含无效 Unit 定义。");
                    continue;
                }
                if (!result.TryAdd(unit.StableId, unit))
                    Add(diagnostics, "semantic.catalog.unit.duplicate", path + ".stableId",
                        $"目录中的 Unit id '{unit.StableId}' 重复。");
            }
            return result;
        }

        static void ValidateNode(
            GraphAuthoringNode node,
            int index,
            IReadOnlyDictionary<string, GraphAuthoringDefinition> definitions,
            IReadOnlyDictionary<string, GraphAuthoringUnitDefinition> units,
            IDictionary<string, GraphAuthoringNode> nodesByKey,
            IDictionary<GraphAuthoringNode, GraphAuthoringDefinition> definitionsByNode,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var path = $"$.nodes[{index}]";
            if (node == null)
            {
                Add(diagnostics, "semantic.node.missing", path, "节点不能为空。");
                return;
            }

            if (!string.IsNullOrEmpty(node.authoringKey) && !nodesByKey.ContainsKey(node.authoringKey))
                nodesByKey.Add(node.authoringKey, node);

            GraphAuthoringDefinition definition = null;
            if (string.IsNullOrEmpty(node.definitionId))
                Add(diagnostics, "semantic.node.definition.missing", path + ".definitionId", "节点定义 id 不能为空。");
            else if (!definitions.TryGetValue(node.definitionId, out definition))
                Add(diagnostics, "semantic.node.definition.unknown", path + ".definitionId",
                    $"节点定义 '{node.definitionId}' 不在当前目录中。");
            else
                definitionsByNode[node] = definition;

            var parameters = IndexParameters(definition, path, diagnostics);
            var represented = new HashSet<string>(StringComparer.Ordinal);
            ValidateValueParameters(node.parameters, parameters, represented, path + ".parameters", diagnostics);
            ValidateGraphRefs(node.graphRefs, parameters, represented, path + ".graphRefs", diagnostics);
            ValidateUnitSlots(node.unitSlots, parameters, units, represented, path + ".unitSlots", diagnostics);
        }

        static Dictionary<string, GraphAuthoringParameter> IndexParameters(
            GraphAuthoringDefinition definition,
            string nodePath,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var result = new Dictionary<string, GraphAuthoringParameter>(StringComparer.Ordinal);
            if (definition == null) return result;
            if (definition.Parameters == null)
            {
                Add(diagnostics, "semantic.catalog.parameters.missing", nodePath + ".definition.parameters",
                    $"节点定义 '{definition.Id}' 的参数集合不能为空。");
                return result;
            }
            foreach (var parameter in definition.Parameters)
                if (parameter != null && !string.IsNullOrEmpty(parameter.Name) && !result.ContainsKey(parameter.Name))
                    result.Add(parameter.Name, parameter);
            return result;
        }

        static void ValidateValueParameters(
            IReadOnlyList<GraphAuthoringParam> source,
            IReadOnlyDictionary<string, GraphAuthoringParameter> parameters,
            ISet<string> represented,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null)
            {
                Add(diagnostics, "semantic.collection.missing", path, "普通参数集合不能为空。");
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                var itemPath = $"{path}[{i}]";
                var item = source[i];
                if (item == null)
                {
                    Add(diagnostics, "semantic.param.missing", itemPath, "普通参数不能为空。");
                    continue;
                }
                if (!TryResolveParameter(item.paramName, parameters, represented, itemPath, diagnostics, out var parameter))
                    continue;
                if (parameter.Type == null || IsUnit(parameter.Type) || IsGraphRef(parameter.Type))
                    Add(diagnostics, "semantic.param.representation", itemPath + ".paramName",
                        $"参数 '{item.paramName}' 不能使用普通值表示。");
            }
        }

        static void ValidateGraphRefs(
            IReadOnlyList<GraphAuthoringGraphRef> source,
            IReadOnlyDictionary<string, GraphAuthoringParameter> parameters,
            ISet<string> represented,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null)
            {
                Add(diagnostics, "semantic.collection.missing", path, "图引用集合不能为空。");
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                var itemPath = $"{path}[{i}]";
                var item = source[i];
                if (item == null)
                {
                    Add(diagnostics, "semantic.graph-ref.missing", itemPath, "图引用不能为空。");
                    continue;
                }
                if (TryResolveParameter(item.paramName, parameters, represented, itemPath, diagnostics, out var parameter) &&
                    !IsGraphRef(parameter.Type))
                    Add(diagnostics, "semantic.param.representation", itemPath + ".paramName",
                        $"参数 '{item.paramName}' 不是 NodeGraphAsset 图引用。");
                if (string.IsNullOrWhiteSpace(item.graphId))
                    Add(diagnostics, "semantic.graph-ref.graph-id.missing", itemPath + ".graphId", "图引用 id 不能为空。");
            }
        }

        static void ValidateUnitSlots(
            IReadOnlyList<GraphAuthoringUnitSlot> source,
            IReadOnlyDictionary<string, GraphAuthoringParameter> parameters,
            IReadOnlyDictionary<string, GraphAuthoringUnitDefinition> units,
            ISet<string> represented,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null)
            {
                Add(diagnostics, "semantic.collection.missing", path, "Unit 槽集合不能为空。");
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                var itemPath = $"{path}[{i}]";
                var item = source[i];
                if (item == null)
                {
                    Add(diagnostics, "semantic.unit-slot.missing", itemPath, "Unit 槽不能为空。");
                    continue;
                }

                var resolved = TryResolveParameter(
                    item.paramName, parameters, represented, itemPath, diagnostics, out var parameter);
                if (resolved && !IsUnit(parameter.Type))
                    Add(diagnostics, "semantic.param.representation", itemPath + ".paramName",
                        $"参数 '{item.paramName}' 不是 Unit 槽。");

                if (item.unit == null)
                {
                    Add(diagnostics, "semantic.unit.missing", itemPath + ".unit", "Unit 槽值不能为空。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.unit.typeId))
                {
                    Add(diagnostics, "semantic.unit.type-id.missing", itemPath + ".unit.typeId", "Unit 稳定 id 不能为空。");
                    continue;
                }
                if (!units.TryGetValue(item.unit.typeId, out var unit))
                {
                    Add(diagnostics, "semantic.unit.unknown-type", itemPath + ".unit.typeId",
                        $"Unit '{item.unit.typeId}' 不在当前目录中。");
                    continue;
                }
                if (resolved && IsUnit(parameter.Type) && !FamilySatisfies(unit.Family, parameter.Type.Name))
                    Add(diagnostics, "semantic.unit.family", itemPath + ".unit.typeId",
                        $"Unit family '{unit.Family}' 不能赋给参数 family '{parameter.Type.Name}'。");
            }
        }

        static bool TryResolveParameter(
            string name,
            IReadOnlyDictionary<string, GraphAuthoringParameter> parameters,
            ISet<string> represented,
            string itemPath,
            List<GraphAuthoringDiagnostic> diagnostics,
            out GraphAuthoringParameter parameter)
        {
            parameter = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                Add(diagnostics, "semantic.param.name.missing", itemPath + ".paramName", "参数名不能为空。");
                return false;
            }
            if (!represented.Add(name))
                Add(diagnostics, "semantic.param.duplicate", itemPath + ".paramName",
                    $"参数 '{name}' 在节点上出现多次。");
            if (!parameters.TryGetValue(name, out parameter))
            {
                Add(diagnostics, "semantic.param.unknown", itemPath + ".paramName",
                    $"参数 '{name}' 未由节点定义声明。");
                return false;
            }
            return true;
        }

        static void ValidateEdges(
            IReadOnlyList<GraphAuthoringEdge> edges,
            IReadOnlyDictionary<string, GraphAuthoringNode> nodesByKey,
            IReadOnlyDictionary<GraphAuthoringNode, GraphAuthoringDefinition> definitionsByNode,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (edges == null)
            {
                Add(diagnostics, "semantic.collection.missing", "$.edges", "边集合不能为空。");
                return;
            }
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                var path = $"$.edges[{i}]";
                if (edge == null)
                {
                    Add(diagnostics, "semantic.edge.missing", path, "边不能为空。");
                    continue;
                }
                ValidateEdgeEnd(edge.from, edge.fromPort, true, path, nodesByKey, definitionsByNode, diagnostics);
                ValidateEdgeEnd(edge.to, edge.toPort, false, path, nodesByKey, definitionsByNode, diagnostics);
            }
        }

        static void ValidateEdgeEnd(
            string nodeKey,
            string portName,
            bool output,
            string edgePath,
            IReadOnlyDictionary<string, GraphAuthoringNode> nodesByKey,
            IReadOnlyDictionary<GraphAuthoringNode, GraphAuthoringDefinition> definitionsByNode,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var end = output ? "from" : "to";
            var port = output ? "fromPort" : "toPort";
            if (!nodesByKey.TryGetValue(nodeKey ?? string.Empty, out var node))
            {
                Add(diagnostics, "semantic.edge.node.unknown", edgePath + "." + end,
                    $"边的{(output ? "起点" : "终点")}节点不存在。");
                return;
            }
            if (!definitionsByNode.TryGetValue(node, out var definition)) return;
            var ports = output ? definition.Outputs : definition.Inputs;
            if (ports == null || !ports.Any(candidate =>
                    candidate != null && string.Equals(candidate.Name, portName, StringComparison.Ordinal)))
                Add(diagnostics, "semantic.edge.port.unknown", edgePath + "." + port,
                    $"端口 '{portName}' 不是节点定义 '{definition.Id}' 的{(output ? "输出" : "输入")}端口。");
        }

        static bool IsUnit(GraphAuthoringType type) => type != null && type.Kind == TypeKind.Unit;

        static bool IsGraphRef(GraphAuthoringType type) =>
            type != null && type.Kind == TypeKind.Object &&
            string.Equals(type.Name, GraphAssetTypeName, StringComparison.Ordinal);

        static bool FamilySatisfies(string actual, string expected) =>
            string.Equals(expected, "Any", StringComparison.Ordinal) ||
            string.Equals(actual, expected, StringComparison.Ordinal);

        static IReadOnlyList<GraphAuthoringDiagnostic> ReadOnly(List<GraphAuthoringDiagnostic> diagnostics) =>
            Array.AsReadOnly(diagnostics.ToArray());

        static void Add(List<GraphAuthoringDiagnostic> diagnostics, string code, string path, string message) =>
            diagnostics.Add(new GraphAuthoringDiagnostic(code, path, message));

        sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
