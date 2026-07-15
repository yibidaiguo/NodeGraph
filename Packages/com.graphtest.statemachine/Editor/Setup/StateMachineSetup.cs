// StateMachineSetup.cs — Editor 生成器，将状态机节点词汇接入 NodeEditor 框架的 asset 定位器
//（NodeDefinitionLocator/NodeRegistryLocator/BlackboardLocator 都通过 AssetDatabase.FindAssets 解析 asset，
// 因此状态机节点类型需要真实的 .asset 实例，框架的编辑器（搜索对话框、检视面板、调试器）才能找到它们）。
// 幂等：在新增节点或编辑 Define() 后可安全重跑。
// 搭产品创作基础：节点定义 / 注册表 projectDomain / 模块黑板 / 状态机领域本地化种子。
// 框架核心资产（本地化表/语言选项/双配置/全局黑板）与框架种子由 FrameworkSetup.EnsureCoreAssets 负责
//（纯 StateMachine 安装同样自足）；定义/注册表管线（含坏资产失败关闭守卫）在框架 DomainSetupPipeline。
// 路径全部读 StateMachineAssetPaths / NodeEditorAssetPaths 两个 SO（准则#14，零硬编码）。仅 Editor/ 程序集。

using UnityEditor;
using UnityEngine;
using NodeEditor;
using NodeEditor.EditorUI;   // NodeEditorAssetPathsLocator + BlackboardLocator + FrameworkSetup + DomainSetupPipeline

namespace StateMachine.EditorUI
{
    public static class StateMachineSetup
    {
        const string Module = "statemachine";

        public static void Run()
        {
            var smPaths = StateMachineAssetPathsLocator.FindOrCreate();
            var nodePaths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (smPaths == null || nodePaths == null) return;
            if (!ProjectAssetPaths.ValidateWritablePaths("StateMachineSetup",
                    smPaths.nodeDefinitionsDir, smPaths.blackboardLayersDir, smPaths.machineGroupsDir,
                    nodePaths.registryPath)) return;

            // 框架核心资产 + 框架种子（幂等；失败关闭即中止，先于任何领域写入）。
            var table = FrameworkSetup.EnsureCoreAssets("StateMachineSetup");
            if (table == null) return;

            var blackboardDir = ProjectAssetPaths.NormalizeAssetPath(smPaths.blackboardLayersDir);
            ProjectAssetPaths.EnsureFolder(blackboardDir);
            ProjectAssetPaths.EnsureFolder(ProjectAssetPaths.NormalizeAssetPath(smPaths.machineGroupsDir));

            var (defs, defsCreated) = DomainSetupPipeline.SetupDefinitions<StateMachineNodeDefinition>(
                "State Machine", smPaths.nodeDefinitionsDir);
            var registryCreated = DomainSetupPipeline.MergeIntoRegistry<StateMachineNodeDefinition>(
                "State Machine", nodePaths.registryPath, defs);
            var blackboardCreated = SetupModuleBlackboard(blackboardDir);
            SetupLocalization(table);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"StateMachineSetup: definitions {defsCreated} created / {defs.Count - defsCreated} refreshed; " +
                      $"registry {(registryCreated ? "created" : "updated")} ({defs.Count} statemachine projectDomain entries); " +
                      $"module blackboard {(blackboardCreated ? "created" : "found")} (module=\"{Module}\"); localization seeded.");
        }

        // ---- 模块黑板：find-or-create「statemachine」模块档（准则#15 分层黑板），落在本域的黑板目录里。
        // 空黑板——项目自行声明它需要的模块级变量。----
        static bool SetupModuleBlackboard(string blackboardDir)
        {
            if (BlackboardLocator.FindLayer(Module, "") != null) return false;
            BlackboardLocator.CreateLayer(Module, "", blackboardDir);
            return true;
        }

