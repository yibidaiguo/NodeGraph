// NodeDataTypes.cs — 子层 4a（node-data），数据基础。
// 每个其他子层都依赖的纯数据类型；它自身不依赖任何东西。
// Namespace NodeEditor。Runtime/ 程序集（可用于构建，而非仅编辑器）。
// 实现 layer-3 node-schema.md。Unity 2021.3+（[SerializeReference]）。
//
// 注意：asset 支撑的 ScriptableObject（BlackboardAsset、NodeGraphAsset、NodeRegistry）以及抽象的
// NodeDefinition 的具体子类，各自都存放在以该类命名的独立文件中——Unity 只会把 MonoScript 绑定到
// 名称与文件名匹配的类型上，而未绑定的 ScriptableObject 会把每个创建出来的 .asset 序列化为带损坏 m_Script 的形式。
// 本文件只保留纯粹的 [Serializable] 数据类型以及抽象的 NodeDefinition 基类
//（抽象类型永远不会作为 asset 被实例化，所以它们不需要 MonoScript）。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor
{
    // ---- 类型词汇表（layer-3 TypeRef）----
    public enum PrimitiveType { Bool, Int, Float, String, Vector2, Vector3, Color }
    // BlackboardValueRef 追加在末尾：序列化按序数存储，插在中间会改写已烘焙 .asset 里的既有值。
    // Unit 同理追加在最后：可组合单元槽（单个内联多态树；多个由装饰器单元 Sequence/And/Or 在内部持 List 表达）。
    // family 存于 enumOrObjectName（"Condition"/"Action"/…）。
    public enum TypeKind { Primitive, Enum, BlackboardKeyRef, Object, List, Any, BlackboardValueRef, Unit }

    [Serializable]
    public class TypeRef
    {
        public TypeKind kind;
        public PrimitiveType primitive;
        public string enumOrObjectName;
        // 自引用（List 的元素类型）。必须是托管引用，而不是按值的
        // [Serializable] 字段，否则 Unity 会触发 "Serialization depth limit 7 exceeded" 警告，
        // 并为每个 TypeRef 深度实例化出 7 层嵌套的 TypeRef。[SerializeReference] 能让 null 保持为 null。
        [SerializeReference] public TypeRef element;

        public static TypeRef Float  => new TypeRef { kind = TypeKind.Primitive, primitive = PrimitiveType.Float };
        public static TypeRef Bool   => new TypeRef { kind = TypeKind.Primitive, primitive = PrimitiveType.Bool };
        public static TypeRef Int    => new TypeRef { kind = TypeKind.Primitive, primitive = PrimitiveType.Int };
        public static TypeRef String => new TypeRef { kind = TypeKind.Primitive, primitive = PrimitiveType.String };
        public static TypeRef Any    => new TypeRef { kind = TypeKind.Any };
        public static TypeRef Enum(string typeName)   => new TypeRef { kind = TypeKind.Enum, enumOrObjectName = typeName };
        public static TypeRef Object(string typeName) => new TypeRef { kind = TypeKind.Object, enumOrObjectName = typeName };
        public static TypeRef BBKey()                 => new TypeRef { kind = TypeKind.BlackboardKeyRef };
        // 值域跟随某个兄弟“键”参数所引用的黑板变量类型的“值”参数（keyParam = 兄弟键参数名，存于 enumOrObjectName）。
        // 纯编辑器提示：Inspector 据此把裸字符串值升级为类型化控件（Bool 键→true/false 下拉、数值键→数字框）。
        // 运行时不读它（值按所引用键的声明类型解析），因此对求值/校验/序列化都安全。
        public static TypeRef BBValue(string keyParam) => new TypeRef { kind = TypeKind.BlackboardValueRef, enumOrObjectName = keyParam };
        // 可组合单元槽：family ∈ {"Condition","Provider","Action","Control"}，存于 enumOrObjectName。
        // 编辑器据此把该参数渲染成「类型下拉（全局通用+领域，按族过滤）+ 可折叠字段 + 装饰嵌套」。
        // 运行时不读 TypeRef——单元值存在 NodeInstance.unitOverrides（SerializeReference），经 ResolveUnit 取出。
        public static TypeRef Unit(string family) => new TypeRef { kind = TypeKind.Unit, enumOrObjectName = family };
    }

    // 类型兼容性——layer-3 validation-logic.md 中 compatible(a,b) 的 C# 实现。
    public static class TypeRefCompat
    {
        public static bool Compatible(TypeRef a, TypeRef b)
        {
            if (a == null || b == null) return true;
            if (a.kind == TypeKind.Any || b.kind == TypeKind.Any) return true;
            if (a.kind != b.kind) return false;
            switch (a.kind)
            {
                case TypeKind.Primitive:
                    if (a.primitive == b.primitive) return true;
                    return IsNumeric(a.primitive) && IsNumeric(b.primitive);
                case TypeKind.Enum:
                case TypeKind.Object:
                    return a.enumOrObjectName == b.enumOrObjectName;
                case TypeKind.List:
                    return Compatible(a.element, b.element);
                default:
                    return true;
            }
        }
        static bool IsNumeric(PrimitiveType p) => p == PrimitiveType.Int || p == PrimitiveType.Float;
    }

    // ---- 分类（layer-3 Axis 1/2）----
    public enum NodePurity { PureLogic, Domain }
    public enum NodeRole   { Provider, Condition, Action, Control }
    public enum RuntimeKind { Tick, Evaluate, EnterExecuteExit, None }

    // ---- arity（基数/连接数量约束）----
    public enum ArityKind { Exactly, AtLeast, Range, Optional, Many }

    [Serializable]
    public struct Arity
    {
        public ArityKind kind;
        public int min, max;
        public bool Satisfies(int count) => kind switch
        {
            ArityKind.Exactly  => count == min,
            ArityKind.AtLeast  => count >= min,
            ArityKind.Range    => count >= min && count <= max,
            ArityKind.Optional => count == 0 || count == 1,
            ArityKind.Many     => true,
            _ => false
        };
        public static Arity ExactlyOne => new Arity { kind = ArityKind.Exactly, min = 1 };
        public static Arity AtLeastOne => new Arity { kind = ArityKind.AtLeast, min = 1 };
        public static Arity Optional   => new Arity { kind = ArityKind.Optional };
        public static Arity Many       => new Arity { kind = ArityKind.Many };
    }

    // ---- 端口 / 参数 ----
    // arity 默认为 Many（无约束）：该 struct 的零值会是 Exactly-0，这会让一个未设置的端口
    // 在校验时拒绝所有连接。作者需要为每个端口显式设置 arity。
    [Serializable] public class PortDef  { public string name; [SerializeReference] public TypeRef type; public Arity arity = new Arity { kind = ArityKind.Many }; }
    [Serializable]
    public class ParamDef
    {
        public string name; [SerializeReference] public TypeRef type;
        public string defaultJson;
        public bool hasBounds; public float boundsMin, boundsMax;
        // 可选：候选值来源标签（如 "dialogue.dbKeys"）。非空时编辑器把该 string 参数渲染成
        // 可搜索下拉（候选由领域层经 ParamChoiceProviders 按此标签提供），避免手填 key 出错。
        // 纯编辑器元数据；运行时不读。
        public string choiceSource;
    }

    // ---- blackboard（黑板，实现 layer-2 的作用域划分）----
    // 作用域不再是变量上的字段，而是「变量所在的那块 BlackboardAsset 处于分层的哪一档」：
    // 全局（module/group 皆空）→ 模块（带 module）→ 组（带 module+group）。在哪块 SO 里编辑就是哪个作用域；
    // 运行时由 BlackboardSet 把适用的各档合并（同名 key 就近覆盖）。详见 BlackboardAsset / BlackboardSet。
    [Serializable] public class BlackboardKeyRef { public string key; }
    [Serializable]
    public class VariableDef { public string key; [SerializeReference] public TypeRef type; public string defaultJson; }

    // BlackboardAsset（ScriptableObject）存放在 BlackboardAsset.cs 中——参见上面"每类一文件"的说明。

    // ---- 校验约束（声明式；由 4c 解释执行）----
    [Serializable] public abstract class Constraint { }
    [Serializable] public class ChildArity : Constraint { public string portName; public Arity arity; }
    [Serializable] public class PortType   : Constraint { public string portName; [SerializeReference] public TypeRef type; }
    [Serializable] public class RequiresEntryReachable : Constraint { }
    [Serializable] public class CustomConstraint : Constraint { public string tag; public string payloadJson; }
    // 第 5 种约束：声明该约束的定义，其实例作为可达性播种源——如状态机 AnyState 这类 source-only
    // 伪节点（无入边、不在 entry 列表里，但其出向内容是活的）。消费方：4c 的 CheckReachability。
    [Serializable] public class ReachabilitySeed : Constraint { }

    // 创作期的菜单特性。存放在 Runtime 中（与 NodeDefinition 同处）——它是纯元数据，
    // 没有编辑器依赖，因此 Runtime 程序集中的 NodeDefinition 子类可以携带它，而不会引入
    // 任何 Editor 程序集（那会破坏 player 构建）。编辑器的添加对话框（5b）会反射读取它。
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeMenuAttribute : Attribute
    {
        public string Path { get; }
        public NodeMenuAttribute(string path) => Path = path;
    }

    // ---- 节点定义（layer-3 NodeDefinition）----
    // 抽象类：它自身永远不会作为 asset 被实例化，所以不需要同名文件 / MonoScript。
    // 具体子类（例如对话节点）必须各自存放在以该类命名的独立文件中。
    public abstract class NodeDefinition : ScriptableObject
    {
        [Header("Metadata")]
        [SerializeField] private string id = System.Guid.NewGuid().ToString();
        [SerializeField] private string displayName;
        [SerializeField] private string category;
        [SerializeField] private string docString;

        [Header("Classification")]
        [SerializeField] private NodePurity purity;
        [SerializeField] private NodeRole role;

        [Header("Interface")]
        [SerializeField] private List<PortDef> inputPorts  = new();
        [SerializeField] private List<PortDef> outputPorts = new();
        [SerializeField] private List<ParamDef> parameters = new();
        [SerializeField] private RuntimeKind runtimeKind;

        [Header("Blackboard")]
        [SerializeField] private List<BlackboardKeyRef> blackboardReads  = new();
        [SerializeField] private List<BlackboardKeyRef> blackboardWrites = new();

        [Header("Validation")]
        [SerializeReference] private List<Constraint> constraints = new();

        [Header("Versioning")]
        [SerializeField] private int version = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public NodePurity Purity => purity;
        public NodeRole Role => role;
        public RuntimeKind Runtime => runtimeKind;
        public IReadOnlyList<PortDef> InputPorts => inputPorts;
        public IReadOnlyList<PortDef> OutputPorts => outputPorts;
        public IReadOnlyList<ParamDef> Parameters => parameters;
        public IReadOnlyList<BlackboardKeyRef> Reads => blackboardReads;
        public IReadOnlyList<BlackboardKeyRef> Writes => blackboardWrites;
        public IReadOnlyList<Constraint> Constraints => constraints;
        public int Version => version;

        // ---- 代码创作钩子 ----
        // 子类在 Define() 中声明自己的接口，而不是手工配置 .asset。
        // 编辑器工具会实例化每个子类并调用 RebuildFromCode() 来烘焙出 asset。
        protected void Meta(string name, NodeRole r) { displayName = name; role = r; }
        protected void AddIn(string portName, Arity a)  => inputPorts.Add(new PortDef { name = portName, arity = a, type = TypeRef.Any });
        protected void AddOut(string portName, Arity a) => outputPorts.Add(new PortDef { name = portName, arity = a, type = TypeRef.Any });
        protected void AddParam(string paramName, TypeRef t) => parameters.Add(new ParamDef { name = paramName, type = t });
        // 带候选来源的重载：把该 string 参数标记为"从一组动态候选里选"（编辑器渲染为可搜索下拉）。
        protected void AddParam(string paramName, TypeRef t, string choiceSource) => parameters.Add(new ParamDef { name = paramName, type = t, choiceSource = choiceSource });
        // 可组合单元槽（见 TypeRef.Unit）：节点不自带比较/门控/赋值参数，改持一个 Unit 槽。
        protected void AddUnitParam(string paramName, string family) => parameters.Add(new ParamDef { name = paramName, type = TypeRef.Unit(family) });
        // 声明式校验约束（见上方 Constraint 层次）：子类在 Define() 里声明，由 4c 校验器按子类型分派消费。
        protected void AddConstraint(Constraint c) => constraints.Add(c);
        protected virtual void Define() { }
        // 用代码创作的定义可以声明一个确定性 id（由其类型/种类派生），这样同一个定义在任何机器上、
        // 任何一次重新生成中都会解析到相同的 id——即使定义的 .asset 被重建，graph 仍能继续工作。
        // 为 null 时则保留创建时分配的随机 GUID。
        protected virtual string StableId => null;
        public void RebuildFromCode()
        {
            if (StableId != null) id = StableId;
            inputPorts.Clear(); outputPorts.Clear(); parameters.Clear(); constraints.Clear();
            Define();
        }
    }

    // ---- 节点实例（layer-3 NodeInstance）----
    [Serializable]
    public class NodeInstance
    {
        public string instanceId = System.Guid.NewGuid().ToString();
        public string definitionId;
        public Vector2 position;
        public string displayName;   // 可选的每节点自定义名称；为空/null => 回退到定义的（本地化）名称
        public string note;          // 可选的每节点备注；非空时在视图上作为节点标题显示（优先于 displayName）
        public bool pinned;          // 钉住=固定节点：不可删除（如对话图的进入/退出节点）。框架提供机制，领域层决定谁被钉住。
        public List<Connection> connections = new();
        public List<ParamOverride> parameterOverrides = new();
        public List<ObjectOverride> objectOverrides = new();   // 真实的 asset 引用（构建安全），用于 Object 类型的参数
        public List<UnitOverride> unitOverrides = new();        // 可组合单元槽（SerializeReference 多态树），用于 Unit/UnitList 类型的参数
    }
    [Serializable] public class Connection    { public string fromPort; public string toInstanceId; public string toPort; }
    [Serializable] public class ParamOverride { public string paramName; public string valueJson; }
    // Object 类型的参数保留一个真正的 UnityEngine.Object 引用（能在 player 构建中存活），
    // 而不是仅编辑器可用的 asset 路径。Primitive/enum/key 类型的参数仍以字符串形式保留在 parameterOverrides 中。
    [Serializable] public class ObjectOverride { public string paramName; public UnityEngine.Object value; }
    // Unit 类型的参数保留一棵内联的多态单元树（SerializeReference，构建安全、可嵌套装饰）。
    // 列表本身按值序列化；仅 value 字段是托管引用，从而 null 保持 null、装饰子树可任意深度。
    [Serializable] public class UnitOverride { public string paramName; [SerializeReference] public Unit value; }

    // 此 graph 属于四种 wire-graph 类型中的哪一种。记录下来，以便类型感知的逻辑
    //（4c 中的校验、5a 中的渲染）可以据此分支。Layer 1 的整个决策树
    // 会选出它；如果不存储，这个决策就会在数据层丢失。
    public enum GraphType { ControlFlow, TickTree, Dataflow, DependencyDag }

    // 顶层的 Q0 决策（layer 1）：这是一个 wire-graph 还是一个声明式 planner 编辑器？
    // 捕获到数据中，以便跨族系的工具（"哪个编辑器打开这个 asset？"）可以基于单个 enum 进行切换。
    // Wire-graph 资产报告为 WireGraph；声明式资产（6a 的 DeclarativeAsset）报告为 Declarative。
    public enum AuthoringFamily { WireGraph, Declarative }

    public interface IAuthoringAsset { AuthoringFamily AuthoringFamily { get; } }

    // NodeGraphAsset（ScriptableObject）存放在 NodeGraphAsset.cs 中——参见上面"每类一文件"的说明。
    // NodeRegistry （ScriptableObject）存放在 NodeRegistry.cs  中——参见上面"每类一文件"的说明。

    // ---- 带版本回填的参数解析（layer-2 第 6 点，layer-3 resolveParam）----
    // 实现版本控制契约：实例覆盖优先；缺失的覆盖则从定义的当前默认值回填。
    // 没有这一点，第一次定义版本号提升就会悄无声息地破坏 graph。
    // 被重命名/移除的端口由校验（4c）暴露出来，而不是在这里处理。
    public static class ParamResolver
    {
        public static UnityEngine.Object ResolveObject(NodeInstance inst, string paramName) =>
            inst.objectOverrides.FirstOrDefault(o => o.paramName == paramName)?.value;

        // 取某 Unit 槽的内联单元树（无覆盖 => null，由运行时按语义兜底，如空门控=可见）。
        public static Unit ResolveUnit(NodeInstance inst, string paramName) =>
            inst.unitOverrides.FirstOrDefault(o => o.paramName == paramName)?.value;

        public static string Resolve(NodeInstance inst, NodeDefinition def, string paramName)
        {
            var ov = inst.parameterOverrides.FirstOrDefault(p => p.paramName == paramName);
            if (ov != null) return ov.valueJson;                 // 实例覆盖优先
            var pd = def.Parameters.FirstOrDefault(p => p.name == paramName);
            return pd?.defaultJson;                              // 从当前定义的默认值回填
        }

        // 如果某个实例连接指向了定义中已不存在的端口（被重命名/移除的端口），则返回 true——
        // 校验（4c）会把这些情况转为错误，而不是悄无声息地丢弃。
        public static bool PortExists(NodeDefinition def, string portName) =>
            def.InputPorts.Any(p => p.name == portName) || def.OutputPorts.Any(p => p.name == portName);
    }
}
