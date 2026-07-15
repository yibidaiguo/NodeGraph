using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using NodeEditor;
using NodeEditor.EditorUI;

namespace TaskEditor.EditorUI
{
    [InitializeOnLoad]
    public static class TaskParamChoices
    {
        static TaskParamChoices()
        {
            ParamChoiceProviders.Register("task.labels", LabelNames, allowCustom: true);
            ParamChoiceProviders.Register("task.localizationKeys", LocalizationKeys);
            ParamChoiceProviders.Register("task.stepGraphs", StepGraphPaths);
            ParamReferenceEditors.Register("task.localizationKeys", (_, key) => LocalizationKeyEditor(key));
        }

        static IEnumerable<string> LabelNames(ParamChoiceContext ctx)
        {
            if (ctx.asset == null || ctx.registry == null) yield break;
            foreach (var inst in ctx.asset.instances)
            {
                if (ctx.registry.Find(inst.definitionId) is not TaskNodeDefinition def) continue;
                if (def.Kind != TaskNodeKind.Label) continue;
                var name = ParamResolver.Resolve(inst, def, "labelName");
                if (!string.IsNullOrEmpty(name)) yield return name;
            }
        }

        static IEnumerable<string> LocalizationKeys(ParamChoiceContext ctx)
        {
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (paths == null) yield break;
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(paths.localizationTablePath);
            if (table == null) yield break;

            foreach (var key in table.Entries
                         .Where(e => e != null && e.key != null && e.key.StartsWith("task."))
                         .Select(e => e.key)
                         .Distinct())
                yield return key;
        }

        static IEnumerable<string> StepGraphPaths(ParamChoiceContext ctx)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:NodeGraphAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
                if (graph != null && graph.module == TaskGraphScaffold.Module && graph.graphType == GraphType.ControlFlow)
                    yield return path;
            }
        }

        static VisualElement LocalizationKeyEditor(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (paths == null) return null;
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(paths.localizationTablePath);
            return table == null ? null : LocalizationTablePane.BuildDetail(table, new DataItem(key, key, "", "", key));
        }
    }
}
