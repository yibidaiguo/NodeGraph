// NodeSchema.cs — 节点定义的纯 C# 形态。Runtime 程序集（纯层）。
//
// 为什么要有它：NodeDefinition 是 ScriptableObject（Unity 载体，靠 MonoScript GUID 绑定 .asset，
// 30 个领域子类都继承它），不可能进纯层。但执行器只需要定义里的**数据**——端口、参数、角色、
// 运行时形态——不需要 asset 身份。NodeSchema 就是这份数据，由 NodeDefinition.ToSchema() 烘出。
//
// 于是：创作期仍然是 .asset（编辑体验不变），执行期读的是 NodeSchema（可在无 Unity 环境构造）。
// 一份数据、两种表示，不是两套定义。

using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeEditor
{
    [Serializable]
    public class NodeSchema
    {
        public string id;
        public string displayName;
        // 空 = 通用定义，任何模块的图都能用（对应 NodeDefinition.Module）。
        public string module;
        // 领域种类标签，取自领域基类的 Kind（如 "Line" / "Transition" / "Objective"）。
        // 框架不解释它；领域执行器把它解析回自己的 enum（见各 Runner 的 KindOf）。
        // 为空表示该定义不属于任何领域家族。
        public string kind;

        public NodePurity purity;
        public NodeRole role;
        public RuntimeKind runtimeKind;
        public int version = 1;

        public List<PortDef> inputPorts = new();
        public List<PortDef> outputPorts = new();
        public List<ParamDef> parameters = new();
        public List<BlackboardKeyRef> reads = new();
        public List<BlackboardKeyRef> writes = new();
        public List<Constraint> constraints = new();

        public ParamDef Param(string name) => parameters.FirstOrDefault(p => p.name == name);

        // 实例连接指向的端口是否仍然存在（端口被重命名/移除时，校验据此报错而不是静默丢弃）。
        public bool PortExists(string portName) =>
            inputPorts.Any(p => p.name == portName) || outputPorts.Any(p => p.name == portName);
    }

    // 按 definitionId 解析 NodeSchema 的来源。执行器只认这个接口，不认 NodeRegistry。
    //
    // API 兼容要点：Unity 侧的 NodeRegistry 实现了本接口，所以既有调用点
    // `new DialogueRunner(registry, ...)` 一字不改仍然编译通过；纯 C# 侧则传
    // SchemaSet（下方）或任意自定义实现。
    public interface ISchemaSource
    {
        NodeSchema FindSchema(string definitionId);
    }

    // 纯 C# 的 ISchemaSource 实现：一张 id -> schema 的表。JSON 载入、测试构造都用它。
    public sealed class SchemaSet : ISchemaSource
    {
        readonly Dictionary<string, NodeSchema> m_ById;

        public SchemaSet(IEnumerable<NodeSchema> schemas)
        {
            m_ById = new Dictionary<string, NodeSchema>();
            if (schemas == null) return;
            foreach (var s in schemas)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                // 重复 id 保留第一个并让调用方可查（与 NodeRegistry.Find 的"重复即错误"语义一致，
                // 但纯层没有日志出口，故记录到 Duplicates 而不是吞掉）。
                if (!m_ById.ContainsKey(s.id)) m_ById[s.id] = s;
                else Duplicates.Add(s.id);
            }
        }

        public List<string> Duplicates { get; } = new();

        public NodeSchema FindSchema(string definitionId) =>
            definitionId != null && m_ById.TryGetValue(definitionId, out var s) ? s : null;

        public IEnumerable<NodeSchema> All() => m_ById.Values;
    }
}
