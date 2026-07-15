// LineNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Line")]
    [NodeDoc(Language.English, "Dialogue Line", "Shows one line of dialogue, resolved from the localization database by key.")]
    [NodeDoc(Language.Chinese, "台词", "显示一句台词，按 key 从本地化数据库取文本/说话人。")]
    [ParamDoc(Language.English, "lineKey", "Line Key", "Localization key looked up in the DialogueDatabase.")]
    [ParamDoc(Language.Chinese, "lineKey", "台词键", "在 DialogueDatabase 中查找的本地化键。")]
    public class LineNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Line;
        protected override void Define()
        {
            Meta("Dialogue Line", NodeRole.Action);
            AddParam("lineKey", TypeRef.String, "dialogue.lineKeys");   // 只列用途=台词/通用 的库 key，避免手填错
            AddIn("in", Arity.Many);
            AddOut("next", Arity.Optional);
        }
    }
}
