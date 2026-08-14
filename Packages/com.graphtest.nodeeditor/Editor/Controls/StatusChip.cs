// StatusChip.cs — 第 5 层（连线图编辑器），共享控件。
// 顶栏最右的校验状态胶囊：无问题 / N 警告 / N 错误。有问题时可点，逐个巡回到出问题的节点上
// —— 旧外壳只把数字摆在那里，看见"2 错误"也不知道错在哪个节点。Editor/ 程序集。

using System;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class StatusChip : Button
    {
        public const string RootClass = "ne-status-chip";
        public const string PipClass = "ne-status-chip-pip";
        public const string TextClass = "ne-status-chip-text";
        public const string WarnClass = "ne-status-chip--warn";
        public const string ErrorClass = "ne-status-chip--error";

        readonly Label m_Text;

        public StatusChip(Action onClick) : base(onClick)
        {
            AddToClassList(RootClass);

            var pip = new VisualElement();
            pip.AddToClassList(PipClass);
            Add(pip);

            m_Text = new Label();
            m_Text.AddToClassList(TextClass);
            Add(m_Text);
        }

        public void SetCounts(int errors, int warnings, bool hasGraph)
        {
            style.display = hasGraph ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList(ErrorClass, errors > 0);
            EnableInClassList(WarnClass, errors == 0 && warnings > 0);
            // 没问题时它只是个读数，点了也无处可去 —— 置灰以免看起来像个按钮。
            SetEnabled(errors > 0 || warnings > 0);

            if (errors == 0 && warnings == 0)
            {
                m_Text.text = Localizer.UI("ui.noIssues", "No issues");
                tooltip = Localizer.UI("ui.noIssues", "No issues");
                return;
            }
            m_Text.text = string.Format(Localizer.UI("ui.issueCount", "{0} errors · {1} warnings"), errors, warnings);
            tooltip = Localizer.UI("ui.issueJumpTip", "Click to jump to the next problem node");
        }
    }
}
