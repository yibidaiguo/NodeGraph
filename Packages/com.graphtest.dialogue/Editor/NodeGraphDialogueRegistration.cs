using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace Dialogue.EditorUI
{
    [NodeIcon(typeof(StartNode), NodeIconKind.Entry)]
    [NodeIcon(typeof(EndNode), NodeIconKind.Terminal)]
    [NodeIcon(typeof(LineNode), NodeIconKind.Dialogue)]
    [NodeIcon(typeof(ChoiceNode), NodeIconKind.Choice)]
    [NodeIcon(typeof(OptionNode), NodeIconKind.Option)]
    [NodeIcon(typeof(ConditionNode), NodeIconKind.Condition)]
    [NodeIcon(typeof(ActionNode), NodeIconKind.Action)]
    [NodeIcon(typeof(JumpNode), NodeIconKind.Jump)]
    [NodeIcon(typeof(LabelNode), NodeIconKind.Label)]
    [NodeIcon(typeof(SubDialogueNode), NodeIconKind.SubGraph)]
    [InitializeOnLoad]
    static class NodeGraphDialogueRegistration
    {
        static NodeGraphDialogueRegistration()
        {
            var descriptor = new NodeGraphModuleDescriptor(
                "com.graphtest.dialogue",
                "Dialogue",
                100,
                new[]
                {
                    new NodeGraphModuleAction("open", "Open Editor", DialogueEditorLauncher.Open),
                    new NodeGraphModuleAction("setup", "Setup Assets", DialogueSetup.Run),
                    new NodeGraphModuleAction("asset-paths", "Open Asset Paths", DialogueAssetPathsLocator.OpenAssetPaths),
                });

            if (!NodeGraphModules.Registry.TryRegister(descriptor, out var error))
                Debug.LogError(error);

            NodeGraphInstallSetupCoordinator.Register(
                ProjectAssetPaths.CreateInstallSetupDescriptor<DialogueAssetPaths>(
                    "com.graphtest.dialogue",
                    "Dialogue",
                    100,
                    "Dialogue",
                    DialogueAssetPathsLocator.ApplyDefaults,
                    DialogueSetup.Run));
        }
    }
}
