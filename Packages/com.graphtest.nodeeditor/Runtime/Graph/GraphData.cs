// GraphData.cs —— 一张图的纯 C# 形态。Runtime 程序集（纯层）。
//
// 与 NodeGraphAsset（Unity 载体）的关系：**不是拷贝，是同一份数据的另一种持有方式**。
// NodeGraphAsset.ToData() 返回的 GraphData 直接引用 asset 那个 List<NodeInstance> 实例
//（不是深拷贝），因此两侧看到的是同一批节点，编辑器改了执行器立刻可见，不存在同步问题。
// 这样才对得上"一份数据结构、一个执行器、三种表示"——若 ToData 做深拷贝，就变成两份数据了。
//
// 为什么不让 NodeGraphAsset 直接持有一个 GraphData 字段：那样 YAML 会多嵌一层
//（instances 变成 data.instances），已发布的每一个 .asset 都要迁移。保持 asset 字段扁平、
// 由 ToData() 包一层，已有资产一字节不动。
//
// graphId：跨会话稳定的图身份。子图引用（NodeInstance.graphRefs）按它解析，
// 取代 0.0.x 的 UnityEngine.Object 引用——纯层由此能解析图到图的链接。

using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    [Serializable]
    public class GraphData
    {
        // 跨会话稳定的图身份。Unity 侧由 NodeGraphAsset 播种（默认取 asset GUID）；
        // 纯 C# / JSON 侧由载入方提供。空 = 这张图不可被其他图引用为子图。
        public string graphId = "";

        // 模块标签（领域显式标注，如 "dialogue"）。框架只认这个字符串、不认领域语义。
        public string module = "";
        // 模块内的分组标签。与 module 一起决定本图的「有效黑板」分层（全局⊕模块⊕组）。
        public string group = "";

        public GraphType graphType;
        public GraphOrientation orientation = GraphOrientation.Inherit;

        public List<NodeInstance> instances = new();
        public List<string> entryInstanceIds = new();   // 入口侧列表（控制流 / tick-tree）

        // ---- O(1) 查找 ----
        // 执行器过去各自维护 Dictionary<instanceId, NodeInstance> 缓存（DialogueRunner #6、
        // StateMachineRunner、TaskRunner 各一份）。收敛到这里：按需惰性建、随图走，
        // 三个执行器共用同一套索引语义。
        [NonSerialized] Dictionary<string, NodeInstance> m_Index;

        public NodeInstance Find(string instanceId)
        {
            if (instanceId == null) return null;
            if (m_Index == null || m_Index.Count != instances.Count) RebuildIndex();
            return m_Index.TryGetValue(instanceId, out var n) ? n : null;
        }

        // 结构变更（增删节点）后调用；Find 也会在数量对不上时自愈重建。
        public void RebuildIndex()
        {
            m_Index = new Dictionary<string, NodeInstance>(instances.Count);
            foreach (var inst in instances)
                if (inst?.instanceId != null) m_Index[inst.instanceId] = inst;
        }

        // 沿某个出端口走一步。悬空/被切断的边返回 null（执行器据此收束，而不是抛异常）。
        public NodeInstance Next(NodeInstance from, string port) =>
            from == null ? null : Find(from.connections.FirstOrDefault(c => c.fromPort == port)?.toInstanceId);

        // 图的入口：优先取第一个声明的 entryInstanceId，取不到则由调用方按领域语义兜底
        //（如对话图回退到第一个 Start 节点）。
        public NodeInstance Entry() => Find(entryInstanceIds.FirstOrDefault());
    }
}
