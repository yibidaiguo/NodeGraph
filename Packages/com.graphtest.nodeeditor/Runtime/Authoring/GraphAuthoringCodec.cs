// GraphAuthoringCodec.cs —— GraphData 与 AI 友好文档之间的无损纯层转换。
// 不依赖编辑器与 JSON，不修改输入；导入只在整个 draft 构建并验证成功后才返回 GraphData。

using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    public static class GraphAuthoringCodec
    {
        public static GraphAuthoringExportResult Export(
            GraphData source,
            IUnitAuthoringCatalog unitCatalog,
            GraphAuthoringRevisionVector revisionVector = null)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            if (source == null)
            {
                Add(diagnostics, "authoring.source.missing", "$", "GraphData 不能为空。");
                return new GraphAuthoringExportResult(null, diagnostics);
            }

            ValidateEnum(source.graphType, "$.graphType", diagnostics);
            ValidateEnum(source.orientation, "$.orientation", diagnostics);
            ValidateRevisionVector(revisionVector, diagnostics);
            if (source.instances == null) Add(diagnostics, "authoring.collection.missing", "$.nodes", "节点集合不能为空。");
            if (source.entryInstanceIds == null) Add(diagnostics, "authoring.collection.missing", "$.entries", "入口集合不能为空。");
            if (source.instances == null || source.entryInstanceIds == null)
                return new GraphAuthoringExportResult(null, diagnostics);

            var byInstanceId = new Dictionary<string, NodeInstance>(StringComparer.Ordinal);
            var keyByNode = new Dictionary<NodeInstance, string>(ReferenceComparer<NodeInstance>.Instance);
            var usedKeys = new HashSet<string>(StringComparer.Ordinal);
            var legacyNodes = new List<NodeInstance>();

            for (int i = 0; i < source.instances.Count; i++)
            {
                var path = $"$.nodes[{i}]";
                var node = source.instances[i];
                if (node == null)
                {
                    Add(diagnostics, "authoring.node.missing", path, "节点不能为空。");
                    continue;
                }
                if (string.IsNullOrEmpty(node.instanceId))
                    Add(diagnostics, "authoring.instance-id.missing", path + ".instanceId", "instanceId 不能为空。");
                else if (!byInstanceId.TryAdd(node.instanceId, node))
                    Add(diagnostics, "authoring.instance-id.duplicate", path + ".instanceId", "instanceId 在图内必须唯一。");

                if (string.IsNullOrEmpty(node.authoringKey)) legacyNodes.Add(node);
                else if (!GraphAuthoringKeys.IsValid(node.authoringKey))
                    Add(diagnostics, "authoring.key.invalid", path + ".authoringKey", "authoringKey 不得有首尾空白或控制字符。");
                else if (!usedKeys.Add(node.authoringKey))
                    Add(diagnostics, "authoring.key.duplicate", path + ".authoringKey", "authoringKey 在图内必须唯一。");
                else keyByNode[node] = node.authoringKey;
            }

            // 以 instanceId 排序后分配，碰撞结果不受节点列表顺序影响。
            foreach (var node in legacyNodes.OrderBy(node => node?.instanceId, StringComparer.Ordinal))
            {
                if (node == null || string.IsNullOrEmpty(node.instanceId) || keyByNode.ContainsKey(node)) continue;
                if (GraphAuthoringKeys.TryCreateLegacyKey(node.instanceId, usedKeys, out var key)) keyByNode[node] = key;
                else Add(diagnostics, "authoring.key.collision", "$.nodes", "确定性 authoringKey 的完整哈希仍发生碰撞。");
            }

            var document = new GraphAuthoringDocument
            {
                schemaVersion = GraphAuthoringDocument.CurrentSchemaVersion,
                authoringKeysPersisted = legacyNodes.Count == 0,
                revisionVector = CloneRevisionVector(revisionVector),
                graphId = source.graphId,
                module = source.module,
                group = source.group,
                graphType = source.graphType,
                orientation = source.orientation
            };

            for (int i = 0; i < source.instances.Count; i++)
            {
                var node = source.instances[i];
                if (node == null || !keyByNode.TryGetValue(node, out var key)) continue;
                var path = $"$.nodes[{i}]";
                var encoded = new GraphAuthoringNode
                {
                    authoringKey = key,
                    instanceId = node.instanceId,
                    definitionId = node.definitionId,
                    positionX = node.position.x,
                    positionY = node.position.y,
                    displayName = node.displayName,
                    note = node.note,
                    pinned = node.pinned
                };
                if (!IsFinite(encoded.positionX) || !IsFinite(encoded.positionY))
                    Add(diagnostics, "authoring.position.invalid", path + ".position", "节点位置必须是有限数值。");
                CopyParams(node.parameterOverrides, encoded.parameters, path + ".parameters", diagnostics);
                CopyGraphRefs(node.graphRefs, encoded.graphRefs, path + ".graphRefs", diagnostics);
                GraphAuthoringUnitCodec.EncodeSlots(node.unitOverrides, encoded.unitSlots, unitCatalog, path + ".unitSlots", diagnostics);
                document.nodes.Add(encoded);

                if (node.connections == null)
                {
                    Add(diagnostics, "authoring.collection.missing", path + ".connections", "连接集合不能为空。");
                    continue;
                }
                for (int c = 0; c < node.connections.Count; c++)
                {
                    var connection = node.connections[c];
                    var edgePath = path + $".connections[{c}]";
                    if (connection == null)
                    {
                        Add(diagnostics, "authoring.edge.missing", edgePath, "连接不能为空。");
                        continue;
                    }
                    var portsValid = ValidatePorts(connection.fromPort, connection.toPort, edgePath, diagnostics);
                    if (!byInstanceId.TryGetValue(connection.toInstanceId ?? "", out var target) ||
                        !keyByNode.TryGetValue(target, out var targetKey))
                    {
                        Add(diagnostics, "authoring.edge.dangling", edgePath + ".toInstanceId", "连接目标不存在。");
                        continue;
                    }
                    if (!portsValid) continue;
                    document.edges.Add(new GraphAuthoringEdge
                    {
                        from = key,
                        fromPort = connection.fromPort,
                        to = targetKey,
                        toPort = connection.toPort
                    });
                }
            }

            for (int i = 0; i < source.entryInstanceIds.Count; i++)
            {
                if (!byInstanceId.TryGetValue(source.entryInstanceIds[i] ?? "", out var entry) ||
                    !keyByNode.TryGetValue(entry, out var key))
                    Add(diagnostics, "authoring.entry.dangling", $"$.entries[{i}]", "入口引用的节点不存在。");
                else document.entries.Add(key);
            }

            return diagnostics.Count == 0
                ? new GraphAuthoringExportResult(document, diagnostics)
                : new GraphAuthoringExportResult(null, diagnostics);
        }

        public static GraphAuthoringImportResult Import(
            GraphAuthoringDocument document,
            IUnitAuthoringCatalog unitCatalog)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            if (document == null)
            {
                Add(diagnostics, "authoring.document.missing", "$", "创作文档不能为空。");
                return new GraphAuthoringImportResult(null, diagnostics);
            }
            if (document.schemaVersion != GraphAuthoringDocument.CurrentSchemaVersion)
            {
                Add(diagnostics, "authoring.schema.unsupported", "$.schemaVersion",
                    $"不支持 schemaVersion {document.schemaVersion}；当前版本为 {GraphAuthoringDocument.CurrentSchemaVersion}。");
                return new GraphAuthoringImportResult(null, diagnostics);
            }

            ValidateEnum(document.graphType, "$.graphType", diagnostics);
            ValidateEnum(document.orientation, "$.orientation", diagnostics);
            ValidateRevisionVector(document.revisionVector, diagnostics);
            if (document.nodes == null) Add(diagnostics, "authoring.collection.missing", "$.nodes", "节点集合不能为空。");
            if (document.entries == null) Add(diagnostics, "authoring.collection.missing", "$.entries", "入口集合不能为空。");
            if (document.edges == null) Add(diagnostics, "authoring.collection.missing", "$.edges", "边集合不能为空。");
            if (document.nodes == null || document.entries == null || document.edges == null)
                return new GraphAuthoringImportResult(null, diagnostics);

            var draft = new GraphData
            {
                graphId = document.graphId,
                module = document.module,
                group = document.group,
                graphType = document.graphType,
                orientation = document.orientation
            };
            var byKey = new Dictionary<string, NodeInstance>(StringComparer.Ordinal);
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < document.nodes.Count; i++)
            {
                var path = $"$.nodes[{i}]";
                var sourceNode = document.nodes[i];
                if (sourceNode == null)
                {
                    Add(diagnostics, "authoring.node.missing", path, "节点不能为空。");
                    continue;
                }
                if (string.IsNullOrEmpty(sourceNode.authoringKey))
                    Add(diagnostics, "authoring.key.missing", path + ".authoringKey", "schema v1 的 authoringKey 不能为空。");
                else if (!GraphAuthoringKeys.IsValid(sourceNode.authoringKey))
                    Add(diagnostics, "authoring.key.invalid", path + ".authoringKey", "authoringKey 不得有首尾空白或控制字符。");
                else if (byKey.ContainsKey(sourceNode.authoringKey))
                    Add(diagnostics, "authoring.key.duplicate", path + ".authoringKey", "authoringKey 在图内必须唯一。");

                if (string.IsNullOrEmpty(sourceNode.instanceId))
                    Add(diagnostics, "authoring.instance-id.missing", path + ".instanceId", "instanceId 不能为空。");
                else if (!instanceIds.Add(sourceNode.instanceId))
                    Add(diagnostics, "authoring.instance-id.duplicate", path + ".instanceId", "instanceId 在图内必须唯一。");
                if (!IsFinite(sourceNode.positionX) || !IsFinite(sourceNode.positionY))
                    Add(diagnostics, "authoring.position.invalid", path + ".position", "节点位置必须是有限数值。");

                var node = new NodeInstance
                {
                    authoringKey = sourceNode.authoringKey,
                    instanceId = sourceNode.instanceId,
                    definitionId = sourceNode.definitionId,
                    position = new Vec2(sourceNode.positionX, sourceNode.positionY),
                    displayName = sourceNode.displayName,
                    note = sourceNode.note,
                    pinned = sourceNode.pinned
                };
                DecodeParams(sourceNode.parameters, node.parameterOverrides, path + ".parameters", diagnostics);
                DecodeGraphRefs(sourceNode.graphRefs, node.graphRefs, path + ".graphRefs", diagnostics);
                GraphAuthoringUnitCodec.DecodeSlots(sourceNode.unitSlots, node.unitOverrides, unitCatalog, path + ".unitSlots", diagnostics);
                draft.instances.Add(node);

                if (GraphAuthoringKeys.IsValid(sourceNode.authoringKey) && !byKey.ContainsKey(sourceNode.authoringKey))
                    byKey.Add(sourceNode.authoringKey, node);
            }

            for (int i = 0; i < document.edges.Count; i++)
            {
                var edge = document.edges[i];
                var path = $"$.edges[{i}]";
                if (edge == null)
                {
                    Add(diagnostics, "authoring.edge.missing", path, "边不能为空。");
                    continue;
                }
                var portsValid = ValidatePorts(edge.fromPort, edge.toPort, path, diagnostics);
                if (!byKey.TryGetValue(edge.from ?? "", out var from))
                    Add(diagnostics, "authoring.edge.dangling", path + ".from", "边的起点不存在。");
                if (!byKey.TryGetValue(edge.to ?? "", out var to))
                    Add(diagnostics, "authoring.edge.dangling", path + ".to", "边的终点不存在。");
                if (portsValid && from != null && to != null)
                    from.connections.Add(new Connection
                    {
                        fromPort = edge.fromPort,
                        toInstanceId = to.instanceId,
                        toPort = edge.toPort
                    });
            }

            for (int i = 0; i < document.entries.Count; i++)
            {
                if (!byKey.TryGetValue(document.entries[i] ?? "", out var entry))
                    Add(diagnostics, "authoring.entry.dangling", $"$.entries[{i}]", "入口引用的节点不存在。");
                else draft.entryInstanceIds.Add(entry.instanceId);
            }

            return diagnostics.Count == 0
                ? new GraphAuthoringImportResult(draft, diagnostics)
                : new GraphAuthoringImportResult(null, diagnostics);
        }

        static bool ValidatePorts(string fromPort, string toPort, string path, List<GraphAuthoringDiagnostic> diagnostics)
        {
            var valid = true;
            if (string.IsNullOrEmpty(fromPort))
            {
                Add(diagnostics, "authoring.edge.port.missing", path + ".fromPort", "边的 fromPort 不能为空。");
                valid = false;
            }
            if (string.IsNullOrEmpty(toPort))
            {
                Add(diagnostics, "authoring.edge.port.missing", path + ".toPort", "边的 toPort 不能为空。");
                valid = false;
            }
            return valid;
        }

        static void CopyParams(List<ParamOverride> source, List<GraphAuthoringParam> target, string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null) { Add(diagnostics, "authoring.collection.missing", path, "参数集合不能为空。"); return; }
            for (int i = 0; i < source.Count; i++)
            {
                var value = source[i];
                if (value == null) { Add(diagnostics, "authoring.param.missing", $"{path}[{i}]", "参数不能为空。"); continue; }
                target.Add(new GraphAuthoringParam { paramName = value.paramName, valueJson = value.valueJson });
            }
        }

        static void CopyGraphRefs(List<GraphRef> source, List<GraphAuthoringGraphRef> target, string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null) { Add(diagnostics, "authoring.collection.missing", path, "图引用集合不能为空。"); return; }
            for (int i = 0; i < source.Count; i++)
            {
                var value = source[i];
                if (value == null) { Add(diagnostics, "authoring.graph-ref.missing", $"{path}[{i}]", "图引用不能为空。"); continue; }
                target.Add(new GraphAuthoringGraphRef { paramName = value.paramName, graphId = value.graphId });
            }
        }

        static void DecodeParams(List<GraphAuthoringParam> source, List<ParamOverride> target, string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null) { Add(diagnostics, "authoring.collection.missing", path, "参数集合不能为空。"); return; }
            for (int i = 0; i < source.Count; i++)
            {
                var value = source[i];
                if (value == null) { Add(diagnostics, "authoring.param.missing", $"{path}[{i}]", "参数不能为空。"); continue; }
                target.Add(new ParamOverride { paramName = value.paramName, valueJson = value.valueJson });
            }
        }

        static void DecodeGraphRefs(List<GraphAuthoringGraphRef> source, List<GraphRef> target, string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (source == null) { Add(diagnostics, "authoring.collection.missing", path, "图引用集合不能为空。"); return; }
            for (int i = 0; i < source.Count; i++)
            {
                var value = source[i];
                if (value == null) { Add(diagnostics, "authoring.graph-ref.missing", $"{path}[{i}]", "图引用不能为空。"); continue; }
                target.Add(new GraphRef { paramName = value.paramName, graphId = value.graphId });
            }
        }

        static GraphAuthoringRevisionVector CloneRevisionVector(GraphAuthoringRevisionVector source)
        {
            var clone = new GraphAuthoringRevisionVector();
            if (source?.owners == null) return clone;
            foreach (var owner in source.owners)
                clone.owners.Add(owner == null ? null : new GraphAuthoringRevisionOwner
                {
                    ownerId = owner.ownerId,
                    ownerPath = owner.ownerPath,
                    contentHash = owner.contentHash,
                    expectedState = owner.expectedState
                });
            return clone;
        }

        static void ValidateRevisionVector(GraphAuthoringRevisionVector vector,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (vector == null) return;
            if (vector.owners == null)
            {
                Add(diagnostics, "authoring.collection.missing", "$.revisionVector.owners", "revision owners 集合不能为空。");
                return;
            }
            for (int i = 0; i < vector.owners.Count; i++)
            {
                var owner = vector.owners[i];
                if (owner == null) Add(diagnostics, "revision.owner-missing", $"$.revisionVector.owners[{i}]", "revision owner 不能为空。");
                else ValidateEnum(owner.expectedState, $"$.revisionVector.owners[{i}].expectedState", diagnostics);
            }
        }

        static void ValidateEnum<T>(T value, string path, List<GraphAuthoringDiagnostic> diagnostics) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                Add(diagnostics, "authoring.enum.invalid", path, $"'{value}' 不是有效的 {typeof(T).Name} 值。");
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
