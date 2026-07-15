// NodeRuntime.cs — 子层 4b（node-runtime），运行时接口与执行契约。
// 依赖 4a 的数据类型。定义按 graph 类型划分的运行时接口，以及
// 编辑器调试器（5c）通过其读取的 IRuntimeGraph 契约。
// Namespace NodeEditor。Runtime/ 程序集。实现 layer-3 runtime-interfaces.md。

using System;

namespace NodeEditor
{
    // tick 状态——layer-3 契约，永远不要塌缩成 bool。
    // None 用于 IRuntimeGraph 尚无判断的节点（例如本次运行中执行指针还未到达的某个
    // control-flow 节点）——GraphDebugger 的状态 switch 已经将它映射到暗淡的
    // "status-inactive" class，因此一个能区分"尚未运行"与"已运行并产生结果"的运行时，
    // 会得到正确的编辑期/播放模式高亮，而不会让每个未触及的节点都被错误地读作 Success。
    public enum Status { Success, Failure, Running, None }

    // 交给每个运行时节点的执行上下文（layer-3 NodeContext）
    public struct NodeContext
    {
        public IScopedBlackboard blackboard;   // 此实例的作用域化黑板
        public float dt;
        public string instanceId;
        public IParamLookup parameters;
    }

    // ---- 支撑契约（被 NodeContext / dataflow 引用）----
    public interface IScopedBlackboard
    {
        object Get(string key);
        void Set(string key, object value);
        float GetF(string key);
        void SetF(string key, float value);
    }
    public interface IParamLookup { string Get(string paramName); }
    public interface IInputResolver { object Get(string portName); }

    // 默认的 IParamLookup，桥接到 4a 的 ParamResolver，这样运行时代码就不必
    // 重新实现"先覆盖再回填"的查找逻辑。为每个运行中的实例构造一个。
    //（依赖 4a 的 ParamResolver/NodeInstance/NodeDefinition——处于 4b->4a 的方向之内。）
    public class InstanceParamLookup : IParamLookup
    {
        readonly NodeInstance m_Inst;
        readonly NodeDefinition m_Def;
        public InstanceParamLookup(NodeInstance inst, NodeDefinition def) { m_Inst = inst; m_Def = def; }
        public string Get(string paramName) => ParamResolver.Resolve(m_Inst, m_Def, paramName);
    }

    // ---- 按 graph 类型划分的运行时接口（layer-3 签名）----
    public interface ITickNode
    {
        Status Tick(NodeContext ctx);
        void Abort(NodeContext ctx);
        void Reset();
    }

    public interface IDataflowNode
    {
        object Evaluate(NodeContext ctx, IInputResolver inputs);
    }

    public enum StepKind { Advance, Waiting, Done }
    public struct StepResult
    {
        public StepKind kind;
        public string advancePort;
        public static StepResult Advance(string p) => new StepResult { kind = StepKind.Advance, advancePort = p };
        public static readonly StepResult Waiting = new StepResult { kind = StepKind.Waiting };
        public static readonly StepResult Done    = new StepResult { kind = StepKind.Done };
    }

    public interface IControlFlowNode
    {
        void Enter(NodeContext ctx);
        StepResult Execute(NodeContext ctx);
        void Exit(NodeContext ctx);
    }

    // ---- 运行时 graph 契约（运行时与编辑器调试器之间的接缝）----
    // 4b 定义它；运行时实现提供它；5c（调试器）通过它读取。
    // 这个接口正是 layer-4 运行时与 layer-5 编辑器之间的解耦点：
    // 编辑器永远不触碰运行时内部，只触碰这个接口。
    public interface IRuntimeGraph
    {
        Status StatusOf(string instanceId);
        object RuntimeNodeOf(string instanceId);
    }

    // Optional graph-identity contract consumed by editor-side runtime discovery.
    // Separate from IRuntimeGraph so existing third-party runtimes remain source-compatible.
    public interface IRuntimeGraphSource
    {
        bool OwnsGraph(NodeGraphAsset graph);
    }

