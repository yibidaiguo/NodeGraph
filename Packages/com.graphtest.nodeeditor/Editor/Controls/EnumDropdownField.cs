// EnumDropdownField.cs — 第 5 层（连线图编辑器）通用可复用控件。数据-UI-重设计规范（§1 Shared UI Kit）
// 要求的「枚举下拉」：用于**有限固定值**（枚举 / 真假 / 语言码 / 条目用途…），渲染为 Unity 原生
// PopupField——因此自带原生下拉箭头、悬停、键盘导航，外观与编辑器其它原生字段一致（即设计师看到的「用途」那种）。
//   · 可传 display 把原始值映射为本地化显示名（"localized labels where available"）。
//   · 自动保证「当前值在候选里」（不在则插到最前），避免 PopupField 抛「默认值不在列表」。
// 与 [SearchableDropdownField] 分工：候选**有限固定**→本控件（原生、无搜索框）；候选**很多/动态**（库 key、
// 黑板 key、单元类型路径）→ SearchableDropdownField（按钮 + 可搜索弹窗）。两者共用同一行对齐（label 宽度类）。
// Unity 6。Editor/ 程序集。NodeEditor.Editor 不依赖任何领域类型。

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class EnumDropdownField : PopupField<string>
    {
        // onChanged 可空：有的调用方（如建变量弹窗）只在提交时读 .value，不需要逐次回调。
        public EnumDropdownField(string label, List<string> choices, string value, Action<string> onChanged = null,
            Func<string, string> display = null, string tooltip = null)
            : base(label, Prepared(choices, value), Prepared(choices, value).IndexOf(value ?? ""))
        {
            if (display != null) { formatSelectedValueCallback = display; formatListItemCallback = display; }
            this.tooltip = tooltip;
            AddToClassList("enum-dropdown");
            // 扩展方法需显式 this 接收者（裸调用会被当成实例方法而找不到）。
            this.RegisterValueChangedCallback(e => onChanged?.Invoke(e.newValue));
        }

        // 候选副本 + 保证当前值在内（不在则插到最前），避免 PopupField 因默认值缺席而抛异常。
        // 两次调用结果一致（确定性），故 base() 里既用它取列表又用它定位下标是安全的。
        static List<string> Prepared(List<string> choices, string value)
        {
            var list = choices != null ? new List<string>(choices) : new List<string>();
            value ??= "";
            if (!list.Contains(value)) list.Insert(0, value);
            return list;
        }
    }
}
