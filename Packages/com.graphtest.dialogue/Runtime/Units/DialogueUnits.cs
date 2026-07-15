// DialogueUnits.cs —— 对话「领域」可组合单元。继承框架族基类（NodeEditor.Units.cs），由 UnitRegistry 反射发现，
// 在检视面板的 Unit 槽下拉里出现在「领域」分组下（与「全局通用」的比较/逻辑/设变量等并列）。
// 这里放需要领域能力的单元——目前是触发对话事件（需要 DialogueUnitContext 的事件出口）。
// 普通 [Serializable] 类，按 SerializeReference 内联序列化（非 SO，不受每类一文件 MonoScript 规则约束）。
// Runtime 程序集 —— 无 editor-only 依赖。

using System;
using NodeEditor;

namespace Dialogue
{
    // 触发一个具名游戏事件（可带参数）。等价于旧的 Event 节点，现为可组合动作单元，可被 Sequence/Conditional 装饰。
    [Serializable] [Unit("unit.dialogue.fireEvent.name", "Fire Event", "unit.group.action", "Action")]
    public class FireEventAction : ActionUnit
    {
        public string eventId;
        public string arg;
        public override void Execute(NodeContext ctx) => (ctx.blackboard as IDialogueEventSink)?.Emit(eventId, arg);
    }
}
