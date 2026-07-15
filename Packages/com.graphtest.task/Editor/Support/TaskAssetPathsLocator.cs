using UnityEditor;
using TaskEditor;
using NodeEditor.EditorUI;

namespace TaskEditor.EditorUI
{
    public static class TaskAssetPathsLocator
    {
        const string ModuleName = "Task";

        public static TaskAssetPaths FindOrCreate()
            => ProjectAssetPaths.FindOrCreate<TaskAssetPaths>("Task", ApplyDefaults);

        public static void OpenAssetPaths() =>
            ProjectAssetPaths.Open<TaskAssetPaths>("Task", ApplyDefaults);

        // Test seam proving installation paths do not affect project-owned configuration.
        static string DefaultBootstrapPathForScriptPath(string _)
        {
            return ProjectAssetPaths.BootstrapPath<TaskAssetPaths>();
        }

        internal static void ApplyDefaults(TaskAssetPaths cfg)
        {
            var root = ProjectAssetPaths.ContentRoot(ModuleName);
            cfg.nodeDefinitionsDir = $"{root}/Nodes/Definitions";
            cfg.taskGraphsDir = $"{root}/Tasks";
            cfg.stepGraphsDir = $"{root}/Steps";
            cfg.blackboardLayersDir = $"{root}/Blackboards";
        }

    }
}
