// StateMachine 项目资源路径的唯一配置权威。配置资产使用固定 bootstrap 位置；
// 其内容路径是 Assets/StateMachineContent 下的可编辑默认值，不依赖安装位置或脚本位置。

using UnityEditor;
using StateMachine;
using NodeEditor.EditorUI;

namespace StateMachine.EditorUI
{
    public static class StateMachineAssetPathsLocator
    {
        const string ModuleName = "StateMachine";

        public static StateMachineAssetPaths FindOrCreate()
            => ProjectAssetPaths.FindOrCreate<StateMachineAssetPaths>("StateMachine", ApplyDefaults);

        public static void OpenAssetPaths() =>
            ProjectAssetPaths.Open<StateMachineAssetPaths>("StateMachine", ApplyDefaults);

        // Test seam proving installation paths do not affect project-owned configuration.
        static string DefaultBootstrapPathForScriptPath(string _)
        {
            return ProjectAssetPaths.BootstrapPath<StateMachineAssetPaths>();
        }

        internal static void ApplyDefaults(StateMachineAssetPaths cfg)
        {
            var root = ProjectAssetPaths.ContentRoot(ModuleName);
            cfg.nodeDefinitionsDir = $"{root}/Nodes/Definitions";
            cfg.machineGroupsDir = $"{root}/Machines";
            cfg.blackboardLayersDir = $"{root}/Blackboards";
        }

    }
}
