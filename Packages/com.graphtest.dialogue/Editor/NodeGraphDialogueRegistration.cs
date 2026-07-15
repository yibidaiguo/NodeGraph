using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace Dialogue.EditorUI
{
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
        }
    }
}
