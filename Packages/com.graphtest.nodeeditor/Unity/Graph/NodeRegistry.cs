// NodeRegistry.cs —— 子层 4a（节点数据）ScriptableObject（第 2 层节点池）。
// 必须放在与类同名的独立文件中，这样 Unity 才会绑定其 MonoScript（理由见 BlackboardAsset.cs ——
// 否则该 registry .asset 会得到一个损坏的 m_Script，NodeRegistryLocator.Find 将无法看到它）。
// 命名空间 NodeEditor。Runtime/ 程序集。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor
{
    // 实现纯层的 ISchemaSource：执行器只认 FindSchema，不认 NodeRegistry 本身。
    // Find（返回 NodeDefinition）原样保留给编辑器侧使用——因此既有调用点
    // `new DialogueRunner(registry, ...)` 与 `registry.Find(id)` 都一字不改仍然编译通过。
    [CreateAssetMenu(menuName = "NodeEditor/Node Registry")]
    public class NodeRegistry : ScriptableObject, ISchemaSource
    {
        public List<NodeDefinition> universal = new();
        public List<NodeDefinition> projectDomain = new();
        public NodeDefinition Find(string id)
        {
            var matches = universal.Concat(projectDomain)
                .Where(definition => definition != null && definition.Id == id)
                .Take(2).ToList();
            if (matches.Count > 1)
            {
                Debug.LogError($"NodeEditor: NodeRegistry contains multiple definitions with id '{id}'.");
                return null;
            }
            return matches.Count == 1 ? matches[0] : null;
        }

        // ---- 纯层接缝 ----
        // 把定义烘成 NodeSchema 给执行器。按 id 缓存：执行器在热路径上每个节点每拍都要查
        //（KindOf / Param 都走这里），每次重新烘 6 个 List 会很浪费。
        //
        // 缓存失效：编辑期改定义后由 Invalidate() 清空（Editor 侧在 RebuildFromCode 之后调用）。
        // 播放期定义不变，故运行时无需失效。
        [NonSerialized] Dictionary<string, NodeSchema> m_SchemaCache;

        public NodeSchema FindSchema(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId)) return null;
            m_SchemaCache ??= new Dictionary<string, NodeSchema>();
            if (m_SchemaCache.TryGetValue(definitionId, out var cached)) return cached;

            var def = Find(definitionId);
            var schema = def != null ? def.ToSchema() : null;
            m_SchemaCache[definitionId] = schema;   // 也缓存 null，避免反复扫描找不到的 id
            return schema;
        }

        // 定义发生变更后清空烘焙缓存（编辑期用）。
        public void InvalidateSchemaCache() => m_SchemaCache = null;

        // 全量烘焙：导出 JSON 镜像、构造纯 C# SchemaSet（dotnet test / 服务器侧）时用。
        public SchemaSet ToSchemaSet() => new SchemaSet(
            universal.Concat(projectDomain).Where(d => d != null).Select(d => d.ToSchema()));
    }
}
