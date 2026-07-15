// StateMachineRuntimeBridge.cs — 将 StateMachinePlayer 的 runner 生命周期事件接到 NodeEditor 的运行模式
// 调试器。StateMachinePlayer（Runtime 程序集）无法引用 NodeEditor.EditorUI.RuntimeGraphRegistry
//（仅 Editor），所以它改为抛出普通的 Action<IRuntimeGraph> 事件；这个 Editor 程序集的桥接是唯一
// 连接两者的东西，从而让 StateMachine.Runtime 不带任何 Editor 依赖（红线 §6）。照 DialogueRuntimeBridge 逐行。

using UnityEditor;
using NodeEditor;
using NodeEditor.EditorUI;

namespace StateMachine.EditorUI
{
    [InitializeOnLoad]
    static class StateMachineRuntimeBridge
    {
        static StateMachineRuntimeBridge()
        {
            StateMachinePlayer.OnRunnerCreated += RuntimeGraphRegistry.Register;
            StateMachinePlayer.OnRunnerDestroyed += RuntimeGraphRegistry.Unregister;
        }
    }
}
