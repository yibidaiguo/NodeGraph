using NodeEditor;
using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace Dialogue.EditorUI
{
    [NodeIcon(typeof(StartNode), NodeIconKind.Entry)]
    [NodeIcon(typeof(EndNode), NodeIconKind.Terminal)]
    [NodeIcon(typeof(LineNode), NodeIconKind.Dialogue)]
    [NodeIcon(typeof(ChoiceNode), NodeIconKind.Choice)]
    [NodeIcon(typeof(OptionNode), NodeIconKind.Option)]
    [NodeIcon(typeof(ConditionNode), NodeIconKind.Condition)]
    [NodeIcon(typeof(ActionNode), NodeIconKind.Action)]
    [NodeIcon(typeof(JumpNode), NodeIconKind.Jump)]
    [NodeIcon(typeof(LabelNode), NodeIconKind.Label)]
    [NodeIcon(typeof(SubDialogueNode), NodeIconKind.SubGraph)]
    [InitializeOnLoad]
    static class NodeGraphDialogueRegistration
    {
        static NodeGraphDialogueRegistration()
        {
            var descriptor = new NodeGraphModuleDescriptor(
                "com.graphtest.dialogue",
                "Dialogue",
                100,
                new[]
                {
                    new NodeGraphModuleAction("open", "Open Editor", DialogueEditorLauncher.Open),
                    new NodeGraphModuleAction("setup", "Setup Assets", DialogueSetup.Run),
                    new NodeGraphModuleAction("asset-paths", "Open Asset Paths", DialogueAssetPathsLocator.OpenAssetPaths),
                },
                GraphOrientation.Horizontal,
                // 图上记录的短模块键。框架按它把 NodeGraphAsset.module 解析回本描述符——
                // 过去框架靠包 id 后缀去猜，那既会误命中同后缀的第三方包，也让"加个新模块"变成"改框架"。
                moduleKey: "dialogue",
                // 0.0.4 时代的独立样例包。框架过去把三个领域的包名和路径硬编码在模块管理器里；
                // 现在由本模块自己声明，框架只负责遍历清理。新模块没有历史包袱就不必声明。
                retiredSamplePackage: "com.graphtest.dialogue.samples",
                retiredSamplePath: "Assets/Samples/NodeGraph Dialogue Samples/0.0.4/Dialogue Basics");

            if (!NodeGraphModules.Registry.TryRegister(descriptor, out var error))
                Debug.LogError(error);

            NodeGraphInstallSetupCoordinator.Register(
                ProjectAssetPaths.CreateInstallSetupDescriptor<DialogueAssetPaths>(
                    "com.graphtest.dialogue",
                    "Dialogue",
                    100,
                    "Dialogue",
                    DialogueAssetPathsLocator.ApplyDefaults,
                    DialogueSetup.Run));
        }
    }
}
