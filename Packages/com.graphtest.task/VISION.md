# 任务编辑器愿景

任务模块是 `Packages/com.graphtest.nodeeditor/` 之上的领域层，用同一套框架同时表达任务依赖和
任务步骤。外层是 `DependencyDag`，负责任务线、前置关系、分支汇合和解锁；内层是
`ControlFlow`，负责一个可执行任务内部的目标、条件、动作、事件等待、完成和失败。

本文只记录任务领域意图。框架规则、数据安全、模块边界、本地化和验收定义都以
`Packages/com.graphtest.nodeeditor/ARCHITECTURE.md` 为准；编辑器视觉与交互规范以
`Packages/com.graphtest.nodeeditor/UI-STANDARD.md` 为准。当前实现和这两份现行文档优先于历史设计稿。

## 职责边界

任务模块拥有：

- 任务节点定义、任务连接矩阵和任务领域校验。
- 任务模块 setup、用户可编辑路径配置和领域资产生成。
- `TaskRunner`、`TaskJournal`、`TaskBlackboard` 和 `TaskRunnerSnapshot`。
- 任务数据源、任务节点视觉提示和任务参数候选项。

任务模块不拥有画布、检查器、图列表、本地化引擎、黑板系统、数据窗口或连接规则框架。
这些能力留在 NodeEditor 框架内，任务模块只通过扩展点消费。

当前已经使用的领域接缝包括 `GraphCreationRegistry` 显式创建配方、
`ConnectionRules`、`GraphValidator`、`DataSourceRegistry`、`ParamChoiceProviders` 和
`ParamReferenceEditors`。任务模块不得通过直接修改共享窗口、画布或检查器来获得领域能力。

## 当前产品切片

作者可创建 `DependencyDag` 任务依赖、进入被任务引用的 `ControlFlow` 步骤图、运行 `TaskRunner`，并通过稳定 id 保存/恢复。发布边界由这组能力与自动/真实窗口闸门定义，不依赖某个固定名称或固定目录的演示资产。

## 当前实现边界

- 模块窗口提供两个一等创建入口：`task.line` 在 `TaskAssetPaths.taskGraphsDir` 创建
  `DependencyDag` 任务线；`task.steps` 在 `TaskAssetPaths.stepGraphsDir` 创建 `ControlFlow`
  步骤图，并播种一个钉住且登记为入口的 Start。
- 每个 `TaskRunner` 都有显式、幂等的 `IDisposable` 生命周期，并通过 Runtime 生命周期事件发布
  创建/销毁；`TaskRuntimeBridge` 是唯一把这些事件适配到 `RuntimeGraphRegistry` 的 Editor 接缝。
- `TaskValidation` 通过 `NodeDefinitionAvailability` 的单一谓词按模块与图类型强制节点可用性；
  搜索、程序化创建、端口候选和 `ValidateAll` 共用同一结果，`DependencyDag` 与 `ControlFlow`
  只接受各自的任务节点集合。

## 设计边界

任务编辑器是 wire-graph authoring，不是 GOAP/HTN/utility solver。作者显式绘制任务
依赖和步骤流程；运行时只解释这些图，不自动规划任务顺序。

后续如果接入场景 UI 或任务 HUD，应通过 runtime 事件和黑板值协作。运行时代码不引用
编辑器窗口，编辑器只负责创作、校验和调试。

## 已知偏差 / 有意取舍

- **Task 有意不设 `TaskPlayer`**：`TaskRunner` 兼 host（`OnRunnerCreated`/`OnRunnerDisposed` 事件直接在 runner 上，经 `TaskRuntimeBridge` 转发注册）——与 Dialogue/StateMachine 的 Player/Runner 分层不同，是文档化的取舍：任务线常由任务系统本身持有生命周期，无需 MonoBehaviour 宿主。
