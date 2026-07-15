using System.Linq;
using UnityEditor;
using NodeEditor;
using NodeEditor.EditorUI;

namespace TaskEditor.EditorUI
{
    public static class TaskEditorLauncher
    {
        [MenuItem("Tools/NodeGraph/Task", priority = 200)]
        public static void Open()
        {
            var graph = AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset))
                .Select(guid => AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null && asset.module == TaskGraphScaffold.Module)
                .OrderBy(asset => asset.name)
                .FirstOrDefault();

            NodeEditorWindow.OpenModule(TaskGraphScaffold.Module, Localizer.UI("ui.taskEditor", "Task Editor"), graph);
        }
    }
}
