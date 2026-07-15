// StateMachineNodeViews.cs — 通过连线图编辑器的 NodeViewControl 扩展点，为状态机节点词汇提供
// 可读性提示（cue）：节点端口下方一行灰色文本，概括其已配置的参数（生命周期槽、转移条件/优先级、
// 子图名……），让设计师无需对每个节点打开检视面板即可读懂图的结构。照 DialogueNodeViews 成例。
//
// UI-STANDARD 合规说明：cue 的样式类由框架基类 NodeCueControl.OnAttach 统一挂
// EditorUi.NodeCueClass("node-cue") + FormHelpClass——本域**不自造 USS 类、不覆写 CueName**
//（Dialogue 覆写的 "dialogue-cue" 只是元素 name、非样式类；本域直接用框架缺省，更干净）。
// 全部可见文案 Localizer.UI（ui.sm.cue.*），中文种子在 StateMachineSetup；符号前缀 →/?/↳/[p..] 语言中立不入表。
// 仅 Editor/ 程序集 —— 本文件无运行时依赖。

using System.Collections.Generic;
using NodeEditor;
using NodeEditor.EditorUI;

namespace StateMachine.EditorUI
{
    // 本域 cue 基类：只收拢领域公用的小助手，样式/轮询/截断全由框架 NodeCueControl 管。
    public abstract class StateMachineCueControl : NodeCueControl
    {
        // 生命周期槽摘要片段：槽已配置才产出「前缀+单元名」，未配置产出 null（调用方过滤）。
        protected string SlotDesc(NodeInstance inst, string slot, string labelKey, string labelEn) =>
            ParamResolver.ResolveUnit(inst, slot) == null ? null : Localizer.UI(labelKey, labelEn) + UnitDesc(inst, slot);

        // 三个生命周期槽（onEnter/onUpdate/onExit）的合并摘要；全空时给「无生命周期动作」占位。
        protected string LifecycleDesc(NodeInstance inst)
        {
            var parts = new List<string>();
            void Add(string s) { if (s != null) parts.Add(s); }
            Add(SlotDesc(inst, "onEnter", "ui.sm.cue.onEnter", "enter "));
            Add(SlotDesc(inst, "onUpdate", "ui.sm.cue.onUpdate", "update "));
            Add(SlotDesc(inst, "onExit", "ui.sm.cue.onExit", "exit "));
            return parts.Count == 0
                ? Localizer.UI("ui.sm.cue.noLifecycle", "(no lifecycle actions)")
                : string.Join("  ", parts);
        }
    }

    [NodeViewControl(typeof(EntryNode))]
    public class EntryNodeCue : StateMachineCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            "→ " + Localizer.UI("ui.sm.cue.entry", "initial state");
    }

    [NodeViewControl(typeof(StateNode))]
    public class StateNodeCue : StateMachineCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            LifecycleDesc(inst);
    }

    [NodeViewControl(typeof(TransitionNode))]
    public class TransitionNodeCue : StateMachineCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def)
        {
            // ? <条件单元名 | (恒真)> [p<优先级>]——空 condition 槽 = 恒真（无条件转移）。
            var cond = ParamResolver.ResolveUnit(inst, "condition") == null
                ? Localizer.UI("ui.sm.cue.always", "(always)")
                : UnitDesc(inst, "condition");
            var priority = Param(inst, def, "priority");
            return $"? {cond} [p{(string.IsNullOrEmpty(priority) ? "0" : priority)}]";
        }
    }

    [NodeViewControl(typeof(AnyStateNode))]
    public class AnyStateNodeCue : StateMachineCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            "* " + Localizer.UI("ui.sm.cue.anystate", "from any state");
    }

    [NodeViewControl(typeof(SubMachineNode))]
    public class SubMachineNodeCue : StateMachineCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def)
        {
            var sub = ParamResolver.ResolveObject(inst, "graph") as NodeGraphAsset;
            return "↳ " + (sub != null ? sub.name : Localizer.UI("ui.cue.unset", "(unset)"));
        }
    }

    [NodeViewControl(typeof(ExitNode))]
    public class ExitNodeCue : StateMachineCueControl
    {
        protected override string Describe(NodeInstance inst, NodeDefinition def) =>
            "→ " + Localizer.UI("ui.sm.cue.exit", "return to parent / stop");
    }
}
