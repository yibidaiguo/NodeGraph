// SubMachineNode.cs —— 状态机节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StateMachineNodes.cs）。Runtime 程序集。

using NodeEditor;

namespace StateMachine
{
    [NodeMenu("State Machine/Hierarchy/Sub Machine")]
    [NodeDoc(Language.English, "Sub Machine", "Hierarchical sub state machine (HSM): entering runs the sub graph's Entry; when the sub graph reaches Exit, control returns to this node's outgoing transitions. Its own lifecycle wraps the sub machine (outer OnEnter first, outer OnExit last).")]
    [NodeDoc(Language.Chinese, "子状态机", "分层状态机（HSM）节点：进入即运行子图的 Entry 指向态；子图走到 Exit 后回到父层、本节点的出向转移继续求值。自身生命周期包裹子机（进入序自外向内：本节点 OnEnter 先于子态；退出序自内向外：子态 OnExit 先于本节点）。")]
    [ParamDoc(Language.English, "graph", "Sub Graph", "The state-machine graph asset to run inside (module must be \"statemachine\" and contain an Entry). Self/circular references are validation errors.")]
    [ParamDoc(Language.Chinese, "graph", "子图", "内嵌运行的状态机图资产（必须是 module=\"statemachine\" 的图且含 Entry）。自引用/环引用（子机链回到祖先图）= 校验 ERROR（无限递归）。")]
    [ParamDoc(Language.English, "onEnter", "On Enter", "Action unit run once when this sub machine is entered (before the sub graph's initial state enters); empty = do nothing.")]
    [ParamDoc(Language.Chinese, "onEnter", "进入动作", "进入本子机的瞬间跑一次的动作单元（先于子图初始状态的进入——自外向内）；留空 = 不做任何事。")]
    [ParamDoc(Language.English, "onUpdate", "On Update", "Action unit run once per tick while the sub machine is active, before the active inner state's OnUpdate (outer-to-inner); empty = do nothing.")]
    [ParamDoc(Language.Chinese, "onUpdate", "更新动作", "本子机活动期间每 tick 跑一次的动作单元，先于当前子态的 OnUpdate（自外向内）；留空 = 不做任何事。")]
    [ParamDoc(Language.English, "onExit", "On Exit", "Action unit run once when this sub machine is left (after the inner active state's OnExit — inner-to-outer); empty = do nothing.")]
    [ParamDoc(Language.Chinese, "onExit", "退出动作", "退出本子机的瞬间跑一次的动作单元（后于子态的 OnExit——自内向外）；留空 = 不做任何事。")]
    public class SubMachineNode : StateMachineNodeDefinition
    {
        public override StateMachineNodeKind Kind => StateMachineNodeKind.SubMachine;
        protected override void Define()
        {
            // 与 State 同形（in/transitions + 生命周期三槽），外加一个子图 Object 引用
            //（真实资产引用走 objectOverrides，构建安全——照 SubDialogueNode 成例）。
            Meta("Sub Machine", NodeRole.Control);
            AddIn("in", Arity.Many);
            AddOut("transitions", Arity.Many);
            AddParam("graph", TypeRef.Object(typeof(NodeGraphAsset).FullName));
            AddUnitParam("onEnter", "Action");
            AddUnitParam("onUpdate", "Action");
            AddUnitParam("onExit", "Action");
        }
    }
}
