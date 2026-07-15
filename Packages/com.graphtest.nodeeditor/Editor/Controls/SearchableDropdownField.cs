// SearchableDropdownField.cs — 第 5 层（连线图编辑器）通用可复用控件。
// 一个"可搜索下拉字段"，可选允许临时键入新值（combobox）：
//   · allowCustom=false（默认）：一个像下拉的按钮，点击弹出 StringSearchWindow 从候选里选（取值必须是已存在项）。
//   · allowCustom=true：一个可编辑文本框 + 右侧 ▾ 按钮——既能从候选里选，也能直接键入一个新名字
//     （如还没建出来的 Label 目标）。
// 候选用 Func 惰性取（每次打开都拿最新）；弹窗定位需要面板→屏幕换算，由宿主注入。
// 与具体面板解耦——Inspector、卡片编辑器、未来领域工具都可直接 new 出来复用。Unity 6。Editor/ 程序集。

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class SearchableDropdownField : VisualElement
    {
        readonly string m_Label;
        readonly Func<List<string>> m_Choices;
        readonly Action<string> m_OnChanged;
        readonly bool m_AllowCustom;
        readonly char m_Separator;            // '\0' 平铺；否则把点分 key 折成分类树
        readonly Func<string, string> m_Display;
        readonly Func<Vector2, Vector2> m_PanelToScreen;

        readonly TextField m_Text;            // allowCustom 时：可编辑输入框
        readonly Button m_Button;             // 非 allowCustom 时：显示当前值、点击弹选择器
        readonly Label m_ValueLabel;          // 按钮内的「当前值」文本（左对齐撑满；▾ 由 USS 钉在右缘）
        string m_Value;

        // choices：惰性候选（打开选择器时调用，保证最新）。display：把空值/哨兵显示成友好文案（仅影响展示）。
        // panelToScreen：把字段的面板坐标换成屏幕坐标以定位 SearchWindow（宿主注入；为 null 时退回 focusedWindow）。
        public SearchableDropdownField(string label, Func<List<string>> choices, string value,
            Action<string> onChanged, bool allowCustom = false, char separator = '\0',
            Func<string, string> display = null, Func<Vector2, Vector2> panelToScreen = null, string tooltip = null)
        {
            m_Label = label;
            m_Choices = choices ?? (() => new List<string>());
            m_OnChanged = onChanged;
            m_AllowCustom = allowCustom;
            m_Separator = separator;
            m_Display = display ?? (s => s);
            m_PanelToScreen = panelToScreen;
            m_Value = value ?? "";

            AddToClassList("field-row");
            this.tooltip = tooltip;

            var lab = new Label(label);
            lab.AddToClassList("field-label");
            lab.AddToClassList("ne-ui-detail-label");
            Add(lab);

            if (allowCustom)
            {
                // 可编辑：键入即提交（与普通文本字段一致）；▾ 仍可从候选里挑。
                m_Text = new TextField { value = m_Value };
                m_Text.AddToClassList("choice-text");
                m_Text.AddToClassList("ne-ui-detail-field");
                m_Text.RegisterValueChangedCallback(e => { m_Value = e.newValue; m_OnChanged?.Invoke(m_Value); });
                Add(m_Text);
                var arrow = new Button(OpenPicker) { text = "▾" };
                arrow.AddToClassList("choice-arrow");
                Add(arrow);
            }
            else
            {
                // 像原生 PopupField：当前值文本左对齐撑满 + ▾ 钉在右缘（由 .choice-value / .choice-caret 布局）。
                // 不再把 ▾ 拼进文本——那样箭头会跟在文本末尾浮在中间，与原生下拉对不齐。
                m_Button = new Button(OpenPicker);
                m_Button.AddToClassList("choice-input");
                m_Button.AddToClassList("ne-ui-detail-field");
                m_ValueLabel = new Label(ValueText(m_Value));
                m_ValueLabel.AddToClassList("choice-value");
                m_Button.Add(m_ValueLabel);
                var caret = new Label("▾");
                caret.AddToClassList("choice-caret");
                m_Button.Add(caret);
                Add(m_Button);
            }
        }

        string ValueText(string v)
        {
            var d = m_Display(v);
            return string.IsNullOrEmpty(d) ? Localizer.UI("ui.none", "(None)") : d;
        }

        // 从选择器选中：刷新显示并通知一次（文本模式用 SetValueWithoutNotify 避免再触发自身回调而双写）。
        void Commit(string v)
        {
            m_Value = v ?? "";
            m_Text?.SetValueWithoutNotify(m_Value);
            if (m_ValueLabel != null) m_ValueLabel.text = ValueText(m_Value);
            m_OnChanged?.Invoke(m_Value);
        }

        void OpenPicker()
        {
            var anchor = (VisualElement)m_Text ?? m_Button;
            var origin = anchor.worldBound.position + new Vector2(0, anchor.worldBound.height);
            var screen = m_PanelToScreen != null ? m_PanelToScreen(origin) : FallbackScreen(origin);
            StringSearchWindow.Open(screen, anchor.worldBound.width, m_Choices(), m_Label, m_Separator, m_Display, Commit);
        }

        // 没注入换算时的退路：用当前聚焦的编辑器窗口原点 + 面板坐标。
        static Vector2 FallbackScreen(Vector2 panelPos)
        {
            var w = EditorWindow.focusedWindow;
            return (w != null ? w.position.position : Vector2.zero) + panelPos;
        }
    }
}
