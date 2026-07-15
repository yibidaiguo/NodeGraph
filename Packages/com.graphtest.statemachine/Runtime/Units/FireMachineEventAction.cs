// FireMachineEventAction.cs —— 状态机「领域」可组合动作单元（照 Dialogue/Runtime/Units 的 FireEventAction 成例，
// 但汇入本域事件 sink——领域间不共享）。继承框架族基类（NodeEditor.Units.cs），由 UnitRegistry 反射发现，
// 在检视面板的 Unit 槽下拉里出现在「领域」分组下；类名带 Machine，防与对话领域的 FireEventAction 混淆。
// 普通 [Serializable] 类，按 SerializeReference 内联序列化（非 SO，不受每类一文件 MonoScript 规则约束）。
// Runtime 程序集 —— 无 editor-only 依赖。

using System;
using NodeEditor;

namespace StateMachine
{
    // 发一个具名的状态机自定义事件：把求值上下文的 blackboard（即 StateMachineRunContext）强转回
    // 领域接口 IMachineEventSink 调用 FireEvent，事件经 Runner.OnMachineEvent → Player.onMachineEvent
    // 流向场景订阅方（音效/任务/表现层）。可被 Sequence/Conditional 等编排单元装饰组合。
    [Serializable] [Unit("触发状态机事件", "动作")]
    public class FireMachineEventAction : ActionUnit
    {
        public string eventName;
        public override void Execute(NodeContext ctx) => (ctx.blackboard as IMachineEventSink)?.FireEvent(eventName);
    }
}
