# 更新日志 / Changelog

## [Unreleased]

### 中文

- 为对话节点注册简洁的共享语义图标，并自动继承框架的金属底座、双主题节点层次和整节点运行态照明。

### English

- Registered concise shared semantic icons for Dialogue nodes, inheriting the framework metal base, dual-theme depth, and whole-node runtime illumination.

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

- 首个公开版本，提供基于 NodeGraph 的对话图运行时与编辑器。
- 包含开始、台词、选项、条件、动作、跳转、标签、子对话和结束等节点。
- 提供 `DialogueRunner`、`DialoguePlayer`、对话数据库、领域校验和连接规则。
- 可通过 `Tools/NodeGraph/Dialogue` 或模块管理器完成初始化。

### English

- First public release of the NodeGraph-based dialogue runtime and editor.
- Includes Start, Line, Choice, Option, Condition, Action, Jump, Label, Sub-Dialogue, and End nodes.
- Provides `DialogueRunner`, `DialoguePlayer`, a dialogue database, domain validation, and connection rules.
- Supports setup through `Tools/NodeGraph/Dialogue` or the module manager.
