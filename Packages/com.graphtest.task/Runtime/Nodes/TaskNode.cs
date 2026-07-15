using NodeEditor;

namespace TaskEditor
{
    [NodeMenu("Task/DAG/Task")]
    [NodeDoc(Language.English, "Task", "A quest or task in the dependency DAG.")]
    [NodeDoc(Language.Chinese, "任务", "依赖 DAG 中的一项任务或任务线节点。")]
    [ParamDoc(Language.English, "taskId", "Task Id", "Stable id used by progress and save data.")]
    [ParamDoc(Language.Chinese, "taskId", "任务 ID", "进度与存档使用的稳定 ID。")]
    [ParamDoc(Language.English, "titleKey", "Title Key", "Localization key for the task title.")]
    [ParamDoc(Language.Chinese, "titleKey", "标题键", "任务标题的本地化键。")]
    [ParamDoc(Language.English, "summaryKey", "Summary Key", "Localization key for the task summary.")]
    [ParamDoc(Language.Chinese, "summaryKey", "摘要键", "任务摘要的本地化键。")]
    [ParamDoc(Language.English, "category", "Category", "Authoring category for filtering and journal grouping.")]
    [ParamDoc(Language.Chinese, "category", "分类", "用于筛选和任务日志分组的作者分类。")]
    [ParamDoc(Language.English, "stepGraph", "Step Graph", "Control-flow graph executed when this task starts.")]
    [ParamDoc(Language.Chinese, "stepGraph", "步骤图", "任务开始后执行的控制流步骤图。")]
    [ParamDoc(Language.English, "repeatable", "Repeatable", "Whether the task may be completed more than once.")]
    [ParamDoc(Language.Chinese, "repeatable", "可重复", "该任务是否允许多次完成。")]
    public class TaskNode : TaskNodeDefinition
    {
        public override TaskNodeKind Kind => TaskNodeKind.Task;

        protected override void Define()
        {
            Meta("Task", NodeRole.Control);
            AddParam("taskId", TypeRef.String);
            AddParam("titleKey", TypeRef.String, "task.localizationKeys");
            AddParam("summaryKey", TypeRef.String, "task.localizationKeys");
            AddParam("category", TypeRef.String);
            AddParam("stepGraph", TypeRef.Object(typeof(NodeGraphAsset).FullName), "task.stepGraphs");
            AddParam("repeatable", TypeRef.Bool);
            AddIn("prerequisite", Arity.Many);
            AddOut("unlocks", Arity.Many);
        }
    }
}
