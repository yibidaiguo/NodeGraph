// StateMachineBlackboard.cs —— 状态机运行时的每实例作用域黑板。
// 存储机制（Dictionary + BlackboardSet 播种 + Get/Set/GetF/SetF + Declared 声明视图）已收敛进框架
// RuntimeBlackboard（三领域过去各自复制一份、逐行相同）；本类只保留领域类型名以兼容既有 API/存档代码。
// 每个运行中的状态机一个实例，绝非全局可变单例（准则#15）。Runtime 程序集 —— 无 editor-only 依赖（红线 §6）。

using NodeEditor;

namespace StateMachine
{
    public class StateMachineBlackboard : RuntimeBlackboard
    {
        public StateMachineBlackboard(BlackboardSet set) : base(set) { }
    }
}
