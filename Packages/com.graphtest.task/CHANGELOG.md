# Changelog

## [0.0.1] - 2026-07-15

首个正式版。基于 com.graphtest.nodeeditor 的任务/目标依赖编辑器领域模块（Runtime + Editor）。

### 内容
- 外层任务线（DependencyDag：Task/Gate 依赖解锁）+ 内层步骤图（ControlFlow：Start/Objective/Condition/Action/WaitEvent/Jump/Label/Complete/Fail），双创建配方。
- 运行时：`TaskRunner`（DAG 前置 + 目标进度 + 事件等待 + 步骤图遍历；兼 host——有意不设 TaskPlayer，见 VISION）+ `TaskJournal` + 快照。
- 领域校验（`TaskValidation`）与连接矩阵（经框架 `ConnectionRuleMatrix`）。
- 模块入口 `Tools/NodeGraph/Task` + Manager 卡片。
- 文档随包：README / EXTENDING / INTEGRATION / VISION。
