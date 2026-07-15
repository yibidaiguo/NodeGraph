// DialoguePlayer.cs —— 面向场景的 MonoBehaviour 驱动器，用于驱动一个 DialogueRunner。
// 在 inspector 中接入四个资源（graph/registry/blackboard/database）+ 语言；该组件
// 在 Awake 中构建一个 Runner 并暴露它，使调用方可订阅 OnLine/OnChoices/OnEvent/OnEnd，
// 并将 Begin/Advance/Choose 以及 Save/Load（#8 Capture/Restore）转发给它。设计上很薄：所有对话
// 逻辑都在 DialogueRunner 中 —— 这里只是引擎侧的句柄。Runtime 程序集，无 UnityEditor（§6）。

using System;
using UnityEngine;
using NodeEditor;

namespace Dialogue
{
    public class DialoguePlayer : MonoBehaviour
    {
        [Tooltip("要播放的对话图（从 Start 节点开始的 control-flow NodeGraphAsset）。")]
        public NodeGraphAsset graph;
        [Tooltip("把每个实例的 definitionId 解析为对应 DialogueNodeDefinition 的注册表（驱动 Kind/参数）。")]
        public NodeRegistry registry;
        [Tooltip("黑板变量声明的分层引用，按「全局→模块→组」由外到内排列；用默认值初始化 runner 的每实例黑板。" +
                 "同名 key 就近覆盖（更专的层级胜出）。运行时构建无 AssetDatabase，故各档在此显式引用；" +
                 "编辑期可由 BlackboardLocator.ResolveFor(graph) 烘出。只放一块=仅全局，行为同旧版。")]
        public BlackboardAsset[] blackboards;
        [Tooltip("按 lineKey/optionKey 寻址的本地化台词/选项内容。可选——没有它则 key 原样显示。")]
        public DialogueDatabase database;
        [Tooltip("运行时本地化配置（枚举语言下拉）。设置后按它选定的语言取文本；留空则用下面的 language 枚举。")]
        public RuntimeLocalizationConfig localizationConfig;
        [Tooltip("台词/选项解析用的语言（仅当未设置上面的本地化配置时生效）。枚举字段——检视面板渲染为原生下拉框，" +
                 "经 Code() 映射为 DialogueDatabase 的 lang 代码（English→en / Chinese→zh）。")]
        public Language language = Language.English;

        // 此组件的解释器。在 Awake 中构建，使订阅者能在 Begin() 之前挂接到它的事件。
        public DialogueRunner Runner { get; private set; }

        // 编辑器调试器的挂接缝：此 Runtime 程序集无法引用 NodeEditor.EditorUI 的
        // RuntimeGraphRegistry（Editor-only），因此改为抛出普通事件；一个驻留在
        // Dialogue.Editor 程序集中的小桥接订阅这些事件并转发给 registry。既让 Runtime 不带任何
        // Editor 依赖，又能让 play-mode 调试器找到活动的 runner（红线 §6）。
        public static event Action<IRuntimeGraph> OnRunnerCreated;
        public static event Action<IRuntimeGraph> OnRunnerDestroyed;

        void Awake()
        {
            // 优先用运行时本地化配置选定的语言；未设置则回退到 language 字符串字段。
            var lang = localizationConfig != null ? localizationConfig.language.Code() : language.Code();
            Runner = new DialogueRunner(registry, new BlackboardSet(blackboards), database, lang);
            OnRunnerCreated?.Invoke(Runner);
        }

        void OnDestroy() => OnRunnerDestroyed?.Invoke(Runner);

        // 启动（或重启）已接入的图。请先订阅 Runner.OnLine/OnChoices/OnEvent/OnEnd —— Begin()
        // 会同步走到第一个 Line/Choice 并在返回前抛出其事件。
        public void Begin() => Runner.Run(graph);

        // 确认当前行并前进；除非正停在某个 Line 上，否则忽略（见 DialogueRunner.Advance）。
        public void Advance() => Runner.Advance();

        // 按呈现的索引选择一个可见的 option；超出范围则忽略（见 DialogueRunner.Choose）。
        public void Choose(int index) => Runner.Choose(index);

        // #8 快速存档：捕获活动指针 + 黑板 + 子对话栈。参见 DialogueRunner 的
        // 图引用持久化注意事项 —— 图对象的标识需要外部 id 映射才能在完整的应用重启后存活；
        // 在同一会话内，返回的状态可经由 Load() 完整往返。
        public DialogueState Save() => Runner.Capture();

        // #8 从捕获的状态恢复：还原指针/黑板/栈，并重新呈现当前
        // 节点（重新抛出 OnLine/OnChoices），故请在调用 Load() 之前挂接事件订阅者。
        public void Load(DialogueState state) => Runner.Restore(state);
    }
}
