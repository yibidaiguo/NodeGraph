// NavigationHistory.cs — 导航历史（后退/前进栈），工具栏后退/前进与面包屑共用。
// 拆自 EditorSupport.cs（B3 内聚拆分：一类型一文件；类型代码逐字未改）。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{
    // ---- 导航历史（后退/前进 + 进入/退出子图） ----
    public class NavigationHistory
    {
        readonly List<NodeGraphAsset> m_Stack = new();   // 从路径根到当前
        int m_Index = -1;

        public bool CanBack => m_Index > 0;
        public bool CanForward => m_Index < m_Stack.Count - 1;

        public void Push(NodeGraphAsset g)
        {
            if (g == null || g == Current) return;

            int existing = m_Stack.FindIndex(x => x == g);
            if (existing >= 0)
            {
                if (existing < m_Stack.Count - 1)
                    m_Stack.RemoveRange(existing + 1, m_Stack.Count - existing - 1);
                m_Index = existing;
                return;
            }

            // 先截断所有前进项，再压栈
            if (m_Index < m_Stack.Count - 1)
                m_Stack.RemoveRange(m_Index + 1, m_Stack.Count - m_Index - 1);
            m_Stack.Add(g);
            m_Index = m_Stack.Count - 1;
        }
        public NodeGraphAsset Back() { if (CanBack) m_Index--; return Current; }
        public NodeGraphAsset Forward() { if (CanForward) m_Index++; return Current; }
        public NodeGraphAsset ClimbTo(int depth)
        {
            if (depth >= 0 && depth < m_Stack.Count) { m_Index = depth; return Current; }
            return null;
        }
        public NodeGraphAsset Current => m_Index >= 0 && m_Index < m_Stack.Count ? m_Stack[m_Index] : null;
        public IEnumerable<string> PathTitles() => m_Stack.Take(m_Index + 1).Select(g => g != null ? g.name : "?");
    }
}
