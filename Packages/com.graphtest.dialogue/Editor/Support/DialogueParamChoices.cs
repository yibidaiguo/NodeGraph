// DialogueParamChoices.cs — 领域层把"参数候选来源"注册进框架的 ParamChoiceProviders，
// 让检视面板把这些 key 类参数做成可搜索下拉（避免手填错）：
//   dialogue.lineKeys   —— DialogueDatabase 里用途=台词/通用 的行 key（Line.lineKey）
//   dialogue.optionKeys —— DialogueDatabase 里用途=选项/通用 的行 key（Option.optionKey）
//   dialogue.dbKeys     —— 全部行 key（保留兼容；不区分用途）
//   dialogue.labels     —— 当前图里所有 Label 节点的 labelName（Jump.targetLabel）
// 框架层不认识这些领域概念，故由这里在 [InitializeOnLoad] 时反向注入。Editor/ 程序集。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using NodeEditor;
using NodeEditor.EditorUI;

namespace Dialogue.EditorUI
{
    [InitializeOnLoad]
    public static class DialogueParamChoices
    {
        static DialogueParamChoices()
        {
            ParamChoiceProviders.Register("dialogue.lineKeys", _ => DatabaseKeys(DialogueEntryKind.Line));
            ParamChoiceProviders.Register("dialogue.optionKeys", _ => DatabaseKeys(DialogueEntryKind.Option));
            ParamChoiceProviders.Register("dialogue.dbKeys", _ => DatabaseKeys(null));   // 兼容：不区分用途，列全部 key
            // 标签允许临时键入新名字：策划常常想先在 Jump 上写好目标名，回头再建对应 Label。
            ParamChoiceProviders.Register("dialogue.labels", LabelNames, allowCustom: true);

            // 图1：lineKey/optionKey/dbKey 的值指向 DialogueDatabase 条目——注册「内联引用数据编辑器」缝，
            // 让检视面板在下拉下方直接显示并编辑被引用的条目（复用数据窗口同款 BuildEntryDetail，含语言增/删）。
            ParamReferenceEditors.Register("dialogue.lineKeys", (_, key) => DbEntryEditor(key));
            ParamReferenceEditors.Register("dialogue.optionKeys", (_, key) => DbEntryEditor(key));
            ParamReferenceEditors.Register("dialogue.dbKeys", (_, key) => DbEntryEditor(key));
        }

        // 为某 key 构建内联数据库条目编辑器（复用 BuildEntryDetail）。key 空 / 库缺 / 无此条目 → null（检视面板不显示引用区）。
        static VisualElement DbEntryEditor(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var db = DialogueDatabaseLocator.Resolve(out _);
            if (db == null || db.Find(key) == null) return null;
            return DialogueDatabaseEditor.BuildEntryDetail(db, new DataItem(key, key, "", "", key));
        }

        // 按 0/1/many 规则解析出的 authoring database 非空 key：kind==null 取全部，否则按用途过滤（本用途 + 通用）。
        static IEnumerable<string> DatabaseKeys(DialogueEntryKind? kind)
        {
            var db = DialogueDatabaseLocator.Resolve(out _);
            return DatabaseKeys(db, kind);
        }

        static IEnumerable<string> DatabaseKeys(DialogueDatabase db, DialogueEntryKind? kind)
        {
            if (db == null) return Enumerable.Empty<string>();
            return kind == null ? db.Keys : db.KeysOfKind(kind.Value);
        }

        // 当前图里所有 Label 节点已命名的 labelName（Jump 的合法跳转目标）。
        static IEnumerable<string> LabelNames(ParamChoiceContext ctx)
        {
            if (ctx.asset == null || ctx.registry == null) yield break;
            foreach (var inst in ctx.asset.instances)
            {
                if (ctx.registry.Find(inst.definitionId) is not DialogueNodeDefinition def) continue;
                if (def.Kind != DialogueNodeKind.Label) continue;
                var name = ParamResolver.Resolve(inst, def, "labelName");
                if (!string.IsNullOrEmpty(name)) yield return name;
            }
        }
    }
}
