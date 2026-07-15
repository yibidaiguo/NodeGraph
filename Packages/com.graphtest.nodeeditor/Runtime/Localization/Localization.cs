// Localization.cs — 本地化的语言枚举 + 两个标注属性（非 ScriptableObject，可同文件）。Runtime 程序集。
// 设计：节点/参数的本地化"名称+说明"优先用代码属性（NodeDoc/ParamDoc）声明，没写则回退英文；
// 集中的 LocalizationTable（SO）作为兜底/补充（编辑器界面 chrome 文案、未加属性的节点/参数）。

using System;

namespace NodeEditor
{
    // 本地化语言。可扩展：新增一个枚举值，再在各处（属性 / 表 / DialogueDatabase 的 lang）补该语言即可。
    public enum Language { English, Chinese }

    // 语言 → DialogueDatabase 等用的 lang 代码（"en"/"zh"）。
    public static class LanguageCodes
    {
        public static string Code(this Language language) => language == Language.Chinese ? "zh" : "en";
    }

    // 标注节点定义类的本地化"显示名 + 功能说明"。可多次标注（每种语言一条）；某语言没写则回退英文
    // （英文取 [NodeDoc(English)] 或 Meta() 的名字）。编辑器按当前编辑器语言（EditorLocalizationConfig）选用。
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class NodeDocAttribute : Attribute
    {
        public Language Language { get; }
        public string Name { get; }
        public string Description { get; }
        public NodeDocAttribute(Language language, string name, string description = null)
        { Language = language; Name = name; Description = description; }
    }

    // 标注某个参数的本地化"显示名 + 说明"。param 对应 Define() 里 AddParam 的名字。可多次标注（每参数×每语言一条）。
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class ParamDocAttribute : Attribute
    {
        public Language Language { get; }
        public string Param { get; }
        public string Name { get; }
        public string Description { get; }
        public ParamDocAttribute(Language language, string param, string name, string description = null)
        { Language = language; Param = param; Name = name; Description = description; }
    }
}
