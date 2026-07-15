// NodeRegistry.cs —— 子层 4a（节点数据）ScriptableObject（第 2 层节点池）。
// 必须放在与类同名的独立文件中，这样 Unity 才会绑定其 MonoScript（理由见 BlackboardAsset.cs ——
// 否则该 registry .asset 会得到一个损坏的 m_Script，NodeRegistryLocator.Find 将无法看到它）。
// 命名空间 NodeEditor。Runtime/ 程序集。

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor
{
    [CreateAssetMenu(menuName = "NodeEditor/Node Registry")]
    public class NodeRegistry : ScriptableObject
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
    }
}
