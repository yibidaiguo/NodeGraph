// DialogueSetup.cs — Editor 生成器，将对话节点词汇接入 NodeEditor
// 框架基于 asset 的定位器（NodeDefinitionLocator/NodeRegistryLocator/BlackboardLocator 都
// 服从项目路径配置或已配置的注册表，因此对话节点类型需要真实的 .asset 实例，
// 框架的 editor（搜索对话框、inspector、debugger）才能找到它们）。幂等：在新增节点或编辑 Define() 后可安全重跑。
// 只搭产品创作基础：节点定义 / 注册表 / 领域本地化种子。
// 框架核心资产（本地化表/语言选项/双配置/全局黑板）与框架种子由 FrameworkSetup.EnsureCoreAssets 负责
//（C11 所有权：框架 key 框架种、对话 key 本文件种）；定义/注册表管线在框架 DomainSetupPipeline。
// 可运行演示属于可选 samples 包，不是产品 Setup 的依赖。仅 Editor/ 程序集 —— 本文件无运行时依赖。

using UnityEditor;
using UnityEngine;
using NodeEditor;
using NodeEditor.EditorUI;   // NodeEditorAssetPathsLocator + FrameworkSetup + DomainSetupPipeline

namespace Dialogue.EditorUI
{
    public static class DialogueSetup
    {
        public static void Run()
        {
            // 产品 Setup 不创建或依赖可选 samples 包中的演示内容。
            var dialoguePaths = DialogueAssetPathsLocator.FindOrCreate();
            var nodePaths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (dialoguePaths == null || nodePaths == null) return;
            if (!ProjectAssetPaths.ValidateWritablePaths("DialogueSetup",
                    dialoguePaths.nodeDefinitionsDir, dialoguePaths.dialogueGroupsDir,
                    dialoguePaths.blackboardLayersDir, nodePaths.registryPath)) return;

            // 框架核心资产 + 框架种子（幂等；失败关闭即中止，先于任何领域写入）。
            var table = FrameworkSetup.EnsureCoreAssets("DialogueSetup");
            if (table == null) return;
            if (!ProjectAssetPaths.PrepareWritableDirectories("DialogueSetup",
                    dialoguePaths.nodeDefinitionsDir, dialoguePaths.dialogueGroupsDir,
                    dialoguePaths.blackboardLayersDir)) return;

            var (defs, defsCreated) = DomainSetupPipeline.SetupDefinitions<DialogueNodeDefinition>(
                "Dialogue", dialoguePaths.nodeDefinitionsDir);
            var registryCreated = DomainSetupPipeline.MergeIntoRegistry<DialogueNodeDefinition>(
                "Dialogue", nodePaths.registryPath, defs);
            SetupLocalization(table);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"DialogueSetup: framework only — definitions {defsCreated} created / {defs.Count - defsCreated} already present; " +
                      $"registry {(registryCreated ? "created" : "updated")} ({defs.Count} projectDomain entries); " +
                      "framework core assets ensured (global blackboard stays empty; the project declares its own vars). " +
                      "Optional samples are not generated or referenced here.");
        }

