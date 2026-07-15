# 更新日志 / Changelog

## [Unreleased]

### 中文

- 节点轮廓新增与面板按钮一致的上沿高光、下沿阴影和精密压边层次，完整支持亮色与暗色主题。
- 新增共享 Painter2D 语义图标注册表与 24px 金属底座控件；27 个领域节点复用 19 种简洁图标，并保留按角色回退。
- 运行中、成功、失败改为整个节点填充与辉光；选中状态仅叠加独立描边，不再覆盖节点填充。

### English

- Added premium top highlights, bottom shadows, and pressed-edge depth to every node silhouette in both themes.
- Added a shared Painter2D semantic icon registry and 24px metal-base control; 27 domain nodes reuse 19 concise icons with role fallbacks.
- Runtime states now illuminate the whole node, while selection adds only an independent outline.

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

- 首个公开版本，提供可复用的节点图数据模型、运行时接口和 GraphView 编辑器框架。
- 包含分层黑板、节点注册表、连接规则、校验引擎和领域初始化管线。
- 提供 `Tools/NodeGraph/Manager`，用于安装或移除可选领域模块与示例。
- 随包提供架构、UI 标准、扩展和集成文档。

### English

- First public release with reusable graph data models, runtime contracts, and a GraphView editor framework.
- Includes layered blackboards, node registries, connection rules, validation, and domain setup pipelines.
- Provides `Tools/NodeGraph/Manager` for installing or removing optional domain modules and samples.
- Ships with architecture, UI standard, extension, and integration documentation.
