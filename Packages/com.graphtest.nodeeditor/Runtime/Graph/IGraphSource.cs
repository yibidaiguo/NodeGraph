// IGraphSource.cs —— 按稳定 graphId 解析一张图。Runtime 程序集（纯层）。
//
// 子图引用（SubDialogue / SubMachine / Task 的 stepGraph）在 0.0.x 是 UnityEngine.Object 直连，
// 执行器拿到就能用；0.1.0 收敛为 graphId 字符串后，需要有人把 id 变回 GraphData——就是这个接口。
//
// Unity 侧：由持有若干 NodeGraphAsset 的注册表实现（asset.graphId -> asset.ToData()）。
// 纯 C# 侧：JSON 载入后建一张 id -> GraphData 表即可（下方 GraphSet）。
// 为 null 时执行器按语义兜底（子图未设置 => 跳过该节点），不抛异常。

using System.Collections.Generic;

namespace NodeEditor
{
    public interface IGraphSource
    {
        GraphData FindGraph(string graphId);
    }

    // 纯 C# 实现：一张 graphId -> GraphData 表。
    public sealed class GraphSet : IGraphSource
    {
        readonly Dictionary<string, GraphData> m_ById = new();

        public GraphSet(IEnumerable<GraphData> graphs = null)
        {
            if (graphs == null) return;
            foreach (var g in graphs) Add(g);
        }

        public GraphSet Add(GraphData graph)
        {
            if (graph != null && !string.IsNullOrEmpty(graph.graphId)) m_ById[graph.graphId] = graph;
            return this;
        }

        public GraphData FindGraph(string graphId) =>
            graphId != null && m_ById.TryGetValue(graphId, out var g) ? g : null;
    }
}
