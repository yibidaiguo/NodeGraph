// ParamChoiceProviders.cs — 第 5 层（连线图编辑器）。框架级"参数候选值提供器"注册表。
// 让领域层（如对话）为某个 string 参数声明一组**动态候选**（标签名、数据库 key……），
// Inspector 据此把它渲染成可搜索下拉，而框架本身不认识任何领域类型——保持层次单向
// （NodeEditor.Editor 不依赖 Dialogue；由 Dialogue.Editor 在 [InitializeOnLoad] 时反向注入）。Editor/ 程序集。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    // 解析候选时可用的上下文：当前图 / 所选节点实例 / 注册表 / 黑板（Inspector 持有这些，按需取用）。
    public struct ParamChoiceContext
    {
        public NodeGraphAsset asset;
        public NodeInstance instance;
        public NodeRegistry registry;
        public BlackboardSet blackboard;   // 这张图的有效黑板（全局⊕模块⊕组合并视图）
    }

    // 领域层 Register("source", ctx => keys...)；ParamDef.choiceSource 持有该 source 字符串。
    // 未注册的 source -> Resolve 返回 null（Inspector 退回普通文本框，绝不报错）。
    // allowCustom：该来源是否允许临时键入候选之外的新值（如还没建出来的 Label 名）。
    public static class ParamChoiceProviders
    {
        static readonly Dictionary<string, Func<ParamChoiceContext, IEnumerable<string>>> s_Providers = new();
        static readonly HashSet<string> s_AllowCustom = new();

        public static void Register(string source, Func<ParamChoiceContext, IEnumerable<string>> provider, bool allowCustom = false)
        {
            if (string.IsNullOrEmpty(source) || provider == null) return;
            if (s_Providers.ContainsKey(source))
                Debug.LogWarning($"NodeEditor: parameter choice provider '{source}' already registered; overwriting.");
            s_Providers[source] = provider;
            if (allowCustom) s_AllowCustom.Add(source); else s_AllowCustom.Remove(source);
        }

        public static void Unregister(string source)
        {
            if (string.IsNullOrEmpty(source)) return;
            s_Providers.Remove(source);
            s_AllowCustom.Remove(source);
        }

        public static bool Has(string source) => !string.IsNullOrEmpty(source) && s_Providers.ContainsKey(source);

        public static bool AllowsCustom(string source) => !string.IsNullOrEmpty(source) && s_AllowCustom.Contains(source);

        // 解析候选：去空、去重、稳定排序。未注册返回 null（与"空列表"区分：null=没有提供器）。
        public static List<string> Resolve(string source, ParamChoiceContext ctx)
        {
            if (string.IsNullOrEmpty(source) || !s_Providers.TryGetValue(source, out var f)) return null;
            return (f(ctx) ?? Enumerable.Empty<string>())
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
        }
    }
}
