// DialogueBlackboard.cs —— 对话运行时的作用域变量存储 + 字符串<->类型化值解析。
// 存储机制（Dictionary + BlackboardSet 播种 + Get/Set/GetF/SetF）已收敛进框架 RuntimeBlackboard
//（三领域过去各自复制一份、逐行相同）；本类只保留领域类型名以兼容既有 API/存档代码。
// 这是 blackboard-scoping.md 所述的每实例存储：每个运行中的对话一个实例，而非全局
// 可变单例（红线 §6）。Runtime 程序集 —— 无 editor-only 依赖。

using NodeEditor;

namespace Dialogue
{
    // 把编辑器的字符串值参数桥接为类型化的运行时值。纯函数，无可变状态。
    // 解析/数值归约的真身在框架 NodeEditor.UnitValues（可组合单元共用同一套语义）；此处仅作领域侧的薄转发，
    // 保持既有调用点（黑板播种、Capture/Restore）不变。
    public static class ValueParse
    {
        public static object To(TypeRef t, string s) => UnitValues.To(t, s);
        public static double Number(object v) => UnitValues.Number(v);
    }

    // 每对话实例的黑板：存储语义见框架 RuntimeBlackboard（播种/快速路径/空集可用等）。
    public class DialogueBlackboard : RuntimeBlackboard
    {
        public DialogueBlackboard(BlackboardSet set) : base(set) { }
    }
}
