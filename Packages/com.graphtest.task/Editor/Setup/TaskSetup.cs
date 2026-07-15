// TaskSetup.cs — Editor 生成器，将任务节点词汇接入 NodeEditor 框架（幂等，可安全重跑）。
// 搭产品创作基础：节点定义 / 注册表 projectDomain / 模块黑板 / 任务领域本地化种子。
// 框架核心资产（本地化表/语言选项/双配置/全局黑板）与框架种子由 FrameworkSetup.EnsureCoreAssets 负责
//（修复：纯 Task 安装同样自足，不再依赖其它模块先跑 Setup）；定义/注册表管线在框架 DomainSetupPipeline。
// 路径全部读 TaskAssetPaths / NodeEditorAssetPaths 两个 SO（准则#14，零硬编码）。仅 Editor/ 程序集。

using UnityEditor;
using UnityEngine;
using NodeEditor;
using NodeEditor.EditorUI;
using TaskEditor;

namespace TaskEditor.EditorUI
{
    public static class TaskSetup
    {
        public static void Run()
        {
            var taskPaths = TaskAssetPathsLocator.FindOrCreate();
            var nodePaths = NodeEditorAssetPathsLocator.FindOrCreate();
            if (taskPaths == null || nodePaths == null) return;
            if (!ProjectAssetPaths.ValidateWritablePaths("TaskSetup",
                    taskPaths.nodeDefinitionsDir, taskPaths.taskGraphsDir,
                    taskPaths.stepGraphsDir, taskPaths.blackboardLayersDir,
                    nodePaths.registryPath)) return;

            // 框架核心资产 + 框架种子（幂等；失败关闭即中止，先于任何领域写入）。
            var table = FrameworkSetup.EnsureCoreAssets("TaskSetup");
            if (table == null) return;

            var blackboardDir = ProjectAssetPaths.NormalizeAssetPath(taskPaths.blackboardLayersDir);
            ProjectAssetPaths.EnsureFolder(ProjectAssetPaths.NormalizeAssetPath(taskPaths.taskGraphsDir));
            ProjectAssetPaths.EnsureFolder(ProjectAssetPaths.NormalizeAssetPath(taskPaths.stepGraphsDir));
            ProjectAssetPaths.EnsureFolder(blackboardDir);

            var (defs, defsCreated) = DomainSetupPipeline.SetupDefinitions<TaskNodeDefinition>(
                "Task", taskPaths.nodeDefinitionsDir);
            var registryCreated = DomainSetupPipeline.MergeIntoRegistry<TaskNodeDefinition>(
                "Task", nodePaths.registryPath, defs);
            SetupBlackboardLayer(TaskGraphScaffold.Module, "", blackboardDir);
            SeedLocalization(table);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"TaskSetup: definitions {defsCreated} created / {defs.Count - defsCreated} already present; " +
                      $"registry {(registryCreated ? "created" : "updated")} with task entries.");
        }

        static BlackboardAsset SetupBlackboardLayer(string module, string group, string blackboardDir)
        {
            var existing = BlackboardLocator.FindLayer(module, group);
            if (existing != null) return existing;
            return BlackboardLocator.CreateLayer(module, group, blackboardDir);
        }

        // ---- 任务领域本地化种子（add-if-missing）。只种任务 key：领域 chrome / cue / 校验 val.task.* / kind.*；
        // 框架通用 key（含连接规则格式键 val.conn*）由 FrameworkSetup.SeedFrameworkUI 统一播种。----
        static void SeedLocalization(LocalizationTable table)
        {
            Undo.RegisterCompleteObjectUndo(table, "Seed Task Localization");
            EnsureUI(table, "ui.taskEditor", "任务编辑器");
            EnsureUI(table, "ui.graphs.task", "任务");
            EnsureUI(table, "ui.noGraphs.task", "项目中暂无任务图");
            EnsureUI(table, "ui.newGraphPrompt.task", "创建一个新的任务图");
            EnsureUI(table, "ui.newTaskLine", "新建任务线");
            EnsureUI(table, "ui.newTaskSteps", "新建步骤图");
            EnsureUI(table, "module.task", "任务");
            EnsureUI(table, "ui.taskModuleVariables", "任务模块变量");
            EnsureUI(table, "ui.taskNodeDefs", "任务节点定义（只读）");
            EnsureUI(table, "ui.taskCue.unset", "（未设置）");
            EnsureUI(table, "ui.taskCue.task", "任务：");
            EnsureUI(table, "ui.taskCue.gate", "门控：");
            EnsureUI(table, "ui.taskCue.objective", "目标：");
            EnsureUI(table, "ui.taskCue.if", "条件：");
            EnsureUI(table, "ui.taskCue.action", "动作：");
            EnsureUI(table, "ui.taskCue.wait", "等待：");
            EnsureUI(table, "ui.taskCue.payload", "载荷：");
            EnsureUI(table, "ui.taskCue.jump", "跳转：");
            EnsureUI(table, "ui.taskCue.label", "标签：");
            EnsureUI(table, "val.task.selfEdge", "任务依赖边不能指回同一个节点");
            EnsureUI(table, "val.task.taskIdMissing", "任务节点未设置 taskId");
            EnsureUI(table, "val.task.taskIdDuplicate", "重复的任务 ID「{0}」");
            EnsureUI(table, "val.task.stepGraphWrongType", "stepGraph 必须引用 NodeGraphAsset");
            EnsureUI(table, "val.task.stepGraphWrongModule", "stepGraph 必须属于任务模块");
            EnsureUI(table, "val.task.stepGraphWrongGraphType", "stepGraph 必须是控制流图");
            EnsureUI(table, "val.task.noStart", "任务步骤图缺少开始（Start）节点");
            EnsureUI(table, "val.task.oneStart", "每张任务步骤图只允许一个开始（Start）节点");
            EnsureUI(table, "val.task.unreachableStep", "任务步骤节点无法从 Start 到达");
            EnsureUI(table, "val.task.labelMissing", "标签节点未设置 labelName");
            EnsureUI(table, "val.task.dupLabel", "重复的步骤标签「{0}」");
            EnsureUI(table, "val.task.jumpNoTarget", "跳转（Jump）未设置目标标签");
            EnsureUI(table, "val.task.jumpNoMatch", "跳转目标标签「{0}」找不到对应的标签（Label）节点");
            EnsureUI(table, "val.task.noTerminal", "从该步骤出发没有路径可到达 Complete 或 Fail");
            EnsureUI(table, "val.task.paramUndefinedKey", "参数「{0}」引用了未声明的黑板变量「{1}」");
            EnsureUI(table, "val.task.unitUndefinedKey", "可组合单元引用了未声明的黑板变量「{0}」");
            EnsureUI(table, "val.task.definitionUnavailable", "此节点不可用于当前任务图");
            EnsureUI(table, "kind.Task", "任务");
            EnsureUI(table, "kind.Gate", "门控");
            EnsureUI(table, "kind.Start", "开始");
            EnsureUI(table, "kind.Objective", "目标");
            EnsureUI(table, "kind.Condition", "条件");
            EnsureUI(table, "kind.Action", "动作");
            EnsureUI(table, "kind.WaitEvent", "等待事件");
            EnsureUI(table, "kind.Jump", "跳转");
            EnsureUI(table, "kind.Label", "标签");
            EnsureUI(table, "kind.Complete", "完成");
            EnsureUI(table, "kind.Fail", "失败");
            EditorUtility.SetDirty(table);
        }

        static void EnsureUI(LocalizationTable t, string key, string zh) => FrameworkSetup.EnsureUI(t, key, zh);
    }
}
