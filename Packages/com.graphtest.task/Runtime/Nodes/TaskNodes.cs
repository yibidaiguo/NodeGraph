using NodeEditor;

namespace TaskEditor
{
    public enum TaskNodeKind
    {
        Task,
        Gate,
        Start,
        Objective,
        Condition,
        Action,
        WaitEvent,
        Jump,
        Label,
        Complete,
        Fail
    }

    public enum TaskGateMode { All, Any }

    public abstract class TaskNodeDefinition : NodeDefinition
    {
        public override string Module => "task";
        public abstract TaskNodeKind Kind { get; }
        protected override string StableId => "task." + Kind;
    }
}
