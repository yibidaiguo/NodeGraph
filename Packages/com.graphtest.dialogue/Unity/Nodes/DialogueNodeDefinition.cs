// DialogueNodeDefinition.cs —— 对话节点定义的公共基类（ScriptableObject）。Dialogue.Unity 程序集。
//
// 从 DialogueNodes.cs 抽出：枚举 DialogueNodeKind 是纯数据，留在 Dialogue.Runtime（纯层）；
// 本基类继承 NodeEditor.NodeDefinition（ScriptableObject），属于 Unity 侧。
// 抽象类型从不实例化为 asset，故不受"每类一文件 MonoScript"硬规则约束；
// 十个具体节点（StartNode / LineNode / …）仍各自独立文件，见同目录。

using NodeEditor;

namespace Dialogue
{
    // 每个对话节点的公共基类：钉死一个由 Kind 推导出的确定性 StableId，使得一个定义在任何机器上/
    // 任何重新生成后都解析到相同的 id，从而让已有的图继续可用。
    public abstract class DialogueNodeDefinition : NodeDefinition
    {
        public override string Module => "dialogue";
        public abstract DialogueNodeKind Kind { get; }
        protected override string StableId => "dialogue." + Kind;

        // 烘进 NodeSchema.kind，供纯层执行器经 DialogueKinds.Parse 解析回 DialogueNodeKind。
        // 这是"执行器不再认识 ScriptableObject"的关键一环：过去是
        // `registry.Find(id) as DialogueNodeDefinition` 然后读 .Kind，现在是读 schema.kind 字符串。
        public override string KindTag => Kind.ToString();
    }
}
