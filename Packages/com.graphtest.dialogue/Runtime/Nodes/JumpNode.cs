// JumpNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Flow/Jump")]
    [NodeDoc(Language.English, "Jump", "Redirects flow to the Label whose name matches targetLabel (resolved at runtime).")]
    [NodeDoc(Language.Chinese, "跳转", "把流程重定向到 labelName 匹配 targetLabel 的 Label（运行时解析）。")]
    [ParamDoc(Language.English, "targetLabel", "Target Label", "Name of the Label to jump to.")]
    [ParamDoc(Language.Chinese, "targetLabel", "目标标签", "要跳转到的 Label 名称。")]
    public class JumpNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Jump;
        protected override void Define()
        {
            // 按 label 名称重定向流程（运行时解析到匹配的 Label），而不是按易变的 instance id ——
            // label 能在复制/粘贴和图编辑中存续。
            Meta("Jump", NodeRole.Control);
            AddParam("targetLabel", TypeRef.String, "dialogue.labels");   // 从图里已有的 Label 名里选
            AddIn("in", Arity.Many);
        }
    }
}
