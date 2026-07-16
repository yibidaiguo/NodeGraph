// ClipboardCodec.cs — 画布复制/粘贴的纯逻辑编解码器 + 剪贴板载荷数据类（拆自 GraphCanvasView.cs，代码逐字未改）。
// 内嵌的 ClipboardPayloadHost/UnitCloneHost 仅作 CreateInstance→JSON 往返/深拷的瞬态宿主、随即 DestroyImmediate，
// 从不落盘成 .asset，故不受硬规则 A1「一类一文件」约束（A1 防的是 MonoScript 绑定坏 .asset）。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;   // 仅限于此适配器文件使用
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset、……）

namespace NodeEditor.EditorUI
{

    // ---- 剪贴板编解码器 ----------------------------------------------------
    // 用于节点复制/粘贴的纯粹（不依赖 GraphView）编码/解码，从 GraphCanvas 中提取出来，使那些棘手的
    // 部分 —— 丢弃指向未被复制节点的连线、粘贴时重映射 instanceId、跳过不再能解析的
    // definition —— 无需活动 panel 即可单元测试。仅限编辑器使用，因为 Object 类型的
    // 参数通过 GlobalObjectId 往返序列化（在粘贴/会话边界之间构建安全）。
    public static class ClipboardCodec
    {
        sealed class ClipboardPayloadHost : ScriptableObject
        {
            public ClipboardPayload payload = new ClipboardPayload();
        }

        sealed class UnitCloneHost : ScriptableObject
        {
            public List<UnitOverride> values = new();
        }

