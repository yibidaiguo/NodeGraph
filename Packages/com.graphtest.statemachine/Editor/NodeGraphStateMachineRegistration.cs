using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace StateMachine.EditorUI
{
    [NodeIcon(typeof(EntryNode), NodeIconKind.Entry)]
    [NodeIcon(typeof(ExitNode), NodeIconKind.Terminal)]
    [NodeIcon(typeof(StateNode), NodeIconKind.State)]
    [NodeIcon(typeof(TransitionNode), NodeIconKind.Transition)]
    [NodeIcon(typeof(AnyStateNode), NodeIconKind.AnyState)]
    [NodeIcon(typeof(SubMachineNode), NodeIconKind.SubGraph)]
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

            NodeGraphInstallSetupCoordinator.Register(
                ProjectAssetPaths.CreateInstallSetupDescriptor<StateMachineAssetPaths>(
                    "com.graphtest.statemachine",
                    "State Machine",
                    300,
                    "StateMachine",
                    StateMachineAssetPathsLocator.ApplyDefaults,
                    StateMachineSetup.Run));
        }
    }
}
