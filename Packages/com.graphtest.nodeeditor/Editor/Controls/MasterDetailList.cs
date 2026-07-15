using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public class MasterDetailList : VisualElement
    {
        readonly ToolbarSearchField m_Search;
        readonly ScrollView m_Scroll;
        readonly List<DataItem> m_Items = new();
        readonly Dictionary<string, VisualElement> m_Rows = new();
        string m_Filter = "";

        public event Action<DataItem> OnSelectionChanged;
        public string SelectedId { get; private set; }

        public MasterDetailList()
        {
            AddToClassList("ne-master-list");

            m_Search = new ToolbarSearchField();
            m_Search.AddToClassList("ne-master-list-search");
            m_Search.RegisterValueChangedCallback(e =>
            {
                m_Filter = e.newValue ?? "";
                RebuildRows();
            });
            Add(m_Search);

            m_Scroll = new ScrollView(ScrollViewMode.Vertical);
            m_Scroll.AddToClassList("ne-master-list-scroll");
            Add(m_Scroll);
        }

        public void SetItems(IEnumerable<DataItem> items, string selectedId)
        {
            m_Items.Clear();
            m_Items.AddRange(items ?? Array.Empty<DataItem>());

            SelectedId = PickSelectedId(selectedId);
            RebuildRows();
            OnSelectionChanged?.Invoke(Current());
        }

        string PickSelectedId(string requested)
        {
            if (!string.IsNullOrEmpty(requested) && m_Items.Any(i => i.Id == requested))
                return requested;

            return m_Items.FirstOrDefault()?.Id;
        }

        DataItem Current() => m_Items.FirstOrDefault(i => i.Id == SelectedId);

        void Select(DataItem item)
        {
            SelectedId = item?.Id;
            SyncSelection();
            OnSelectionChanged?.Invoke(item);
        }

        void RebuildRows()
        {
            m_Scroll.Clear();
            m_Rows.Clear();

            var visible = m_Items.Where(Matches).ToList();
            if (visible.Count == 0)
            {
                m_Scroll.Add(EditorUi.EmptyState(Localizer.UI("ui.dataEmpty", "(No data)")));
                return;
            }

            string group = null;
            foreach (var item in visible.OrderBy(i => i.Group).ThenBy(i => i.Title))
            {
                if (item.Group != group)
                {
                    group = item.Group;
                    if (!string.IsNullOrEmpty(group))
                    {
                        var header = new Label(group);
                        header.AddToClassList("ne-master-list-group");
                        m_Scroll.Add(header);
                    }
                }

                var row = new Button(() => Select(item));
                row.AddToClassList("ne-master-list-row");

                var title = new Label(item.Title);
                title.AddToClassList("ne-master-list-title");
                row.Add(title);

                if (!string.IsNullOrEmpty(item.Preview))
                {
                    var preview = new Label(item.Preview);
                    preview.AddToClassList("ne-master-list-preview");
                    row.Add(preview);
                }

                m_Rows[item.Id] = row;
                m_Scroll.Add(row);
            }

            SyncSelection();
        }

        bool Matches(DataItem item)
        {
            if (string.IsNullOrWhiteSpace(m_Filter))
                return true;

            return (item.Title?.IndexOf(m_Filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (item.Preview?.IndexOf(m_Filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (item.Group?.IndexOf(m_Filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (item.Id?.IndexOf(m_Filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        void SyncSelection()
        {
            foreach (var row in m_Rows)
                row.Value.EnableInClassList("is-selected", row.Key == SelectedId);
        }
    }
}
