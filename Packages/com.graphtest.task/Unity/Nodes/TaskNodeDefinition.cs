// TaskNodeDefinition.cs —— 任务节点定义基类（ScriptableObject）。Task.Unity 程序集。
// 枚举 TaskNodeKind 是纯数据，留在 Task.Runtime；本基类继承 NodeDefinition，属 Unity 侧。
using NodeEditor;

namespace TaskEditor
{
    public abstract class TaskNodeDefinition : NodeDefinition
    {
        public override string Module => "task";
        public abstract TaskNodeKind Kind { get; }
        protected override string StableId => "task." + Kind;
        // 烘进 NodeSchema.kind，供纯层执行器经 TaskKinds.Parse 解析回枚举。
        public override string KindTag => Kind.ToString();
    }
}
