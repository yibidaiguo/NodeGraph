// DataSources.cs — 第 5 层「数据编辑窗口」的框架留缝。
// 通用数据编辑窗口（DataEditorWindow）本身不认识任何具体数据：它只枚举注册进来的
// IDataSource，按归属作用域（项目 / 领域 / 单图）分组、调各自的 BuildUI 渲染右侧面板。
// 框架自有数据（黑板 / 本地化 / 图概览）由 FrameworkDataSources 注册；领域数据（对话数据库等）
// 由领域 Editor 程序集经 [InitializeOnLoad] 注册——与 GraphValidator.RegisterExtension /
// ParamChoiceProviders / GraphListPane.RegisterModuleInitializer 同一套「框架留缝、领域填充」。
// Editor/ 程序集。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    // 数据的归属作用域（= 用户要的三档层级）。窗口左列据此分组。
    public enum DataScope { Project, Domain, Graph }

    // 窗口建源时注入的上下文：已定位好的项目级资产 + 当前图 + 领域过滤。
    public sealed class DataSourceContext
    {
        public NodeRegistry Registry;
        public BlackboardAsset Blackboard;
        public NodeGraphAsset Graph;        // 单图作用域看哪张图；总中心未选图时为 null
        public string DomainFilter;         // null/"" = 总数据中心（不按领域过滤）；否则只留该领域 + 项目级源
    }

    // 一个可在数据窗口里查看 / 编辑的数据「源」。一种数据 = 一个 IDataSource。
    public interface IDataSource
    {
        string Id { get; }                  // 稳定 id（左列去重 / 记忆选中），如 "blackboard"
        string Title { get; }               // 左列显示名（实现里走 Localizer.UI 取本地化）
        DataScope Scope { get; }            // 归属作用域
        string Domain { get; }              // 领域标签：项目级源给 null/""；领域级源给如 "dialogue"
        VisualElement BuildUI(DataSourceContext ctx);   // 右侧编辑面板
    }

    public sealed class DataItem
    {
        public string Id { get; }
        public string Title { get; }
        public string Group { get; }
        public string Preview { get; }
        public object Payload { get; }

        public DataItem(string id, string title, string group = null, string preview = null, object payload = null)
        {
            Id = id ?? "";
            Title = string.IsNullOrEmpty(title) ? Id : title;
            Group = group ?? "";
            Preview = preview ?? "";
            Payload = payload;
        }
    }

    public interface IListDataSource : IDataSource
    {
        IEnumerable<DataItem> Items(DataSourceContext ctx);
        VisualElement BuildDetail(DataSourceContext ctx, DataItem item);
    }

    // 数据源注册表（框架留缝）。框架 / 领域在 [InitializeOnLoad] 里按 id 注册一个工厂；
    // 窗口用 Sources(ctx) 拿到「适用于本上下文」的源。按 id 入字典 → 跨域重载天然幂等
    //（同 ParamChoiceProviders 的做法），重复注册覆盖而非叠加。
    public static class DataSourceRegistry
    {
        static readonly Dictionary<string, Func<DataSourceContext, IDataSource>> s_Factories = new();

        public static void Register(string id, Func<DataSourceContext, IDataSource> factory)
        {
            if (string.IsNullOrEmpty(id) || factory == null) return;
            if (s_Factories.ContainsKey(id))
                Debug.LogWarning($"NodeEditor: data source '{id}' already registered; overwriting.");
            s_Factories[id] = factory;
        }

        // 注销一个源（对称于 Register）。框架/领域正常靠跨域重载覆盖即可，无需手动注销；
        // 此方法供测试隔离用——按 id 精确移除自己注册的源，而不是 Clear() 抹掉框架真实注册。
        public static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            s_Factories.Remove(id);
        }

        // 已注册 id 的只读快照。供测试在 TearDown 里枚举并精确清理自己注册的源
        //（如 test.* 前缀），不触碰框架 InitializeOnLoad 注册的真实源。每次返回新副本，
        // 可在遍历它的同时安全调用 Unregister。
        public static IReadOnlyCollection<string> RegisteredIds => new List<string>(s_Factories.Keys);

        // 枚举适用于给定上下文的数据源：
        //  - 工厂返回 null 表示「本上下文没有可编辑数据」（如单图源在未选图时）→ 跳过；
        //  - DomainFilter 非空时，只保留项目级源（Domain 空）与该领域的源；
        //  - 总中心（DomainFilter 空）显示全部。
        // 工厂抛异常只丢弃该源、不拖垮整窗。
        public static IEnumerable<IDataSource> Sources(DataSourceContext ctx)
        {
            foreach (var f in s_Factories.Values)
            {
                IDataSource src;
                try { src = f(ctx); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); continue; }
                if (src == null) continue;
                if (!string.IsNullOrEmpty(ctx?.DomainFilter)
                    && !string.IsNullOrEmpty(src.Domain)
                    && src.Domain != ctx.DomainFilter) continue;
                yield return src;
            }
        }
    }

    // 一个最常见的 IDataSource 默认实现：把「元数据 + 一个 BuildUI 委托」打包成源，
    // 让框架 / 领域注册时无需各写一个类。需要更复杂行为时仍可自实现 IDataSource。
    public sealed class DelegateDataSource : IDataSource
    {
        readonly Func<DataSourceContext, VisualElement> m_Build;
        public string Id { get; }
        public string Title { get; }
        public DataScope Scope { get; }
        public string Domain { get; }
        public DelegateDataSource(string id, string title, DataScope scope, string domain,
                                  Func<DataSourceContext, VisualElement> build)
        { Id = id; Title = title; Scope = scope; Domain = domain; m_Build = build; }
        public VisualElement BuildUI(DataSourceContext ctx) => m_Build?.Invoke(ctx) ?? new VisualElement();
    }

    public sealed class DelegateListDataSource : IListDataSource
    {
        readonly Func<DataSourceContext, IEnumerable<DataItem>> m_Items;
        readonly Func<DataSourceContext, DataItem, VisualElement> m_BuildDetail;
        readonly Func<DataSourceContext, VisualElement> m_BuildFallback;

        public string Id { get; }
        public string Title { get; }
        public DataScope Scope { get; }
        public string Domain { get; }

        public DelegateListDataSource(
            string id,
            string title,
            DataScope scope,
            string domain,
            Func<DataSourceContext, IEnumerable<DataItem>> items,
            Func<DataSourceContext, DataItem, VisualElement> buildDetail,
            Func<DataSourceContext, VisualElement> buildFallback = null)
        {
            Id = id;
            Title = title;
            Scope = scope;
            Domain = domain;
            m_Items = items;
            m_BuildDetail = buildDetail;
            m_BuildFallback = buildFallback;
        }

        public IEnumerable<DataItem> Items(DataSourceContext ctx) =>
            m_Items?.Invoke(ctx) ?? Array.Empty<DataItem>();

        public VisualElement BuildDetail(DataSourceContext ctx, DataItem item) =>
            m_BuildDetail?.Invoke(ctx, item) ?? new VisualElement();

        public VisualElement BuildUI(DataSourceContext ctx) =>
            m_BuildFallback?.Invoke(ctx) ?? new VisualElement();
    }
}
