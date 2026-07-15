// FrameworkDataSources.cs — 框架自有数据的数据源注册（Editor/ 程序集，[InitializeOnLoad]）。
// 「框架留缝、框架填框架数据」：黑板 / 本地化是框架概念，由框架自注册为数据源，于是任何基于本框架的
// 编辑器都白拿这几项（领域只需注册自己的领域数据，见 DialogueDataSources）。三档作用域：
//   · 黑板变量（项目）—— 复用现成的 VariablePane；
//   · 本地化表（项目）—— LocalizationTablePane（补齐此前无编辑界面的缺口）；
//   · 图参数总览（单图）—— 当前图所有节点的只读参数概览（编辑节点数据仍归画布检视面板，避免双入口）。

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    [InitializeOnLoad]
    public static class FrameworkDataSources
    {
        static FrameworkDataSources()
        {
            // 全局变量（项目级，无领域）：全局档黑板（c.Blackboard = BlackboardLocator.FindGlobal()）。复用 VariablePane。
            DataSourceRegistry.Register("blackboard", ctx =>
                new DelegateListDataSource("blackboard", Localizer.UI("ui.globalVariables", "Global Variables"),
                    DataScope.Project, null,
                    c => VariablePane.Items(c.Blackboard),
                    (c, item) => VariablePane.BuildVariableDetail(c.Registry, c.Blackboard, c.Graph, item),
                    c =>
                    {
                        var pane = new VariablePane();
                        pane.Bind(c.Registry, c.Blackboard, c.Graph);
                        return pane;
                    }));

            // 组变量（单图级）：本图所属「模块+组」的那块黑板。图未选 / 图无组(group 为空) 时返回 null（该档不显示）。
            DataSourceRegistry.Register("blackboard.group", ctx =>
            {
                if (ctx.Graph == null || string.IsNullOrEmpty(ctx.Graph.group)) return null;
                var module = ctx.Graph.module;
                var group = ctx.Graph.group;
                var asset = BlackboardLocator.FindLayer(module, group);
                return new DelegateListDataSource("blackboard.group", Localizer.UI("ui.groupVariables", "Group Variables"),
                    DataScope.Graph, null,
                    _ => VariablePane.Items(asset),
                    (c, item) => VariablePane.BuildVariableDetail(c.Registry, asset, c.Graph, item),
                    c => BuildLayerPane(c, module, group));
            });

            // 本地化表（项目级，无领域）：补齐缺口的 key×语言 编辑面板。
            DataSourceRegistry.Register("localization", ctx =>
            {
                var table = LocalizationTableLocator.Find();
                return new DelegateListDataSource("localization", Localizer.UI("ui.localization", "Localization"),
                    DataScope.Project, null,
                    _ => LocalizationTablePane.Items(table),
                    (_, item) => LocalizationTablePane.BuildDetail(table, item),
                    _ => new LocalizationTablePane(table));
            });

            // 图参数总览（单图级）：未选图时返回 null（该作用域显示「（暂无数据）」直到选定一张图）。
            DataSourceRegistry.Register("graph.overview", ctx =>
            {
                if (ctx.Graph == null) return null;
                return new DelegateDataSource("graph.overview", Localizer.UI("ui.graphOverview", "Graph Overview"),
                    DataScope.Graph, null, BuildGraphOverview);
            });

            // 全局节点定义（项目级，只读）：注册表 universal 档——框架通用节点（领域无关，当前可能为空）。
            // 节点按层分别展示：全局/通用节点归本框架源；各领域自有节点归领域源（如 DialogueDataSources 的「对话节点定义」）。
            DataSourceRegistry.Register("nodedefs.global", ctx =>
            {
                var reg = NodeRegistryLocator.Find();
                return new DelegateListDataSource("nodedefs.global", Localizer.UI("ui.globalNodeDefs", "Global Node Definitions (read-only)"),
                    DataScope.Project, null,
                    _ => NodeDefItems(reg != null ? reg.universal : null),
                    BuildNodeDefDetail,
                    _ => BuildNodeDefsView(reg != null ? reg.universal : null));
            });

            // 全局可组合单元（项目级，只读目录）：框架程序集里的通用 Unit（取值/条件/动作/控制族）。只读浏览，编辑在节点的 Unit 槽里做。
            // 领域自有单元（如对话「触发事件」）由领域源展示（见 DialogueDataSources），与节点定义一样按层分别列出。
            DataSourceRegistry.Register("units.global", ctx =>
                new DelegateListDataSource("units.global", Localizer.UI("ui.globalUnits", "Global Units (read-only)"),
                    DataScope.Project, null,
                    _ => UnitCatalogItems(false),
                    BuildUnitDetail,
                    _ => BuildUnitCatalogView(false)));
        }

        // 某一档（模块 / 组）黑板的编辑面板。复用 VariablePane（列变量 / 改默认值 / 新建）。该档黑板尚不存在时，
        // 给出提示 + 「新建该作用域黑板」按钮（新建后就地换成绑定好的 VariablePane）。供框架的「组」源与领域的「模块」源共用。
        public static VisualElement BuildLayerPane(DataSourceContext ctx, string module, string group)
        {
            var root = new VisualElement();
            var asset = BlackboardLocator.FindLayer(module, group);
            if (asset != null)
            {
                var pane = new VariablePane();
                pane.Bind(ctx.Registry, asset, ctx.Graph);
                root.Add(pane);
                return root;
            }

            var hint = new Label(Localizer.UI("ui.noTierBlackboard", "No blackboard for this scope yet."));
            hint.AddToClassList("field-note");
            root.Add(hint);
            var create = new Button(() =>
            {
                // 分层原则（准则 #15）：模块/组黑板落在本模块资产区（= 当前图所在文件夹）；全局档由 LayerFolder 兜底取框架区。
                string folder = string.IsNullOrEmpty(module) || ctx.Graph == null
                    ? null
                    : System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(ctx.Graph)).Replace('\\', '/');
                var made = BlackboardLocator.CreateLayer(module, group, folder);
                root.Clear();
                var pane = new VariablePane();
                pane.Bind(ctx.Registry, made, ctx.Graph);
                root.Add(pane);
            }) { text = Localizer.UI("ui.createBlackboard", "+ New blackboard for this scope") };
            create.AddToClassList("add-button");
            root.Add(create);
            return root;
        }

        // 当前图所有节点的只读概览：每节点列名称 / 角色 / 各参数解析值。只读——节点数据的编辑仍归画布检视面板。
        static VisualElement BuildGraphOverview(DataSourceContext ctx)
        {
            var root = new VisualElement();
            if (ctx.Graph == null) return root;
            var reg = ctx.Registry;

            foreach (var inst in ctx.Graph.instances)
            {
                var def = reg != null ? reg.Find(inst.definitionId) : null;
                var card = new VisualElement();
                card.AddToClassList("entry-card");

                string name = !string.IsNullOrEmpty(inst.displayName) ? inst.displayName
                            : (def != null ? Localizer.NodeName(def) : inst.definitionId);
                var head = new Label(def != null ? $"{name}  [{def.Role}]" : name);
                head.AddToClassList("inspector-section-title");
                card.Add(head);

                if (def != null)
                {
                    foreach (var pd in def.Parameters)
                    {
                        var row = new VisualElement();
                        row.AddToClassList("field-row");
                        var label = new Label(Localizer.ParamName(def, pd.name));
                        label.AddToClassList("field-label");
                        row.Add(label);
                        var val = new Label(ParamResolver.Resolve(inst, def, pd.name) ?? "");
                        val.AddToClassList("field-note");
                        row.Add(val);
                        card.Add(row);
                    }
                }
                root.Add(card);
            }
            return root;
        }

        // ---- 节点定义只读概览（框架通用：仅读 NodeDefinition 的角色/端口/参数，零领域语义）。
        // 供框架「全局节点定义」源（universal 档）与各领域「自有节点定义」源（projectDomain 档）复用——节点按层分别展示、互不耦合。----
        public static IEnumerable<DataItem> NodeDefItems(IEnumerable<NodeDefinition> defs)
        {
            foreach (var def in (defs ?? Enumerable.Empty<NodeDefinition>()).Where(d => d != null).OrderBy(Localizer.NodeName))
            {
                var ports = string.Format(Localizer.UI("ui.portsInline", "{0} in / {1} out"), def.InputPorts.Count, def.OutputPorts.Count);
                yield return new DataItem(def.Id, Localizer.NodeName(def), def.Role.ToString(), ports, def);
            }
        }

        public static VisualElement BuildNodeDefDetail(DataSourceContext ctx, DataItem item)
        {
            var root = new VisualElement();
            var def = item?.Payload as NodeDefinition;
            if (def == null && ctx.Registry != null && item != null) def = ctx.Registry.Find(item.Id);
            if (def == null) { root.Add(EditorUi.EmptyState(Localizer.UI("ui.dataEmpty", "(no data)"))); return root; }
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.id", "ID"), new Label(def.Id)));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.name", "Name"), new Label(Localizer.NodeName(def))));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.role", "Role"), new Label(def.Role.ToString())));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.ports", "Ports"),
                new Label(string.Format(Localizer.UI("ui.portsDetail", "in: {0}   out: {1}"),
                    string.Join(", ", def.InputPorts.Select(p => p.name)), string.Join(", ", def.OutputPorts.Select(p => p.name))))));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.params", "Parameters"),
                new Label(string.Join(", ", def.Parameters.Select(p => p.name)))));
            return root;
        }

        public static VisualElement BuildNodeDefsView(IEnumerable<NodeDefinition> defs)
        {
            var root = new VisualElement();
            foreach (var def in (defs ?? Enumerable.Empty<NodeDefinition>()).Where(d => d != null))
            {
                var card = new VisualElement();
                card.AddToClassList("entry-card");
                var head = new Label($"{Localizer.NodeName(def)}  [{def.Role}]");
                head.AddToClassList("inspector-section-title");
                card.Add(head);
                card.Add(NoteRow(Localizer.UI("ui.ports", "Ports"),
                    string.Format(Localizer.UI("ui.portsDetail", "in: {0}   out: {1}"),
                        string.Join(", ", def.InputPorts.Select(p => p.name)), string.Join(", ", def.OutputPorts.Select(p => p.name)))));
                card.Add(NoteRow(Localizer.UI("ui.params", "Parameters"), string.Join(", ", def.Parameters.Select(p => p.name))));
                root.Add(card);
            }
            return root;
        }

        static VisualElement NoteRow(string label, string value)
        {
            var row = new VisualElement(); row.AddToClassList("field-row");
            var l = new Label(label); l.AddToClassList("field-label"); row.Add(l);
            var v = new Label(value); v.AddToClassList("field-note"); row.Add(v);
            return row;
        }

        // ---- 可组合单元（Unit）只读目录（框架通用：反射读族/分组/字段，零领域语义）。只读浏览「现有哪些逻辑单元」，
        // 实际编辑在节点的 Unit 槽里做。domainTier=false 列框架程序集的全局通用单元；=true 列领域程序集单元。
        // 供框架「全局单元」源与各领域「自有单元」源（如对话）复用——与节点定义一样按层分别展示。----
        static readonly string[] s_UnitFamilies = { "Provider", "Condition", "Action", "Control" };

        static string UnitFamilyDisplay(string fam) => fam switch
        {
            "Provider"  => Localizer.UI("ui.unitFamProvider", "Provider (value)"),
            "Condition" => Localizer.UI("ui.unitFamCondition", "Condition"),
            "Action"    => Localizer.UI("ui.unitFamAction", "Action"),
            "Control"   => Localizer.UI("ui.unitFamControl", "Control"),
            _ => fam
        };

        public static IEnumerable<DataItem> UnitCatalogItems(bool domainTier)
        {
            foreach (var fam in s_UnitFamilies)
                foreach (var c in UnitRegistry.ForFamily(fam))
                    if (c.isDomain == domainTier)
                        yield return new DataItem(c.type.FullName, c.displayName, UnitFamilyDisplay(fam), c.group, c.type);
        }

        public static VisualElement BuildUnitDetail(DataSourceContext ctx, DataItem item)
        {
            var root = new VisualElement();
            var t = item?.Payload as System.Type;
            if (t == null) { root.Add(EditorUi.EmptyState(Localizer.UI("ui.dataEmpty", "(no data)"))); return root; }
            var attr = t.GetCustomAttribute<UnitAttribute>();
            var unitName = attr != null ? Localizer.UI(attr.NameKey, attr.NameFallback) : t.Name;
            var unitGroup = attr != null ? Localizer.UI(attr.GroupKey, attr.GroupFallback) : "";
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.name", "Name"), new Label(unitName)));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.unitGroup", "Group"), new Label(unitGroup)));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.unitType", "Type"), new Label(t.Name)));
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length > 0)
            {
                var head = new Label(Localizer.UI("ui.unitFields", "Configurable fields"));
                head.AddToClassList("inspector-section-title");
                root.Add(head);
                foreach (var f in fields)
                    root.Add(NoteRow(f.Name, UnitFieldDesc(f)));
            }
            return root;
        }

        public static VisualElement BuildUnitCatalogView(bool domainTier)
        {
            var root = new VisualElement();
            foreach (var fam in s_UnitFamilies)
                foreach (var c in UnitRegistry.ForFamily(fam))
                    if (c.isDomain == domainTier)
                    {
                        var card = new VisualElement();
                        card.AddToClassList("entry-card");
                        var head = new Label($"{c.displayName}  [{UnitFamilyDisplay(fam)}]");
                        head.AddToClassList("inspector-section-title");
                        card.Add(head);
                        if (!string.IsNullOrEmpty(c.group)) card.Add(NoteRow(Localizer.UI("ui.unitGroup", "Group"), c.group));
                        card.Add(NoteRow(Localizer.UI("ui.unitType", "Type"), c.type.Name));
                        root.Add(card);
                    }
            return root;
        }

        // 单元字段描述：类型名 + [黑板键]/[内嵌单元槽] 标注（只读目录用）。
        static string UnitFieldDesc(FieldInfo f)
        {
            var s = UnitTypeName(f.FieldType);
            if (f.GetCustomAttribute<BlackboardKeyAttribute>() != null) s += "  " + Localizer.UI("ui.unitFieldBBKey", "[blackboard key]");
            if (f.GetCustomAttribute<UnityEngine.SerializeReference>() != null) s += "  " + Localizer.UI("ui.unitFieldSlot", "[nested unit slot]");
            return s;
        }

        static string UnitTypeName(System.Type t) =>
            t.IsGenericType
                ? t.Name.Split('`')[0] + "<" + string.Join(", ", t.GetGenericArguments().Select(a => a.Name)) + ">"
                : t.Name;
    }
}
