using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class StringSearchWindow : EditorWindow
    {
        List<string> m_Values;
        Func<string, string> m_Display;
        string m_Title;
        char m_Separator;
        Action<string> m_OnSelect;
        string m_Filter = "";
        ScrollView m_List;

        public static void Open(Vector2 screenPos, float width, IEnumerable<string> values,
            string title, char separator, Func<string, string> display, Action<string> onSelect)
        {
            var window = CreateInstance<StringSearchWindow>();
            window.m_Values = values?.ToList() ?? new List<string>();
            window.m_Title = string.IsNullOrEmpty(title) ? Localizer.UI("ui.select", "Select") : title;
            window.m_Separator = separator;
            window.m_Display = display ?? (s => s);
            window.m_OnSelect = onSelect;
            window.titleContent = new GUIContent(window.m_Title);

            // 高度随内容自适应（行 + 分组头 + 搜索/标题镶边），免得条目少时底下一大块空白。
            var seen = new HashSet<string>();
            int lines = 0;
            foreach (var value in window.m_Values)
            {
                var parts = window.Parts(window.m_Display(value) ?? "");
                for (int i = 0; i < parts.Length - 1; i++)
                    if (seen.Add(string.Join(window.m_Separator.ToString(), parts.Take(i + 1)))) lines++;
                lines++;
            }
            var size = new Vector2(Mathf.Max(width, 260f), Mathf.Clamp(72f + lines * 28f, 150f, 420f));

            // 水平方向钳在宿主窗口内：字段贴着检视面板右缘时，弹窗不再悬出编辑器外。
            var pos = screenPos;
            var owner = focusedWindow != null ? focusedWindow.position : default;
            if (owner.width > 0)
                pos.x = Mathf.Max(owner.xMin + 8f, Mathf.Min(pos.x, owner.xMax - size.x - 8f));

            window.ShowAsDropDown(new Rect(pos, Vector2.zero), size);
        }

        void CreateGUI()
        {
            EditorUi.ConfigureWindow(rootVisualElement);
            var root = rootVisualElement;
            root.AddToClassList("string-search-popup");

            // 标题在最上、兼作拖拽把手（ShowAsDropDown 弹窗没有系统标题栏，抓标题条即可挪窗）。
            var title = new Label(m_Title);
            title.AddToClassList("string-search-title");
            Vector2 grab = default;
            title.RegisterCallback<PointerDownEvent>(e =>
            {
                grab = (Vector2)e.position;
                title.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            title.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!title.HasPointerCapture(e.pointerId)) return;
                var delta = (Vector2)e.position - grab;
                position = new Rect(position.position + delta, position.size);
            });
            title.RegisterCallback<PointerUpEvent>(e => title.ReleasePointer(e.pointerId));
            root.Add(title);

            var search = new ToolbarSearchField();
            search.AddToClassList("string-search-field");
            search.RegisterValueChangedCallback(e =>
            {
                m_Filter = e.newValue ?? "";
                RebuildRows();
            });
            root.Add(search);

            m_List = new ScrollView();
            m_List.AddToClassList("string-search-list");
            root.Add(m_List);

            RebuildRows();
            search.Focus();
        }

        void RebuildRows()
        {
            if (m_List == null) return;
            m_List.Clear();

            var groups = new HashSet<string>();
            var items = FilteredItems().ToList();
            foreach (var item in items)
            {
                var parts = Parts(item.Display);
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var path = string.Join(m_Separator.ToString(), parts.Take(i + 1));
                    if (!groups.Add(path)) continue;

                    var group = new Label(parts[i]);
                    group.AddToClassList("string-search-group");
                    group.style.marginLeft = i * 10;
                    m_List.Add(group);
                }

                var row = new Button(() => Select(item.Value)) { text = parts[^1], tooltip = item.Display };
                row.AddToClassList("string-search-row");
                row.style.marginLeft = Math.Max(0, parts.Length - 1) * 10;
                m_List.Add(row);
            }

            if (items.Count != 0) return;

            var empty = new Label(Localizer.UI("ui.noResults", "No results"));
            empty.AddToClassList("string-search-empty");
            m_List.Add(empty);
        }

        IEnumerable<SearchItem> FilteredItems()
        {
            var filter = m_Filter.Trim();
            return m_Values
                .Select(value => new SearchItem(value, m_Display(value) ?? ""))
                .Where(item => string.IsNullOrEmpty(filter) ||
                    item.Display.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(item => item.Display, StringComparer.Ordinal);
        }

        string[] Parts(string display)
        {
            if (m_Separator == '\0') return new[] { display };
            var parts = display.Split(m_Separator).Where(part => !string.IsNullOrEmpty(part)).ToArray();
            return parts.Length == 0 ? new[] { display } : parts;
        }

        void Select(string value)
        {
            m_OnSelect?.Invoke(value);
            Close();
        }

        readonly struct SearchItem
        {
            public SearchItem(string value, string display)
            {
                Value = value;
                Display = display;
            }

            public string Value { get; }
            public string Display { get; }
        }
    }
}
