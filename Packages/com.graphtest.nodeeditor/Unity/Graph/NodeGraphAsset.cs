// NodeGraphAsset.cs —— 子层 4a（节点数据）ScriptableObject。
// 必须放在与类同名的独立文件中，这样 Unity 才会绑定其 MonoScript（完整理由见 BlackboardAsset.cs ——
// 否则每个保存的 graph .asset 都会得到一个损坏的 m_Script，编辑器将无法加载它）。
// 命名空间 NodeEditor。Runtime/ 程序集。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor
{
    [CreateAssetMenu(menuName = "NodeEditor/Graph")]
    public class NodeGraphAsset : ScriptableObject, IAuthoringAsset
    {
        public AuthoringFamily AuthoringFamily => AuthoringFamily.WireGraph;
        // 模块标签（领域显式标注，如 "dialogue"；任务编辑器日后用 "quest" 等）。左侧图列表据此分组、
        // 领域入口据此过滤（见 GraphListPane 分组 + NodeEditorWindow 模块模式）。空串=未分组（归入"其他"组）。
        // 框架只认这个字符串、不认任何领域语义；"我属于哪个模块"由领域层在新建/Setup 时播种（机制/策略分层）。
        public string module = "";
        // 模块内的分组标签（如 "chapter1"）。与 module 一起决定本图的「有效黑板」分层：
        // 全局 ⊕ 模块(module) ⊕ 组(module+group)（见 BlackboardSet / BlackboardLocator.ResolveFor）。
        // 空串=不属于任何组（只继承 全局+模块 两档）。框架只认这个字符串、不认领域语义。
        public string group = "";
        public GraphType graphType;                  // 第 1 层的决策，记录在此
        public GraphOrientation orientation = GraphOrientation.Inherit;
        public List<NodeInstance> instances = new();
        public List<string> entryInstanceIds = new();   // 入口侧列表（控制流 / tick-tree）

        // 跨会话稳定的图身份，供子图引用（NodeInstance.graphRefs）解析。
        // 空时由编辑器在导入/保存期播种为该 asset 的 GUID（见 Editor 侧 NodeGraphIdSeeder）。
        // 取代 0.0.x 的 UnityEngine.Object 子图引用——纯 C# 层由此也能解析图到图的链接。
        public string graphId = "";

        // ---- 纯层接缝 ----
        // 返回这张图的纯 C# 形态。**缓存且引用稳定**：多次调用返回同一个 GraphData 实例，
        // 因此执行器拿它当字典键 / 做引用相等判断（OwnsGraph、子图栈帧比较）的语义
        // 与 0.0.x 直接用 NodeGraphAsset 时完全一致。
        //
        // 注意 instances 是**同一个 List 实例**，不是拷贝：编辑器改了执行器立刻可见，
        // 反之亦然——这正是"一份数据结构"的含义。因此不要在这里做深拷贝。
        [NonSerialized] GraphData m_Data;

        public GraphData ToData()
        {
            if (m_Data == null) m_Data = new GraphData();
            m_Data.graphId = graphId;
            m_Data.module = module;
            m_Data.group = group;
            m_Data.graphType = graphType;
            m_Data.orientation = orientation;
            m_Data.instances = instances;                 // 共享引用，非拷贝
            m_Data.entryInstanceIds = entryInstanceIds;   // 共享引用，非拷贝
            m_Data.RebuildIndex();
            return m_Data;
        }

        // 用一份纯 C# 图数据回填本 asset（JSON 导入、迁移工具、测试夹具用）。
        public void FromData(GraphData data)
        {
            if (data == null) return;
            graphId = data.graphId ?? "";
            module = data.module ?? "";
            group = data.group ?? "";
            graphType = data.graphType;
            orientation = data.orientation;
            instances = data.instances ?? new List<NodeInstance>();
            entryInstanceIds = data.entryInstanceIds ?? new List<string>();
            m_Data = null;                                // 下次 ToData() 重建，避免指向旧列表
        }
    }
}
