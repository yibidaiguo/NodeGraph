// NodeGraphAsset.cs —— 子层 4a（节点数据）ScriptableObject。
// 必须放在与类同名的独立文件中，这样 Unity 才会绑定其 MonoScript（完整理由见 BlackboardAsset.cs ——
// 否则每个保存的 graph .asset 都会得到一个损坏的 m_Script，编辑器将无法加载它）。
// 命名空间 NodeEditor。Runtime/ 程序集。

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
        public List<NodeInstance> instances = new();
        public List<string> entryInstanceIds = new();   // 入口侧列表（控制流 / tick-tree）
    }
}
