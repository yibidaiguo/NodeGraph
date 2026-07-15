using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    [InitializeOnLoad]
    static class NodeGraphFrameworkRegistration
    {
        static NodeGraphFrameworkRegistration()
        {
            var descriptor = new NodeGraphModuleDescriptor(
                "com.graphtest.nodeeditor",
                "Node Editor Framework",
                0,
                new[]
                {
                    // 框架的 open 语义（自由模式）不同于各领域的 open（模块模式）——显式 nameKey 消歧。
                    new NodeGraphModuleAction("open", "Open Node Editor", NodeEditorWindow.Open, nameKey: "ui.moduleAction.openNodeEditor"),
                    new NodeGraphModuleAction("data", "Node Editor Data", DataEditorWindow.Open),
                    // 框架自足的 Setup：核心资产（本地化表/语言选项/双配置/全局黑板）+ 框架种子，无需任何领域模块。
                    new NodeGraphModuleAction("setup", "Setup Assets", FrameworkSetup.Run),
                    new NodeGraphModuleAction("asset-paths", "Open Asset Paths", NodeEditorAssetPathsLocator.OpenAssetPaths),
                });

            if (!NodeGraphModules.Registry.TryRegister(descriptor, out var error))
                Debug.LogError(error);
        }
    }
}
