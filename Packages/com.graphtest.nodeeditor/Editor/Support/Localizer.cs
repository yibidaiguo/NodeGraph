// Localizer.cs — 编辑器本地化服务（Editor 程序集）。把"当前编辑器语言（EditorLocalizationConfig）+ 节点/参数的
// NodeDoc/ParamDoc 属性 + 兜底 LocalizationTable + 英文缺省"解析成最终要显示的文案。解析优先级：
//   属性(当前语言) → 表(当前语言) → 属性(英文) → 表(英文) → 英文缺省（def 名 / 调用方给的英文）。
// 所有编辑器 UI（节点标题、参数标签、分区标题、工具栏、各弹窗、悬停 tooltip）都经由这里取文案。

using System.Linq;
using UnityEditor;

namespace NodeEditor.EditorUI
{
    // 按 NodeEditorAssetPaths 定位 EditorLocalizationConfig；缓存仅在配置路径仍一致时复用。
    public static class EditorLocalizationLocator
    {
        static EditorLocalizationConfig s_Cached;
        static bool s_Resolved;
        public static EditorLocalizationConfig Config()
        {
            if (s_Resolved) return s_Cached;
            s_Resolved = true;
            var paths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (paths == null) { s_Cached = null; return null; }
            var configuredPath = (paths.editorLocalizationConfigPath ?? string.Empty).Replace('\\', '/').Trim();
            s_Cached = ProjectAssetPaths.FindConfigured<EditorLocalizationConfig>(
                "NodeEditor", configuredPath);
            return s_Cached;
        }
        public static void Invalidate()
        {
            s_Cached = null;
            s_Resolved = false;
        }
    }

    public static class Localizer
    {
        public static Language Lang => EditorLocalizationLocator.Config()?.language ?? Language.English;
        static LocalizationTable Table => EditorLocalizationLocator.Config()?.table;

        // ---- 节点 ----
        public static string NodeName(NodeDefinition def) =>
            ResolveNode(def, a => a.Name, "name", def != null ? (def.DisplayName ?? def.name) : "");
        public static string NodeDesc(NodeDefinition def) =>
            ResolveNode(def, a => a.Description, "desc", "");

        // ---- 参数 ----
        public static string ParamName(NodeDefinition def, string param) =>
            ResolveParam(def, param, a => a.Name, "name", param);
        public static string ParamDesc(NodeDefinition def, string param) =>
            ResolveParam(def, param, a => a.Description, "desc", "");

        // ---- 黑板变量：变量面板里每个变量的简短注释（作 tooltip）。仅查表 var.<key>.desc（当前语言 → 英文回退），无属性来源 ----
        public static string VariableDesc(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string id = "var." + key + ".desc";
            var v = Table?.Get(id, Lang);
            if (!string.IsNullOrEmpty(v)) return v;
            if (Lang != Language.English)
            {
                var e = Table?.Get(id, Language.English);
                if (!string.IsNullOrEmpty(e)) return e;
            }
            return "";
        }

        // ---- 编辑器界面 chrome 文案：english 是调用方内联的英文缺省 ----
        public static string UI(string key, string english)
        {
            var v = Table?.Get(key, Lang);
            if (!string.IsNullOrEmpty(v)) return v;
            if (Lang != Language.English)
            {
                var e = Table?.Get(key, Language.English);
                if (!string.IsNullOrEmpty(e)) return e;
            }
            return english;
        }

        // ---- 解析实现 ----
        static string ResolveNode(NodeDefinition def, System.Func<NodeDocAttribute, string> pick, string suffix, string englishDefault)
        {
            if (def == null) return englishDefault;
            string id = def.Id;
            // 当前语言：属性 → 表
            string v = NodeAttr(def, Lang, pick) ?? Table?.Get($"node.{id}.{suffix}", Lang);
            if (!string.IsNullOrEmpty(v)) return v;
            // 英文回退：属性 → 表 → 缺省
            if (Lang != Language.English)
            {
                v = NodeAttr(def, Language.English, pick) ?? Table?.Get($"node.{id}.{suffix}", Language.English);
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return englishDefault;
        }

        static string ResolveParam(NodeDefinition def, string param, System.Func<ParamDocAttribute, string> pick, string suffix, string englishDefault)
        {
            if (def == null) return englishDefault;
            string id = def.Id;
            string v = ParamAttr(def, param, Lang, pick) ?? Table?.Get($"param.{id}.{param}.{suffix}", Lang);
            if (!string.IsNullOrEmpty(v)) return v;
            if (Lang != Language.English)
            {
                v = ParamAttr(def, param, Language.English, pick) ?? Table?.Get($"param.{id}.{param}.{suffix}", Language.English);
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return englishDefault;
        }

        static string NodeAttr(NodeDefinition def, Language lang, System.Func<NodeDocAttribute, string> pick)
        {
            var a = def.GetType().GetCustomAttributes(typeof(NodeDocAttribute), false)
                       .Cast<NodeDocAttribute>().FirstOrDefault(x => x.Language == lang);
            return a != null ? pick(a) : null;
        }

        static string ParamAttr(NodeDefinition def, string param, Language lang, System.Func<ParamDocAttribute, string> pick)
        {
            var a = def.GetType().GetCustomAttributes(typeof(ParamDocAttribute), false)
                       .Cast<ParamDocAttribute>().FirstOrDefault(x => x.Language == lang && x.Param == param);
            return a != null ? pick(a) : null;
        }
    }
}
