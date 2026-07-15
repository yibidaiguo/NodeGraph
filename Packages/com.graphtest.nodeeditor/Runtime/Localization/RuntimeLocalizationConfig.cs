// RuntimeLocalizationConfig.cs — 运行时本地化配置（ScriptableObject，独立文件以便绑定 MonoScript）。Runtime 程序集。
// 控制游戏运行时对话文本用哪种语言：DialoguePlayer/DialogueRunner 据此从 DialogueDatabase（已按 lang 存多语文本）
// 取对应语言。language 是枚举字段，检视面板自动渲染为下拉框。

using UnityEngine;

namespace NodeEditor
{
    [CreateAssetMenu(menuName = "NodeEditor/Runtime Localization Config")]
    public class RuntimeLocalizationConfig : ScriptableObject
    {
        public Language language = Language.Chinese;   // 默认中文（面向中文产品；样例 DialogueDatabase 已含 zh 文本）
    }
}
