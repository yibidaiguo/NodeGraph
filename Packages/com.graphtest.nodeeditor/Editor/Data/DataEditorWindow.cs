// DataEditorWindow.cs — 第 5 层「通用数据编辑窗口」，模板层。
// 专职查看 / 修改「数据层」（子层 4a 的纯数据 SO）。窗口本身不认识任何具体数据：
// 左列按归属作用域（项目 / 领域 / 单图）分组列出注册进来的 IDataSource，右列渲染选中源的
// BuildUI。两个入口、同一个窗口、只差上下文：
//   · NodeGraph Manager / Node Editor Data → 总数据中心（DomainFilter=null，显示全部作用域全部源）；
//   · DataEditorWindow.Open(domain, graph) → 领域窗口（从某编辑器工具栏调，绑定当前图、
//     过滤到 项目 + 该领域 + 该图）。
// 数据源由框架（FrameworkDataSources）/ 领域（如 DialogueDataSources）经 [InitializeOnLoad] 注册。
// Unity 6。Editor/ 程序集。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public static class DataEditorSelection
    {
        public static string PickSourceId(IEnumerable<IDataSource> sources, string requested)
        {
            var list = sources?.ToList() ?? new List<IDataSource>();
            if (!string.IsNullOrEmpty(requested) && list.Any(s => s.Id == requested)) return requested;
            return list.FirstOrDefault()?.Id;
        }
    }

    public class DataEditorWindow : EditorWindow
    {
        // 跨域重载保留：总中心 / 领域过滤 + 当前图 + 选中源，重建时还原。
        [SerializeField] string m_DomainFilter;          // null/"" = 总数据中心
        [SerializeField] NodeGraphAsset m_Graph;         // 单图作用域看哪张图
        [SerializeField] string m_SelectedId;            // 当前选中的源 id

        VisualElement m_Left;                            // 左列：按作用域分组的源列表
        VisualElement m_Middle;
        VisualElement m_Detail;
        MasterDetailList m_List;
        readonly Dictionary<string, Button> m_Rows = new();   // 源 id → 左列行（用于高亮选中）
        readonly Dictionary<string, string> m_SelectedItemBySource = new();

        public static void Open()
        {
            var w = GetWindow<DataEditorWindow>();
            w.titleContent = new GUIContent(Localizer.UI("ui.dataWindow", "Data"));
            w.minSize = new Vector2(720, 420);
            w.m_DomainFilter = null;                     // 总数据中心
            w.RebuildAll();
        }

        // 领域入口：从某编辑器工具栏调，绑定当前图、过滤到该领域（+ 项目级源）。
        public static void Open(string domain, NodeGraphAsset graph)
        {
            var w = GetWindow<DataEditorWindow>();
            w.titleContent = new GUIContent(Localizer.UI("ui.dataWindow", "Data"));
            w.minSize = new Vector2(720, 420);
            w.m_DomainFilter = domain;
            w.m_Graph = graph;
            w.RebuildAll();
        }

        public void CreateGUI()
        {
            EditorUi.ConfigureWindow(rootVisualElement);
            RebuildAll();
        }

        // 解析当前上下文：注册表经 locator 找到（每项目假设一个）；Blackboard 给全局档（模块/组档由各自源按标签解析）；图取选中图。
        DataSourceContext Context() => new DataSourceContext
        {
            Registry = NodeRegistryLocator.Find(),
            Blackboard = BlackboardLocator.FindGlobal(),
            Graph = m_Graph,
            DomainFilter = m_DomainFilter,
        };

        void RebuildAll()
        {
            var root = rootVisualElement;
            root.Clear();

            root.Add(BuildHeader());

            var splitLeft = new TwoPaneSplitView(0, 220, TwoPaneSplitViewOrientation.Horizontal);
            var splitRight = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);

            m_Left = new VisualElement();
            m_Left.AddToClassList("data-left");
            // 面板底色/分隔线画在滚动容器上（占满整栏高），内容元素只有内容高，画在它上面会露底。
            var leftScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            leftScroll.AddToClassList("data-pane-scroll");
            leftScroll.AddToClassList("data-pane-scroll--left");
            leftScroll.Add(m_Left);

            m_Middle = new VisualElement();
            m_Middle.AddToClassList("data-middle");

            m_Detail = new VisualElement();
            m_Detail.AddToClassList("data-detail");
            var detailScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            detailScroll.AddToClassList("data-pane-scroll");
            detailScroll.Add(m_Detail);

            splitRight.Add(m_Middle);
            splitRight.Add(detailScroll);
            splitLeft.Add(leftScroll);
            splitLeft.Add(splitRight);
            root.Add(splitLeft);

            RebuildLeft();
        }

        // 顶部：当前图选择框（单图作用域据此取数据）+ 刷新。总中心可在此选任意图；
        // 领域窗口预填传入的图，亦可改选。
        VisualElement BuildHeader()
        {
            var bar = new Toolbar();
            bar.AddToClassList(EditorUi.ToolbarClass);

            var graphField = new ObjectField(Localizer.UI("ui.dataGraphField", "Graph")) { objectType = typeof(NodeGraphAsset), value = m_Graph };
            graphField.AddToClassList("toolbar-graphfield");
            graphField.RegisterValueChangedCallback(e =>
            {
                m_Graph = e.newValue as NodeGraphAsset;
                RebuildLeft();   // 单图作用域的源依赖它
            });
            bar.Add(graphField);

            var refresh = new ToolbarButton(RebuildLeft) { text = Localizer.UI("ui.refresh", "Refresh") };
            EditorUi.ApplyToolbarTextButton(refresh);
            bar.Add(refresh);

            return bar;
        }

        void RebuildLeft()
        {
            m_Left.Clear();
            m_Rows.Clear();
            var ctx = Context();
            var sources = DataSourceRegistry.Sources(ctx).ToList();

            // 三档作用域始终成组展示（即便为空也保留标题），让「项目 / 领域 / 单图」三层一眼可见。
            AddScopeGroup(DataScope.Project, Localizer.UI("ui.dataProject", "Project"), sources);
            AddScopeGroup(DataScope.Domain, Localizer.UI("ui.dataDomain", "Domain"), sources);
            AddScopeGroup(DataScope.Graph, Localizer.UI("ui.dataGraph", "Graph"), sources);

            // 还原 / 落地选中：优先上次选中，否则第一个可用源。
            var pickId = DataEditorSelection.PickSourceId(sources, m_SelectedId);
            IDataSource pick = sources.FirstOrDefault(s => s.Id == pickId);
            ShowSource(pick);
        }

        void AddScopeGroup(DataScope scope, string title, List<IDataSource> all)
        {
            // 扁平侧边导航：小节抬头 + 透明导航行（不再套 inspector-section 卡片，避免「卡片里塞按钮」的嵌套感）。
            var section = new VisualElement();
            section.AddToClassList("data-scope-group");
            var head = new Label(title);
            head.AddToClassList("data-scope-title");
            section.Add(head);

            var group = all.Where(s => s.Scope == scope).ToList();
            if (group.Count == 0)
            {
                // 单图作用域为空多半是「还没选图」（组变量/图概览依赖当前图）——给出明确指引而非笼统「(无数据)」。
                var msg = (scope == DataScope.Graph && m_Graph == null)
                    ? Localizer.UI("ui.dataGraphHint", "Select or drop a Graph above to see its group variables / overview.")
                    : Localizer.UI("ui.dataEmpty", "(no data)");
                var empty = new Label(msg);
                empty.AddToClassList("field-note");
                section.Add(empty);
            }
            else
            {
                foreach (var src in group)
                {
                    var captured = src;
                    var row = new Button(() => ShowSource(captured)) { text = src.Title };
                    row.AddToClassList("data-source-row");
                    m_Rows[src.Id] = row;
                    section.Add(row);
                }
            }
            m_Left.Add(section);
        }

        void ShowSource(IDataSource src)
        {
            m_Middle.Clear();
            m_Detail.Clear();
            // 左列高亮：清旧、标新。
            foreach (var r in m_Rows.Values) r.RemoveFromClassList("data-source-row--selected");
            if (src == null)
            {
                var hint = new Label(Localizer.UI("ui.dataPick", "Select data on the left to view / edit."));
                hint.AddToClassList("field-note");
                m_Detail.Add(hint);
                return;
            }
            m_SelectedId = src.Id;
            if (m_Rows.TryGetValue(src.Id, out var sel)) sel.AddToClassList("data-source-row--selected");

            m_Detail.Add(EditorUi.Header(src.Title));

            if (src is IListDataSource listSource)
            {
                m_List = new MasterDetailList();
                var ctx = Context();
                var items = listSource.Items(ctx).ToList();
                m_List.OnSelectionChanged += item =>
                {
                    m_Detail.Clear();
                    m_Detail.Add(EditorUi.Header(src.Title));
                    if (item == null)
                    {
                        m_Detail.Add(EditorUi.EmptyState(Localizer.UI("ui.dataPick", "Select data on the left to view / edit.")));
                        return;
                    }

                    m_SelectedItemBySource[src.Id] = item.Id;
                    m_Detail.Add(listSource.BuildDetail(Context(), item));
                };
                m_Middle.Add(m_List);
                m_List.SetItems(items, m_SelectedItemBySource.TryGetValue(src.Id, out var selected) ? selected : null);
                if (items.Count == 0)
                {
                    m_Detail.Clear();
                    m_Detail.Add(EditorUi.Header(src.Title));
                    m_Detail.Add(src.BuildUI(Context()));
                }
                return;
            }

            m_Middle.Add(EditorUi.EmptyState(Localizer.UI("ui.dataSinglePanel", "This data source has no list.")));
            m_Detail.Add(src.BuildUI(Context()));
        }
    }
}
