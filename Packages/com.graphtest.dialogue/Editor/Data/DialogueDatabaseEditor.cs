// DialogueDatabaseEditor.cs — DialogueDatabase 的 UI Toolkit 自定义 inspector。
// 让文案作者无需触碰节点图即可管理本地化的对白/选项内容（条目 + 各语言文本），
// 与 NodeEditor/Editor/Inspector/InspectorPane.cs + NodeEditorStyles.uss 建立的
// inspector-section/field-row/add-button 视觉语言保持一致。仅 Editor/ 程序集。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Dialogue;
using NodeEditor;
using NodeEditor.EditorUI;

namespace Dialogue.EditorUI
{
    [CustomEditor(typeof(DialogueDatabase))]
    public class DialogueDatabaseEditor : Editor
    {
        SerializedProperty m_Entries;
        SerializedProperty m_DefaultLang;
        VisualElement m_EntriesContainer;
        HelpBox m_DuplicateKeyWarning;

        public static string FirstUnusedLanguageCode(IEnumerable<string> choices, IEnumerable<string> used)
        {
            var finiteChoices = (choices ?? Enumerable.Empty<string>())
                .Select(c => c?.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();
            if (finiteChoices.Count == 0) finiteChoices.Add(Language.English.Code());

            var usedSet = new HashSet<string>((used ?? Enumerable.Empty<string>()).Where(c => !string.IsNullOrEmpty(c)));
            return finiteChoices.FirstOrDefault(c => !usedSet.Contains(c)) ?? finiteChoices[0];
        }

        public static IEnumerable<DataItem> Items(DialogueDatabase db)
        {
            if (db == null) yield break;
            foreach (var key in db.Keys.OrderBy(k => k))
            {
                var entry = db.Find(key);
                if (entry == null) continue;
                var group = Prefix(key);
                var zh = entry.texts.FirstOrDefault(t => t.lang == "zh")?.text;
                var en = entry.texts.FirstOrDefault(t => t.lang == "en")?.text;
                var preview = $"{entry.speaker} {(string.IsNullOrEmpty(zh) ? en : zh)}".Trim();
                yield return new DataItem(key, key, group, preview, key);
            }
        }

        public static VisualElement BuildEntryDetail(DialogueDatabase db, DataItem item)
        {
            var root = new VisualElement();
            if (db == null || item == null)
            {
            root.Add(EditorUi.EmptyState(Localizer.UI("ui.noDatabase", "Dialogue database not found. Run Setup Assets in Tools/NodeGraph/Manager first.")));
                return root;
            }

            var entry = db.Find(item.Id);
            if (entry == null)
            {
                root.Add(EditorUi.EmptyState(Localizer.UI("ui.dataEmpty", "(no data)")));
                return root;
            }

            root.Add(EditorUi.DetailRow(Localizer.UI("ui.key", "Key"), new Label(entry.key)));

            // 用途：决定该 key 进哪个下拉（台词键 / 选项键）。走共享 EnumDropdownField（原生枚举下拉 + 本地化显示名）。
            var kind = new EnumDropdownField(null, KindNames(), KindNames()[(int)entry.kind],
                v =>
                {
                    Undo.RegisterCompleteObjectUndo(db, "Edit Dialogue Entry Kind");
                    entry.kind = (DialogueEntryKind)KindNames().IndexOf(v);
                    EditorUtility.SetDirty(db);
                });
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.entryKind", "Used As"), kind));

            var speaker = new TextField { value = entry.speaker };
            speaker.RegisterValueChangedCallback(e =>
            {
                Undo.RegisterCompleteObjectUndo(db, "Edit Dialogue Speaker");
                entry.speaker = e.newValue;
                EditorUtility.SetDirty(db);
            });
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.speaker", "Speaker"), speaker));

            var portrait = new ObjectField { objectType = typeof(Sprite), value = entry.portrait };
            portrait.RegisterValueChangedCallback(e =>
            {
                Undo.RegisterCompleteObjectUndo(db, "Edit Dialogue Portrait");
                entry.portrait = e.newValue as Sprite;
                EditorUtility.SetDirty(db);
            });
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.portrait", "Portrait"), portrait));

            var voice = new ObjectField { objectType = typeof(AudioClip), value = entry.voice };
            voice.RegisterValueChangedCallback(e =>
            {
                Undo.RegisterCompleteObjectUndo(db, "Edit Dialogue Voice");
                entry.voice = e.newValue as AudioClip;
                EditorUtility.SetDirty(db);
            });
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.voice", "Voice"), voice));

            root.Add(EditorUi.CurrentLanguageTextRow(
                code => LanguageText(entry, code),
                (code, value) =>
                {
                    Undo.RegisterCompleteObjectUndo(db, "Edit Dialogue Text");
                    SetLanguageText(entry, code, value);
                    EditorUtility.SetDirty(db);
                },
                Localizer.UI("ui.localizedText", "Localized Text")));
            return root;
        }

        static string LanguageText(DialogueLineEntry entry, string lang)
        {
            return entry?.texts.FirstOrDefault(t => t.lang == lang)?.text ?? "";
        }

        static void SetLanguageText(DialogueLineEntry entry, string lang, string text)
        {
            if (entry == null) return;
            lang = string.IsNullOrWhiteSpace(lang) ? Localizer.Lang.Code() : lang.Trim();
            var localized = entry.texts.FirstOrDefault(t => t.lang == lang);
            if (localized == null)
            {
                localized = new LocalizedText { lang = lang };
                entry.texts.Add(localized);
            }
            localized.text = text;
        }

        static string Prefix(string key)
        {
            var dot = key?.IndexOf('.') ?? -1;
            return dot > 0 ? key.Substring(0, dot) : Localizer.UI("ui.general", "General");
        }

        void OnEnable()
        {
            m_Entries = serializedObject.FindProperty("entries");
            m_DefaultLang = serializedObject.FindProperty("defaultLang");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var styleSheet = Resources.Load<StyleSheet>("NodeEditorStyles");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            EditorUi.BindTheme(root);
            EditorUi.InstallTooltip(root);
            root.AddToClassList("inspector-root");

            var header = new Label(Localizer.UI("ui.dialogueDatabase", "Dialogue Database"));
            header.AddToClassList("inspector-header");
            root.Add(header);

            var body = new VisualElement();
            body.AddToClassList("inspector-body");
            root.Add(body);

            // ---- general 区段：defaultLang（可折叠，默认展开——字段少）----
            var general = new NodeEditor.EditorUI.CollapsibleCard(true);
            general.AddToClassList("inspector-section");
            var generalTitle = new Label(Localizer.UI("ui.general", "General"));
            generalTitle.AddToClassList("inspector-section-title");
            general.HeaderMid.Add(generalTitle);

            var defaultLangPopup = LanguagePopup(Localizer.UI("ui.defaultLanguage", "Default Language"), m_DefaultLang.stringValue, v =>
            {
                Undo.RecordObject(target, "Edit Dialogue Database Default Lang");
                serializedObject.Update();
                m_DefaultLang.stringValue = v;
                serializedObject.ApplyModifiedProperties();
            });
            general.Content.Add(defaultLangPopup);
            body.Add(general);

            // ---- entries 区段（可折叠：条目 > 3 默认收起，避免大表全展开撑爆布局）----
            var entriesSection = new NodeEditor.EditorUI.CollapsibleCard(
                NodeEditor.EditorUI.CollapsibleCard.AutoExpanded(m_Entries.arraySize));
            entriesSection.AddToClassList("inspector-section");
            var entriesTitle = new Label(Localizer.UI("ui.entries", "Entries"));
            entriesTitle.AddToClassList("inspector-section-title");
            entriesSection.HeaderMid.Add(entriesTitle);

            m_DuplicateKeyWarning = new HelpBox("", HelpBoxMessageType.Warning) { style = { display = DisplayStyle.None } };
            entriesSection.Content.Add(m_DuplicateKeyWarning);

            m_EntriesContainer = new VisualElement();
            entriesSection.Content.Add(m_EntriesContainer);

            var addEntry = new Button(AddEntry) { text = Localizer.UI("ui.addEntry", "+ Entry") };
            addEntry.AddToClassList("add-button");
            entriesSection.Content.Add(addEntry);

            body.Add(entriesSection);

            RefreshEntries();
            return root;
        }

        void RefreshEntries()
        {
            m_EntriesContainer.Clear();
            // 条目多（> 3）时每条卡片默认收起，少时默认展开。
            bool expanded = NodeEditor.EditorUI.CollapsibleCard.AutoExpanded(m_Entries.arraySize);
            for (int i = 0; i < m_Entries.arraySize; i++)
                m_EntriesContainer.Add(BuildEntryRow(i, expanded));
            RefreshDuplicateKeyWarning();
        }

        void RefreshDuplicateKeyWarning()
        {
            var dupes = ((DialogueDatabase)target).DuplicateKeys().ToList();
            if (dupes.Count == 0)
            {
                m_DuplicateKeyWarning.style.display = DisplayStyle.None;
                return;
            }
            m_DuplicateKeyWarning.text = Localizer.UI("ui.duplicateKeys", "Duplicate keys: runtime uses the first match.") + " " + string.Join(", ", dupes);
            m_DuplicateKeyWarning.style.display = DisplayStyle.Flex;
        }

        VisualElement BuildEntryRow(int index, bool expanded)
        {
            var entryProp = m_Entries.GetArrayElementAtIndex(index);
            var keyProp = entryProp.FindPropertyRelative("key");
            var kindProp = entryProp.FindPropertyRelative("kind");
            var speakerProp = entryProp.FindPropertyRelative("speaker");
            var portraitProp = entryProp.FindPropertyRelative("portrait");
            var voiceProp = entryProp.FindPropertyRelative("voice");
            var textsProp = entryProp.FindPropertyRelative("texts");

            var card = new NodeEditor.EditorUI.CollapsibleCard(expanded);
            card.AddToClassList("entry-card");

            // 头部行：key 字段（仍可编辑——折叠用专用箭头，不抢字段交互） + 移除按钮
            var keyField = new TextField(Localizer.UI("ui.key", "Key")) { value = keyProp.stringValue };
            keyField.style.flexGrow = 1;
            keyField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(target, "Edit Dialogue Entry Key");
                serializedObject.Update();
                keyProp.stringValue = e.newValue;
                serializedObject.ApplyModifiedProperties();
                RefreshDuplicateKeyWarning();
            });
            card.HeaderMid.Add(keyField);

