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

    // TaskNodeDefinition（ScriptableObject 基类）已移至 Unity/Nodes/TaskNodeDefinition.cs
    //（Task.Unity 程序集）。执行器不再按类型强转定义，改读 NodeSchema.kind——见下。

    // 把 NodeSchema.kind 字符串解析回领域枚举（由 TaskNodeDefinition.KindTag 烘出）。
    public static class TaskKinds
    {
        public static TaskNodeKind? Parse(string kind) =>
            System.Enum.TryParse<TaskNodeKind>(kind, out var k) ? k : (TaskNodeKind?)null;
    }
}
