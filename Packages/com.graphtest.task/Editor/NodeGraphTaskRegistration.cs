using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace TaskEditor.EditorUI
{
    [NodeIcon(typeof(TaskStartNode), NodeIconKind.Entry)]
    [NodeIcon(typeof(TaskNode), NodeIconKind.Task)]
    [NodeIcon(typeof(TaskGateNode), NodeIconKind.Gate)]
    [NodeIcon(typeof(TaskObjectiveNode), NodeIconKind.Objective)]
    [NodeIcon(typeof(TaskConditionNode), NodeIconKind.Condition)]
    [NodeIcon(typeof(TaskActionNode), NodeIconKind.Action)]
    [NodeIcon(typeof(TaskWaitEventNode), NodeIconKind.WaitEvent)]
    [NodeIcon(typeof(TaskJumpNode), NodeIconKind.Jump)]
    [NodeIcon(typeof(TaskLabelNode), NodeIconKind.Label)]
    [NodeIcon(typeof(TaskCompleteNode), NodeIconKind.Complete)]
    [NodeIcon(typeof(TaskFailNode), NodeIconKind.Failure)]
    [InitializeOnLoad]
    static class NodeGraphTaskRegistration
    {
        static NodeGraphTaskRegistration()
        {
            var descriptor = new NodeGraphModuleDescriptor(
                "com.graphtest.task",
                "Task",
                200,
                new[]
                {
                    new NodeGraphModuleAction("open", "Open Editor", TaskEditorLauncher.Open),
                    new NodeGraphModuleAction("setup", "Setup Assets", TaskSetup.Run),
                    new NodeGraphModuleAction("asset-paths", "Open Asset Paths", TaskAssetPathsLocator.OpenAssetPaths),
                });

            if (!NodeGraphModules.Registry.TryRegister(descriptor, out var error))
                Debug.LogError(error);

            NodeGraphInstallSetupCoordinator.Register(
                ProjectAssetPaths.CreateInstallSetupDescriptor<TaskAssetPaths>(
                    "com.graphtest.task",
                    "Task",
                    200,
                    "Task",
                    TaskAssetPathsLocator.ApplyDefaults,
                    TaskSetup.Run));
        }
    }
}
