// DialogueEditorLauncher.cs — 对话领域层：Tools/NodeGraph/Dialogue 的“模块模式”编辑器入口。
// 框架（NodeEditorWindow）提供"按模块打开"这一**机制**（领域无关：左侧只列该模块的图、隐藏工具栏对象框，
// 本模块内仍可多图切换）；本类提供**策略**：菜单入口 + 锁到哪个模块（"dialogue"）+ 初始打开哪张图 + 窗口标题。
// 与 DialogueGraphScaffold（新建图播种）、DialogueConnectionRules（连接规则矩阵）同属"框架出机制、领域填策略"。仅 Editor/ 程序集。
//
// 两个入口对比：
//   NodeGraph Manager / Open Node Editor → 自由模式：左侧按全部模块分组、工具栏对象框可切换任意 NodeGraphAsset。
//   Tools/NodeGraph/Dialogue → 模块模式：左侧只列 "dialogue" 模块的对话组、无对象框；可在对话组间切换。

using System.Linq;
using UnityEditor;
using NodeEditor;            // NodeGraphAsset
using NodeEditor.EditorUI;   // NodeEditorWindow（OpenModule 机制）+ Localizer

namespace Dialogue.EditorUI
{
    public static class DialogueEditorLauncher
    {
        // 领域策略：模块模式初始打开哪张对话图。路径取自 DialogueAssetPaths（SO，可在检视面板改）；
        // Open the first dialogue graph if one exists; otherwise open the module shell so the user can create one.
        [MenuItem("Tools/NodeGraph/Dialogue", priority = 100)]
        public static void Open()
        {
            var graph = AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                .Select(guid => AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null && asset.module == DialogueGraphScaffold.Module)
                .OrderBy(asset => asset.name)
                .FirstOrDefault();
            NodeEditorWindow.OpenModule(DialogueGraphScaffold.Module, Localizer.UI("ui.dialogueEditor", "Dialogue Editor"), graph);
        }
    }
}
