using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor.EditorUI;
using UnityEditor;

namespace TaskEditor.EditorUI
{
    [InitializeOnLoad]
    public static class TaskGraphAuthoringExtension
    {
        const string ModuleId = "task";

        static TaskGraphAuthoringExtension()
        {
            GraphAuthoringModuleRegistry.Register(new GraphAuthoringModuleDescriptor(
                ModuleId,
                FindGraphRoots));
        }

        // AI 发现是纯读取：只暴露已配置的任务图目录，不隐式创建配置资产。
        static IReadOnlyList<string> FindGraphRoots()
        {
            var paths = TaskAssetPathsLocator.Find();
            if (paths == null) return Array.Empty<string>();

            return new[] { paths.taskGraphsDir, paths.stepGraphsDir }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
        }
    }
}
