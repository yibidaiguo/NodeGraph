// DialogueDatabase.cs —— 本地化对话内容存储（即"内容存于 SO，图只持有 key"的决策）。
// 行的呈现（speaker、portrait、voice、各语言文本）以一个稳定字符串为 key；对话
// 图节点（Line/Option）只携带该 key。这让资源引用对构建安全（图中无 editor-only 路径），
// 将文本与本地化统一，并让编剧无需触碰节点图就能编辑内容。
// Runtime 程序集 —— 无 editor-only 依赖（红线 §6）。

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dialogue
{
    // 某一语言对一行的呈现。用 [TextArea] 让多行对话便于创作。
    [System.Serializable]
    public class LocalizedText
    {
        public string lang;
        [TextArea] public string text;
    }

    // 条目用途：决定它出现在哪个 key 下拉里。Line=台词键(LineNode.lineKey)；Option=选项键(OptionNode.optionKey)；
    // Any=两个下拉都列（可被当台词也可被当选项复用）。序数即序列化值，追加只能加在末尾。默认 Line（最常见）。
    public enum DialogueEntryKind { Line, Option, Any }

    // 单条可寻址的行：稳定 key + 呈现资源 + 其各语言译文。
    // portrait/voice 是真正的 UnityEngine.Object 引用（能在 player 构建中存活），而非路径。
    [System.Serializable]
    public class DialogueLineEntry
    {
        public string key;
        public DialogueEntryKind kind = DialogueEntryKind.Line;   // 用途：台词/选项/通用（决定进哪个 key 下拉）
        public string speaker;
        public Sprite portrait;
        public AudioClip voice;
        public List<LocalizedText> texts = new();
    }

    [CreateAssetMenu(menuName = "Dialogue/Database")]
    public class DialogueDatabase : ScriptableObject
    {
        [SerializeField] private List<DialogueLineEntry> entries = new();

        // 当某条目缺少所请求的 lang 时使用的语言（回退链中的第二环）。
        public string defaultLang = "en";

        public int Count => entries.Count;
        public void AddEntry(DialogueLineEntry e) => entries.Add(e);

        // 全部非空、去重的行 key —— 供编辑器把 Line/Option 的 key 参数做成可搜索下拉（避免手填错）。
        public IEnumerable<string> Keys => entries.Select(e => e.key).Where(k => !string.IsNullOrEmpty(k)).Distinct();

        // 某用途下可选的 key：本用途 + 通用(Any)。lineKey 下拉传 Line（得 Line+Any），optionKey 下拉传 Option（得 Option+Any），
        // 这样选项键下拉不再混进台词键。Any 条目两边都列。
        public IEnumerable<string> KeysOfKind(DialogueEntryKind kind) =>
            entries.Where(e => e.kind == kind || e.kind == DialogueEntryKind.Any)
                   .Select(e => e.key).Where(k => !string.IsNullOrEmpty(k)).Distinct();

        // 第一个匹配此 key 的条目，或 null。设计上是线性的：数据库存放创作好的行，并非热路径
        //（runner 每个节拍解析一行，而非每帧）。预期 key 唯一；重复时解析为第一条。
        public DialogueLineEntry Find(string key) => entries.FirstOrDefault(e => e.key == key);

        // 出现在不止一个条目上的非空 key —— Find() 对这些会静默地"先到先得"，
        // 故由编辑器工具把它们暴露出来，而不是让一个拼错的重复 key 永远藏掉某一行。
        public IEnumerable<string> DuplicateKeys() =>
            entries.Select(e => e.key).Where(k => !string.IsNullOrEmpty(k))
                .GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).Distinct();

        // 解析某个 key 在请求语言下的显示文本。
        // 回退链（契约 §"Content + blackboard"）：请求的 lang -> defaultLang -> 第一个可用项 -> key 本身。
        // 未命中时返回 key（绝不返回 null/空），使未本地化/拼错的 key 在游戏中可见，而不是一片空白。
        public string Resolve(string key, string lang)
        {
            var e = Find(key);
            if (e == null || e.texts.Count == 0) return key;
            return (e.texts.FirstOrDefault(t => t.lang == lang)
                 ?? e.texts.FirstOrDefault(t => t.lang == defaultLang)
                 ?? e.texts[0]).text;
        }
    }
}
