using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public static class EditorUi
    {
        public const string DarkThemeClass = "ne-theme-dark";
        public const string WindowRootClass = "ne-window-root";
        const string DarkThemePrefKey = "NodeEditor.DarkTheme";

        // 主题切换广播：BindTheme 过的根元素靠它实时换肤。
        public static event Action ThemeChanged;

        // 深色主题（暖炭铜金）开关，EditorPrefs 持久化。写入即广播。
        public static bool DarkTheme
        {
            get => EditorPrefs.GetBool(DarkThemePrefKey, false);
            set
            {
                if (value == DarkTheme) return;
                EditorPrefs.SetBool(DarkThemePrefKey, value);
                ThemeChanged?.Invoke();
            }
        }

        // 每个 styleSheets.Add(NodeEditorStyles) 的挂载元素都要调它：主题 token 定义在
        // :root（浅色）与 .ne-theme-dark（深色）上，:root 匹配的正是挂载元素本身，所以
        // 深色类也必须挂在同一元素上才能在该元素的层叠里覆写 token。脱离面板自动退订，
        // 重新入面板（停靠拖动）再订上。
        public static void BindTheme(VisualElement element)
        {
            if (element == null) return;
            Action apply = () => element.EnableInClassList(DarkThemeClass, DarkTheme);
            apply();
            element.RegisterCallback<AttachToPanelEvent>(_ => { ThemeChanged -= apply; ThemeChanged += apply; apply(); });
            element.RegisterCallback<DetachFromPanelEvent>(_ => ThemeChanged -= apply);
            ThemeChanged += apply;
        }

        public const string HeaderClass = "ne-ui-header";
        public const string EmptyClass = "ne-ui-empty";
        public const string DetailRowClass = "ne-ui-detail-row";
        public const string DetailLabelClass = "ne-ui-detail-label";
        public const string DetailFieldClass = "ne-ui-detail-field";
        public const string ToolbarClass = "ne-toolbar";
        public const string ToolbarCommandClass = "toolbar-command";
        public const string ToolbarIconButtonClass = "toolbar-iconbtn";
        public const string ToolbarTextButtonClass = "toolbar-textbtn";
        public const string ToolbarIconClass = "toolbar-icon";
        public const string ToolbarToggleClass = "ne-toolbar-toggle";
        public const string BannerClass = "graph-banner";
        public const string BannerIssueClass = "graph-banner--issue";
        public const string NodeCueClass = "node-cue";
        public const string BadgeClass = "ne-badge";
        public const string ChipClass = "ne-chip";
        public const string FormRowClass = "ne-form-row";
        public const string FormLabelClass = "ne-form-label";
        public const string FormFieldClass = "ne-form-field";
        public const string FormHelpClass = "ne-form-help";
        public const string FormWarningClass = "ne-form-warning";
        public const string FormErrorClass = "ne-form-error";
        public const string NodeHoverTipClass = "node-hover-tip";
        public const string NodeHoverTitleClass = "node-hover-title";
        public const string NodeHoverDescClass = "node-hover-desc";
        public const string NodeHoverParamClass = "node-hover-param";
        public const string CurrentLanguageTextRowClass = "loc-detail-row";
        public const string CurrentLanguageSelectorRowClass = "loc-language-row";
        public const string CurrentLanguageDropdownClass = "loc-language-dropdown";
        public const string CurrentLanguageTextFieldClass = "loc-detail-text";
        public const int CurrentLanguageTextRowMinHeight = 132;
        public const int CurrentLanguageTextMinHeight = 104;

        public const string TooltipClass = "ne-tooltip";

        public static void ConfigureWindow(VisualElement root)
        {
            if (root == null) return;
            var style = Resources.Load<StyleSheet>("NodeEditorStyles");
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);
            root.AddToClassList(WindowRootClass);
            BindTheme(root);
            InstallTooltip(root);
        }

        // 自绘主题化 tooltip：原生 tooltip 是编辑器铬（跟 Unity 皮肤走深色），与本编辑器主题打架。
        // 在窗口根上拦 TooltipEvent——清空事件文本压掉原生提示，用根下的浮动 Label 按主题显示。
        // 只装在各窗口根（不装 GraphCanvas 等内层挂载点，事件会冒泡到根，装两层会重复处理）。
        public static void InstallTooltip(VisualElement root)
        {
            if (root == null) return;
            Label tip = null;
            VisualElement current = null;
            EventCallback<MouseLeaveEvent> hide = _ => { if (tip != null) tip.style.display = DisplayStyle.None; };

            root.RegisterCallback<TooltipEvent>(evt =>
            {
                // 必须 TrickleDown：TooltipEvent 不冒泡，挂冒泡期在根上永远收不到。
                // 抢跑意味着事件文本还没被目标默认行为填充（是空串），所以文本不依赖事件——
                // 沿目标祖先链找第一个带 tooltip 的元素；随后 StopImmediatePropagation 连原生填充一起掐死。
                var target = evt.target as VisualElement;
                var text = evt.tooltip;
                if (string.IsNullOrEmpty(text))
                {
                    for (var e = target; e != null; e = e.parent)
                        if (!string.IsNullOrEmpty(e.tooltip)) { text = e.tooltip; target = e; break; }
                }
                evt.tooltip = string.Empty;          // 原生提示读到空文本即不弹
                evt.StopImmediatePropagation();
                if (string.IsNullOrEmpty(text)) { hide(null); return; }

                if (tip == null)
                {
                    tip = new Label { pickingMode = PickingMode.Ignore };
                    tip.AddToClassList(TooltipClass);
                    tip.style.position = Position.Absolute;
                    root.Add(tip);
                }

                if (target != current)
                {
                    current?.UnregisterCallback(hide);
                    current = target;
                    current?.RegisterCallback(hide);
                }

                var anchor = target != null ? target.worldBound : evt.rect;
                var local = root.WorldToLocal(new Vector2(anchor.xMin, anchor.yMax));
                tip.text = text;
                tip.style.display = DisplayStyle.Flex;
                tip.style.left = local.x;
                tip.style.top = local.y + 4f;
                tip.BringToFront();
                // 布局出来才知道自身宽高：钳回窗口内，下方放不下就翻到目标上方。
                tip.schedule.Execute(() =>
                {
                    if (tip.resolvedStyle.display == DisplayStyle.None) return;
                    float x = Mathf.Clamp(local.x, 4f, Mathf.Max(4f, root.layout.width - tip.resolvedStyle.width - 4f));
                    float y = local.y + 4f;
                    if (y + tip.resolvedStyle.height > root.layout.height - 4f)
                        y = root.WorldToLocal(new Vector2(0f, anchor.yMin)).y - tip.resolvedStyle.height - 4f;
                    tip.style.left = x;
                    tip.style.top = Mathf.Max(4f, y);
                });
            }, TrickleDown.TrickleDown);

            root.RegisterCallback<MouseLeaveEvent>(hide);
            root.RegisterCallback<PointerDownEvent>(_ => hide(null), TrickleDown.TrickleDown);
        }

        public static Label Header(string text)
        {
            var label = new Label(text ?? "");
            label.AddToClassList(HeaderClass);
            return label;
        }

        public static Label EmptyState(string text)
        {
            var label = new Label(text ?? "");
            label.AddToClassList(EmptyClass);
            return label;
        }

        public static VisualElement DetailRow(string label, VisualElement field)
        {
            var row = new VisualElement();
            row.AddToClassList(DetailRowClass);
            row.AddToClassList(FormRowClass);

            var labelElement = new Label(label ?? "");
            labelElement.AddToClassList(DetailLabelClass);
            labelElement.AddToClassList(FormLabelClass);
            row.Add(labelElement);

            if (field != null)
            {
                field.AddToClassList(DetailFieldClass);
                field.AddToClassList(FormFieldClass);
                field.style.flexShrink = 1;
                field.style.flexBasis = 0;
                field.style.minWidth = 0;
                row.Add(field);
            }

            return row;
        }

        public static Label Badge(string text)
        {
            var label = new Label(text ?? "");
            label.AddToClassList(BadgeClass);
            return label;
        }

        public static Label Chip(string text)
        {
            var label = new Label(text ?? "");
            label.AddToClassList(ChipClass);
            return label;
        }

        public static VisualElement FormRow(string label, VisualElement field) => DetailRow(label, field);

        public static VisualElement CurrentLanguageTextRow(string value, Action<string> onChanged, string tooltip = null)
        {
            var initialLanguage = Localizer.Lang.Code();
            return CurrentLanguageTextRow(
                code => code == initialLanguage ? value : "",
                (code, text) =>
                {
                    if (code == initialLanguage) onChanged?.Invoke(text);
                },
                tooltip);
        }

        public static VisualElement CurrentLanguageTextRow(
            Func<string, string> textForLanguage,
            Action<string, string> onChanged,
            string tooltip = null)
        {
            var row = new VisualElement();
            row.AddToClassList(CurrentLanguageTextRowClass);
            row.style.minHeight = CurrentLanguageTextRowMinHeight;
            row.style.alignItems = Align.Stretch;
            row.style.flexDirection = FlexDirection.Column;
            row.style.flexShrink = 0;

            var languageRow = new VisualElement();
            languageRow.AddToClassList(CurrentLanguageSelectorRowClass);

            var language = Localizer.Lang.Code();
            var choices = LanguageChoices(language);
            var dropdown = new EnumDropdownField(
                null,
                choices,
                language,
                null,
                LanguageOptionsLocator.DisplayName,
                Localizer.UI("ui.language", "Language"));
            dropdown.AddToClassList(CurrentLanguageDropdownClass);
            languageRow.Add(dropdown);
            row.Add(languageRow);

            var field = new TextField { multiline = true, value = textForLanguage?.Invoke(language) ?? "" };
            field.AddToClassList(CurrentLanguageTextFieldClass);
            field.style.flexGrow = 1;
            field.style.flexShrink = 0;
            field.style.minHeight = CurrentLanguageTextMinHeight;
            if (!string.IsNullOrEmpty(tooltip)) field.tooltip = tooltip;
            dropdown.RegisterValueChangedCallback(e =>
            {
                language = e.newValue;
                field.SetValueWithoutNotify(textForLanguage?.Invoke(language) ?? "");
            });
            if (onChanged != null) field.RegisterValueChangedCallback(e => onChanged(language, e.newValue));
            row.Add(field);
            return row;
        }

        static List<string> LanguageChoices(string current)
        {
            var choices = LanguageOptionsLocator.Codes();
            current ??= "";
            if (!string.IsNullOrEmpty(current) && !choices.Contains(current))
                choices.Insert(0, current);
            return choices;
        }

        public static void ApplyToolbarIconButton(Button button)
        {
            if (button == null) return;
            button.AddToClassList(ToolbarCommandClass);
            button.AddToClassList(ToolbarIconButtonClass);
        }

        public static void ApplyToolbarTextButton(Button button)
        {
            if (button == null) return;
            button.AddToClassList(ToolbarCommandClass);
            button.AddToClassList(ToolbarTextButtonClass);
        }

        public static void ApplyToolbarToggle(Toggle toggle)
        {
            if (toggle == null) return;
            toggle.AddToClassList(ToolbarToggleClass);
        }

        public static ToolbarToggle CreateThemeToggle()
        {
            var toggle = new ToolbarToggle
            {
                text = Localizer.UI("ui.darkTheme", "Dark"),
                tooltip = Localizer.UI("ui.darkThemeTip", "Toggle dark theme")
            };
            ApplyToolbarToggle(toggle);
            toggle.SetValueWithoutNotify(DarkTheme);
            toggle.RegisterValueChangedCallback(evt => DarkTheme = evt.newValue);

            Action sync = () => toggle.SetValueWithoutNotify(DarkTheme);
            toggle.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ThemeChanged -= sync;
                ThemeChanged += sync;
                sync();
            });
            toggle.RegisterCallback<DetachFromPanelEvent>(_ => ThemeChanged -= sync);
            return toggle;
        }
    }
}
