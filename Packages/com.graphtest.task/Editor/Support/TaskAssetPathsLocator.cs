using UnityEditor;
using TaskEditor;
using NodeEditor.EditorUI;

namespace TaskEditor.EditorUI
{
    public static class TaskAssetPathsLocator
    {
        const string ModuleName = "Task";

        // 只读发现供工具链使用；缺失或歧义时失败关闭，绝不在读取期间创建配置。
        public static TaskAssetPaths Find()
            => ProjectAssetPaths.FindExisting<TaskAssetPaths>("Task");

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
