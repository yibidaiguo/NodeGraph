// ActionNode.cs —— 对话节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StartNode.cs）。Runtime 程序集。

using NodeEditor;

namespace Dialogue
{
    [NodeMenu("Dialogue/Logic/Action")]
    [NodeDoc(Language.English, "Action", "Runs a composable action unit (set variable, fire event, …), then continues. Use a Sequence to run several.")]
    [NodeDoc(Language.Chinese, "动作", "执行一个可组合动作单元（设变量、触发事件…），然后继续。要一次多个用「顺序」装饰。")]
    [ParamDoc(Language.English, "actions", "Action", "Action unit to run; pick/compose from the global + dialogue action registry (Sequence/Conditional to combine).")]
    [ParamDoc(Language.Chinese, "actions", "动作", "要执行的动作单元；从「全局通用 + 对话」动作注册表里下拉选择/装饰组合（顺序/条件执行可组合多个）。")]
    public class ActionNode : DialogueNodeDefinition
    {
        public override DialogueNodeKind Kind => DialogueNodeKind.Action;
        protected override void Define()
        {
            // 通用副作用节点：副作用委托给可组合动作单元（actions 槽）。旧的 SetVariable/Event 专用节点已并入为动作单元。
            Meta("Action", NodeRole.Action);
            AddUnitParam("actions", "Action");
            AddIn("in", Arity.Many);
            AddOut("next", Arity.ExactlyOne);
        }
    }
}