            var removeEntry = new Button(() => RemoveEntry(index)) { text = Localizer.UI("ui.remove", "Remove") };
            removeEntry.AddToClassList("add-button");
            card.HeaderRight.Add(removeEntry);

            // 用途：台词/选项/通用（决定该 key 进哪个下拉）。走共享 EnumDropdownField；enumValueIndex 即序数，与 KindNames 顺序对应。
            var kindField = new EnumDropdownField(Localizer.UI("ui.entryKind", "Used As"), KindNames(), KindNames()[kindProp.enumValueIndex],
                v =>
                {
                    Undo.RecordObject(target, "Edit Dialogue Entry Kind");
                    serializedObject.Update();
                    kindProp.enumValueIndex = KindNames().IndexOf(v);
                    serializedObject.ApplyModifiedProperties();
                });
            card.Content.Add(kindField);

            // 说话者
            var speakerField = new TextField(Localizer.UI("ui.speaker", "Speaker")) { value = speakerProp.stringValue };
            speakerField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(target, "Edit Dialogue Entry Speaker");
                serializedObject.Update();
                speakerProp.stringValue = e.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            card.Content.Add(speakerField);

            // portrait / voice 通过 PropertyField（已绑定——Undo/dirty 由绑定本身处理）
            var portraitField = new PropertyField(portraitProp, Localizer.UI("ui.portrait", "Portrait"));
            portraitField.Bind(serializedObject);
            card.Content.Add(portraitField);