        // ---- 对话领域本地化种子（add-if-missing，不覆盖作者改过的文案）。
        // 只种对话 key：领域 chrome / 数据库编辑器 / 对话校验 val.* / kind.* / unit.dialogue.*；
        // 框架通用 key（ui.*/val.*/unit.* 共享部分）由 FrameworkSetup.SeedFrameworkUI 统一播种。----
        public static void SetupLocalization(LocalizationTable table)
        {
            Undo.RegisterCompleteObjectUndo(table, "Seed Dialogue Localization");
            // 领域 chrome：模块窗口标题 / 图列表的对话措辞（ui.*.dialogue 模块覆盖 key）/ 模块分组名。
            EnsureUI(table, "ui.dialogueEditor", "对话编辑器");
            EnsureUI(table, "ui.graphs.dialogue", "对话组");
            EnsureUI(table, "ui.noGraphs.dialogue", "项目中暂无对话组");
            EnsureUI(table, "ui.newDialogueGraph", "新建对话");
            EnsureUI(table, "ui.newGraphPrompt.dialogue", "创建一个新的对话组");
            EnsureUI(table, "module.dialogue", "对话");
            EnsureUI(table, "ui.dialogueNoGraph", "未找到对话图。请先在 Tools/NodeGraph/Manager 中运行 Dialogue 的 Setup Assets。");
            // 黑板变量的简短注释（变量面板 tooltip，var.<key>.desc）——刻意极简，中英各一条。
            EnsureVarDesc(table, "playerName", "玩家名", "Player name");
            EnsureVarDesc(table, "metHero", "见过英雄", "Met the hero");
            EnsureVarDesc(table, "trust", "信任度", "Trust level");
            EnsureVarDesc(table, "trustedHero", "信任英雄", "Trusts the hero");
            // 数据窗口的对话领域源标题 + 只读目录。
            EnsureUI(table, "ui.dialogueData", "对话数据库");
            EnsureUI(table, "ui.dialogueNodeDefs", "对话节点定义（只读）");   // 对话 projectDomain 档（领域级）
            EnsureUI(table, "ui.dialogueUnits", "对话单元（只读）");
            EnsureUI(table, "unit.dialogue.fireEvent.name", "触发事件", "Fire Event");
            // 数据库解析 / 创作数据库选择。
            EnsureUI(table, "ui.noDatabase", "未找到对话数据库。请先在 Tools/NodeGraph/Manager 中运行 Dialogue 的 Setup Assets。");
            EnsureUI(table, "ui.noDatabaseCreate", "项目中没有对话数据库。请通过 Create > Dialogue > Database 创建。",
                "No Dialogue database exists. Create one with Create > Dialogue > Database.");
            EnsureUI(table, "ui.multipleDatabasesSelect", "项目中有多个对话数据库。请在 DialogueAssetPaths 中选择 authoringDatabase。",
                "Multiple Dialogue databases exist. Select authoringDatabase in DialogueAssetPaths.");
            EnsureUI(table, "ui.authoringDatabase", "创作数据库", "Authoring Database");
            EnsureUI(table, "ui.assetPathsUnavailable", "对话资源路径配置不可用。", "Dialogue Asset Paths configuration is unavailable.");
            EnsureUI(table, "ui.noRegistry", "未找到节点注册表。请先在 Tools/NodeGraph/Manager 中运行 Dialogue 的 Setup Assets。");
            // 画布节点线索（cue）文案（F4）：英文走 DialogueNodeViews 调用处内联兜底；符号前缀 ?/→/#/↳ 语言中立不入表。
            EnsureUI(table, "ui.cue.line", "台词：");
            EnsureUI(table, "ui.cue.optionsOne", "1 个选项");
            EnsureUI(table, "ui.cue.options", "{0} 个选项");
            EnsureUI(table, "ui.cue.if", "若 ");
            // 连接规则的节点种类名（kind.<Kind>，拒绝消息里点名集合用；通用格式键 val.conn* 由框架种）。
            EnsureUI(table, "kind.Option", "选项");
            EnsureUI(table, "kind.Choice", "选择");
            // 数据库编辑器（条目卡片 / 明细）的中文文案。
            EnsureUI(table, "ui.dialogueDatabase", "对话数据库");
            EnsureUI(table, "ui.defaultLanguage", "默认语言");
            EnsureUI(table, "ui.entries", "条目");
            EnsureUI(table, "ui.addEntry", "+ 条目");
            EnsureUI(table, "ui.addLanguage", "+ 语言");
            EnsureUI(table, "ui.duplicateKeys", "重复键：运行时会使用第一条匹配项。");
            EnsureUI(table, "ui.speaker", "说话人");
            EnsureUI(table, "ui.portrait", "头像");
            EnsureUI(table, "ui.voice", "语音");
            EnsureUI(table, "ui.localizedText", "本地化文本");
            EnsureUI(table, "ui.remove", "移除");
            // 条目用途（台词/选项/通用）下拉。
            EnsureUI(table, "ui.entryKind", "用途");
            EnsureUI(table, "ui.entryKindLine", "台词");
            EnsureUI(table, "ui.entryKindOption", "选项");
            EnsureUI(table, "ui.entryKindAny", "通用");
            // ---- 对话结构校验消息（DialogueValidation，val.*）：节点红黄框 + 画布横幅（C11）。----
            EnsureUI(table, "val.noStart", "对话图缺少进入（Start）节点");
            EnsureUI(table, "val.oneStart", "每张对话图只允许一个进入（Start）节点");
            EnsureUI(table, "val.dupLabel", "重复的标签名「{0}」——跳转只会取第一个匹配");
            EnsureUI(table, "val.jumpNoTarget", "跳转（Jump）未设置目标标签");
            EnsureUI(table, "val.jumpNoMatch", "跳转目标标签「{0}」找不到对应的标签（Label）节点");
            EnsureUI(table, "val.paramUndefinedKey", "参数「{0}」引用了未声明的黑板变量「{1}」");
            EnsureUI(table, "val.unitUndefinedKey", "可组合单元引用了未声明的黑板变量「{0}」");
            EnsureUI(table, "val.dialogue.definitionUnavailable", "此节点不可用于对话图");
            EditorUtility.SetDirty(table);
        }

        static void EnsureUI(LocalizationTable t, string key, string zh) => FrameworkSetup.EnsureUI(t, key, zh);
        static void EnsureUI(LocalizationTable t, string key, string zh, string en) => FrameworkSetup.EnsureUI(t, key, zh, en);
        static void EnsureVarDesc(LocalizationTable t, string key, string zh, string en) => FrameworkSetup.EnsureVarDesc(t, key, zh, en);
    }
}
