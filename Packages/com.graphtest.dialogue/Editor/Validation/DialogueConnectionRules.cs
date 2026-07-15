// DialogueConnectionRules.cs — 对话族的“连接规则”集中矩阵（include/exclude，双向），通过
// ConnectionRules.RegisterRule 钩子注册进框架（ConnectionRules.cs / GraphValidator.cs 本身从不在此编辑）。
// 把“哪种节点能接哪种节点”的约定显式化，于是策划配错时：
//   · 拖拽期直接连不上（GraphCanvasView.GetCompatiblePorts 调 ConnectionRules.Evaluate）；
//   · 已存在的非法边被校验报错并标红（GraphValidator.CheckConnectionRules）。
// 两条路径共享这一份规则源。新增/调整约束 = 改 s_Matrix 一行。
// 判定引擎（出向/入向、端口专属优先、本地化拒绝消息）在框架 ConnectionRuleMatrix<TDef,TKind>——
// 本文件只持有「对话矩阵数据」这份领域策略（框架出机制、领域填策略）。仅 Editor 程序集，无运行时依赖。

using UnityEditor;
using NodeEditor;

namespace Dialogue.EditorUI
{
    [InitializeOnLoad]
    public static class DialogueConnectionRules
    {
        // —— 对话连接矩阵：被显式化的“约定” ——
        // 当前唯一的硬约束是 Choice <-> Option 的双向锁：
        //   · Choice 的 options 出口只能接 Option；
        //   · Option 的输入只接受来自 Choice。
        // 这两条合起来就完整表达了“Option 只作为 Choice 的子项出现”——其余节点间的流转保持自由（默认允许）。
        // 要新增约束，在此加一行即可（Exclude 用法见 EXTENDING.md 配方）。
        static readonly ConnectionRuleMatrix<DialogueNodeDefinition, DialogueNodeKind> s_Matrix =
            new ConnectionRuleMatrix<DialogueNodeDefinition, DialogueNodeKind>(new[]
            {
                new ConnectionRuleEntry<DialogueNodeKind> { node = DialogueNodeKind.Choice, port = "options", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = new[] { DialogueNodeKind.Option } },
                new ConnectionRuleEntry<DialogueNodeKind> { node = DialogueNodeKind.Option, port = "in",      side = ConnectSide.In,  mode = ConnectMode.Include, kinds = new[] { DialogueNodeKind.Choice } },
            }, def => def.Kind);

        static DialogueConnectionRules() => ConnectionRules.RegisterRule("dialogue", s_Matrix.Evaluate);

        // 直接判定入口（EditMode 测试逐格断言用；与注册进框架的是同一个矩阵实例）。
        public static ConnectionVerdict Evaluate(ConnectionContext ctx) => s_Matrix.Evaluate(ctx);
    }
}
