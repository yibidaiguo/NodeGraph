// UnitInspector.cs — 可组合单元槽的递归检视编辑器（编辑器层）。Editor/ 程序集。
//
// 给一个 Unit 槽渲染：可折叠 Foldout（收起/展开）+ 顶部「类型下拉」（UnitRegistry：全局通用/领域 两级、
// 按族过滤、可搜索）+ 当前单元的字段。字段按类型生成强类型控件；遇到嵌套 Unit 字段（装饰器的 inner）
// 或 List<…Unit>（And/Or/Sequence 的 items）则递归出子 Foldout + 子下拉——从而「把一个装饰成多个」、任意层级。
//
// 所有写入都走 NodeInspectorEdits.EditUnits（Undo.RegisterCompleteObjectUndo + SetDirty，满足数据安全硬规则#2）。
// 结构性改动（换单元类型 / 列表增删）后调用 rebuild 让整面板重建（与既有 RebuildSoon 同策略）；
// 叶子值改动只写不重建，避免丢焦点。用反射读写单元字段（单元是普通 [Serializable] 类，按值/SerializeReference 内联）。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public class UnitInspector
    {
        readonly NodeGraphAsset m_Asset;
        readonly BlackboardSet m_Blackboard;   // 这张图的有效黑板（全局⊕模块⊕组合并视图）
        readonly Func<Vector2, Vector2> m_PanelToScreen;
        readonly Action m_Rebuild;

        public UnitInspector(NodeGraphAsset asset, BlackboardSet bb, Func<Vector2, Vector2> panelToScreen, Action rebuild)
        {
            m_Asset = asset; m_Blackboard = bb; m_PanelToScreen = panelToScreen; m_Rebuild = rebuild;
        }

        // 节点实例上的一个顶层 Unit 槽（参数 paramName，族 family）。
        public VisualElement BuildSlot(NodeInstance inst, string paramName, string family, string label, string tooltip)
        {
            return BuildUnitField(label, tooltip, family,
                get: () => inst.unitOverrides.FirstOrDefault(o => o.paramName == paramName)?.value,
                set: u => NodeInspectorEdits.EditUnits(m_Asset, () => SetSlot(inst, paramName, u)));
        }

        void SetSlot(NodeInstance inst, string paramName, Unit u)
        {
            var ov = inst.unitOverrides.FirstOrDefault(o => o.paramName == paramName);
            if (u == null) { if (ov != null) inst.unitOverrides.Remove(ov); return; }
            if (ov == null) { ov = new UnitOverride { paramName = paramName }; inst.unitOverrides.Add(ov); }
            ov.value = u;
        }

        // 一个「单元引用位置」的编辑器：由 get/set 抽象（顶层槽=改 override；嵌套=反射改父单元字段/列表元素）。
        VisualElement BuildUnitField(string label, string tooltip, string family, Func<Unit> get, Action<Unit> set)
        {
            var current = get();
            var foldout = new Foldout { value = true };
            foldout.AddToClassList("unit-slot");
            foldout.text = $"{label}: {DisplayOf(current)}";
            // 悬浮说明走 EditorUi.InstallTooltip 的自绘主题化 tip（窗口根拦 TooltipEvent），不占面板空间。
            if (!string.IsNullOrEmpty(tooltip)) foldout.tooltip = tooltip;

            // 类型选择下拉：全局通用/领域 两级，按族过滤；可搜索；含「（清空）」。
            var (paths, map) = ChoicesFor(family);
            string currentPath = current != null ? PathOf(current.GetType(), family) : "";
            foldout.Add(new SearchableDropdownField(Localizer.UI("ui.unitType", "Type"),
                () => paths, currentPath,
                v =>
                {
                    Unit next = string.IsNullOrEmpty(v) || !map.TryGetValue(v, out var t) ? null
                              : (Unit)Activator.CreateInstance(t);
                    set(next);
                    m_Rebuild?.Invoke();   // 换型→字段集合变，整面板重建
                },
                allowCustom: false, separator: '/',
                display: s => string.IsNullOrEmpty(s) ? "（无）" : s,
                panelToScreen: m_PanelToScreen, tooltip: tooltip));

            if (current != null)
            {
                var body = new VisualElement();
                body.AddToClassList("unit-body");
                BuildFields(body, current);
                foldout.Add(body);
            }
            return foldout;
        }

        // 渲染某单元实例的全部可序列化字段。
        void BuildFields(VisualElement body, Unit unit)
        {
            foreach (var f in SerializableFields(unit.GetType()))
            {
                var label = Nicify(f.Name);
                var ft = f.FieldType;

                if (typeof(Unit).IsAssignableFrom(ft))               // 嵌套单个单元（装饰 inner）
                {
                    body.Add(BuildUnitField(label, null, FamilyOf(ft),
                        () => (Unit)f.GetValue(unit),
                        v => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, v))));
                }
                else if (IsUnitList(ft, out var elem))              // 嵌套单元列表（And/Or/Sequence items）
                {
                    body.Add(BuildUnitList(label, FamilyOf(elem), (IList)f.GetValue(unit) ?? CreateList(f, unit), elem));
                }
                else if (HasBlackboardKey(f))                       // 黑板键 → 已声明 key 下拉
                {
                    body.Add(KeyDropdown(label, () => (string)f.GetValue(unit),
                        v =>
                        {
                            NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, v));
                            if (HasBlackboardTypedLiteral(unit)) m_Rebuild?.Invoke();
                        }));
                }
                else if (ft.IsEnum)
                {
                    body.Add(EnumDropdown(label, ft, f.GetValue(unit)?.ToString(),
                        v => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, Enum.Parse(ft, v)))));
                }
                else if (ft == typeof(bool))
                {
                    body.Add(BoolDropdown(label, (bool)f.GetValue(unit),
                        v => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, v))));
                }
                else if (ft == typeof(int))
                {
                    var n = new IntegerField(label) { value = (int)f.GetValue(unit) };
                    n.RegisterValueChangedCallback(e => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, e.newValue)));
                    body.Add(n);
                }
                else if (ft == typeof(float))
                {
                    var n = new FloatField(label) { value = (float)f.GetValue(unit) };
                    n.RegisterValueChangedCallback(e => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, e.newValue)));
                    body.Add(n);
                }
                else if (ft == typeof(string))
                {
                    var valueType = BlackboardLiteralType(unit, f);
                    if (valueType != null)
                    {
                        body.Add(BlackboardLiteralEditor(label, valueType, (string)f.GetValue(unit) ?? "",
                            v => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, v))));
                        continue;
                    }
                    var tf = new TextField(label) { value = (string)f.GetValue(unit) ?? "" };
                    tf.RegisterValueChangedCallback(e => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, e.newValue)));
                    body.Add(tf);
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(ft))
                {
                    var of = new ObjectField(label) { objectType = ft, value = (UnityEngine.Object)f.GetValue(unit) };
                    of.RegisterValueChangedCallback(e => NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(unit, e.newValue)));
                    body.Add(of);
                }
                else
                {
                    var note = new Label($"{label}: ({ft.Name})");
                    note.AddToClassList("field-note");
                    body.Add(note);
                }
            }
        }

        // 单元列表：每个元素一个递归子槽 + 移除按钮；底部「+ 添加」。增删后重建整面板。
        VisualElement BuildUnitList(string label, string family, IList list, Type elem)
        {
            var box = new VisualElement();
            box.AddToClassList("unit-list");
            var head = new Label(label);
            head.AddToClassList("inspector-section-title");
            box.Add(head);

            for (int idx = 0; idx < list.Count; idx++)
            {
                int i = idx;
                var row = new VisualElement();
                row.AddToClassList("unit-list-row");
                row.Add(BuildUnitField($"[{i}]", null, family,
                    () => (Unit)list[i],
                    v => NodeInspectorEdits.EditUnits(m_Asset, () => list[i] = v)));
                var del = new Button(() => NodeInspectorEdits.EditUnits(m_Asset, () => { list.RemoveAt(i); m_Rebuild?.Invoke(); })) { text = "✕" };
                del.AddToClassList("unit-list-del");
                row.Add(del);
                box.Add(row);
            }

            var add = new Button(() => NodeInspectorEdits.EditUnits(m_Asset, () => { list.Add(null); m_Rebuild?.Invoke(); }))
            { text = Localizer.UI("ui.addUnit", "+ Add") };
            add.AddToClassList("add-button");
            box.Add(add);
            return box;
        }

        // ---- 控件助手 ----

        VisualElement KeyDropdown(string label, Func<string> get, Action<string> set) =>
            new SearchableDropdownField(label,
                () => { var keys = m_Blackboard != null ? m_Blackboard.KeysOfType(null).ToList() : new List<string>(); keys.Insert(0, ""); return keys; },
                get() ?? "", set, allowCustom: false, separator: '\0',
                display: s => string.IsNullOrEmpty(s) ? "（无）" : s, panelToScreen: m_PanelToScreen);

        // 布尔字段：有限固定值 → 原生 true/false 下拉（规范 §1 EnumDropdownField），避免 Toggle 的深色输入框。
        // For units shaped like "[BlackboardKey] string key + string value", keep the serialized
        // value as a string but render it with the selected blackboard key's declared type.
        VisualElement BlackboardLiteralEditor(string label, TypeRef type, string current, Action<string> set)
        {
            if (type.kind == TypeKind.Primitive)
            {
                switch (type.primitive)
                {
                    case PrimitiveType.Bool:
                    {
                        var cur = ParseBoolString(current) ? "true" : "false";
                        return new EnumDropdownField(label, new List<string> { "true", "false" }, cur, set);
                    }
                    case PrimitiveType.Int:
                    {
                        var f = new IntegerField(label) { value = ParseIntInv(current) };
                        f.RegisterValueChangedCallback(e => set(e.newValue.ToString(CultureInfo.InvariantCulture)));
                        return f;
                    }
                    case PrimitiveType.Float:
                    {
                        var f = new FloatField(label) { value = ParseFloatInv(current) };
                        f.RegisterValueChangedCallback(e => set(e.newValue.ToString(CultureInfo.InvariantCulture)));
                        return f;
                    }
                }
            }

            var tf = new TextField(label) { value = current ?? "" };
            tf.RegisterValueChangedCallback(e => set(e.newValue));
            return tf;
        }

        VisualElement BoolDropdown(string label, bool current, Action<bool> set) =>
            new EnumDropdownField(label, new List<string> { "true", "false" }, current ? "true" : "false",
                v => set(v == "true"));

        // 枚举字段：有限固定值 → 原生枚举下拉（规范 §1），自带原生箭头、无需搜索框。
        VisualElement EnumDropdown(string label, Type enumType, string current, Action<string> set)
        {
            var names = Enum.GetNames(enumType).ToList();
            current ??= names.FirstOrDefault() ?? "";
            return new EnumDropdownField(label, names, current, set,
                display: s => string.IsNullOrEmpty(s) ? Localizer.UI("ui.unset", "(unset)") : s);
        }

        // ---- 反射 / 注册表助手 ----

        // family → 候选路径串（「全局通用|领域 / group / 显示名」）+ 路径→类型映射；含「（清空）」。
        (List<string>, Dictionary<string, Type>) ChoicesFor(string family)
        {
            var paths = new List<string> { Localizer.UI("ui.unitClear", "(clear)") };
            var map = new Dictionary<string, Type>();
            foreach (var c in UnitRegistry.ForFamily(family))
            {
                var p = PathOf(c);
                paths.Add(p);
                map[p] = c.type;
            }
            return (paths, map);
        }

        static string PathOf(UnitChoice c)
        {
            var scope = c.isDomain ? Localizer.UI("ui.unitDomain", "Domain") : Localizer.UI("ui.unitGlobalCommon", "Global Common");
            return string.IsNullOrEmpty(c.group) ? $"{scope}/{c.displayName}" : $"{scope}/{c.group}/{c.displayName}";
        }

        // 当前单元类型在某族下的路径（用于下拉回显当前值）。
        static string PathOf(Type t, string family)
        {
            var c = UnitRegistry.ForFamily(family).FirstOrDefault(x => x.type == t);
            return c.type != null ? PathOf(c) : "";
        }

        static string DisplayOf(Unit u)
        {
            if (u == null) return Localizer.UI("ui.none", "(None)");
            var a = u.GetType().GetCustomAttribute<UnitAttribute>();
            return a != null ? Localizer.UI(a.NameKey, a.NameFallback) : Nicify(u.GetType().Name);
        }

        static string FamilyOf(Type fieldType)
        {
            if (typeof(ConditionUnit).IsAssignableFrom(fieldType)) return "Condition";
            if (typeof(ProviderUnit).IsAssignableFrom(fieldType)) return "Provider";
            if (typeof(ActionUnit).IsAssignableFrom(fieldType)) return "Action";
            if (typeof(ControlUnit).IsAssignableFrom(fieldType)) return "Control";
            return "";
        }

        static bool IsUnitList(Type t, out Type elem)
        {
            elem = null;
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(List<>)) return false;
            elem = t.GetGenericArguments()[0];
            return typeof(Unit).IsAssignableFrom(elem);
        }

        IList CreateList(FieldInfo f, Unit owner)
        {
            var list = (IList)Activator.CreateInstance(f.FieldType);
            NodeInspectorEdits.EditUnits(m_Asset, () => f.SetValue(owner, list));
            return list;
        }

        static bool HasBlackboardKey(FieldInfo f) => f.GetCustomAttribute<BlackboardKeyAttribute>() != null;

        // 与 Unity 序列化口径一致：public 实例字段 + 带 [SerializeField] 的非公有字段；跳过 [NonSerialized]/静态/常量。
        TypeRef BlackboardLiteralType(Unit unit, FieldInfo valueField)
        {
            if (unit == null || valueField == null || valueField.FieldType != typeof(string) || valueField.Name != "value")
                return null;

            var keyField = SerializableFields(unit.GetType())
                .FirstOrDefault(f => f.FieldType == typeof(string) && HasBlackboardKey(f));
            var key = keyField?.GetValue(unit) as string;
            return string.IsNullOrEmpty(key) ? null : m_Blackboard?.Find(key)?.type;
        }

        static bool HasBlackboardTypedLiteral(Unit unit) =>
            unit != null
            && SerializableFields(unit.GetType()).Any(f => f.FieldType == typeof(string) && HasBlackboardKey(f))
            && SerializableFields(unit.GetType()).Any(f => f.FieldType == typeof(string) && f.Name == "value");

        static bool ParseBoolString(string s)
        {
            var raw = (s ?? "").Trim();
            return bool.TryParse(raw, out var b) ? b : raw == "1";
        }

        static int ParseIntInv(string s) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;

        static float ParseFloatInv(string s) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;

        static IEnumerable<FieldInfo> SerializableFields(Type t)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.IsStatic || f.IsLiteral || f.IsInitOnly) continue;
                if (f.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                yield return f;
            }
        }

        static string Nicify(string n) => ObjectNames.NicifyVariableName(n.EndsWith("Unit") ? n[..^4] : n);
    }
}
