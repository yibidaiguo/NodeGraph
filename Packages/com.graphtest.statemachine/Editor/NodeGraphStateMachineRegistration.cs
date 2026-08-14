using NodeEditor;
using NodeEditor.EditorUI;
using UnityEditor;
using UnityEngine;

namespace StateMachine.EditorUI
{
    [NodeIcon(typeof(EntryNode), NodeIconKind.Entry)]
    [NodeIcon(typeof(ExitNode), NodeIconKind.Terminal)]
    [NodeIcon(typeof(StateNode), NodeIconKind.State)]
    [NodeIcon(typeof(TransitionNode), NodeIconKind.Transition)]
    [NodeIcon(typeof(AnyStateNode), NodeIconKind.AnyState)]
    [NodeIcon(typeof(SubMachineNode), NodeIconKind.SubGraph)]
    [InitializeOnLoad]
    static class NodeGraphStateMachineRegistration
    {
        static NodeGraphStateMachineRegistration()
        {
            var descriptor = new NodeGraphModuleDescriptor(
                "com.graphtest.statemachine",
                "State Machine",
                300,
                new[]
                {
                    new NodeGraphModuleAction("open", "Open Editor", StateMachineEditorLauncher.Open),
                    new NodeGraphModuleAction("setup", "Setup Assets", StateMachineSetup.Run),
                    new NodeGraphModuleAction("asset-paths", "Open Asset Paths", StateMachineAssetPathsLocator.OpenAssetPaths),
                },
                GraphOrientation.Vertical,
                // 图上记录的短模块键。框架按它把 NodeGraphAsset.module 解析回本描述符——
                // 过去框架靠包 id 后缀去猜，那既会误命中同后缀的第三方包，也让"加个新模块"变成"改框架"。
                moduleKey: "statemachine",
                // 0.0.4 时代的独立样例包。框架过去把三个领域的包名和路径硬编码在模块管理器里；
                // 现在由本模块自己声明，框架只负责遍历清理。新模块没有历史包袱就不必声明。
                retiredSamplePackage: "com.graphtest.statemachine.samples",
                retiredSamplePath: "Assets/Samples/NodeGraph State Machine Samples/0.0.4/State Machine Basics");

            if (!NodeGraphModules.Registry.TryRegister(descriptor, out var error))
                Debug.LogError(error);

            NodeGraphInstallSetupCoordinator.Register(
                ProjectAssetPaths.CreateInstallSetupDescriptor<StateMachineAssetPaths>(
                    "com.graphtest.statemachine",
                    "State Machine",
                    300,
                    "StateMachine",
                    StateMachineAssetPathsLocator.ApplyDefaults,
                    StateMachineSetup.Run));
        }
    }
}