        // ---- 状态机领域本地化种子（add-if-missing，不覆盖作者改过的文案）。
        // 只种本域 key：领域 chrome / cue / 数据源标题 / 校验 val.sm.* / kind.*；
        // 框架通用 key（含连接规则格式键 val.conn* 与共享 ui.cue.unset）由 FrameworkSetup.SeedFrameworkUI 统一播种。----
        static void SetupLocalization(LocalizationTable table)
        {
            Undo.RegisterCompleteObjectUndo(table, "Seed State Machine Localization");

            // 编辑器 chrome：窗口标题（StateMachineEditorLauncher）+ 左侧图列表的模块分组名（module.<key>）。
            // 键名跟框架 UpdateWindowTitle 的约定走：ui.{module}Editor（module 全小写）——否则标题回退英文。
            EnsureUI(table, "ui.statemachineEditor", "状态机编辑器");
            EnsureUI(table, "module.statemachine", "状态机");

            // 画布节点线索 cue（StateMachineNodeViews）：符号前缀 →/?/↳/[p..] 语言中立不入表。
            EnsureUI(table, "ui.sm.cue.entry", "初始状态");
            EnsureUI(table, "ui.sm.cue.anystate", "任意状态转移源");
            EnsureUI(table, "ui.sm.cue.exit", "返回父层 / 停机");
            EnsureUI(table, "ui.sm.cue.onEnter", "进 ");
            EnsureUI(table, "ui.sm.cue.onUpdate", "更 ");
            EnsureUI(table, "ui.sm.cue.onExit", "出 ");
            EnsureUI(table, "ui.sm.cue.noLifecycle", "（无生命周期动作）");
            EnsureUI(table, "ui.sm.cue.always", "（恒真）");

            // 数据窗口（StateMachineDataSources）的两个领域源标题。
            EnsureUI(table, "ui.sm.moduleVariables", "状态机模块变量");
            EnsureUI(table, "ui.sm.nodeDefs", "状态机节点定义（只读）");

            // ---- 校验 / 诊断消息（StateMachineValidation，val.sm.*）：节点红黄框 + 画布横幅的可见文案（C11）。----
            EnsureUI(table, "val.sm.transWiring.noIn", "转移（Transition）没有任何入边——至少需要一条来自 状态/任意状态/子状态机");
            EnsureUI(table, "val.sm.transWiring.badSource", "转移的入边只能来自 状态/任意状态/子状态机（当前来自「{0}」）");
            EnsureUI(table, "val.sm.transWiring.outCount", "转移必须恰好有一条出边指向目标（当前 {0} 条）");
            EnsureUI(table, "val.sm.transWiring.badTarget", "转移只能指向 状态/子状态机/出口（当前指向「{0}」）");
            EnsureUI(table, "val.sm.anystate.noincoming", "任意状态（Any State）不得有入边");
            EnsureUI(table, "val.sm.submachine.noGraph", "子状态机未设置子图引用");
            EnsureUI(table, "val.sm.submachine.wrongModule", "子图「{0}」不是状态机模块的图（module 必须为 statemachine）");
            EnsureUI(table, "val.sm.submachine.noEntry", "子图「{0}」不含入口（Entry）节点");
            EnsureUI(table, "val.sm.submachine.cycle", "子机链成环：子图「{0}」沿子机链回到了祖先图（会无限递归）");
            EnsureUI(table, "val.sm.state.deadend", "状态没有任何出向转移——将永远停留在此状态（若为终态设计可忽略）");
            EnsureUI(table, "val.sm.transition.nocondition", "该转移永远不会触发：源节点「{0}」有一条恒真（空条件）转移排在它之前");
            EnsureUI(table, "val.sm.definitionUnavailable", "该节点不属于状态机领域，不能放进状态机图");

            // 样例包 Manager 动作名（样例导入后出现在 Manager 卡片；样例包无 Setup，由本域代种）。
            EnsureUI(table, "ui.moduleAction.create-sample-3d", "生成样例场景（3D）");
            EnsureUI(table, "ui.moduleAction.create-sample-2d", "生成样例场景（2D）");

            // 连接规则的节点种类名（kind.<Kind>，拒绝消息里点名集合用；通用格式键 val.conn* 由框架种）。
            EnsureUI(table, "kind.Entry", "入口");
            EnsureUI(table, "kind.State", "状态");
            EnsureUI(table, "kind.Transition", "转移");
            EnsureUI(table, "kind.AnyState", "任意状态");
            EnsureUI(table, "kind.SubMachine", "子状态机");
            EnsureUI(table, "kind.Exit", "出口");

            EditorUtility.SetDirty(table);
        }

        static void EnsureUI(LocalizationTable t, string key, string zh) => FrameworkSetup.EnsureUI(t, key, zh);
    }
}
