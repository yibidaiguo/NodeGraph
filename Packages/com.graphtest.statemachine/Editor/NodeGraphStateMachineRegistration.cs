using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace StateMachine.EditorUI
{
    [InitializeOnLoad]
    static class NodeGraphStateMachineRegistration
    {
        static NodeGraphStateMachineRegistration()
        {
            var descriptor = new NodeGraphModuleDescriptor(
                "com.graphtest.statemachine",
                "State Machine",
                300,
                new[]
                {
                    new NodeGraphModuleAction("open", "Open Editor", StateMachineEditorLauncher.Open),
                    new NodeGraphModuleAction("setup", "Setup Assets", StateMachineSetup.Run),
                    new NodeGraphModuleAction("asset-paths", "Open Asset Paths", StateMachineAssetPathsLocator.OpenAssetPaths),
                });

            if (!NodeGraphModules.Registry.TryRegister(descriptor, out var error))
                Debug.LogError(error);
        }
    }
}
