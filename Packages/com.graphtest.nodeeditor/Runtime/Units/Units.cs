// Units.cs — 可组合「单元（Unit）」子系统的「全局通用」具体单元（框架层）。Runtime/ 程序集，无 editor 依赖。
//
// 角色族基类（Unit / ConditionUnit / ProviderUnit / ActionUnit / ControlUnit）定义在 NodeRuntime.cs（4b）——
// 它们是本套件早就约定好的四角色身份基类，这里给它们补齐「内联可组合」的统一执行入口
// （Evaluate/Get/Execute/Tick，均以 4b 的 NodeContext 为上下文，经 ctx.blackboard 读写）。
//
// 为什么存在：节点不应自带一套比较/门控/赋值逻辑（key/op/value 烘成参数）。需要条件/取值/副作用时，
// 节点持有一个 Unit 槽（NodeInstance.unitOverrides，见 NodeDataTypes.cs），内联存一棵多态对象树，
// 经 [SerializeReference] 序列化，可层层装饰（And/Or/Not、Sequence/Conditional…）。编辑器从「全局通用
// （本程序集）+ 领域（如 Dialogue.Runtime）」两级注册表里按角色族下拉选择（见 UnitRegistry）。
//
// 这里给出大量「全局通用」具体单元/装饰器；领域专属单元（如触发对话事件，需要领域上下文能力）放在各领域
// Runtime 程序集里，继承同样的族基类即可被注册表发现。单元都是普通 [Serializable] 类（非 SO/MonoBehaviour，
// 按 SerializeReference 内联序列化、从不作为独立 .asset），故不受「每类一文件 MonoScript」硬规则约束，可同文件共处。

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NodeEditor
{
    // 比较 / 算术——全局概念，供全局通用单元复用（原先散落在 Dialogue，现上提到框架）。
    public enum CompareOp { Eq, Neq, Gt, Lt, Gte, Lte }
    public enum ArithOp { Add, Sub, Mul, Div }

    // 创作期元数据：稳定本地化 key + 英文回退。编辑器反射读取并按当前语言解析。
    [AttributeUsage(AttributeTargets.Class)]
    public class UnitAttribute : Attribute
    {
        public string NameKey { get; }
        public string NameFallback { get; }
        public string GroupKey { get; }
        public string GroupFallback { get; }
        public UnitAttribute(string nameKey, string nameFallback, string groupKey, string groupFallback)
        {
            NameKey = nameKey;
            NameFallback = nameFallback;
            GroupKey = groupKey;
            GroupFallback = groupFallback;
        }

        // 给独立版本领域包迁移时保留源码兼容；框架/Dialogue 现行代码与文档只使用四参稳定 key 形式。
        public UnitAttribute(string displayName, string group)
            : this(displayName, displayName, group, group) { }
    }

    // 标记某 string 字段为「黑板键」：检视面板据此把它渲染成已声明 key 的下拉（而非裸文本框）。纯编辑器提示。
    [AttributeUsage(AttributeTargets.Field)]
    public class BlackboardKeyAttribute : Attribute { }

    // ===================== 全局通用：Provider（取值）=====================

    [Serializable] [Unit("unit.const.name", "Constant", "unit.group.provider", "Provider")]
    public class ConstProvider : ProviderUnit
    {
        public PrimitiveType type = PrimitiveType.Float;
        public string value;
        public override object Get(NodeContext ctx) => UnitValues.ToPrimitive(type, value);
    }

    [Serializable] [Unit("unit.blackboardProvider.name", "Read Blackboard", "unit.group.provider", "Provider")]
    public class BlackboardProvider : ProviderUnit
    {
        [BlackboardKey] public string key;
        public override object Get(NodeContext ctx) => ctx.blackboard?.Get(key);
    }

    // 装饰：把两个取值单元用算术运算组合成一个（数值语义）。
    [Serializable] [Unit("unit.arithmeticProvider.name", "Arithmetic", "unit.group.providerDecorator", "Provider/Decorator")]
    public class ArithmeticProvider : ProviderUnit
    {
        [SerializeReference] public ProviderUnit a;
        public ArithOp op;
        [SerializeReference] public ProviderUnit b;
        public override object Get(NodeContext ctx)
        {
            double x = UnitValues.Number(a?.Get(ctx)), y = UnitValues.Number(b?.Get(ctx));
            return op switch
            {
                ArithOp.Add => x + y,
                ArithOp.Sub => x - y,
                ArithOp.Mul => x * y,
                ArithOp.Div => y != 0 ? x / y : 0,
                _ => 0
            };
        }
    }

    // ===================== 全局通用：Condition（判定）=====================

    // 完全可组合的比较：左右各是一个取值单元，按 op 比较（数值或序数，见 UnitValues.Compare）。
    [Serializable] [Unit("unit.compareCondition.name", "Compare", "unit.group.condition", "Condition")]
    public class CompareCondition : ConditionUnit
    {
        [SerializeReference] public ProviderUnit left;
        public CompareOp op;
        [SerializeReference] public ProviderUnit right;
        public override bool Evaluate(NodeContext ctx) => UnitValues.Compare(op, left?.Get(ctx), right?.Get(ctx));
    }

    // 便捷比较（常见快路径）：直接「黑板键 op 字面量」，字面量按该键当前值的类型解析。等价于旧的门控/条件参数。
    [Serializable] [Unit("unit.blackboardCompareCondition.name", "Compare Blackboard", "unit.group.condition", "Condition")]
    public class BlackboardCompareCondition : ConditionUnit
    {
        [BlackboardKey] public string key;
        public CompareOp op;
        public string value;
        public override bool Evaluate(NodeContext ctx)
        {
            object cur = ctx.blackboard?.Get(key);
            object rhs = UnitValues.ParseLike(cur, value);
            return UnitValues.Compare(op, cur, rhs);
        }
    }

    [Serializable] [Unit("unit.alwaysCondition.name", "Constant", "unit.group.condition", "Condition")]
    public class AlwaysCondition : ConditionUnit
    {
        public bool value = true;
        public override bool Evaluate(NodeContext ctx) => value;
    }

    // 装饰：非
    [Serializable] [Unit("unit.notCondition.name", "Not", "unit.group.conditionDecorator", "Condition/Decorator")]
    public class NotCondition : ConditionUnit
    {
        [SerializeReference] public ConditionUnit inner;
        public override bool Evaluate(NodeContext ctx) => !(inner?.Evaluate(ctx) ?? false);
    }

    // 装饰：与（空集为真）
    [Serializable] [Unit("unit.andCondition.name", "And", "unit.group.conditionDecorator", "Condition/Decorator")]
    public class AndCondition : ConditionUnit
    {
        [SerializeReference] public List<ConditionUnit> items = new();
        public override bool Evaluate(NodeContext ctx)
        {
            foreach (var i in items) if (i != null && !i.Evaluate(ctx)) return false;
            return true;
        }
    }

    // 装饰：或（空集为假）
    [Serializable] [Unit("unit.orCondition.name", "Or", "unit.group.conditionDecorator", "Condition/Decorator")]
    public class OrCondition : ConditionUnit
    {
        [SerializeReference] public List<ConditionUnit> items = new();
        public override bool Evaluate(NodeContext ctx)
        {
            foreach (var i in items) if (i != null && i.Evaluate(ctx)) return true;
            return false;
        }
    }

    // ===================== 全局通用：Action（副作用）=====================

    // 设变量：值来自一个取值单元（可组合，如算术/读黑板）。写回前按该键当前值的类型强转。
    [Serializable] [Unit("unit.setVariableAction.name", "Set Variable", "unit.group.action", "Action")]
    public class SetVariableAction : ActionUnit
    {
        [BlackboardKey] public string key;
        [SerializeReference] public ProviderUnit value;
        public override void Execute(NodeContext ctx)
        {
            if (ctx.blackboard == null || string.IsNullOrEmpty(key)) return;
            ctx.blackboard.Set(key, UnitValues.CoerceLike(ctx.blackboard.Get(key), value?.Get(ctx)));
        }
    }

    // 设变量（字面量快路径）：直接写一个字面量，按该键当前值的类型解析。等价于旧的 SetVariable 节点。
    [Serializable] [Unit("unit.setVariableLiteralAction.name", "Set Variable (Literal)", "unit.group.action", "Action")]
    public class SetVariableLiteralAction : ActionUnit
    {
        [BlackboardKey] public string key;
        public string value;
        public override void Execute(NodeContext ctx)
        {
            if (ctx.blackboard == null || string.IsNullOrEmpty(key)) return;
            ctx.blackboard.Set(key, UnitValues.ParseLike(ctx.blackboard.Get(key), value));
        }
    }

    // 装饰：顺序执行多个动作。
    [Serializable] [Unit("unit.sequenceAction.name", "Sequence", "unit.group.actionDecorator", "Action/Decorator")]
    public class SequenceAction : ActionUnit
    {
        [SerializeReference] public List<ActionUnit> items = new();
        public override void Execute(NodeContext ctx) { foreach (var a in items) a?.Execute(ctx); }
    }

    // 装饰：条件执行——条件成立（或无条件）时才执行内层动作。组合了 Condition + Action 两族。
    [Serializable] [Unit("unit.conditionalAction.name", "Conditional", "unit.group.actionDecorator", "Action/Decorator")]
    public class ConditionalAction : ActionUnit
    {
        [SerializeReference] public ConditionUnit condition;
        [SerializeReference] public ActionUnit action;
        public override void Execute(NodeContext ctx)
        {
            if (condition == null || condition.Evaluate(ctx)) action?.Execute(ctx);
        }
    }

    // ===================== 全局通用：Control（编排）=====================

    // 叶子：把一个条件桥接为控制结果（成立=Success，否则=Failure）。让 Control 子树能引用条件族。
    [Serializable] [Unit("unit.conditionControl.name", "Condition", "unit.group.control", "Control")]
    public class ConditionControl : ControlUnit
    {
        [SerializeReference] public ConditionUnit condition;
        public override Status Tick(NodeContext ctx) =>
            (condition?.Evaluate(ctx) ?? false) ? Status.Success : Status.Failure;
    }

    // 选择器：依次 Tick 子节点，遇到第一个「非 Failure」即返回；全失败则 Failure。
    [Serializable] [Unit("unit.selectorControl.name", "Selector", "unit.group.control", "Control")]
    public class SelectorControl : ControlUnit
    {
        [SerializeReference] public List<ControlUnit> children = new();
        public override Status Tick(NodeContext ctx)
        {
            foreach (var ch in children)
            {
                var r = ch?.Tick(ctx) ?? Status.Failure;
                if (r != Status.Failure) return r;
            }
            return Status.Failure;
        }
    }

    // 序列：依次 Tick 子节点，遇到第一个「非 Success」即返回；全成功则 Success。
    [Serializable] [Unit("unit.sequenceControl.name", "Sequence", "unit.group.control", "Control")]
    public class SequenceControl : ControlUnit
    {
        [SerializeReference] public List<ControlUnit> children = new();
        public override Status Tick(NodeContext ctx)
        {
            foreach (var ch in children)
            {
                var r = ch?.Tick(ctx) ?? Status.Success;
                if (r != Status.Success) return r;
            }
            return Status.Success;
        }
    }

    // 并行：每次 Tick **所有**子节点（不短路）。requireAll=true → 全成功才 Success、任一失败即 Failure；
    // false → 任一成功即 Success、全失败才 Failure；以上未定且有 Running 则 Running。空子集：requireAll→Success、否则→Failure。
    [Serializable] [Unit("unit.parallelControl.name", "Parallel", "unit.group.control", "Control")]
    public class ParallelControl : ControlUnit
    {
        [SerializeReference] public List<ControlUnit> children = new();
        public bool requireAll = true;
        public override Status Tick(NodeContext ctx)
        {
            bool anyRunning = false, anySuccess = false, anyFailure = false;
            foreach (var ch in children)
            {
                var r = ch?.Tick(ctx) ?? Status.Failure;
                if (r == Status.Running) anyRunning = true;
                else if (r == Status.Success) anySuccess = true;
                else if (r == Status.Failure) anyFailure = true;
            }
            if (requireAll) return anyFailure ? Status.Failure : anyRunning ? Status.Running : Status.Success;
            return anySuccess ? Status.Success : anyRunning ? Status.Running : Status.Failure;
        }
    }

    // 反转（装饰）：翻转内层控制结果——Success↔Failure；Running/None 原样透传。空内层视为 Failure（无可反转）。
    [Serializable] [Unit("unit.inverterControl.name", "Inverter", "unit.group.control", "Control")]
    public class InverterControl : ControlUnit
    {
        [SerializeReference] public ControlUnit inner;
        public override Status Tick(NodeContext ctx)
        {
            if (inner == null) return Status.Failure;
            var r = inner.Tick(ctx);
            return r == Status.Success ? Status.Failure : r == Status.Failure ? Status.Success : r;
        }
    }

    // ---- 值解析 / 比较助手（框架层，单元共用；Dialogue.ValueParse 现委托至此）----
    public static class UnitValues
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // 按 TypeRef 把字符串解析为类型化值（仅基元；其余/ null 原样返回字符串）。
        public static object To(TypeRef t, string s) =>
            t == null || t.kind != TypeKind.Primitive ? (object)(s ?? "") : ToPrimitive(t.primitive, s);

        public static object ToPrimitive(PrimitiveType p, string s)
        {
            var raw = (s ?? "").Trim();
            return p switch
            {
                PrimitiveType.Bool  => bool.TryParse(raw, out var b) ? b : raw == "1",
                PrimitiveType.Int   => int.TryParse(raw, NumberStyles.Integer, Inv, out var i) ? i : 0,
                PrimitiveType.Float => float.TryParse(raw, NumberStyles.Float, Inv, out var f) ? f : 0f,
                _ => s ?? ""
            };
        }

        // 按「样板值」的运行时类型解析字符串字面量（无需声明类型即可与黑板当前值同型比较/赋值）。
        // 样板为 null 时尽力推断：bool → int → float → string。
        public static object ParseLike(object sample, string s)
        {
            var raw = (s ?? "").Trim();
            switch (sample)
            {
                case bool _:   return bool.TryParse(raw, out var b) ? b : raw == "1";
                case int _:    return int.TryParse(raw, NumberStyles.Integer, Inv, out var i) ? i : 0;
                case long _:   return long.TryParse(raw, NumberStyles.Integer, Inv, out var l) ? l : 0L;
                case float _:  return float.TryParse(raw, NumberStyles.Float, Inv, out var f) ? f : 0f;
                case double _: return double.TryParse(raw, NumberStyles.Float, Inv, out var d) ? d : 0d;
                case string _: return s ?? "";
                case null:
                    if (bool.TryParse(raw, out var bb)) return bb;
                    if (int.TryParse(raw, NumberStyles.Integer, Inv, out var ii)) return ii;
                    if (float.TryParse(raw, NumberStyles.Float, Inv, out var ff)) return ff;
                    return s ?? "";
                default: return s ?? "";
            }
        }

        // 把任意值强转到「样板值」的运行时类型（设变量时用，保证 Const/算术结果落到正确类型）。
        public static object CoerceLike(object sample, object v) => sample switch
        {
            bool _   => Number(v) != 0,
            int _    => (int)Number(v),
            long _   => (long)Number(v),
            float _  => (float)Number(v),
            double _ => Number(v),
            string _ => v?.ToString() ?? "",
            _ => v
        };

        // 归约为 double 以做数值比较/算术。数字串也接受（"10">"9" 用数值序而非字典序）。
        public static double Number(object v) => v switch
        {
            int i    => i,
            long l   => l,
            float f  => f,
            double d => d,
            bool b   => b ? 1 : 0,
            string s when double.TryParse(s, NumberStyles.Any, Inv, out var d) => d,
            _ => 0
        };

        // 把装箱的黑板值扁平化为不变量字符串（对基元类型而言是 To 的逆运算）。
        // 三领域 runner 的快照序列化（Capture）过去各自复制一份——收敛于此单点。
        public static string ToInvariantString(object v) => v switch
        {
            null => "",
            float f  => f.ToString(Inv),
            double d => d.ToString(Inv),
            bool b   => b ? "true" : "false",
            _ => System.Convert.ToString(v, Inv)
        };

        static bool IsNumeric(object v) =>
            v is int || v is long || v is float || v is double || v is bool
            || (v is string s && double.TryParse(s, NumberStyles.Any, Inv, out _));

        // 通用比较：两边都可数值化→按数值；否则按序数字符串。未知 op→false。
        public static bool Compare(CompareOp op, object a, object b)
        {
            int cmp = IsNumeric(a) && IsNumeric(b)
                ? Number(a).CompareTo(Number(b))
                : string.CompareOrdinal(a?.ToString() ?? "", b?.ToString() ?? "");
            return op switch
            {
                CompareOp.Eq  => cmp == 0,
                CompareOp.Neq => cmp != 0,
                CompareOp.Gt  => cmp > 0,
                CompareOp.Lt  => cmp < 0,
                CompareOp.Gte => cmp >= 0,
                CompareOp.Lte => cmp <= 0,
                _ => false
            };
        }
    }
}
