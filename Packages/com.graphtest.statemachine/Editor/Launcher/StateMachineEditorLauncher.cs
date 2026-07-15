// StateMachineEditorLauncher.cs — 状态机领域层：Tools/NodeGraph/State Machine 的「模块模式」编辑器入口。
// 框架（NodeEditorWindow）提供「按模块打开」这一**机制**（领域无关：左侧只列该模块的图、隐藏工具栏对象框，
// 本模块内仍可多图切换）；本类提供**策略**：菜单入口 + 锁到哪个模块（"statemachine"）+ 初始打开哪张图 + 窗口标题。
// 与 StateMachineGraphScaffold（新建图播种）、StateMachineConnectionRules（连接矩阵）同属
// 「框架出机制、领域填策略」。照 DialogueEditorLauncher 成例。仅 Editor/ 程序集。

using System.Linq;
using UnityEditor;
using NodeEditor;            // NodeGraphAsset
using NodeEditor.EditorUI;   // NodeEditorWindow（OpenModule 机制）+ Localizer

namespace StateMachine.EditorUI
{
    public static class StateMachineEditorLauncher
    {
        // 领域策略：模块模式初始打开哪张状态机图——本域第一张（按名序）；一张没有则开模块空壳让用户新建。
        [MenuItem("Tools/NodeGraph/State Machine", priority = 300)]
        public static void Open()
        {
            var graph = AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                .Select(guid => AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null && asset.module == StateMachineGraphScaffold.Module)
                .OrderBy(asset => asset.name)
                .FirstOrDefault();
            // 标题键跟框架 UpdateWindowTitle 的 ui.{module}Editor 约定（module 全小写）——导航后框架用该键重刷标题。
            NodeEditorWindow.OpenModule(StateMachineGraphScaffold.Module, Localizer.UI("ui.statemachineEditor", "State Machine Editor"), graph);
        }
    }
}
