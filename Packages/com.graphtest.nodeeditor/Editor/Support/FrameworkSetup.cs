// FrameworkSetup.cs — 框架核心资产 + 框架本地化种子的「自足入口」（C11 所有权修正）。
// 过去框架的中文种子（通用 chrome ui.*、结构校验 val.*、连接规则 val.conn*、通用单元 unit.*）全部物化在
// DialogueSetup.SetupLocalization 里——导致「只装 Task / 只装 StateMachine」的工程拿不到框架文案与
// EditorLocalizationConfig。现在框架 key 由框架自己种：每个领域 Setup 先调 EnsureCoreAssets（幂等，
// add-if-missing，不覆盖作者改过的文案），再只种自己领域的 key；Manager 里框架卡片的 Setup Assets 也直达此处。
// 涵盖：本地化表 / LanguageOptions / EditorLocalizationConfig(挂表) / RuntimeLocalizationConfig / 全局黑板。
// 路径全部读 NodeEditorAssetPaths（SO，准则 C16 零硬编码），失败关闭（ValidateWritablePaths）。
// 仅 Editor/ 程序集。

using System.IO;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public static class FrameworkSetup
    {
        // Manager「Node Editor Framework」卡片的 Setup Assets 入口：框架自足（无需任何领域模块）。
        public static void Run()
        {
            var table = EnsureCoreAssets("NodeEditorSetup");
            if (table == null) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NodeEditorSetup: framework core assets ensured (localization table/options/configs + global blackboard) and framework seeds applied.");
        }

        // 框架核心资产 ensure + 框架种子。返回兜底表（null = 路径配置失败关闭，调用方应中止）。
        public static LocalizationTable EnsureCoreAssets(string owner)
        {
            var nodePaths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (nodePaths == null) return null;
            if (!ProjectAssetPaths.ValidateWritablePaths(owner,
                    nodePaths.globalBlackboardPath, nodePaths.localizationTablePath,
                    nodePaths.editorLocalizationConfigPath, nodePaths.runtimeLocalizationConfigPath,
                    nodePaths.languageOptionsPath)) return null;

            var blackboardPath = ProjectAssetPaths.NormalizeAssetPath(nodePaths.globalBlackboardPath);
            var tablePath = ProjectAssetPaths.NormalizeAssetPath(nodePaths.localizationTablePath);
            var editorLocPath = ProjectAssetPaths.NormalizeAssetPath(nodePaths.editorLocalizationConfigPath);
            var runtimeLocPath = ProjectAssetPaths.NormalizeAssetPath(nodePaths.runtimeLocalizationConfigPath);
            var languageOptionsPath = ProjectAssetPaths.NormalizeAssetPath(nodePaths.languageOptionsPath);
            foreach (var ap in new[] { blackboardPath, tablePath, editorLocPath, runtimeLocPath, languageOptionsPath })
                ProjectAssetPaths.EnsureFolder(Path.GetDirectoryName(ap)?.Replace('\\', '/'));

            EnsureGlobalBlackboard(blackboardPath);

            // 语言选项资产：DialogueDatabase 等运行时内容的 lang code 候选（en/zh…）。
            var languageOptions = AssetDatabase.LoadAssetAtPath<LanguageOptions>(languageOptionsPath);
            if (languageOptions == null)
            {
                languageOptions = ScriptableObject.CreateInstance<LanguageOptions>();
                AssetDatabase.CreateAsset(languageOptions, languageOptionsPath);
                Undo.RegisterCreatedObjectUndo(languageOptions, "Create Language Options");
                EditorUtility.SetDirty(languageOptions);
            }

            // 兜底表：编辑器界面 chrome 文案 + 没加属性的节点/参数。add-if-missing，不覆盖作者改过的文案。
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(tablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LocalizationTable>();
                AssetDatabase.CreateAsset(table, tablePath);
                Undo.RegisterCreatedObjectUndo(table, "Create Localization Table");
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(table, "Seed Framework Localization");
            }
            SeedFrameworkUI(table);
            EditorUtility.SetDirty(table);

            // 编辑器本地化配置：find-or-create，默认中文，挂上兜底表。
            var editorCfg = AssetDatabase.LoadAssetAtPath<EditorLocalizationConfig>(editorLocPath);
            bool editorCfgCreated = false;
            if (editorCfg == null)
            {
                editorCfg = ScriptableObject.CreateInstance<EditorLocalizationConfig>();
                AssetDatabase.CreateAsset(editorCfg, editorLocPath);
                Undo.RegisterCreatedObjectUndo(editorCfg, "Create Editor Localization Config");
                editorCfgCreated = true;
            }
            if (editorCfg.table == null)
            {
                if (!editorCfgCreated)
                    Undo.RegisterCompleteObjectUndo(editorCfg, "Configure Editor Localization");
                editorCfg.table = table;
                EditorUtility.SetDirty(editorCfg);
            }

            // 运行时本地化配置：find-or-create。
            var runtimeCfg = AssetDatabase.LoadAssetAtPath<RuntimeLocalizationConfig>(runtimeLocPath);
            if (runtimeCfg == null)
            {
                runtimeCfg = ScriptableObject.CreateInstance<RuntimeLocalizationConfig>();
                AssetDatabase.CreateAsset(runtimeCfg, runtimeLocPath);
                Undo.RegisterCreatedObjectUndo(runtimeCfg, "Create Runtime Localization Config");
                EditorUtility.SetDirty(runtimeCfg);
            }

            return table;
        }

        // 全局黑板：仅 find-or-create（框架的"全局档"）。不播种业务变量；项目自行声明。
        static void EnsureGlobalBlackboard(string blackboardPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BlackboardAsset>(blackboardPath);
            if (existing != null)
            {
                EnsureMainObjectName(existing, blackboardPath);
                return;
            }
            var blackboard = ScriptableObject.CreateInstance<BlackboardAsset>();
            blackboard.name = Path.GetFileNameWithoutExtension(blackboardPath);
            AssetDatabase.CreateAsset(blackboard, blackboardPath);
            Undo.RegisterCreatedObjectUndo(blackboard, "Create Global Blackboard");
            EditorUtility.SetDirty(blackboard);
        }

        static void EnsureMainObjectName(Object asset, string assetPath)
        {
            if (asset == null || string.IsNullOrEmpty(assetPath)) return;
            var expected = Path.GetFileNameWithoutExtension(assetPath);
            if (asset.name == expected) return;
            Undo.RegisterCompleteObjectUndo(asset, "Normalize Blackboard Name");
            asset.name = expected;
            EditorUtility.SetDirty(asset);
        }

        // ---- 共享种子助手（领域 Setup 复用，替代各自的私有副本）----
        public static void EnsureUI(LocalizationTable t, string key, string zh)
        {
            if (string.IsNullOrEmpty(t.Get(key, Language.Chinese))) t.Set(key, Language.Chinese, zh);
        }

        public static void EnsureUI(LocalizationTable t, string key, string zh, string en)
        {
            EnsureUI(t, key, zh);
            if (string.IsNullOrEmpty(t.Get(key, Language.English))) t.Set(key, Language.English, en);
        }

        // 黑板变量注释：写入 var.<key>.desc 的中/英两条（add-if-missing，不覆盖作者改过的文案）。
        public static void EnsureVarDesc(LocalizationTable t, string key, string zh, string en)
        {
            var k = "var." + key + ".desc";
            if (string.IsNullOrEmpty(t.Get(k, Language.Chinese))) t.Set(k, Language.Chinese, zh);
            if (string.IsNullOrEmpty(t.Get(k, Language.English))) t.Set(k, Language.English, en);
        }

        // ---- 框架种子：仅框架代码消费的 key（领域 key 由各领域 Setup 种，见 C11）。----
        static void SeedFrameworkUI(LocalizationTable table)
        {
            // 检视面板 / 变量面板 chrome。
            EnsureUI(table, "ui.inspector", "检视面板");
            EnsureUI(table, "ui.general", "常规");
            EnsureUI(table, "ui.parameters", "参数");
            EnsureUI(table, "ui.name", "名称");
            EnsureUI(table, "ui.note", "备注");
            EnsureUI(table, "ui.variables", "变量");
            EnsureUI(table, "ui.addVariable", "+ 变量");
            EnsureUI(table, "ui.noVariables", "暂无变量——点下方按钮添加。");
            // 图列表面板（领域措辞种 ui.*.<module> 覆盖 key，由各领域 Setup 掌管）。
            EnsureUI(table, "ui.graphs", "图列表");
            EnsureUI(table, "ui.refresh", "刷新");
            EnsureUI(table, "ui.noGraphs", "项目中暂无图");
            EnsureUI(table, "ui.newGraph", "新建");
            EnsureUI(table, "ui.newGraphPrompt", "创建一张新图");
            EnsureUI(table, "ui.graphSetupFailed", "图初始化失败。请先运行当前模块的 Setup Assets，然后重试。");
            EnsureUI(table, "ui.deleteGraph", "删除");
            EnsureUI(table, "ui.deleteGraphConfirm", "确定删除图「{0}」？可通过撤销恢复。");
            EnsureUI(table, "ui.delete", "删除");
            EnsureUI(table, "ui.cancel", "取消");
            EnsureUI(table, "module.none", "其他");
            // 工具栏 / 导航。
            EnsureUI(table, "ui.find", "查找");
            EnsureUI(table, "ui.findNode", "查找节点");
            EnsureUI(table, "ui.back", "后退");
            EnsureUI(table, "ui.forward", "前进");
            EnsureUI(table, "ui.minimap", "缩略图");
            EnsureUI(table, "ui.minimapTip", "显示/隐藏缩略图");
            EnsureUI(table, "ui.darkTheme", "深色");
            EnsureUI(table, "ui.darkThemeTip", "切换深色主题");
            EnsureUI(table, "ui.nodeIconConflict", "节点“{0}”的图标已注册为 {1}；已忽略冲突的 {2}。");
            EnsureUI(table, "ui.nodeEditor", "节点编辑器");
            EnsureUI(table, "ui.language", "语言");
            // 新建变量弹窗 / 节点搜索。
            EnsureUI(table, "ui.newVariable", "新建变量");
            EnsureUI(table, "ui.varName", "变量名");
            EnsureUI(table, "ui.type", "类型");
            EnsureUI(table, "ui.create", "创建");
            EnsureUI(table, "ui.addNode", "添加节点");
            EnsureUI(table, "ui.searchHint", "输入以筛选…");
            EnsureUI(table, "ui.noType", "（无类型）");
            EnsureUI(table, "ui.typeToMatch", "输入以匹配节点标题");
            EnsureUI(table, "ui.matchCount", "个匹配");
            EnsureUI(table, "ui.noBlackboard", "未加载黑板。请先运行所在模块的 Setup Assets（或创建一个 Blackboard）。");
            // 端口容量 tooltip（悬停端口时显示）：区分单连线 / 多连线。
            EnsureUI(table, "ui.port.single", "单连线：只能连一条（连新线会替换旧线）");
            EnsureUI(table, "ui.port.multi", "多连线：可连接多条");
            // 数据编辑窗口（通用 · 三作用域）的界面文案。
            EnsureUI(table, "ui.dataWindow", "数据");
            EnsureUI(table, "ui.dataProject", "项目");
            EnsureUI(table, "ui.dataDomain", "领域");
            EnsureUI(table, "ui.dataGraph", "单图");
            EnsureUI(table, "ui.dataGraphField", "当前图");
            EnsureUI(table, "ui.dataEmpty", "（暂无数据）");
            EnsureUI(table, "ui.dataPick", "从左侧选择要查看 / 编辑的数据。");
            EnsureUI(table, "ui.dataSinglePanel", "该数据源没有列表。");
            EnsureUI(table, "ui.localization", "本地化");
            EnsureUI(table, "ui.graphOverview", "图参数总览");
            EnsureUI(table, "ui.referencedData", "引用数据");
            EnsureUI(table, "ui.dataGraphHint", "选择或拖入上方「当前图」，查看其组变量 / 图概览。");
            // 分层黑板（作用域 = 所在档）的三档界面文案。
            EnsureUI(table, "ui.globalVariables", "全局变量");
            EnsureUI(table, "ui.moduleVariables", "模块变量");
            EnsureUI(table, "ui.groupVariables", "组变量");
            EnsureUI(table, "ui.noTierBlackboard", "该作用域尚无黑板。");
            EnsureUI(table, "ui.createBlackboard", "+ 新建该作用域黑板");
            EnsureUI(table, "ui.nodeDefs", "节点定义（只读）");
            EnsureUI(table, "ui.globalNodeDefs", "全局节点定义（只读）");      // 框架 universal 档（项目级）
            // 可组合单元只读目录 + 单元检视。
            EnsureUI(table, "ui.globalUnits", "全局单元（只读）");
            EnsureUI(table, "ui.unitFamProvider", "取值");
            EnsureUI(table, "ui.unitFamCondition", "条件");
            EnsureUI(table, "ui.unitFamAction", "动作");
            EnsureUI(table, "ui.unitFamControl", "控制");
            EnsureUI(table, "ui.unitGroup", "分组");
            EnsureUI(table, "ui.unitType", "类型");
            EnsureUI(table, "ui.unitFields", "可配置字段");
            EnsureUI(table, "ui.unitFieldBBKey", "[黑板键]");
            EnsureUI(table, "ui.unitFieldSlot", "[内嵌单元槽]");
            EnsureUI(table, "ui.unitClear", "（清空）");
            EnsureUI(table, "ui.addUnit", "+ 添加");
            // 框架通用单元与共享分组（UnitAttribute 只存稳定 key + 英文回退；框架定义并拥有这些 key）。
            EnsureUI(table, "unit.group.provider", "取值", "Provider");
            EnsureUI(table, "unit.group.providerDecorator", "取值/装饰", "Provider/Decorator");
            EnsureUI(table, "unit.group.condition", "条件", "Condition");
            EnsureUI(table, "unit.group.conditionDecorator", "条件/装饰", "Condition/Decorator");
            EnsureUI(table, "unit.group.action", "动作", "Action");
            EnsureUI(table, "unit.group.actionDecorator", "动作/装饰", "Action/Decorator");
            EnsureUI(table, "unit.group.control", "控制", "Control");
            EnsureUI(table, "unit.const.name", "常量", "Constant");
            EnsureUI(table, "unit.blackboardProvider.name", "读黑板", "Read Blackboard");
            EnsureUI(table, "unit.arithmeticProvider.name", "算术", "Arithmetic");
            EnsureUI(table, "unit.compareCondition.name", "比较（通用）", "Compare");
            EnsureUI(table, "unit.blackboardCompareCondition.name", "黑板比较", "Compare Blackboard");
            EnsureUI(table, "unit.alwaysCondition.name", "恒定", "Constant");
            EnsureUI(table, "unit.notCondition.name", "非 NOT", "Not");
            EnsureUI(table, "unit.andCondition.name", "与 AND", "And");
            EnsureUI(table, "unit.orCondition.name", "或 OR", "Or");
            EnsureUI(table, "unit.setVariableAction.name", "设置变量", "Set Variable");
            EnsureUI(table, "unit.setVariableLiteralAction.name", "设置变量（字面量）", "Set Variable (Literal)");
            EnsureUI(table, "unit.sequenceAction.name", "顺序", "Sequence");
            EnsureUI(table, "unit.conditionalAction.name", "条件执行", "Conditional");
            EnsureUI(table, "unit.conditionControl.name", "条件判定（控制）", "Condition");
            EnsureUI(table, "unit.selectorControl.name", "选择器 Selector", "Selector");
            EnsureUI(table, "unit.sequenceControl.name", "序列 Sequence", "Sequence");
            EnsureUI(table, "unit.parallelControl.name", "并行 Parallel", "Parallel");
            EnsureUI(table, "unit.inverterControl.name", "反转 Inverter", "Inverter");
            // 节点定义只读明细 / 端口摘要。
            EnsureUI(table, "ui.ports", "端口");
            EnsureUI(table, "ui.portsInline", "{0} 入 / {1} 出");
            EnsureUI(table, "ui.portsDetail", "入：{0}   出：{1}");
            EnsureUI(table, "ui.params", "参数");
            EnsureUI(table, "ui.id", "ID");
            EnsureUI(table, "ui.role", "角色");
            EnsureUI(table, "ui.default", "默认值");
            EnsureUI(table, "ui.key", "键");
            EnsureUI(table, "ui.addKey", "+ 键");
            EnsureUI(table, "ui.noLocTable", "未找到本地化表。请先运行所在模块的 Setup Assets。");
            // 画布节点线索（cue）共享兜底键（NodeCueControl.UnsetText）。
            EnsureUI(table, "ui.cue.unset", "(未设置)");
            // 新建变量弹窗的表单错误（ui.err*）。
            EnsureUI(table, "ui.errNoBlackboard", "没有黑板资产。");
            EnsureUI(table, "ui.errVarNameRequired", "请输入变量名。");
            EnsureUI(table, "ui.errPickType", "请选择类型。");
            EnsureUI(table, "ui.errVarExists", "本黑板中已存在名为「{0}」的变量。");
            // ---- 框架结构校验消息（GraphValidator，val.*）：节点红黄框 + 画布横幅（C11）。----
            EnsureUI(table, "val.definitionMissing", "无法解析节点定义");
            EnsureUI(table, "val.definitionWrongModule", "此节点属于其他图模块");
            EnsureUI(table, "val.missingDef", "缺少节点定义");
            EnsureUI(table, "val.inArity", "输入端口「{0}」连接数不符（要求 {1}，实得 {2}）");
            EnsureUI(table, "val.outArity", "输出端口「{0}」连接数不符（要求 {1}，实得 {2}）");
            EnsureUI(table, "val.childArity", "端口「{0}」的子节点连接数不符（实得 {1}）");
            EnsureUI(table, "val.portType", "端口「{0}」的类型不符合其 PortType 约束");
            EnsureUI(table, "val.unreachableRequired", "不可达节点（已声明必须可达 RequiresEntryReachable）");
            EnsureUI(table, "val.unreachable", "不可达节点（死内容）");
            EnsureUI(table, "val.undefinedKey", "引用了未声明的黑板变量「{0}」");
            EnsureUI(table, "val.noEntryHeadless", "图没有进入节点（每个节点都有入边——无头环）");
            EnsureUI(table, "val.noSingleEntry", "图没有唯一进入点：有 {0} 个根节点没有入边——把它们接到同一个进入节点之下，或指定一个进入点");
            EnsureUI(table, "val.noEntryEmpty", "图没有进入节点（entryInstanceIds 为空）");
            EnsureUI(table, "val.entryMissing", "entryInstanceIds 指向了不存在的实例");
            EnsureUI(table, "val.tickOneRoot", "行为树必须恰好有一个根（现有 {0} 个）");
            EnsureUI(table, "val.tickStrictTree", "行为树必须是严格树，但该节点有 {0} 个父节点");
            EnsureUI(table, "val.cycle", "成环：该节点的连线闭合了一个回路（此图类型要求无环）");
            EnsureUI(table, "val.edgeType", "连线类型不匹配：输出「{0}」与输入「{1}」不兼容");
            EnsureUI(table, "val.providerNoWrite", "取值节点（Provider）不得写黑板（它声明了写入）");
            EnsureUI(table, "val.conditionNoWrite", "条件节点（Condition）不得写黑板");
            EnsureUI(table, "val.conditionOneOut", "条件节点必须恰好有一个输出端口（现有 {0} 个）");
            EnsureUI(table, "val.conditionBoolOut", "条件节点的唯一输出必须是布尔（Bool）");
            EnsureUI(table, "val.controlNoWrite", "编排节点（Control）不得写黑板");
            EnsureUI(table, "val.controlNoParams", "编排节点（Control）不应携带数据参数");
            EnsureUI(table, "val.actionNoEffect", "纯逻辑动作节点没有任何黑板写入，不产生效果");
            // ---- 连接规则拒绝消息（ConnectionRuleMatrix，val.conn*）：跨域统一格式键（占位顺序与 string.Format 实参一致）。----
            EnsureUI(table, "val.connOutInclude", "'{0}.{1}' 只能连到：{2}（不能连到 '{3}'）");
            EnsureUI(table, "val.connOutExclude", "'{0}.{1}' 不能连到：{2}（当前连到了 '{3}'）");
            EnsureUI(table, "val.connInInclude", "'{0}.{1}' 只接受来自：{2}（不能来自 '{3}'）");
            EnsureUI(table, "val.connInExclude", "'{0}.{1}' 不接受来自：{2}（当前来自 '{3}'）");
            // ---- NodeGraph Manager（模块安装器）：标题/分节/状态/按钮/对话框/诊断（C11——安装器同守铁律#5）。----
            EnsureUI(table, "ui.moduleManager", "NodeGraph 管理器");
            EnsureUI(table, "ui.moduleManager.framework", "框架");
            EnsureUI(table, "ui.moduleManager.products", "模块");
            EnsureUI(table, "ui.moduleManager.nodeEditor", "节点编辑器框架");
            EnsureUI(table, "ui.moduleManager.installed", "已安装");
            EnsureUI(table, "ui.moduleManager.available", "可安装");
            EnsureUI(table, "ui.moduleManager.unavailable", "不可用");
            EnsureUI(table, "ui.moduleManager.install", "安装");
            EnsureUI(table, "ui.moduleManager.remove", "移除");
            EnsureUI(table, "ui.moduleManager.installing", "安装中");
            EnsureUI(table, "ui.moduleManager.removing", "移除中");
            EnsureUI(table, "ui.moduleManager.failed", "失败");
            EnsureUI(table, "ui.moduleManager.samplesSuffix", " 样例");
            EnsureUI(table, "ui.moduleManager.import", "导入：");
            EnsureUI(table, "ui.moduleManager.imported", "已导入：");
            EnsureUI(table, "ui.moduleManager.importFailed", "无法导入「{0}」。");
            EnsureUI(table, "ui.moduleManager.noActions", "已安装的模块尚未注册它的 NodeGraph 动作。");
            EnsureUI(table, "ui.moduleManager.frameworkLocked", "框架包承载本窗口，不能移除自身。");
            EnsureUI(table, "ui.moduleManager.actionUnavailable", "NodeGraph 动作「{0}」当前不可用。");
            EnsureUI(table, "ui.moduleManager.errBusy", "另一个 NodeGraph 包操作正在进行。");
            EnsureUI(table, "ui.moduleManager.errNoPlan", "安装/移除计划不可用。");
            EnsureUI(table, "ui.moduleManager.errNoSource", "NodeGraph 的 Git 包源不可用。");
            EnsureUI(table, "ui.moduleManager.errInstallFailed", "Unity Package Manager 安装失败。");
            EnsureUI(table, "ui.moduleManager.errRemoveFailed", "Unity Package Manager 移除失败。");
            EnsureUI(table, "ui.moduleManager.errNoCatalog", "NodeGraph 模块目录不可用。");
            EnsureUI(table, "ui.moduleManager.errUnknownPackage", "未知的 NodeGraph 包「{0}」。");
            EnsureUI(table, "ui.moduleManager.errDependents", "不能移除「{0}」：以下已安装包依赖它：{1}。");
            EnsureUI(table, "ui.moduleManager.errCatalogMissing", "框架包中找不到 GraphTestModuleCatalog.json。");
            EnsureUI(table, "ui.moduleManager.errResolveFramework", "Unity Package Manager 无法解析已安装的 NodeGraph 框架包。");
            EnsureUI(table, "ui.ok", "确定");
            // Manager 卡片动作名（NodeGraphModuleAction 渲染时按 ui.moduleAction.<id> 解析；保留英文名便于对照文档）。
            EnsureUI(table, "ui.moduleAction.openNodeEditor", "打开节点编辑器");
            EnsureUI(table, "ui.moduleAction.open", "打开编辑器");
            EnsureUI(table, "ui.moduleAction.data", "数据窗口");
            EnsureUI(table, "ui.moduleAction.setup", "生成资产（Setup Assets）");
            EnsureUI(table, "ui.moduleAction.asset-paths", "资源路径（Asset Paths）");
        }
    }
}
