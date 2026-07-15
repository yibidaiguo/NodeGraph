# 更新日志 / Changelog

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

- 首个公开版本，提供 Dialogue Basics 可运行示例。
- 包含示例对话图、黑板、数据库、场景和 IMGUI 对话界面。

### English

- First public release with a playable Dialogue Basics sample.
- Includes sample dialogue graphs, blackboards, a database, a scene, and an IMGUI dialogue interface.
