// UnitRegistry.cs — 可组合单元的「全局通用 + 领域」两级发现（编辑器层）。Editor/ 程序集。
//
// 检视面板的 Unit 槽下拉据此列出候选：反射扫描所有已加载程序集中某族基类（ConditionUnit/ProviderUnit/
// ActionUnit/ControlUnit）的全部非抽象子类，按族过滤，并区分「全局通用」（与框架 Unit 基类同处一个程序集，
// 即 NodeEditor.Runtime）与「领域」（其余程序集，如 Dialogue.Runtime）。沿用本套件其它反射发现的风格
// （NodeMenuAttribute 的添加对话框、ParamChoiceProviders）。结果按程序集缓存一次。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    // 下拉用的一条候选：具体类型 + 显示名 + 分组 + 是否领域（false=全局通用）。
    public struct UnitChoice
    {
        public Type type;
        public string displayName;
        public string group;
        public bool isDomain;
    }

    public static class UnitRegistry
    {
        // family（"Condition"/"Provider"/"Action"/"Control"）→ 族基类。
        static readonly Dictionary<string, Type> s_Families = new()
        {
            { "Condition", typeof(ConditionUnit) },
            { "Provider",  typeof(ProviderUnit) },
            { "Action",    typeof(ActionUnit) },
            { "Control",   typeof(ControlUnit) },
        };

        static List<UnitChoice> s_All;   // 全部具体单元（一次扫描，缓存）
        static Language? s_Language;

        public static Type FamilyBase(string family) => family != null && s_Families.TryGetValue(family, out var t) ? t : null;

        // 给定 family 基类，取该族 Unit 应填什么（单个/列表都用同一组候选，列表由检视面板自行加增删）。
        public static IEnumerable<UnitChoice> ForFamily(string family)
        {
            var baseType = FamilyBase(family);
            if (baseType == null) return Enumerable.Empty<UnitChoice>();
            EnsureScanned();
            return s_All
                .Where(c => baseType.IsAssignableFrom(c.type))
                // 全局通用在前、领域在后，再按分组/名称稳定排序——下拉里「全局通用 / 领域」自然分块。
                .OrderBy(c => c.isDomain).ThenBy(c => c.group, StringComparer.Ordinal).ThenBy(c => c.displayName, StringComparer.Ordinal)
                .ToList();
        }

        static void EnsureScanned()
        {
            var language = Localizer.Lang;
            if (s_All != null && s_Language == language) return;
            s_Language = language;
            s_All = new List<UnitChoice>();
            var frameworkAsm = typeof(Unit).Assembly;   // 与 Unit 基类同程序集 = 全局通用
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(Unit).IsAssignableFrom(t)) continue;
                    var attr = t.GetCustomAttribute<UnitAttribute>();
                    s_All.Add(new UnitChoice
                    {
                        type = t,
                        displayName = attr != null ? Localizer.UI(attr.NameKey, attr.NameFallback) : Nicify(t.Name),
                        group = attr != null ? Localizer.UI(attr.GroupKey, attr.GroupFallback) : "",
                        isDomain = t.Assembly != frameworkAsm
                    });
                }
            }
        }

        static string Nicify(string n) => n.EndsWith("Unit") ? n[..^4] : n;
    }
}
