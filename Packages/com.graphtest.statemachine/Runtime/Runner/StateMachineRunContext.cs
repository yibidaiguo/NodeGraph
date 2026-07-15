// StateMachineRunContext.cs —— 把状态机运行时能力暴露给可组合单元（照 DialogueRunContext 成例）。
// 单元（框架 Units.cs 的比较/逻辑/设变量等，以及领域的 FireMachineEventAction）求值时拿到的
// NodeContext.blackboard 就是它：
//   · 它本身是框架的 IScopedBlackboard（Get/Set/GetF/SetF），委托给每实例的 StateMachineBlackboard——
//     所以单元用 ctx.blackboard 读写的就是这次运行的变量。
//   · 它还实现领域接口 IMachineEventSink（发状态机自定义事件）——领域单元 FireMachineEventAction 把
//     ctx.blackboard 强转回 IMachineEventSink 调用 FireEvent，事件经 OnEvent 汇流到 Runner 的
//     OnMachineEvent（再由 Player 转发给场景订阅方 / UnityEvent 接线）。
// Runtime 程序集 —— 无 editor-only 依赖（红线 §6）。

using System;
using NodeEditor;

namespace StateMachine
{
    // 领域能力：让动作单元能发状态机自定义事件，而不必让框架的 IScopedBlackboard 认识「事件」这个领域概念。
    public interface IMachineEventSink { void FireEvent(string name); }

    public sealed class StateMachineRunContext : IScopedBlackboard, IMachineEventSink
    {
        readonly StateMachineBlackboard m_BB;

        // 领域单元发出的自定义事件（事件名）。由 Runner 订阅并转发为 OnMachineEvent。
        public event Action<string> OnEvent;

        public StateMachineRunContext(StateMachineBlackboard bb) { m_BB = bb; }

        // 内层每实例黑板（Runner 的 Capture/Restore 经它读写值并取声明视图；测试/UI 可播种或检视变量）。
        public StateMachineBlackboard Blackboard => m_BB;

        public object Get(string key) => m_BB?.Get(key);
        public void Set(string key, object value) => m_BB?.Set(key, value);
        public float GetF(string key) => m_BB?.GetF(key) ?? 0f;
        public void SetF(string key, float value) => m_BB?.SetF(key, value);

        public void FireEvent(string name) => OnEvent?.Invoke(name);

        // 为单元求值组装 NodeContext（照 DialogueRunner.UnitCtx 成例；状态机是逐帧运行时，故额外携带 dt）。
        public NodeContext ToNodeContext(float dt, string instanceId) =>
            new NodeContext { blackboard = this, dt = dt, instanceId = instanceId };
    }
}
