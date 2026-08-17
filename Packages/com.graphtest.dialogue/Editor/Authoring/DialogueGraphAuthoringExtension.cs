using System;
using System.Collections.Generic;
using NodeEditor.EditorUI;
using UnityEditor;

namespace Dialogue.EditorUI
{
    [InitializeOnLoad]
    public static class DialogueGraphAuthoringExtension
    {
        const string ModuleId = "dialogue";

        static DialogueGraphAuthoringExtension()
        {
            GraphAuthoringModuleRegistry.Register(new GraphAuthoringModuleDescriptor(
                ModuleId,
                FindGraphRoots,
                typeof(Dialogue.FireEventAction)));
        }

        // AI 发现是纯读取：未配置目录时不猜默认路径，也不隐式创建配置资产。
        static IReadOnlyList<string> FindGraphRoots()
        {
            string root = DialogueAssetPathsLocator.Find()?.dialogueGroupsDir;
            return string.IsNullOrWhiteSpace(root) ? Array.Empty<string>() : new[] { root };
        }
    }
}
