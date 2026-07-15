// DialogueRunContext.cs —— 把对话运行时能力暴露给可组合单元。单元（CompareCondition/SetVariableAction/… 见框架
// Units.cs，以及领域的 FireEventAction）求值时拿到的 NodeContext.blackboard 就是它：
//   · 它本身是框架的 IScopedBlackboard（Get/Set/GetF/SetF），委托给每实例的 DialogueBlackboard——
//     所以单元用 ctx.blackboard 读写的就是这次播放的变量；runner.Blackboard 暴露的是同一个内层实例。
//   · 它还实现领域接口 IDialogueEventSink（触发对话事件）——领域单元 FireEventAction 把 ctx.blackboard
//     强转回 IDialogueEventSink 调用 Emit，事件经 OnEvent 流向 runner 的表现层 OnEvent。
// Runtime 程序集 —— 无 editor-only 依赖（红线 §6）。

using System;
using NodeEditor;

namespace Dialogue
{
    // 领域能力：让动作单元能触发对话事件，而不必让框架的 IScopedBlackboard 认识"事件"这个领域概念。
    public interface IDialogueEventSink { void Emit(string eventId, string arg); }

    public class DialogueRunContext : IScopedBlackboard, IDialogueEventSink
    {
        readonly DialogueBlackboard m_BB;
        public event Action<string, string> OnEvent;

        public DialogueRunContext(DialogueBlackboard bb) { m_BB = bb; }

        public object Get(string key) => m_BB?.Get(key);
        public void Set(string key, object value) => m_BB?.Set(key, value);
        public float GetF(string key) => m_BB?.GetF(key) ?? 0f;
        public void SetF(string key, float value) => m_BB?.SetF(key, value);

        public void Emit(string eventId, string arg) => OnEvent?.Invoke(eventId, arg);
    }
}
