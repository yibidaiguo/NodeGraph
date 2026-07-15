# 任务接入指南

> **UPM 边界（1.0）**：Task 产品源码位于 `Packages/com.graphtest.task/`，通过 GraphTest Module Manager 安装；项目可变数据仍位于可配置的 `Assets/TaskContent`。下文 `Runtime/`、`Editor/` 路径均相对于 Task package。

本文面向在游戏系统中消费任务运行时的集成者。扩展编辑器请看
`EXTENDING.md`，框架规则请看 `Packages/com.graphtest.nodeeditor/ARCHITECTURE.md`。

## 运行时装配

创建 `TaskRunner` 时传入：

- `NodeRegistry`：包含任务节点定义。
- `NodeGraphAsset`：外层任务 `DependencyDag`。
- `BlackboardSet`：全局、任务模块、任务组黑板的合并声明。

先订阅 runner 事件，再调用 `StartTask(taskId)`。如果任务没有步骤图，
`StartTask` 会立即完成该任务；如果任务引用 `ControlFlow` 步骤图，runner 会在
`Objective`、`WaitEvent` 等节点处停驻，等待外部系统回驱。

`TaskRunner` 实现显式、幂等的 `IDisposable`；创建者结束使用时必须调用 `Dispose()`。
Runtime 只发布 runner 创建/销毁事件，Editor-only 的 `TaskRuntimeBridge` 是唯一把这些事件
转接到 `RuntimeGraphRegistry.Register/Unregister` 的接缝，Task Runtime 不引用 `UnityEditor`。

## 外部系统回驱

- 目标进度：调用 `ReportObjective(objectiveId, amount)`。
- 游戏事件：调用 `EmitEvent(eventId, payload)`，可选 payload 会写入节点声明的黑板 key。
- 状态读写：通过 `runner.Blackboard.Get/Set` 访问每实例黑板值。
- 任务可用性：用 `IsAvailable(taskId)` 查询前置分支是否满足。

事件包括 `OnTaskStarted`、`OnObjectiveUpdated`、`OnTaskCompleted`、
`OnTaskFailed` 和 `OnCustomEvent`。表现层或 HUD 只订阅这些事件，不直接读取编辑器 UI。

## 存档

`TaskRunnerSnapshot` 保存活动任务 id、当前步骤 instance id、完成/失败任务集合、
目标进度、已访问步骤和黑板值。跨会话保存时把 snapshot 序列化到项目自己的存档格式；
恢复时重新用同一任务图和节点 registry 创建 runner，再调用 `Restore(snapshot)`。

## 最小接入验证

创建两条前置任务并接入 `Gate(All)`，确认两者完成后后续任务才解锁；再让其中一条引用 `ControlFlow` 步骤图并等待一次 gameplay 目标上报。这样可同时验证依赖图、步骤图、目标事件与存档恢复，不需要任何随包演示资产。

## 编辑器调用

编辑器期使用 `Tools/NodeGraph/Task` 打开任务模块窗口。程序化开窗只能在编辑器
程序集里调用 `NodeEditor.EditorUI.NodeEditorWindow.OpenModule`；运行时玩家 UI 应由
runner 事件驱动，不应打开编辑器窗口。