    // Optional O(1), side-effect-free current-graph contract for runtimes that switch graphs.
    // ActiveGraph is null while idle/stopped/disposed; nested runtimes report their deepest
    // executing graph. A non-null result must also satisfy OwnsGraph(ActiveGraph).
    // Editor discovery falls back to IRuntimeGraphSource for existing third-party runtimes.
    public interface IActiveRuntimeGraphSource : IRuntimeGraphSource
    {
        NodeGraphAsset ActiveGraph { get; }
    }

    // ====================================================================================
    // Unit 基类层次——逻辑单元的命名约定。
    //
    // Unit 是每个逻辑单元的根基类（本套件的"Object"）。四种 Axis-2 角色
    //（参见 node-classification）各自得到一个供用户继承的抽象基类：
    //   ActionUnit    — 执行单元（副作用 / 状态变更）
    //   ConditionUnit — 判断单元（返回驱动分支的 bool）
    //   ProviderUnit  — 取值单元（读取/计算一个值，无副作用）
    //   ControlUnit   — 结构单元（编排子单元：Selector/Sequence/Decorator）
    //
    // 关键设计要点：基类声明单元的角色（身份 + 角色级不变量）；它不绑定运行时接口。
    // 运行时行为是正交的——同一个 Action 在 tick-tree 中实现 ITickNode，在 control-flow graph 中
    // 实现 IControlFlowNode。所以一个具体单元继承一个角色基类，并实现其 graph 类型所需的
    // 那个运行时接口。这让"是哪种单元"与"如何运行"彼此独立，并且让校验器和编辑器
    // 无需反射即可获得 Role（Unit.Role）。
    //
    // Role 属性与 4a 的 NodeRole enum 相对应，因此一个单元的编译期基类与其
    // 创作出的 NodeDefinition.role 在构造上保持一致（DealDamageAction : ActionUnit 的 Role == Action）。
    // ====================================================================================

    // 所有逻辑单元的根基类——类比于 Object 是所有 C# 类型的根。
    // [Serializable] 必须标在抽象基类上：单元以 [SerializeReference] 内联序列化，且会嵌套
    //（装饰器持有 List<ConditionUnit> 等）——基类不是 [Serializable] 时，Unity 会把这些字段判为不可序列化而整段丢弃。
    [Serializable]
    public abstract class Unit
    {
        public abstract NodeRole Role { get; }   // 此单元属于哪个 Axis-2 角色（无需反射）
    }

    // 执行单元（命名为 ActionUnit 而非 Action，以避免与 System.Action 委托冲突）：产生副作用 / 改变状态。纯逻辑 Action 只写
    // 黑板；领域 Action 则触及某个游戏系统。（运行时：ITickNode / IControlFlowNode 等。）
    // Execute(ctx)：可组合单元（内联 SerializeReference 树）的统一执行入口——经 ctx.blackboard 读写、必要时强转出领域能力。
    [Serializable]
    public abstract class ActionUnit : Unit
    {
        public sealed override NodeRole Role => NodeRole.Action;
        public abstract void Execute(NodeContext ctx);
    }

    // 判断单元：求值为驱动分支的 bool。按角色契约它没有副作用
    //（它是一个 Bool 类型的 Provider，之所以单独列出是因为 condition 会对流程进行门控）。
    [Serializable]
    public abstract class ConditionUnit : Unit
    {
        public sealed override NodeRole Role => NodeRole.Condition;
        public abstract bool Evaluate(NodeContext ctx);   // 判断——必须无副作用
    }

    // 取值单元：读取或计算一个值，无副作用。黑板 getter、常量、
    // 算术运算。（运行时：通常是 Evaluate / 内联读取。）
    // Get(ctx)：可组合单元的统一取值入口（返回类型化装箱值，供 Compare/SetVariable 等消费）。
    [Serializable]
    public abstract class ProviderUnit : Unit
    {
        public sealed override NodeRole Role => NodeRole.Provider;
        public abstract object Get(NodeContext ctx);
    }

    // 结构单元：编排子单元而非处理数据（Selector、Sequence、
    // Parallel、Decorator）。不携带数据处理参数；其子节点的 arity 是一个约束。
    // Tick(ctx)：可组合单元的统一编排入口，返回 Status（复用框架 tick 状态词汇）。
    [Serializable]
    public abstract class ControlUnit : Unit
    {
        public sealed override NodeRole Role => NodeRole.Control;
        public abstract Status Tick(NodeContext ctx);
    }
}
