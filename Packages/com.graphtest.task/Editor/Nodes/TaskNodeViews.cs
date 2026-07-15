using NodeEditor;
using NodeEditor.EditorUI;

namespace TaskEditor.EditorUI
{
    public abstract class TaskCueControl : NodeCueControl
    {
        protected override string CueName => "task-cue";
        protected override string UnsetText => Localizer.UI("ui.taskCue.unset", "(unset)");
    }

    [NodeViewControl(typeof(TaskNode))]
    public class TaskNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.taskCue.task", "Task: ") + Clip(Param(inst, def, "taskId"));
    }

    [NodeViewControl(typeof(TaskGateNode))]
    public class TaskGateNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.taskCue.gate", "Gate: ") + Clip(Param(inst, def, "mode"));
    }

    [NodeViewControl(typeof(TaskObjectiveNode))]
    public class TaskObjectiveNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.taskCue.objective", "Objective: ") +
            Clip(Param(inst, def, "objectiveId")) +
            " x" +
            Clip(Param(inst, def, "requiredCount"), 8);
    }

    [NodeViewControl(typeof(TaskConditionNode))]
    public class TaskConditionNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.taskCue.if", "If: ") + UnitDesc(inst, "predicate");
    }

    [NodeViewControl(typeof(TaskActionNode))]
    public class TaskActionNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.taskCue.action", "Action: ") + UnitDesc(inst, "actions");
    }

    [NodeViewControl(typeof(TaskWaitEventNode))]
    public class TaskWaitEventNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def)
        {
            var eventId = Clip(Param(inst, def, "eventId"));
            var payload = Param(inst, def, "payloadKey");
            return string.IsNullOrEmpty(payload)
                ? Localizer.UI("ui.taskCue.wait", "Wait: ") + eventId
                : Localizer.UI("ui.taskCue.wait", "Wait: ") + eventId + "\n" +
                  Localizer.UI("ui.taskCue.payload", "Payload: ") + Clip(payload);
        }
    }

    [NodeViewControl(typeof(TaskJumpNode))]
    public class TaskJumpNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.taskCue.jump", "Jump: ") + Clip(Param(inst, def, "targetLabel"));
    }

    [NodeViewControl(typeof(TaskLabelNode))]
    public class TaskLabelNodeCue : TaskCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.taskCue.label", "Label: ") + Clip(Param(inst, def, "labelName"));
    }
}
