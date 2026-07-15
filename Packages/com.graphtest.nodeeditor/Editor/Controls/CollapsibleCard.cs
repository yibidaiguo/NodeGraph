// CollapsibleCard.cs — 可复用的折叠原语。Editor/ 程序集。
// 数据编辑器里一条记录的字段一多、记录一多就撑得很长。这个原语把「标题 + 一堆内容」收成可开合的卡片：
// 头部三槽 [▾箭头按钮][标题/摘要槽][操作槽(如删除)]，下面是可折叠的内容容器。
// 代码库一贯手搓 UI（不用 Unity Foldout），且卡片头部要容纳可编辑字段 + 按钮——整行点击会和编辑冲突，
// 所以这里用一个**专用箭头按钮**切换开合，标题/字段保持可交互。容器只加结构 class，
// 底色等视觉沿用调用方追加的现有 class（如 entry-card / inspector-section），复用既有视觉语言。

using System;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class CollapsibleCard : VisualElement
    {
        readonly Button m_Arrow;
        readonly VisualElement m_Content;
        bool m_Expanded;

        public VisualElement HeaderMid { get; }     // 调用方填标题 / 摘要（可含可编辑字段）
        public VisualElement HeaderRight { get; }    // 调用方填操作按钮（如删除）
        public VisualElement Content => m_Content;   // 调用方填字段 / 子列表
        // 开合状态变化回调（调用方据此持久化"哪些组被收起"，使分组在 Reload 后保持开合）。
        // 构造期的初始 SetExpanded 早于调用方订阅，故不会误触发。
        public Action<bool> OnExpandedChanged;

        public CollapsibleCard(bool expanded = true)
        {
            AddToClassList("collapsible-card");

            var header = new VisualElement();
            header.AddToClassList("collapsible-header");

            m_Arrow = new Button(Toggle);
            m_Arrow.AddToClassList("collapsible-arrow");
            header.Add(m_Arrow);

            HeaderMid = new VisualElement();
            HeaderMid.AddToClassList("collapsible-header-mid");
            header.Add(HeaderMid);

            HeaderRight = new VisualElement();
            HeaderRight.AddToClassList("collapsible-header-right");
            header.Add(HeaderRight);

            Add(header);

            m_Content = new VisualElement();
            m_Content.AddToClassList("collapsible-content");
            Add(m_Content);

            SetExpanded(expanded);
        }

        void Toggle() => SetExpanded(!m_Expanded);

        public void SetExpanded(bool expanded)
        {
            m_Expanded = expanded;
            m_Arrow.text = expanded ? "▾" : "▸";
            m_Content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            OnExpandedChanged?.Invoke(expanded);
        }

        // 集中的「按数量自动」阈值：同级记录 / 行数 ≤ 3 默认展开，否则默认收起。
        public static bool AutoExpanded(int siblingCount) => siblingCount <= 3;
    }
}
