// NodeGraphSource.cs —— 用一组 NodeGraphAsset 实现纯层的 IGraphSource。NodeEditor.Unity 程序集。
//
// 子图引用在 0.0.x 是 NodeInstance.objectOverrides 里的 UnityEngine.Object 直连，执行器拿到就能用；
// 0.1.0 收敛为稳定 graphId 后，需要有人把 id 变回图——Unity 侧就是本类。
//
// 为什么要显式登记而不是自动查找：player 构建里没有 AssetDatabase，无法按 id 全局搜图。
// 这与 blackboards 一直以来的做法一致（"运行时构建无 AssetDatabase，故各档在此显式引用"）——
// 把被引用的子图挂进 inspector，构建才带得上它们。编辑期可由工具一键收集（见 MIGRATION.md）。

using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor
{
    public sealed class NodeGraphSource : IGraphSource
    {
        readonly Dictionary<string, NodeGraphAsset> m_ById = new();

        public NodeGraphSource(params NodeGraphAsset[] graphs) : this((IEnumerable<NodeGraphAsset>)graphs) { }

        public NodeGraphSource(IEnumerable<NodeGraphAsset> graphs)
        {
            if (graphs == null) return;
            foreach (var g in graphs) Add(g);
        }

        // 这里判 null 用的是 UnityEngine.Object 的重载（强类型 NodeGraphAsset 引用），
        // 因此被销毁/丢失的资产会被正确剔除——纯层拿接口比较做不到这点，所以过滤放在这一侧。
        public NodeGraphSource Add(NodeGraphAsset graph)
        {
            if (graph == null) return this;
            var id = graph.graphId;
            if (!string.IsNullOrEmpty(id)) m_ById[id] = graph;
            return this;
        }

        public GraphData FindGraph(string graphId) =>
            graphId != null && m_ById.TryGetValue(graphId, out var g) && g != null ? g.ToData() : null;
    }
}
