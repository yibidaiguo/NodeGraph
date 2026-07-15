using UnityEditor;
using Dialogue;
using NodeEditor.EditorUI;

namespace Dialogue.EditorUI
{
    public static class DialogueAssetPathsLocator
    {
        const string ModuleName = "Dialogue";

        public static DialogueAssetPaths FindOrCreate()
            => ProjectAssetPaths.FindOrCreate<DialogueAssetPaths>("Dialogue", cfg => ApplyDefaultsForRoot(cfg, null));

        public static void OpenAssetPaths() =>
            ProjectAssetPaths.Open<DialogueAssetPaths>("Dialogue", cfg => ApplyDefaultsForRoot(cfg, null));

        // Test seam proving installation paths do not affect project-owned configuration.
        static string DefaultBootstrapPathForScriptPath(string _)
        {
            return ProjectAssetPaths.BootstrapPath<DialogueAssetPaths>();
        }

        static void ApplyDefaultsForRoot(DialogueAssetPaths cfg, string _)
        {
            var root = ProjectAssetPaths.ContentRoot(ModuleName);
            cfg.nodeDefinitionsDir = $"{root}/Nodes/Definitions";
            cfg.dialogueGroupsDir = $"{root}/Dialogues";
            cfg.blackboardLayersDir = $"{root}/Blackboards";
        }

    }
}
