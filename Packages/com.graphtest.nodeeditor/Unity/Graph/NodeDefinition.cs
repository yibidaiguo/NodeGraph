// NodeDefinition.cs —— 节点定义的 Unity 载体（ScriptableObject）。NodeEditor.Unity 程序集。
//
// 从 NodeDataTypes.cs 抽出到独立文件 + 独立程序集：定义是 asset 支撑的创作物
//（30 个领域子类各自一个 .asset，靠 MonoScript GUID 绑定），天然属于 Unity 侧；
// 而执行器只需要其中的**数据**。二者由 ToSchema() 接缝分开——
// 创作期照旧编辑 .asset，执行期读纯 C# 的 NodeSchema（见 Runtime/Graph/NodeSchema.cs）。
//
// 注意：本类保持 abstract，自身永不实例化为 asset，故不受"每类一文件 MonoScript"硬规则约束；
// 但具体子类（LineNode / StateNode / …）必须各自独立文件——见各领域包的说明。
//
// 兼容性：字段、[SerializeField] 名称、公开属性与创作钩子（Meta/AddIn/AddOut/AddParam/
// AddUnitParam/AddConstraint/Define/StableId/RebuildFromCode）全部原样保留，
// 因此 30 个领域子类和已烘好的 .asset 一字不改、一字节不动。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor
{
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
        // Empty means this definition is universal and may be used by every graph module.
        public virtual string Module => null;
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

        // 领域种类标签，烘进 NodeSchema.kind 供领域执行器解析回自己的 enum。
        // 领域基类覆盖为 Kind.ToString()（如 DialogueNodeDefinition => "Line"）。
        // 框架不解释它；为 null 表示该定义不属于任何领域家族。
        public virtual string KindTag => null;

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
        // 声明式校验约束（见 Constraint 层次）：子类在 Define() 里声明，由 4c 校验器按子类型分派消费。
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
            m_Schema = null;   // 定义变了，烘焙缓存作废
        }

        // ---- 纯层接缝 ----
        // 本定义的纯 C# 投影，**带缓存**。编辑器侧调用点（InspectorPane / NodeViewControl /
        // 各 Validation）在每节点每次重绘的热路径上取参数，若每次都 ToSchema() 会反复分配 6 个 List。
        // 这里缓存一份；RebuildFromCode() 会作废它。
        //
        // 迁移用法：0.0.x 的 `ParamResolver.Resolve(inst, def, name)`
        //      → 0.1.0 改为 `ParamResolver.Resolve(inst, def.Schema, name)`。
        [NonSerialized] NodeSchema m_Schema;
        public NodeSchema Schema => m_Schema ??= ToSchema();

        // 把这份定义烘成执行器吃的纯 C# NodeSchema。端口/参数/约束是 [Serializable] 纯类型，
        // 按引用传递即可（它们已在纯层，且创作期之后不再变动）；列表做浅拷贝，避免调用方
        // 拿到 schema 后改动反噬到 asset。
        public NodeSchema ToSchema() => new NodeSchema
        {
            id = id,
            displayName = displayName,
            module = Module,
            kind = KindTag,
            purity = purity,
            role = role,
            runtimeKind = runtimeKind,
            version = version,
            inputPorts = new List<PortDef>(inputPorts),
            outputPorts = new List<PortDef>(outputPorts),
            parameters = new List<ParamDef>(parameters),
            reads = new List<BlackboardKeyRef>(blackboardReads),
            writes = new List<BlackboardKeyRef>(blackboardWrites),
            constraints = new List<Constraint>(constraints),
        };
    }
}
