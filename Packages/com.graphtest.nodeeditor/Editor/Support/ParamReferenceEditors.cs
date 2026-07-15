// ParamReferenceEditors.cs — 第 5 层（连线图编辑器）。框架级「参数引用数据的内联编辑器」注册表。
// 与 ParamChoiceProviders 平行（机制在框架、策略在领域）：当某个 string 参数（如对话的 lineKey/optionKey）
// 经 choiceSource 渲染成可搜索下拉、其值指向某条领域数据（如 DialogueDatabase 条目）时，领域层可在此为该
// choiceSource 注册一个「按当前值构建内联编辑器」的回调；检视面板据此在下拉下方内联显示并编辑被引用的数据，
// 免去另开数据窗口。框架本身不认识任何领域数据类型——由领域在 [InitializeOnLoad] 反向注入。Editor/ 程序集。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public static class ParamReferenceEditors
    {
        // source（= ParamDef.choiceSource）→ (上下文, 当前值) → 内联编辑器。回调返回 null 表示当前值无可编辑数据
        // （如空值 / 未找到对应条目），此时检视面板不显示引用区。
        static readonly Dictionary<string, Func<ParamChoiceContext, string, VisualElement>> s_Builders = new();

        public static void Register(string source, Func<ParamChoiceContext, string, VisualElement> builder)
        {
            if (string.IsNullOrEmpty(source) || builder == null) return;
            if (s_Builders.ContainsKey(source))
                Debug.LogWarning($"NodeEditor: parameter reference editor '{source}' already registered; overwriting.");
            s_Builders[source] = builder;
        }

        public static void Unregister(string source)
        {
            if (!string.IsNullOrEmpty(source)) s_Builders.Remove(source);
        }

        public static bool Has(string source) => !string.IsNullOrEmpty(source) && s_Builders.ContainsKey(source);

        // 构建当前值的内联编辑器；未注册或回调返回 null 时返回 null（检视面板据此不显示引用区）。
        public static VisualElement Build(string source, ParamChoiceContext ctx, string currentValue)
        {
            if (string.IsNullOrEmpty(source) || !s_Builders.TryGetValue(source, out var f)) return null;
            return f(ctx, currentValue);
        }
    }
}
