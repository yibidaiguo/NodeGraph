using NodeEditor;
using NodeEditor.EditorUI;
using UnityEditor;

namespace TaskEditor.EditorUI
{
    [InitializeOnLoad]
    public static class TaskRuntimeBridge
    {
        static TaskRuntimeBridge()
        {
            TaskRunner.OnRunnerCreated += RuntimeGraphRegistry.Register;
            TaskRunner.OnRunnerDisposed += RuntimeGraphRegistry.Unregister;
        }
    }
}
