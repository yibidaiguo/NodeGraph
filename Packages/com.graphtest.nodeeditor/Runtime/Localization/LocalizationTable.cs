// LocalizationTable.cs — 集中本地化表（ScriptableObject，独立文件以便 Unity 绑定 MonoScript）。Runtime 程序集。
// 作为属性（NodeDoc/ParamDoc）的兜底/补充：编辑器界面 chrome 文案、以及没用属性标注的节点/参数文案都从这里查。
// 每条 = (key, language) -> text。key 约定：
//   ui.*                                 —— 编辑器界面文案（如 ui.inspector / ui.variables / ui.note）
//   node.<defId>.name / node.<defId>.desc        —— 节点本地化名/说明（属性缺省时的兜底）
//   param.<defId>.<paramName>.name / .desc       —— 参数本地化名/说明（属性缺省时的兜底）

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor
{
    [CreateAssetMenu(menuName = "NodeEditor/Localization Table")]
    public class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public class Entry { public string key; public Language language; [TextArea] public string text; }

        [SerializeField] private List<Entry> entries = new();
        public IReadOnlyList<Entry> Entries => entries;

        // 查一条文案；查不到返回 null（调用方据此回退）。
        public string Get(string key, Language language)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].language == language && entries[i].key == key) return entries[i].text;
            return null;
        }

        // 写一条文案（已存在同 key+language 则覆盖，否则追加）。
        public void Set(string key, Language language, string text)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].language == language && entries[i].key == key) { entries[i].text = text; return; }
            entries.Add(new Entry { key = key, language = language, text = text });
        }
    }
}
