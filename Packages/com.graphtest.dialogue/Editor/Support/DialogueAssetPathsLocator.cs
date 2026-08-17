using UnityEditor;
using Dialogue;
using NodeEditor.EditorUI;

namespace Dialogue.EditorUI
{
    public static class DialogueAssetPathsLocator
    {
        const string ModuleName = "Dialogue";

        // 只读发现供工具链使用；缺失或歧义时失败关闭，绝不在读取期间创建配置。
        public static DialogueAssetPaths Find()
            => ProjectAssetPaths.FindExisting<DialogueAssetPaths>("Dialogue");

        public static DialogueAssetPaths FindOrCreate()
            => ProjectAssetPaths.FindOrCreate<DialogueAssetPaths>("Dialogue", ApplyDefaults);

        public static void OpenAssetPaths() =>
            ProjectAssetPaths.Open<DialogueAssetPaths>("Dialogue", ApplyDefaults);

        // Test seam proving installation paths do not affect project-owned configuration.
        static string DefaultBootstrapPathForScriptPath(string _)
        {
            return ProjectAssetPaths.BootstrapPath<DialogueAssetPaths>();
        }

        internal static void ApplyDefaults(DialogueAssetPaths cfg)
        {
            var root = ProjectAssetPaths.ContentRoot(ModuleName);
            cfg.nodeDefinitionsDir = $"{root}/Nodes/Definitions";
            cfg.dialogueGroupsDir = $"{root}/Dialogues";
            cfg.blackboardLayersDir = $"{root}/Blackboards";
        }

    }
}