            var voiceField = new PropertyField(voiceProp, Localizer.UI("ui.voice", "Voice"));
            voiceField.Bind(serializedObject);
            card.Content.Add(voiceField);

            // ---- 本地化文本：内部多行列表，包成嵌套可折叠卡片（语言 > 3 默认收起）----
            var textsCard = new NodeEditor.EditorUI.CollapsibleCard(
                NodeEditor.EditorUI.CollapsibleCard.AutoExpanded(textsProp.arraySize));
            var textsTitle = new Label(Localizer.UI("ui.localizedText", "Localized Text"));
            textsTitle.AddToClassList("inspector-section-title");
            textsCard.HeaderMid.Add(textsTitle);

            textsCard.Content.Add(BuildCurrentTextRow(textsProp));

            card.Content.Add(textsCard);

            return card;
        }

        VisualElement BuildCurrentTextRow(SerializedProperty textsProp)
        {
            return EditorUi.CurrentLanguageTextRow(
                code => CurrentSerializedText(textsProp, code),
                (code, value) =>
                {
                    Undo.RecordObject(target, "Edit Dialogue Localized Text");
                    serializedObject.Update();
                    EnsureSerializedTextProperty(textsProp, code).stringValue = value;
                    serializedObject.ApplyModifiedProperties();
                },
                Localizer.UI("ui.localizedText", "Localized Text"));
        }

