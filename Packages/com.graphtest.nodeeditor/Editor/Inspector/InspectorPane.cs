// InspectorPane.cs — 第 5 层（连线图编辑器），模板级别。
// 类型化的参数编辑器（下拉框/选择器/滑块），绝不使用裸字符串字段。
// Blackboard 引用会变成一个声明键的下拉框。映射第 3 层的每种 ParamDef/TypeRef 情形。
// 同时包含用于声明 blackboard 变量的 VariablePane。Unity 6。Editor/ 程序集。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层的数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{
    public class InspectorPane : VisualElement
    {
        readonly ScrollView m_Body;
        NodeView m_Current;
        NodeRegistry m_Registry;
        BlackboardSet m_Blackboard;   // 这张图的有效黑板（全局⊕模块⊕组合并视图），供「键」下拉与类型推断
        NodeGraphAsset m_Asset;   // 拥有所选节点的图——每次编辑都必须标脏/记录 undo
        // 面板坐标→屏幕坐标换算（由窗口注入）：可搜索下拉要在字段处弹出 SearchWindow，需要屏幕坐标。
        public System.Func<Vector2, Vector2> PanelToScreen;

        public InspectorPane()
        {
            AddToClassList("inspector-root");
            var header = new Label(Localizer.UI("ui.inspector", "Inspector"));
            header.AddToClassList("inspector-header");
            Add(header);
            m_Body = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            m_Body.AddToClassList("inspector-body");
            Add(m_Body);
        }

        public void Show(NodeView node, NodeRegistry registry, BlackboardSet blackboard, NodeGraphAsset asset)
        {
            m_Current = node; m_Registry = registry; m_Blackboard = blackboard; m_Asset = asset;
            m_Body.Clear();
            if (node == null) return;

            // 重命名 + 注释（对齐 Behavior Designer）
            var general = new VisualElement();
            general.AddToClassList("inspector-section");
            var generalTitle = new Label(Localizer.UI("ui.general", "GENERAL"));
            generalTitle.AddToClassList("inspector-section-title");
            general.Add(generalTitle);

            // 名称（自定义名）：写入 displayName；非空时作为标题（次于备注）。
            var nameField = new TextField(Localizer.UI("ui.name", "Name")) { value = node.Instance.displayName };
            nameField.RegisterValueChangedCallback(e =>
            {
                NodeInspectorEdits.Rename(m_Asset, node.Instance, node.Definition, e.newValue);
                node.title = NodeInspectorEdits.ResolveTitle(node.Instance, node.Definition);
            });
            general.Add(nameField);

            // 备注：写入 note；非空时优先作为节点标题显示。
            var noteField = new TextField(Localizer.UI("ui.note", "Note")) { value = node.Instance.note };
            noteField.RegisterValueChangedCallback(e =>
            {
                NodeInspectorEdits.WriteNote(m_Asset, node.Instance, e.newValue);
                node.title = NodeInspectorEdits.ResolveTitle(node.Instance, node.Definition);
            });
            general.Add(noteField);
            m_Body.Add(general);

            var parameters = new VisualElement();
            parameters.AddToClassList("inspector-section");
            var parametersTitle = new Label(Localizer.UI("ui.parameters", "PARAMETERS"));
            parametersTitle.AddToClassList("inspector-section-title");
            parameters.Add(parametersTitle);

            foreach (var pd in node.Definition.Parameters)
                parameters.Add(EditorFor(pd, node));
            m_Body.Add(parameters);
        }

        VisualElement EditorFor(ParamDef pd, NodeView node)
        {
            // 参数标签 + tooltip 走本地化（Localizer 按编辑器语言：属性 → 表 → 英文回退）。WriteOverride 仍用原始 pd.name 作 key。
            string label = Localizer.ParamName(node.Definition, pd.name);
            string tip = Localizer.ParamDesc(node.Definition, pd.name);
            if (pd.type == null) { var note = new Label($"{label}: {Localizer.UI("ui.noType", "(no type)")}"); note.AddToClassList("field-note"); note.tooltip = tip; return note; }   // TypeRef 可能为 null（未设置 / 反序列化之后）
            // 领域声明了候选来源（动态、可能很多）：渲染为可搜索下拉（复用 SearchWindow，自带过滤框），避免手填 key 出错。
            // allowCustom 的来源（如 Label 名）允许临时键入候选之外的新值。候选惰性取，打开时总是最新。
            if (pd.type.kind != TypeKind.Object && !string.IsNullOrEmpty(pd.choiceSource) && ParamChoiceProviders.Has(pd.choiceSource))
            {
                var src = pd.choiceSource;
                if (!ParamReferenceEditors.Has(src))
                    return new SearchableDropdownField(label,
                        () => ParamChoiceProviders.Resolve(src, ChoiceCtx(node)) ?? new List<string>(),
                        CurrentString(node, pd.name, ""),
                        v => WriteOverride(node, pd.name, v),
                        allowCustom: ParamChoiceProviders.AllowsCustom(src),
                        separator: '.', display: null, panelToScreen: PanelToScreen, tooltip: tip);

                // 该参数的值指向一条领域数据（如对话 lineKey → 数据库条目）：下拉 + 下方内联「引用数据」编辑区，随选值刷新。
                // 框架只调缝 ParamReferenceEditors（领域注入），不认识具体数据类型。
                var container = new VisualElement();
                var refSection = new VisualElement();
                void RefreshRef()
                {
                    refSection.Clear();
                    var editor = ParamReferenceEditors.Build(src, ChoiceCtx(node), CurrentString(node, pd.name, ""));
                    if (editor == null) return;
                    var card = new CollapsibleCard(true);
                    card.AddToClassList("entry-card");
                    var t = new Label(Localizer.UI("ui.referencedData", "Referenced Data"));
                    t.AddToClassList("inspector-section-title");
                    card.HeaderMid.Add(t);
                    card.Content.Add(editor);
                    refSection.Add(card);
                }
                container.Add(new SearchableDropdownField(label,
                    () => ParamChoiceProviders.Resolve(src, ChoiceCtx(node)) ?? new List<string>(),
                    CurrentString(node, pd.name, ""),
                    v => { WriteOverride(node, pd.name, v); RefreshRef(); },
                    allowCustom: ParamChoiceProviders.AllowsCustom(src),
                    separator: '.', display: null, panelToScreen: PanelToScreen, tooltip: tip));
                container.Add(refSection);
                RefreshRef();
                return container;
            }
            switch (pd.type.kind)
            {
                case TypeKind.BlackboardKeyRef:
                {
                    // 引用一个黑板变量 key：列出**全部**已声明的 key（BBKey 只是"引用某个 key"，不限定值类型）。
                    // 置顶 "" 哨兵 = "不引用 / 总是可见"，显示为"（无）"。key 可能很多 → 同样走可搜索下拉（仅选已存在的，不允许自创）。
                    return new SearchableDropdownField(label,
                        () => { var keys = m_Blackboard != null ? m_Blackboard.KeysOfType(null).ToList() : new List<string>(); keys.Insert(0, ""); return keys; },
                        CurrentString(node, pd.name, ""),
                        v =>
                        {
                            WriteOverride(node, pd.name, v);
                            // 有"值"参数的值域跟随此键时，键一变其可选集合就变 —— 重建面板让值编辑器换型。
                            if (node.Definition.Parameters.Any(q => q.type != null
                                    && q.type.kind == TypeKind.BlackboardValueRef && q.type.enumOrObjectName == pd.name))
                                RebuildSoon();
                        },
                        allowCustom: false, separator: '\0',
                        display: s => string.IsNullOrEmpty(s) ? Localizer.UI("ui.none", "(None)") : s,
                        panelToScreen: PanelToScreen, tooltip: tip);
                }
                case TypeKind.BlackboardValueRef:
                    // 值域由所引用的黑板键决定：Bool 键→true/false 下拉、数值键→数字框、其余→文本框。
                    return FollowKeyValueEditor(pd, node, label, tip);
                case TypeKind.Unit:
                {
                    // 可组合单元槽：折叠 Foldout + 类型下拉（全局通用/领域，按族过滤）+ 递归字段/装饰嵌套。
                    var units = new UnitInspector(m_Asset, m_Blackboard, PanelToScreen, RebuildSoon);
                    return units.BuildSlot(node.Instance, pd.name, pd.type.enumOrObjectName, label, tip);
                }
                case TypeKind.Enum:
                {
                    // 有限固定值 → 原生枚举下拉（规范 §1 EnumDropdownField）：自带原生箭头，无需搜索框。
                    var names = EnumNamesOf(pd.type.enumOrObjectName);
                    return new EnumDropdownField(label, names, CurrentString(node, pd.name, names.FirstOrDefault()),
                        v => WriteOverride(node, pd.name, v),
                        display: s => string.IsNullOrEmpty(s) ? Localizer.UI("ui.unset", "(unset)") : s, tooltip: tip);
                }
                case TypeKind.Object:
                {
                    var objectType = ResolveType(pd.type.enumOrObjectName) ?? typeof(UnityEngine.Object);
                    if (!string.IsNullOrEmpty(pd.choiceSource) && ParamChoiceProviders.Has(pd.choiceSource))
                    {
                        var src = pd.choiceSource;
                        return new SearchableDropdownField(label,
                            () =>
                            {
                                var choices = ParamChoiceProviders.Resolve(src, ChoiceCtx(node)) ?? new List<string>();
                                choices.Insert(0, "");
                                return choices;
                            },
                            AssetDatabase.GetAssetPath(ParamResolver.ResolveObject(node.Instance, pd.name)),
                            path =>
                            {
                                if (string.IsNullOrEmpty(path))
                                {
                                    WriteObjectOverride(node, pd.name, null);
                                    return;
                                }
                                var currentChoices = ParamChoiceProviders.Resolve(src, ChoiceCtx(node))
                                    ?? new List<string>();
                                if (!currentChoices.Contains(path)) return;
                                var value = AssetDatabase.LoadAssetAtPath(path, objectType);
                                if (value != null) WriteObjectOverride(node, pd.name, value);
                            },
                            allowCustom: false, separator: '/',
                            display: path => string.IsNullOrEmpty(path) ? Localizer.UI("ui.none", "(None)") : path,
                            panelToScreen: PanelToScreen, tooltip: tip);
                    }
                    var of = new ObjectField(label)
                    {
                        objectType = objectType,
                        value = ParamResolver.ResolveObject(node.Instance, pd.name),   // 真实引用，构建安全
                        tooltip = tip
                    };
                    of.RegisterValueChangedCallback(e => WriteObjectOverride(node, pd.name, e.newValue));
                    return of;
                }
                case TypeKind.Primitive when pd.hasBounds:
                {
                    var row = new VisualElement();
                    row.AddToClassList("field-row");
                    row.tooltip = tip;
                    var s = new Slider(label, pd.boundsMin, pd.boundsMax) { value = CurrentFloat(node, pd.name) };
                    var nf = new FloatField { value = s.value };
                    nf.AddToClassList("field-num");
                    s.RegisterValueChangedCallback(e => { nf.SetValueWithoutNotify(e.newValue); WriteOverride(node, pd.name, e.newValue.ToString()); });
                    nf.RegisterValueChangedCallback(e => { s.SetValueWithoutNotify(e.newValue); WriteOverride(node, pd.name, e.newValue.ToString()); });
                    row.Add(s); row.Add(nf);
                    return row;
                }
                case TypeKind.Primitive when pd.type.primitive == PrimitiveType.Bool:
                {
                    // 真假=有限固定值 → 原生 true/false 下拉（规范 §1），不再用带深色框的 Toggle。
                    var cur = CurrentBool(node, pd.name) ? "true" : "false";
                    return new EnumDropdownField(label, new List<string> { "true", "false" }, cur,
                        v => WriteOverride(node, pd.name, v), tooltip: tip);
                }
                case TypeKind.Primitive when pd.type.primitive == PrimitiveType.Int:
                {
                    var f = new IntegerField(label) { value = ParseIntInv(CurrentString(node, pd.name, "0")), tooltip = tip };
                    f.RegisterValueChangedCallback(e => WriteOverride(node, pd.name, e.newValue.ToString(CultureInfo.InvariantCulture)));
                    return f;
                }
                case TypeKind.Primitive when pd.type.primitive == PrimitiveType.Float:
                {
                    var f = new FloatField(label) { value = ParseFloatInv(CurrentString(node, pd.name, "0")), tooltip = tip };
                    f.RegisterValueChangedCallback(e => WriteOverride(node, pd.name, e.newValue.ToString(CultureInfo.InvariantCulture)));
                    return f;
                }
                default:
                {
                    var tf = new TextField(label) { value = CurrentString(node, pd.name, ""), tooltip = tip };
                    tf.RegisterValueChangedCallback(e => WriteOverride(node, pd.name, e.newValue));
                    return tf;
                }
            }
        }

        // --- 针对第 4 层 NodeInstance 的 override 读/写 ---
        // 委托给 NodeInspectorEdits，使每次写入都记录 undo + 把所属资产（OWNING ASSET）标脏。
        // inspector 在选中时不会触发 graphViewChanged，所以若不这样做，canvas 永远不会把资产标脏，
        // 一个纯 inspector 改动（针对已保存的图）会在保存/关闭时被悄悄丢失。
        void WriteOverride(NodeView node, string param, string valueJson) =>
            NodeInspectorEdits.WriteParam(m_Asset, node.Instance, param, valueJson);
        void WriteObjectOverride(NodeView node, string param, UnityEngine.Object value) =>
            NodeInspectorEdits.WriteObject(m_Asset, node.Instance, param, value);
        string CurrentString(NodeView n, string param, string fallback)
        {
            // override 优先；否则通过 ParamResolver（4a）从定义的当前默认值回填。
            var resolved = ParamResolver.Resolve(n.Instance, n.Definition, param);
            return resolved ?? fallback;
        }
        float CurrentFloat(NodeView n, string p) => float.TryParse(CurrentString(n, p, "0"), out var f) ? f : 0f;
        bool CurrentBool(NodeView n, string p) => bool.TryParse(CurrentString(n, p, "false"), out var b) && b;

        // BlackboardValueRef：值域跟随某个兄弟“键”参数所引用的黑板变量的类型。生成对应的类型化控件
        //（Bool→true/false 下拉、Int/Float→数字框、String/未解析→文本框）。写回仍是字符串覆盖，
        // 与运行时按“键的声明类型”解析值保持一致（裸字符串自由值是无法约束的兜底）。
        VisualElement FollowKeyValueEditor(ParamDef pd, NodeView node, string label, string tip)
        {
            var keyName = CurrentString(node, pd.type.enumOrObjectName, "");   // 当前引用的黑板键
            var keyType = BlackboardKeyType(keyName);
            if (keyType != null && keyType.kind == TypeKind.Primitive)
            {
                switch (keyType.primitive)
                {
                    case PrimitiveType.Bool:
                    {
                        var cur = CurrentString(node, pd.name, "");
                        if (string.IsNullOrEmpty(cur)) cur = "false";          // 空值在运行时即 false，下拉同此语义
                        return new EnumDropdownField(label, new List<string> { "true", "false" }, cur,
                            v => WriteOverride(node, pd.name, v), tooltip: tip);
                    }
                    case PrimitiveType.Int:
                    {
                        var f = new IntegerField(label) { value = ParseIntInv(CurrentString(node, pd.name, "0")), tooltip = tip };
                        f.RegisterValueChangedCallback(e => WriteOverride(node, pd.name, e.newValue.ToString(CultureInfo.InvariantCulture)));
                        return f;
                    }
                    case PrimitiveType.Float:
                    {
                        var f = new FloatField(label) { value = ParseFloatInv(CurrentString(node, pd.name, "0")), tooltip = tip };
                        f.RegisterValueChangedCallback(e => WriteOverride(node, pd.name, e.newValue.ToString(CultureInfo.InvariantCulture)));
                        return f;
                    }
                }
            }
            // String / 未设置键 / 非基元（Vector/Color/…）：回退为自由文本，无法约束为有限集合。
            var tf = new TextField(label) { value = CurrentString(node, pd.name, ""), tooltip = tip };
            tf.RegisterValueChangedCallback(e => WriteOverride(node, pd.name, e.newValue));
            return tf;
        }

        TypeRef BlackboardKeyType(string key) =>
            m_Blackboard == null || string.IsNullOrEmpty(key)
                ? null
                : m_Blackboard.Find(key)?.type;

        // 延迟到下一帧重建检视面板：避免在“键”下拉的值变更回调里就地销毁正在派发事件的控件。
        void RebuildSoon() =>
            schedule.Execute(() => { if (m_Current != null) Show(m_Current, m_Registry, m_Blackboard, m_Asset); });

        static int ParseIntInv(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
        static float ParseFloatInv(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;

        // 可搜索下拉用通用控件 SearchableDropdownField（NodeEditor.Editor，任何面板可复用）；此处只组装上下文。
        ParamChoiceContext ChoiceCtx(NodeView node) =>
            new ParamChoiceContext { asset = m_Asset, instance = node.Instance, registry = m_Registry, blackboard = m_Blackboard };

        static List<string> EnumNamesOf(string enumName)
        {
            var t = ResolveType(enumName);
            return t != null && t.IsEnum ? Enum.GetNames(t).ToList() : new List<string>();
        }
        static Type ResolveType(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
    }

    // 修改 NodeInstance 的 overrides/name，并保证所属的图资产被持久化：每次写入都记录一个资产的
    // Undo 快照（这样 Ctrl+Z 能回退 inspector 的编辑，与 canvas 的结构性编辑保持一致），
    // 并把它标脏（这样 Unity 在保存/关闭时会真正序列化该改动）。提取为纯静态类，
    // 以便在没有绑定面板的情况下也能单元测试。asset 参数可以为 null（不记录/不标脏）。
    public static class NodeInspectorEdits
    {
        public static void WriteParam(NodeGraphAsset asset, NodeInstance inst, string param, string valueJson)
        {
            Mark(asset);
            var ov = inst.parameterOverrides.FirstOrDefault(p => p.paramName == param);
            if (ov == null) { ov = new ParamOverride { paramName = param }; inst.parameterOverrides.Add(ov); }
            ov.valueJson = valueJson;
            Dirty(asset);
        }

        public static void WriteObject(NodeGraphAsset asset, NodeInstance inst, string param, UnityEngine.Object value)
        {
            Mark(asset);
            var ov = inst.objectOverrides.FirstOrDefault(o => o.paramName == param);
            if (ov == null) { ov = new ObjectOverride { paramName = param }; inst.objectOverrides.Add(ov); }
            ov.value = value;
            Dirty(asset);
        }

        // 持久化每个节点的自定义名称；返回要显示的标题（自定义名称，或当名称被清空时返回定义的
        // 默认值）。空字符串/空白会清除该 override，使节点回退到默认值。
        public static string Rename(NodeGraphAsset asset, NodeInstance inst, NodeDefinition def, string newName)
        {
            Mark(asset);
            inst.displayName = string.IsNullOrWhiteSpace(newName) ? null : newName;
            Dirty(asset);
            return inst.displayName ?? (def != null ? (def.DisplayName ?? def.name) : null);
        }

        // 持久化每个节点的备注（带 undo + 标脏）。空白会清除备注。
        public static void WriteNote(NodeGraphAsset asset, NodeInstance inst, string note)
        {
            Mark(asset);
            inst.note = string.IsNullOrWhiteSpace(note) ? null : note;
            Dirty(asset);
        }

        // 解析节点视图标题：备注 note（非空）> 自定义名 displayName（非空）> 定义的本地化名称。
        public static string ResolveTitle(NodeInstance inst, NodeDefinition def) =>
            !string.IsNullOrEmpty(inst.note) ? inst.note
          : !string.IsNullOrEmpty(inst.displayName) ? inst.displayName
          : Localizer.NodeName(def);

        // 修改某节点实例的可组合单元树（unitOverrides 及其内部嵌套字段，由 UnitInspector 经反射改写）。
        // 与其它写入一致：记录整资产 undo + 标脏，使 Ctrl+Z 与保存/关闭都覆盖单元编辑（硬规则#2）。mutate 内做实际改动。
        public static void EditUnits(NodeGraphAsset asset, System.Action mutate)
        {
            Mark(asset);
            mutate?.Invoke();
            Dirty(asset);
        }

        static void Mark(NodeGraphAsset a) { if (a != null) Undo.RegisterCompleteObjectUndo(a, "Edit Node"); }
        static void Dirty(NodeGraphAsset a) { if (a != null) EditorUtility.SetDirty(a); }
    }

    // ---- 变量面板：声明 blackboard 变量 + 作用域（第 2 层 blackboard 作用域机制） ----
    public class VariablePane : VisualElement
    {
        public NodeRegistry Registry { get; private set; }
        public BlackboardAsset Blackboard { get; private set; }
        NodeGraphAsset m_Asset;
        readonly VisualElement m_List;

        public VariablePane()
        {
            // 本面板总是嵌在宿主里（分层变量面板 / 数据窗口详情列），宿主已有自己的标题——
            // 这里不再自带「变量」抬头，避免同屏两个同名标题。
            AddToClassList("variable-pane");

            // 变量列表放进可滚动视图：数量很多时也能滚动，且"+ 变量"按钮始终钉在底部可见。
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("variable-scroll");
            scroll.style.flexGrow = 1;
            m_List = new VisualElement();
            m_List.AddToClassList("variable-list");
            scroll.Add(m_List);
            Add(scroll);

            var add = new Button(AddVariable) { text = Localizer.UI("ui.addVariable", "+ Variable") };
            add.AddToClassList("add-button");
            Add(add);
        }

        public void Bind(NodeRegistry registry, BlackboardAsset blackboard, NodeGraphAsset asset)
        {
            Registry = registry; Blackboard = blackboard; m_Asset = asset;
            Refresh();
        }

        void Refresh()
        {
            m_List.Clear();
            if (Blackboard == null) return;
            // 空态提示：新图/新档黑板还没有变量时，说明这里该干什么，而不是留一片空白。
            if (!Blackboard.All().Any())
            {
                m_List.Add(EditorUi.EmptyState(Localizer.UI("ui.noVariables", "No variables yet — add one below.")));
                return;
            }
            foreach (var v in Blackboard.All())
            {
                var row = new VisualElement();
                row.AddToClassList("field-row");
                row.AddToClassList("variable-row");

                // tooltip 用本地化的简短注释（var.<key>.desc）；没有注释时回退为键名本身。
                var desc = Localizer.VariableDesc(v.key);
                var keyLabel = new Label(v.key) { tooltip = string.IsNullOrEmpty(desc) ? v.key : desc };
                keyLabel.AddToClassList("variable-key");
                row.Add(keyLabel);

                // 类型化的"值"编辑器：读/写该变量的 defaultJson（= 对话开始时的初始值）。
                // 作用域不再逐变量标注：整块面板绑定的就是某一档黑板（全局/模块/组），档别即作用域。
                row.Add(ValueEditorFor(Blackboard, v));

                m_List.Add(row);
            }
        }

        // 按变量类型生成内联值编辑器，绑定到 VariableDef.defaultJson（基元类型用强类型控件，
        // 其余回退到纯文本编辑原始串）。改动即写回黑板资产（记录 undo + 标脏），与 Inspector 的参数编辑一致。
        public static IEnumerable<DataItem> Items(BlackboardAsset asset)
        {
            if (asset == null) yield break;
            foreach (var variable in asset.All().Where(v => v != null).OrderBy(v => v.key))
            {
                var group = variable.type != null ? variable.type.kind.ToString() : Localizer.UI("ui.noType", "(no type)");
                yield return new DataItem(variable.key, variable.key, group, variable.defaultJson, variable.key);
            }
        }

        public static VisualElement BuildVariableDetail(NodeRegistry registry, BlackboardAsset asset, NodeGraphAsset graph, DataItem item)
        {
            var root = new VisualElement();
            if (asset == null || item == null)
            {
                root.Add(EditorUi.EmptyState(Localizer.UI("ui.noTierBlackboard", "No blackboard for this scope yet.")));
                return root;
            }

            var variable = asset.Find(item.Id);
            if (variable == null)
            {
                root.Add(EditorUi.EmptyState(Localizer.UI("ui.dataEmpty", "(no data)")));
                return root;
            }

            root.Add(EditorUi.DetailRow(Localizer.UI("ui.key", "Key"), new Label(variable.key)));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.type", "Type"),
                new Label(variable.type != null ? variable.type.kind.ToString() : Localizer.UI("ui.noType", "(no type)"))));
            root.Add(EditorUi.DetailRow(Localizer.UI("ui.default", "Default"), ValueEditorFor(asset, variable)));
            return root;
        }

        static VisualElement ValueEditorFor(BlackboardAsset asset, VariableDef v)
        {
            VisualElement field;
            var t = v.type;
            if (t != null && t.kind == TypeKind.Primitive && t.primitive == PrimitiveType.Bool)
            {
                // 真假=有限固定值 → 原生 true/false 下拉（规范 §1），与检视面板 bool 参数同一长相；
                // 写回仍存 "True"/"False"，与既有 defaultJson 约定一致。
                var f = new EnumDropdownField(null, new List<string> { "true", "false" },
                    ParseBool(v.defaultJson) ? "true" : "false",
                    val => WriteDefault(asset, v, val == "true" ? "True" : "False"));
                field = f;
            }
            else if (t != null && t.kind == TypeKind.Primitive && t.primitive == PrimitiveType.Int)
            {
                var f = new IntegerField { value = ParseInt(v.defaultJson) };
                f.RegisterValueChangedCallback(e => WriteDefault(asset, v, e.newValue.ToString(CultureInfo.InvariantCulture)));
                field = f;
            }
            else if (t != null && t.kind == TypeKind.Primitive && t.primitive == PrimitiveType.Float)
            {
                var f = new FloatField { value = ParseFloat(v.defaultJson) };
                f.RegisterValueChangedCallback(e => WriteDefault(asset, v, e.newValue.ToString(CultureInfo.InvariantCulture)));
                field = f;
            }
            else
            {
                // String / Vector / Color / Enum / Object / 未知类型：用纯文本编辑原始 json 串。
                var f = new TextField { value = v.defaultJson ?? "" };
                f.RegisterValueChangedCallback(e => WriteDefault(asset, v, e.newValue));
                field = f;
            }
            field.AddToClassList("variable-value");
            return field;
        }

        // 写回某个黑板变量的默认值（记录 undo + 标脏，使 Unity 真正持久化）。不重建列表，避免编辑时丢焦点。
        static void WriteDefault(BlackboardAsset asset, VariableDef v, string json)
        {
            if (asset == null || v == null) return;
            Undo.RegisterCompleteObjectUndo(asset, "Edit Variable Value");
            v.defaultJson = json;
            EditorUtility.SetDirty(asset);
        }

        static bool ParseBool(string s) => bool.TryParse(s, out var b) ? b : (s?.Trim() == "1");
        static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
        static float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;

        void AddVariable()
        {
            // 打开一个小弹窗，为变量命名、选择其类型，然后注册进本档黑板（作用域 = 本面板所绑的档）。
            VariableCreatePopup.Open(Blackboard, Refresh);
        }
    }

    // 分层变量面板：节点编辑器左栏用它取代单档 VariablePane。顶部一排档位按钮（全局 / 模块 / 组，按当前图的
    // module/group 动态出），下方一次只显示选中那一档的编辑面板（复用 FrameworkDataSources.BuildLayerPane：
    // 该档黑板缺失则给「新建」按钮）。默认选最专一档（组），让「编辑这张图的变量」直达它自己的图黑板。
    // 一次只挂一个 VariablePane（其自带滚动撑满下方），避免多档纵叠时嵌套滚动塌成 0 高。
    public class LayeredVariablePane : VisualElement
    {
        readonly VisualElement m_TierBar;
        readonly VisualElement m_Body;
        NodeRegistry m_Registry;
        NodeGraphAsset m_Graph;

        public LayeredVariablePane()
        {
            AddToClassList("variable-root");
            var header = new Label(Localizer.UI("ui.variables", "Variables"));
            header.AddToClassList("inspector-header");
            Add(header);
            m_TierBar = new VisualElement();
            m_TierBar.AddToClassList("ne-seg-bar");
            Add(m_TierBar);
            m_Body = new VisualElement { style = { flexGrow = 1 } };
            Add(m_Body);
        }

        // 按当前图重建档位按钮。全局档恒有；模块档在图有 module 时出；组档在图同时有 module+group 时出。
        public void Bind(NodeRegistry registry, NodeGraphAsset graph)
        {
            m_Registry = registry; m_Graph = graph;
            m_TierBar.Clear();
            m_Body.Clear();

            var tiers = new List<(string title, string module, string group)>
            {
                (Localizer.UI("ui.globalVariables", "Global Variables"), "", ""),
            };
            if (graph != null && !string.IsNullOrEmpty(graph.module))
            {
                tiers.Add((Localizer.UI("ui.moduleVariables", "Module Variables"), graph.module, ""));
                if (!string.IsNullOrEmpty(graph.group))
                    tiers.Add((Localizer.UI("ui.groupVariables", "Group Variables"), graph.module, graph.group));
            }

            for (int i = 0; i < tiers.Count; i++)
            {
                int idx = i;
                var btn = new Button(() => Select(tiers, idx)) { text = tiers[i].title };
                btn.AddToClassList("ne-seg-btn");
                m_TierBar.Add(btn);
            }
            Select(tiers, tiers.Count - 1);   // 默认最专一档（组 > 模块 > 全局）
        }

        void Select(List<(string title, string module, string group)> tiers, int idx)
        {
            for (int i = 0; i < m_TierBar.childCount; i++)
                m_TierBar[i].EnableInClassList("is-selected", i == idx);
            m_Body.Clear();
            var t = tiers[idx];
            var ctx = new DataSourceContext { Registry = m_Registry, Graph = m_Graph, Blackboard = BlackboardLocator.FindGlobal() };
            m_Body.Add(FrameworkDataSources.BuildLayerPane(ctx, t.module, t.group));
        }
    }
}
