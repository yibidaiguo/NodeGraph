// TaskBlackboard.cs —— 任务运行时的每实例作用域黑板。
// 存储机制（Dictionary + BlackboardSet 播种 + Get/Set/GetF/SetF）已收敛进框架 RuntimeBlackboard
//（三领域过去各自复制一份、逐行相同）；本类只保留领域类型名以兼容既有 API/存档代码。
// Runtime 程序集 —— 无 editor-only 依赖。

using NodeEditor;

namespace TaskEditor
{
    public class TaskBlackboard : RuntimeBlackboard
    {
        public TaskBlackboard(BlackboardSet set) : base(set) { }
    }
}
