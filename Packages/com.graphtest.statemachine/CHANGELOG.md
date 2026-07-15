# 更新日志 / Changelog

## [0.0.3] - 2026-07-16

### 中文

- 修复任务编辑器的添加节点菜单会显示并创建对话/状态机节点的问题。
- 各模块图现在仅允许本模块节点和通用节点。
- 保留任务依赖图与流程图各自的节点种类限制。

### English

- Fixed the Task editor add-node menu exposing Dialogue and State Machine nodes.
- Each module graph now permits only its own nodes plus universal nodes.
- Preserved the Task dependency-DAG and control-flow node-kind restrictions.

## [0.0.2] - 2026-07-15

### 中文

- 新增面向包使用者的首次安装路径向导，框架、Dialogue、Task、State Machine 依次配置。
- 确认前不写入配置或生成资产；支持稍后处理与生成失败重试。
- Dialogue 现在会验证并创建节点定义、对话组和黑板的全部配置目录。
- 已有路径配置的项目不会被自动修改。

### English

- Added a first-install path wizard for package consumers, configuring Framework, Dialogue, Task, and State Machine in order.
- No configuration or generated asset is written before confirmation; deferral and generation retry are supported.
- Dialogue now validates and creates every configured node-definition, dialogue-group, and blackboard directory.
- Projects with existing path configurations are never changed automatically.

## [0.0.1] - 2026-07-15

### 中文

- 首个公开版本，提供基于 NodeGraph 的分层状态机运行时与编辑器。
- 包含入口、状态、转换、任意状态、子状态机和出口节点。
- 提供逐帧运行、子状态机栈、事件、快照、领域校验和连接规则。
- 可通过 `Tools/NodeGraph/State Machine` 或模块管理器完成初始化。

### English

- First public release of the NodeGraph-based hierarchical state machine runtime and editor.
- Includes Entry, State, Transition, Any State, Sub-Machine, and Exit nodes.
- Provides frame-based execution, nested state-machine stacks, events, snapshots, domain validation, and connection rules.
- Supports setup through `Tools/NodeGraph/State Machine` or the module manager.
