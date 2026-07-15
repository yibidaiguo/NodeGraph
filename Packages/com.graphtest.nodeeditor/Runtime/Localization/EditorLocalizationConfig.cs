// EditorLocalizationConfig.cs — 编辑器本地化配置（ScriptableObject，独立文件以便绑定 MonoScript）。Runtime 程序集
// （仅数据；逻辑在 Editor 层的 Localizer）。控制编辑器界面用哪种语言显示：节点名/参数名/功能说明/tooltip/面板/工具栏。
// language 是枚举字段——Unity 检视面板会自动渲染成下拉框（满足"枚举 + 下拉框选择"）。table 为可选的兜底本地化表。

using UnityEngine;

namespace NodeEditor
{
    [CreateAssetMenu(menuName = "NodeEditor/Editor Localization Config")]
    public class EditorLocalizationConfig : ScriptableObject
    {
        public Language language = Language.Chinese;   // 默认中文（面向中文创作者）
        public LocalizationTable table;                // 兜底/补充文案；为空则只用属性 + 英文回退
    }
}
