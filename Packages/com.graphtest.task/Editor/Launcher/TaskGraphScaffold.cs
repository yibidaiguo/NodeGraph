using UnityEditor;
using UnityEngine;
using NodeEditor;
using NodeEditor.EditorUI;

namespace TaskEditor.EditorUI
{
    [InitializeOnLoad]
    public static class TaskGraphScaffold
    {
        public const string Module = "task";

        static TaskGraphScaffold()
        {
            GraphCreationRegistry.Register(new GraphCreateRecipe
            {
                id = "task.line",
                module = Module,
                labelKey = "ui.newTaskLine",
                labelFallback = "New Task Line",
                defaultFileName = "NewTaskLine",
                graphRoot = () => Paths()?.taskGraphsDir,
                blackboardFolder = () => Paths()?.blackboardLayersDir,
                initialize = SeedTaskLine
            });
            GraphCreationRegistry.Register(new GraphCreateRecipe
            {
                id = "task.steps",
                module = Module,
                labelKey = "ui.newTaskSteps",
                labelFallback = "New Step Graph",
                defaultFileName = "NewTaskSteps",
                graphRoot = () => Paths()?.stepGraphsDir,
                blackboardFolder = () => Paths()?.blackboardLayersDir,
                initialize = SeedStepGraph
            });
        }

        static TaskAssetPaths Paths() => TaskAssetPathsLocator.FindOrCreate();

        public static bool SeedTaskLine(NodeGraphAsset graph)
        {
            if (graph == null) return false;
            graph.module = Module;
            graph.graphType = GraphType.DependencyDag;
            return true;
        }

        public static bool SeedStepGraph(NodeGraphAsset graph)
        {
            if (graph == null) return false;
            var startDefinition = NodeDefinitionLocator.ForType(typeof(TaskStartNode));
            if (startDefinition == null) return false;
            graph.module = Module;
            graph.graphType = GraphType.ControlFlow;
            var start = new NodeInstance
            {
                definitionId = startDefinition.Id,
                position = Vector2.zero,
                pinned = true
            };
            graph.instances.Add(start);
            graph.entryInstanceIds.Add(start.instanceId);
            return true;
        }
    }
}
