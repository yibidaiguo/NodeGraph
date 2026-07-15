// StateNode.cs —— 状态机节点定义（独立文件以便 Unity 绑定其 MonoScript；见 StateMachineNodes.cs）。Runtime 程序集。

using NodeEditor;

namespace StateMachine
{
    [NodeMenu("State Machine/Core/State")]
    [NodeDoc(Language.English, "State", "A regular state with a full lifecycle: OnEnter once on entry, OnUpdate every tick, OnExit once on leave. Outgoing edges go to Transition nodes.")]
    [NodeDoc(Language.Chinese, "状态", "普通状态，具备完整生命周期：进入瞬间跑一次 OnEnter，每 tick 跑一次 OnUpdate，退出瞬间跑一次 OnExit。出边一律接 Transition（转移）节点。")]
    [ParamDoc(Language.English, "onEnter", "On Enter", "Action unit run once when the state is entered; empty = do nothing. Compose from the global + state-machine action registry.")]
    [ParamDoc(Language.Chinese, "onEnter", "进入动作", "进入该状态的瞬间跑一次的动作单元；留空 = 不做任何事。从「全局通用 + 状态机」动作注册表里下拉选择/装饰组合。")]
    [ParamDoc(Language.English, "onUpdate", "On Update", "Action unit run once per tick while the state is active (after transitions settle); empty = do nothing.")]
    [ParamDoc(Language.Chinese, "onUpdate", "更新动作", "该状态活动期间每 tick 跑一次的动作单元（在转移求值稳定之后执行）；留空 = 不做任何事。")]
    [ParamDoc(Language.English, "onExit", "On Exit", "Action unit run once when the state is left; empty = do nothing.")]
    [ParamDoc(Language.Chinese, "onExit", "退出动作", "退出该状态的瞬间跑一次的动作单元；留空 = 不做任何事。")]
    [ParamDoc(Language.English, "tags", "Tags", "Optional comma-separated tags for external adapters (animation / camera etc.) to subscribe on; does not affect machine logic.")]
    [ParamDoc(Language.Chinese, "tags", "标签", "可选标签，逗号分隔，供外部适配器（动画/相机等）订阅识别；不影响状态机自身逻辑。")]
    public class StateNode : StateMachineNodeDefinition
    {
        public override StateMachineNodeKind Kind => StateMachineNodeKind.State;
        protected override void Define()
        {
            // 生命周期三槽全部是可组合 Unit 槽（红线#13）：条件/动作绝不烘成 key/op/value 参数。
            Meta("State", NodeRole.Control);
            AddIn("in", Arity.Many);
            AddOut("transitions", Arity.Many);
            AddUnitParam("onEnter", "Action");
            AddUnitParam("onUpdate", "Action");
            AddUnitParam("onExit", "Action");
            AddParam("tags", TypeRef.String);
        }
    }
}
