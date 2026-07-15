using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace TaskEditor.EditorUI
{
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
        }
    }
}