        static string CurrentSerializedText(SerializedProperty textsProp, string lang)
        {
            var textProp = FindSerializedTextProperty(textsProp, lang);
            return textProp?.stringValue ?? "";
        }

        static SerializedProperty EnsureSerializedTextProperty(SerializedProperty textsProp, string lang)
        {
            var textProp = FindSerializedTextProperty(textsProp, lang);
            if (textProp != null) return textProp;

            var index = textsProp.arraySize;
            textsProp.InsertArrayElementAtIndex(index);
            var elem = textsProp.GetArrayElementAtIndex(index);
            elem.FindPropertyRelative("lang").stringValue = lang;
            textProp = elem.FindPropertyRelative("text");
            textProp.stringValue = "";
            return textProp;
        }

        static SerializedProperty FindSerializedTextProperty(SerializedProperty textsProp, string lang)
        {
            for (int i = 0; i < textsProp.arraySize; i++)
            {
                var elem = textsProp.GetArrayElementAtIndex(i);
                if (elem.FindPropertyRelative("lang").stringValue == lang)
                    return elem.FindPropertyRelative("text");
            }
            return null;
        }

        // 语言码下拉：走共享 EnumDropdownField（原生枚举下拉 + code→显示名 本地化）。
        static PopupField<string> LanguagePopup(string label, string current, System.Action<string> onChanged) =>
            new EnumDropdownField(label, LanguageChoices(current), current, onChanged,
                display: FormatLanguage, tooltip: Localizer.UI("ui.language", "Language"));

        static List<string> LanguageChoices(string current = null)
        {
            var choices = LanguageOptionsLocator.Codes();
            if (choices.Count == 0) choices.Add(Language.English.Code());
            current = current?.Trim();
            if (!string.IsNullOrEmpty(current) && !choices.Contains(current))
                choices.Insert(0, current);
            return choices;
        }

        static string FormatLanguage(string code) => LanguageOptionsLocator.DisplayName(code);

        // 用途下拉的本地化显示名——顺序必须与 DialogueEntryKind 序数一致（Line=0 / Option=1 / Any=2）。
        static List<string> KindNames() => new()
        {
            Localizer.UI("ui.entryKindLine", "Line"),
            Localizer.UI("ui.entryKindOption", "Option"),
            Localizer.UI("ui.entryKindAny", "Any"),
        };

        static IEnumerable<string> UsedLanguageCodes(SerializedProperty textsProp)
        {
            for (int i = 0; i < textsProp.arraySize; i++)
                yield return textsProp.GetArrayElementAtIndex(i).FindPropertyRelative("lang").stringValue;
        }

        void AddEntry()
        {
            Undo.RecordObject(target, "Add Dialogue Entry");
            serializedObject.Update();
            m_Entries.InsertArrayElementAtIndex(m_Entries.arraySize);
            var newElem = m_Entries.GetArrayElementAtIndex(m_Entries.arraySize - 1);
            newElem.FindPropertyRelative("key").stringValue = "";
            newElem.FindPropertyRelative("speaker").stringValue = "";
            newElem.FindPropertyRelative("texts").ClearArray();
            serializedObject.ApplyModifiedProperties();
            RefreshEntries();
        }

        void RemoveEntry(int index)
        {
            Undo.RecordObject(target, "Remove Dialogue Entry");
            serializedObject.Update();
            m_Entries.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            RefreshEntries();
        }
    }
}
