# 扩展任务编辑器

修改本模块前先读 `Packages/com.graphtest.nodeeditor/ARCHITECTURE.md`。资产安全、模块边界、
本地化、校验、Undo/SetDirty 和完成定义都以该文档为唯一规则权威；可见 UI 同时遵守
`Packages/com.graphtest.nodeeditor/UI-STANDARD.md`。当前代码和这两份现行文档优先于历史设计稿。

## 模块边界

任务代码位于 `Packages/com.graphtest.task/`。

- `Runtime/` 可引用 `NodeEditor.Runtime`，禁止引用 `UnityEditor`。
- `Editor/` 可引用 `NodeEditor.Editor`、`NodeEditor.Runtime` 和 `Task.Runtime`。
- 任务领域行为通过框架扩展点注册，不直接改 NodeEditor 框架。

现有领域接缝是 `GraphCreationRegistry` 显式创建配方、`ConnectionRules`、
`GraphValidator`、`DataSourceRegistry`、`ParamChoiceProviders` 和
`ParamReferenceEditors`。需要新的任务专属工具栏、面板或图创建策略时，先在 NodeEditor
拥有的模块注册接口上建立接缝；不要让 Task 直接修改共享窗口实现。

## 图类型与创建流程

Task 同时拥有外层 `DependencyDag` 和内层 `ControlFlow`。`TaskGraphScaffold` 通过
`GraphCreationRegistry` 注册两个显式配方：`task.line` 使用 `taskGraphsDir` 和
`SeedTaskLine` 创建任务线；`task.steps` 使用 `stepGraphsDir` 和 `SeedStepGraph` 创建步骤图。
步骤图初始化器必须先解析 `TaskStartNode` 定义，解析失败时不修改图并返回 false；成功后才设置
`module="task"` / `ControlFlow`，加入一个 `pinned=true` 的 Start，并把它的 instance id 登记进
`entryInstanceIds`。两个配方的模块/图黑板都落在 `blackboardLayersDir`。

节点是否可用于当前画布由 `TaskValidation` 静态构造函数注册的
`NodeDefinitionAvailability.Register("task", ...)` 谓词决定。该领域谓词同时拥有 `module` 与
`graphType` 语义：`DependencyDag` 只接受外层任务节点种类，`ControlFlow` 只接受步骤节点种类，
其它任务 graphType 与非 Task 定义一律拒绝。精确集合以 `TaskValidation.DagKinds` / `StepKinds`
为单一来源。

节点搜索、程序化创建、兼容端口过滤和保存后 `GraphValidator.ValidateAll` 都消费这一个谓词。
新增节点种类或图类型时只更新 Task 的集合/谓词、拒绝原因本地化和领域测试；不要在框架消费者里
复制 `module` / `graphType` 分支。

## 新增任务节点

新增具体节点定义时在 `Runtime/Nodes/` 下创建单独文件，类名必须等于文件名，
继承 `TaskNodeDefinition`，并声明 `Kind`、端口、参数和本地化属性。运行
在 `Tools/NodeGraph/Manager` 里点 Task 卡片的 **Setup Assets** 按钮后，节点定义资产会生成到
`TaskAssetPaths.nodeDefinitionsDir`，并写入 `NodeEditorAssetPaths.registryPath` 指向的共享
`NodeRegistry`。首次默认值（如 `Assets/TaskContent/Nodes/Definitions`）只是可编辑示例；
安装到 Packages 或移动代码目录都不会改变项目资源落点。

有限候选值优先用枚举、搜索下拉或 `ParamChoiceProviders`，不要让作者手填易错 key。
`TaskParamChoices` 的 `task.stepGraphs` 候选源只返回 Task `ControlFlow` 资产。
对象下拉在提交时会重新解析候选集；不再合法的路径不得修改或标脏图资产，
而显式 `(None)` 清空必须继续经过 Undo 和 `SetDirty`。

## 校验和连接规则

任务领域校验集中在 `TaskValidation`，通过 `GraphValidator` 扩展点参与
`ValidateAll`。外层任务图只对 `DependencyDag` 执行依赖规则；内层步骤图是
`ControlFlow`，允许循环，但必须能从 `Start` 到达终点。

连接矩阵集中在 `TaskConnectionRules`。拖拽过滤和校验兜底都走同一套
`ConnectionRules` 结果，新增节点或端口时同步更新矩阵和测试。

## 数据和编辑器接缝

`TaskAssetPaths` 保存任务模块拥有的全部生成资产目录，项目级唯一配置默认位于
`Assets/NodeEditorSettings/TaskAssetPaths.asset`。通过 `Tools/NodeGraph/Manager` 的 Task **Open Asset Paths** 定位它；
移动任务数据时先改配置，再运行 setup。重复配置会明确失败并列出候选，任何调用方都不得
硬编码默认目录或静默选择第一个配置。

`TaskDataSources` 通过 `DataSourceRegistry` 注册任务黑板和任务节点定义数据源。
新增数据面板时优先注册新的 data source，写值使用 `SerializedObject`、
`Undo.RegisterCompleteObjectUndo` 和 `EditorUtility.SetDirty`。

`TaskNodeViews` 和 `TaskParamChoices` 负责任务节点提示、候选项和检查器体验。
新增可见文案时同一改动里补本地化种子。

## 运行时扩展

`TaskRunner` 只消费 `NodeRegistry`、任务图和 `BlackboardSet`。外部系统通过
事件、黑板、稳定 id 和 `TaskRunnerSnapshot` 接入，不从运行时代码调用编辑器窗口。
新增运行时节点行为时优先使用可组合 `Unit` 槽，避免把条件、取值或副作用烘成
节点专属字符串参数。

`TaskRunner` 实现 `IDisposable`；每个创建者都必须在生命周期结束时调用幂等的 `Dispose()`。
Runtime 只抛 `OnRunnerCreated` / `OnRunnerDisposed` 生命周期事件，`TaskRuntimeBridge` 是唯一把它们
转接到 `RuntimeGraphRegistry` 的 Editor 适配器。不要新增第二条注册路径，也不能让 Task Runtime
引用 `NodeEditor.Editor`。

## 验收清单

- 用 Unity EditMode 与项目编译闸门确认 Runtime/Editor；集成项目的测试与演示保持在产品程序集之外
  程序集边界仍成立。
- 在真实 Task 窗口分别验证依赖图和步骤图：搜索只出现合法节点，非法边拖拽被拒绝，保存后
  `ValidateAll` 仍报告同一规则。
- 新建、删除、复制粘贴、Undo/Redo、模块锁定和双主题切换后重新打开资产，确认数据与当前
  UI 规范一致。
- 扩展 runner 调试时同时验证 Play Mode 生命周期、窗口关闭/重开以及 runner 销毁后的注销。
