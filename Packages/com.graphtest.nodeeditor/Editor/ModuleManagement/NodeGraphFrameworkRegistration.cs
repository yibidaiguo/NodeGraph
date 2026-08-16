using UnityEditor;
using UnityEngine;
using NodeEditor;

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
                    // 框架自己不能在这个窗口里移除（Manager 就住在它里面），所以清理要有独立入口：
                    // 先在这里清干净，再去 Package Manager 移包，才不会留下一地生成物。
                    new NodeGraphModuleAction("cleanup", "Clean Up Generated Files", CleanUp),
                },
                GraphOrientation.Vertical);

            if (!NodeGraphModules.Registry.TryRegister(descriptor, out var error))
                Debug.LogError(error);

            NodeGraphInstallSetupCoordinator.Register(
                ProjectAssetPaths.CreateInstallSetupDescriptor<NodeEditorAssetPaths>(
                    "com.graphtest.nodeeditor",
                    "Node Editor Framework",
                    0,
                    "NodeEditor",
                    NodeEditorAssetPathsLocator.ApplyDefaults,
                    FrameworkSetup.Run));
        }

        static void CleanUp()
        {
            var descriptor = NodeGraphUninstall.FindDescriptor("com.graphtest.nodeeditor");
            if (descriptor == null) return;

            var residue = NodeGraphResidueScanner.Scan(descriptor, isFramework: true);
            if (residue.IsEmpty)
            {
                EditorUtility.DisplayDialog(
                    "NodeGraph",
                    "框架没有留下工程生成物。 / The framework has no generated project files to clean up.",
                    "OK");
                return;
            }
            NodeGraphUninstallWindow.Open(residue, null);
        }
    }
}
