// LocalizationTablePane.cs — 集中本地化表（LocalizationTable）的可复用编辑面板。Editor/ 程序集。
// 这是数据层里此前唯一没有编辑界面的资产（只能手点默认 Inspector 改 list）——本面板补齐它：
// 按 key 前缀（ui./node./param./var./…）分组、可搜索过滤，每个 key 一行当前编辑器语言文本框，支持增删 key。
// 任何编辑器面板都能直接 new 出来用（数据窗口的「本地化」源即用它）；写值经 SerializedObject，
// Undo / 标脏由 ApplyModifiedProperties 自动处理（与 DialogueDatabaseEditor 同一写入idiom）。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public class LocalizationTablePane : VisualElement
    {
        public const string DetailTextRowClass = EditorUi.CurrentLanguageTextRowClass;
        public const int DetailTextRowMinHeight = EditorUi.CurrentLanguageTextRowMinHeight;
        public const string DetailTextFieldClass = EditorUi.CurrentLanguageTextFieldClass;
        public const int DetailTextMinHeight = EditorUi.CurrentLanguageTextMinHeight;

        readonly LocalizationTable m_Table;
        readonly SerializedObject m_SO;
        readonly SerializedProperty m_Entries;
        readonly VisualElement m_List;
        string m_Filter = "";

        public static IEnumerable<DataItem> Items(LocalizationTable table)
        {
            if (table == null) yield break;
            foreach (var key in table.Entries
                         .Select(e => e.key)
                         .Where(k => !string.IsNullOrEmpty(k))
                         .Distinct()
                         .OrderBy(k => k))
            {
                var group = Prefix(key);
                var preview = TextForDisplay(table, key);
                yield return new DataItem(key, key, group, preview, key);
            }
        }

        public static VisualElement BuildDetail(LocalizationTable table, DataItem item)
        {
            var root = new VisualElement();
            if (table == null || item == null)
            {
                root.Add(EditorUi.EmptyState(Localizer.UI("ui.noLocTable", "Localization table not found. Run your module's Setup Assets first.")));
                return root;
            }

            var key = item.Id;
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.key", "Key"), new Label(key)));
            root.Add(EditorUi.CurrentLanguageTextRow(
                code => table.Get(key, LanguageFromCode(code)) ?? "",
                (code, value) =>
            {
                Undo.RegisterCompleteObjectUndo(table, "Edit Localization");
                table.Set(key, LanguageFromCode(code), value);
                EditorUtility.SetDirty(table);
            }));
            return root;
        }

        static string TextForDisplay(LocalizationTable table, string key)
        {
            return table.Get(key, Localizer.Lang);
        }

        public LocalizationTablePane(LocalizationTable table)
        {
            m_Table = table;
            AddToClassList("loc-root");

            if (m_Table == null)
            {
                var hint = new Label(Localizer.UI("ui.noLocTable", "Localization table not found. Run your module's Setup Assets first."));
                hint.AddToClassList("field-note");
                Add(hint);
                return;
            }

            m_SO = new SerializedObject(m_Table);
            m_Entries = m_SO.FindProperty("entries");

            // 搜索框：按 key 子串过滤（表会很大）。
            var search = new ToolbarSearchField();
            search.AddToClassList("loc-search");
            search.RegisterValueChangedCallback(e => { m_Filter = e.newValue ?? ""; Refresh(); });
            Add(search);

            m_List = new VisualElement();
            Add(m_List);

            // 新增 key：输入框 + 按钮，建出该 key 的当前编辑器语言空条目（其余语言按需补）。
            var addRow = new VisualElement();
            addRow.AddToClassList("field-row");
            var newKey = new TextField() { value = "" };
            newKey.style.flexGrow = 1;
            addRow.Add(newKey);
            var addBtn = new Button(() => { AddKey(newKey.value); newKey.value = ""; }) { text = Localizer.UI("ui.addKey", "+ Key") };
            addBtn.AddToClassList("add-button");
            addRow.Add(addBtn);
            Add(addRow);

            Refresh();
        }

        // 表里所有 distinct key（按首次出现顺序）。
        IEnumerable<string> Keys()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < m_Entries.arraySize; i++)
            {
                var k = m_Entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue;
                if (!string.IsNullOrEmpty(k) && seen.Add(k)) yield return k;
            }
        }

        void Refresh()
        {
            m_SO.Update();
            m_List.Clear();

            var keys = Keys().Where(k => string.IsNullOrEmpty(m_Filter)
                                         || k.IndexOf(m_Filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // 按 key 第一段前缀（'.' 之前）分组，便于在大表里定位（ui./node./param./var.…）。
            // 分组与组内每条 key 卡片都做成可折叠；key 多（> 3）的组默认收起，避免大表全展开撑爆布局。
            foreach (var grp in keys.GroupBy(Prefix).OrderBy(g => g.Key))
            {
                var groupKeys = grp.ToList();
                bool expanded = CollapsibleCard.AutoExpanded(groupKeys.Count);
                var section = new CollapsibleCard(expanded);
                section.AddToClassList("inspector-section");
                var title = new Label(grp.Key);
                title.AddToClassList("inspector-section-title");
                section.HeaderMid.Add(title);
                foreach (var key in groupKeys) section.Content.Add(BuildKeyCard(key, expanded));
                m_List.Add(section);
            }

            if (keys.Count == 0)
            {
                var empty = new Label(Localizer.UI("ui.dataEmpty", "(no data)"));
                empty.AddToClassList("field-note");
                m_List.Add(empty);
            }
        }

        static string Prefix(string key)
        {
            int dot = key.IndexOf('.');
            return dot > 0 ? key.Substring(0, dot) : key;
        }

        VisualElement BuildKeyCard(string key, bool expanded)
        {
            var card = new CollapsibleCard(expanded);
            card.AddToClassList("entry-card");

            // 头部：key（只读标签，改 key 易错——改名= 删旧建新）+ 删除。
            var keyLabel = new Label(key) { tooltip = key };
            keyLabel.AddToClassList("variable-key");
            keyLabel.style.flexGrow = 1;
            card.HeaderMid.Add(keyLabel);
            var remove = new Button(() => RemoveKey(key)) { text = "✕" };   // 统一删除记号（与 UnitInspector 同；纯符号豁免铁律#5）
            remove.AddToClassList("toolbar-iconbtn");
            card.HeaderRight.Add(remove);

            // 只显示当前编辑器语言；切换语言后重建面板即可编辑另一种语言。
            var lang = Localizer.Lang;
            var row = EditorUi.CurrentLanguageTextRow(
                code => m_Table.Get(key, LanguageFromCode(code)) ?? "",
                (code, value) => WriteText(key, LanguageFromCode(code), value));
            card.Content.Add(row);
            return card;
        }

        static Language LanguageFromCode(string code)
        {
            foreach (Language language in Enum.GetValues(typeof(Language)))
                if (language.Code() == code)
                    return language;
            return Localizer.Lang;
        }

        // 写一条 (key, lang) 文本：找到对应条目则改，否则新建一条。经 SerializedObject → Undo + 标脏自动。
        void WriteText(string key, Language lang, string text)
        {
            m_SO.Update();
            int idx = IndexOf(key, lang);
            if (idx < 0)
            {
                idx = m_Entries.arraySize;
                m_Entries.InsertArrayElementAtIndex(idx);
                var e = m_Entries.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("key").stringValue = key;
                e.FindPropertyRelative("language").enumValueIndex = (int)lang;
            }
            m_Entries.GetArrayElementAtIndex(idx).FindPropertyRelative("text").stringValue = text;
            m_SO.ApplyModifiedProperties();
        }

        int IndexOf(string key, Language lang)
        {
            for (int i = 0; i < m_Entries.arraySize; i++)
            {
                var e = m_Entries.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("key").stringValue == key
                    && e.FindPropertyRelative("language").enumValueIndex == (int)lang) return i;
            }
            return -1;
        }

        void AddKey(string key)
        {
            key = key?.Trim();
            if (string.IsNullOrEmpty(key)) return;
            if (Keys().Contains(key)) { Refresh(); return; }   // 已存在则只刷新定位
            WriteText(key, Localizer.Lang, "");                 // 建一条当前编辑器语言空条目占位
            Refresh();
        }

        // 删 key = 删它的全部语言条目（从尾往前删，避免索引位移）。
        void RemoveKey(string key)
        {
            m_SO.Update();
            for (int i = m_Entries.arraySize - 1; i >= 0; i--)
                if (m_Entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == key)
                    m_Entries.DeleteArrayElementAtIndex(i);
            m_SO.ApplyModifiedProperties();
            Refresh();
        }
    }
}
