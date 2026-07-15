// DialogueNodeViews.cs — 通过连线图编辑器的 NodeViewControl 扩展点（debug-mode.md #5 / styling.md），
// 为对话节点词汇提供可读性提示。每个提示是节点端口下方的一行灰色文本，概括其已配置的参数
//（lineKey、gate、跳转目标等），让设计师无需对每个节点打开 Inspector 即可读懂图的结构。
// 不新增任何图片资源（保持纯文本，沿用工具栏的字形回退先例）——也不修改 NodeEditor 核心：
// 这里只使用现有的逐节点 attach/refresh 接缝。
//
// Start/End 没有任何已配置参数（DialogueNodes.cs），所以它们的标题本身就已是完整的可读性提示——
// 有意不为它们注册 NodeViewControl。
//
// 参数值在节点视图构建之后仍可能改变（Inspector 编辑、重新连线）；每个控件按一个较短的编辑器
// 计划周期轮询，而不是要求 NodeEditor 核心（InspectorPane/GraphCanvasView）推送一个刷新回调——
// 那意味着为一个仅 Dialogue 相关的需求去触碰已冻结的核心。

using NodeEditor;
using NodeEditor.EditorUI;

namespace Dialogue.EditorUI
{
    // 下面每个提示控件共用的轮询 + 标签管线。
    public abstract class DialogueCueControl : NodeCueControl
    {
        protected override string CueName => "dialogue-cue";
    }

    [NodeViewControl(typeof(LineNode))]
    public class LineNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            Localizer.UI("ui.cue.line", "Line: ") + Clip(Param(inst, def, "lineKey"));
    }

    [NodeViewControl(typeof(ChoiceNode))]
    public class ChoiceNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def)
        {
            int options = 0;
            foreach (var c in inst.connections) if (c.fromPort == "options") options++;
            return options == 1
                ? Localizer.UI("ui.cue.optionsOne", "1 option")
                : string.Format(Localizer.UI("ui.cue.options", "{0} options"), options);
        }
    }

    [NodeViewControl(typeof(OptionNode))]
    public class OptionNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def)
        {
            var key = Clip(Param(inst, def, "optionKey"));
            return ParamResolver.ResolveUnit(inst, "gate") == null ? key : $"{key}\n{Localizer.UI("ui.cue.if", "if ")}{UnitDesc(inst, "gate")}";
        }
    }

    [NodeViewControl(typeof(ConditionNode))]
    public class ConditionNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            "? " + UnitDesc(inst, "predicate");
    }

    [NodeViewControl(typeof(ActionNode))]
    public class ActionNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            UnitDesc(inst, "actions");
    }

    [NodeViewControl(typeof(JumpNode))]
    public class JumpNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            "→ " + Clip(Param(inst, def, "targetLabel"));
    }

    [NodeViewControl(typeof(LabelNode))]
    public class LabelNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            "# " + Clip(Param(inst, def, "labelName"));
    }

    [NodeViewControl(typeof(SubDialogueNode))]
    public class SubDialogueNodeCue : DialogueCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def)
        {
            var sub = ParamResolver.ResolveObject(inst, "subGraph") as NodeGraphAsset;
            return "↳ " + (sub != null ? sub.name : Localizer.UI("ui.cue.unset", "(unset)"));
        }
    }
}
