# 任务编辑器

## 首次安装路径向导 / First-install Path Wizard

其他用户首次安装 Task 时，Unity 会自动打开 Task 路径设置。任务节点定义、任务线、步骤图和黑板目录都可以先修改；点击 **保存并生成 / Save & Generate** 后才会保存 `TaskAssetPaths` 并生成对应资产。选择 **稍后 / Later** 或关闭不会写入工程，下次重启 Unity 会再次提示。

Package consumers automatically receive Task path setup on first install. Node definitions, task graphs, step graphs, and blackboard directories can all be changed before **Save & Generate** persists `TaskAssetPaths` and generates assets. **Later** or closing writes nothing and prompts again after Unity restarts.

从 `Tools/NodeGraph/Task` 打开任务编辑器。首次使用或需要刷新生成资产时运行
`Tools/NodeGraph/Manager` 的 Task **Setup Assets**。

任务模块复用 `Packages/com.graphtest.nodeeditor/` 的共享外壳。外层任务线使用
`DependencyDag`：任务节点通过 `unlocks -> prerequisite` 表达解锁关系，
`Gate` 节点用 `All` 或 `Any` 合并多个前置分支。可执行任务可以引用内层
`ControlFlow` 步骤图，步骤图负责目标、条件、动作、事件等待、完成和失败。
任务节点的“步骤图”字段只列出 `module="task"` 且类型为 `ControlFlow` 的图；
已有的不合法旧引用会原样显示但不会进入候选，只有明确选择 `(None)` 才会清空。

窗口提供两个创建按钮：`新建任务线`（`task.line`）在 `TaskAssetPaths.taskGraphsDir`
创建 `DependencyDag`；`新建步骤图`（`task.steps`）在 `TaskAssetPaths.stepGraphsDir`
创建 `ControlFlow`，并自动加入一个钉住、不可删除且登记为入口的 Start。两种图都在
`blackboardLayersDir` 配齐模块/图黑板。首次运行会在项目自己的
`Assets/NodeEditorSettings/TaskAssetPaths.asset` 播种可编辑默认值（例如
`Assets/TaskContent/Tasks`）；这些只是示例，用户可以在 `Tools/NodeGraph/Manager` 的 Task **Open Asset Paths**
把每个生成目录改到任意规范化的 `Assets/` 子目录，现有资产不会被自动搬动。

## 安装

独立可安装、编译、卸载的 Task Runtime/Editor 模块，只依赖 `com.graphtest.nodeeditor`，不依赖 Dialogue 或 State Machine。通过 `Tools/NodeGraph/Manager` 安装本模块，再运行 Setup 生成资产（见上）。生成的任务图、步骤图、黑板、本地化与 `TaskAssetPaths` 属于项目，默认写入 `Assets/TaskContent`。产品包不含场景、示例数据或示例脚本；需要演示时在 Manager 里单独安装 `com.graphtest.task.samples` 并显式导入 **Task Basics**。

## 创建第一条任务线

先用“新建任务线”创建外层依赖图，再按需要用“新建步骤图”创建内层流程，并从任务节点的过滤下拉中引用它。用两个任务连接到 `Gate(All)` 可表达“全部前置完成后解锁”；用 `Gate(Any)` 表达任一前置满足。运行时通过自己的 gameplay 系统调用 `ReportObjective`、`EmitEvent` 或写入黑板推进步骤。产品生成内容只写入 `TaskAssetPaths` 当前配置的目录，代码安装位置不决定资源位置，也不要求随包演示场景。

## 运行时

运行时入口是 `TaskRunner`。外部系统创建 runner 后先订阅事件，再调用
`StartTask(taskId)`；任务步骤通过 `ReportObjective`、`EmitEvent` 和黑板值推进。
`TaskRunnerSnapshot` 用稳定 id 保存活动任务、当前步骤、任务日志、目标进度和黑板值。

每个 runner 都有显式、幂等的 `IDisposable` 生命周期；拥有者结束使用时必须调用 `Dispose()`。
Runtime 只发布 runner 创建/销毁事件，`TaskRuntimeBridge` 是唯一把这些事件适配到
`RuntimeGraphRegistry` 的 Editor 接缝，因此 Runtime 程序集不引用 `UnityEditor`。

## 规则

任务模块遵守 `Packages/com.graphtest.nodeeditor/ARCHITECTURE.md`。编辑器写入必须可撤销并标脏，
运行时代码禁止引用 `UnityEditor`，新增可见文案需要走本地化表，具体
`ScriptableObject` 类型保持一类一文件。视觉与交互以 `Packages/com.graphtest.nodeeditor/UI-STANDARD.md`
为准；当前实现优先于历史设计稿。