        static string ToJson(ClipboardPayload payload)
        {
            var host = ScriptableObject.CreateInstance<ClipboardPayloadHost>();
            try
            {
                host.payload = payload;
                return EditorJsonUtility.ToJson(host);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        static ClipboardPayload FromJson(string data)
        {
            var host = ScriptableObject.CreateInstance<ClipboardPayloadHost>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(data, host);
                return host.payload;
            }
            catch { return null; }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        static List<UnitOverride> CloneUnits(IEnumerable<UnitOverride> source)
        {
            var host = ScriptableObject.CreateInstance<UnitCloneHost>();
            host.values = source?.Where(x => x != null).ToList() ?? new List<UnitOverride>();
            var clone = UnityEngine.Object.Instantiate(host);
            try { return clone.values ?? new List<UnitOverride>(); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
        // 编码一组节点实例。目标在集合之外的连接会被丢弃（粘贴
        // 绝不能悄悄重连原图）。对 null/空选择返回 ""。
        public static string Serialize(IEnumerable<NodeInstance> instances)
        {
            var list = instances?.Where(i => i != null).ToList() ?? new List<NodeInstance>();
            if (list.Count == 0) return string.Empty;

            var idSet = new HashSet<string>(list.Select(i => i.instanceId));
            var payload = new ClipboardPayload();
            foreach (var inst in list)
            {
                var clip = new ClipboardNode
                {
                    instanceId = inst.instanceId,
                    definitionId = inst.definitionId,
                    position = inst.position,
                    parameterOverrides = inst.parameterOverrides
                        .Select(p => new ParamOverride { paramName = p.paramName, valueJson = p.valueJson }).ToList(),
                    unitOverrides = CloneUnits(inst.unitOverrides)
                };
                foreach (var c in inst.connections)
                    if (idSet.Contains(c.toInstanceId))
                        clip.connections.Add(new Connection { fromPort = c.fromPort, toInstanceId = c.toInstanceId, toPort = c.toPort });
                foreach (var oo in inst.objectOverrides)
                {
                    if (oo.value == null) continue;
                    // GlobalObjectId（GUID+fileID）能在粘贴操作和编辑器会话之间存续，
                    // 与原始 instanceID 不同 —— 这是必需的，因为 Object 引用无法通过 JsonUtility 往返序列化。
                    clip.objectOverrides.Add(new ObjectOverrideClip
                    {
                        paramName = oo.paramName,
                        globalId = GlobalObjectId.GetGlobalObjectIdSlow(oo.value).ToString()
                    });
                }
                payload.nodes.Add(clip);
            }
            return ToJson(payload);
        }

        // 仅当剪贴板数据来自本编解码器，且至少一个节点可被当前图准入时返回 true。
        // 从其他应用复制的文本以及只含其他模块节点的负载都不可粘贴。
        public static bool CanPaste(string data, NodeRegistry registry, NodeGraphAsset graph)
        {
            var payload = Parse(data);
            if (payload == null || registry == null || graph == null) return false;
            return payload.nodes.Any(node =>
                NodeAdmission.Evaluate(graph, registry.Find(node.definitionId)).allowed);
        }

        // 解码并重建为全新实例：新的 GUID instanceId，内部连接重映射到
        // 这些新 id 上（指向未被复制或已跳过节点的连线会被丢弃），位置按 `offset` 偏移，
        // 任何无法解析或不被当前图模块准入的 definition 都会被跳过。
        // 对 null/无效/无法解析的数据返回空列表（绝不返回 null）。
        public static List<NodeInstance> BuildPasted(
            string data, NodeRegistry registry, NodeGraphAsset graph, Vector2 offset)
        {
            var result = new List<NodeInstance>();
            var payload = Parse(data);
            if (payload == null || registry == null || graph == null) return result;

            var idMap = new Dictionary<string, string>();
            foreach (var n in payload.nodes) idMap[n.instanceId] = System.Guid.NewGuid().ToString();

            var byNewId = new Dictionary<string, NodeInstance>();
            foreach (var n in payload.nodes)
            {
                var definition = registry.Find(n.definitionId);
                if (!NodeAdmission.Evaluate(graph, definition).allowed) continue;
                var inst = new NodeInstance
                {
                    instanceId = idMap[n.instanceId],
                    definitionId = n.definitionId,
                    position = n.position + offset,
                    parameterOverrides = n.parameterOverrides
                        .Select(p => new ParamOverride { paramName = p.paramName, valueJson = p.valueJson }).ToList(),
                    unitOverrides = CloneUnits(n.unitOverrides)
                };
                foreach (var oo in n.objectOverrides)
                {
                    if (GlobalObjectId.TryParse(oo.globalId, out var gid))
                    {
                        var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                        if (obj != null) inst.objectOverrides.Add(new ObjectOverride { paramName = oo.paramName, value = obj });
                    }
                }
                result.Add(inst);
                byNewId[inst.instanceId] = inst;
            }

            // 第二遍：将内部连接重映射到新 id 上；丢弃那些目标不在
            // 粘贴集合内、或其 definition 在上文被跳过的连线。
            foreach (var n in payload.nodes)
            {
                if (!idMap.TryGetValue(n.instanceId, out var newFrom)) continue;
                if (!byNewId.TryGetValue(newFrom, out var fromInst)) continue;          // 源 def 被跳过
                foreach (var c in n.connections)
                {
                    if (!idMap.TryGetValue(c.toInstanceId, out var newTo)) continue;     // 目标未被复制
                    if (!byNewId.ContainsKey(newTo)) continue;                            // 目标 def 被跳过
                    fromInst.connections.Add(new Connection { fromPort = c.fromPort, toInstanceId = newTo, toPort = c.toPort });
                }
            }
            return result;
        }

        // 仅当格式正确且带 marker 标记（且节点列表非 null）时才解码；否则返回 null。
        static ClipboardPayload Parse(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            try
            {
                var payload = FromJson(data);
                if (payload == null || payload.marker != ClipboardPayload.Marker || payload.nodes == null) return null;
                return payload;
            }
            catch { return null; }   // 任意剪贴板文本（例如从其他应用复制而来）
        }
    }

    [System.Serializable]
    class ClipboardPayload
    {
        public const string Marker = "NodeEditorClipboard";
        public string marker = Marker;
        public List<ClipboardNode> nodes = new();
    }
    [System.Serializable]
    class ClipboardNode
    {
        public string instanceId;
        public string definitionId;
        public Vector2 position;
        public List<Connection> connections = new();
        public List<ParamOverride> parameterOverrides = new();
        public List<ObjectOverrideClip> objectOverrides = new();
        public List<UnitOverride> unitOverrides = new();
    }
    [System.Serializable]
    class ObjectOverrideClip { public string paramName; public string globalId; }
}
