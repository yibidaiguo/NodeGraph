using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using NodeEditor;
using NodeEditor.EditorUI;

namespace TaskEditor.EditorUI
{
    [InitializeOnLoad]
    public static class TaskDataSources
    {
        const string Domain = TaskGraphScaffold.Module;

        static TaskDataSources()
        {
            DataSourceRegistry.Register("task.blackboard", _ =>
            {
                var asset = BlackboardLocator.FindLayer(Domain, "");
                return new DelegateListDataSource(
                    "task.blackboard",
                    Localizer.UI("ui.taskModuleVariables", "Task Module Variables"),
                    DataScope.Domain,
                    Domain,
                    _ => VariablePane.Items(asset),
                    (c, item) => VariablePane.BuildVariableDetail(c.Registry, asset, c.Graph, item),
                    c => FrameworkDataSources.BuildLayerPane(c, Domain, ""));
            });

            DataSourceRegistry.Register("task.defs", _ =>
            {
                var registry = NodeRegistryLocator.Find();
                IEnumerable<NodeDefinition> Mine() => registry == null
                    ? Enumerable.Empty<NodeDefinition>()
                    : registry.projectDomain.Where(d => d is TaskNodeDefinition);

                return new DelegateListDataSource(
                    "task.defs",
                    Localizer.UI("ui.taskNodeDefs", "Task Node Definitions (read-only)"),
                    DataScope.Domain,
                    Domain,
                    _ => FrameworkDataSources.NodeDefItems(Mine()),
                    FrameworkDataSources.BuildNodeDefDetail,
                    _ => FrameworkDataSources.BuildNodeDefsView(Mine()));
            });
        }
    }
}
