using System;
using System.Collections.Generic;
using NodeEditor.EditorUI;
using UnityEditor;

namespace StateMachine.EditorUI
{
    [InitializeOnLoad]
    public static class StateMachineGraphAuthoringExtension
    {
        const string ModuleId = "statemachine";

        static StateMachineGraphAuthoringExtension()
        {
            GraphAuthoringModuleRegistry.Register(new GraphAuthoringModuleDescriptor(
                ModuleId,
                FindGraphRoots,
                typeof(StateMachine.FireMachineEventAction)));
        }

        // AI 发现是纯读取：未配置目录时不猜默认路径，也不隐式创建配置资产。
        static IReadOnlyList<string> FindGraphRoots()
        {
            string root = StateMachineAssetPathsLocator.Find()?.machineGroupsDir;
            return string.IsNullOrWhiteSpace(root) ? Array.Empty<string>() : new[] { root };
        }
    }
}
