# Changelog

## [0.0.1] - 2026-07-15

首个正式版。可复用节点编辑器框架：创作期数据/校验/运行时契约（Runtime）+ GraphView 编辑器外壳（Editor）+ NodeGraph Module Manager 模块化安装。

### 内容
- 数据模型：`NodeDefinition`（代码声明端口/参数/角色）+ `NodeInstance`/`Connection`、`NodeGraphAsset`、双档 `NodeRegistry`、分层 `BlackboardAsset`（全局/模块/组，就近覆盖）、`TypeRef`/`Arity` 类型词汇。
- 运行时契约：`IRuntimeGraph`/`IRuntimeGraphSource` + `RuntimeGraphRegistry` 桥、可组合 `Unit` 四族（`[SerializeReference]` 内联 + 装饰器）、每实例 `RuntimeBlackboard`。
- 编辑器：多面板 `NodeEditorWindow`（图列表/分层变量/画布/检视/调试器/面包屑）、通用数据窗口（项目/领域/单图三作用域）、校验引擎（按 `GraphType` 分派 + `RegisterExtension` 扩展点）、连接规则缝（`ConnectionRules` + 泛型 `ConnectionRuleMatrix<TDef,TKind>` 矩阵引擎）、`DomainSetupPipeline`（领域 Setup 管线：一类型一资产、坏资产失败关闭）、`FrameworkSetup`（框架核心资产与本地化种子自足入口）。
- UI 标准：单一样式表 `NodeEditorStyles.uss` 双主题（奶咖灰调/暖炭铜金）+ 共享控件（`EditorUi`/`NodeCueControl`/搜索下拉/主从列表），契约测试锁定。
- 本地化横切：`Localizer`（属性→表→英文回退）+ `LocalizationTable`/双配置 SO；框架 key 由框架种子。
- Module Manager：`Tools/NodeGraph/Manager` 目录驱动安装/移除/样例导入，动作名渲染期本地化。
- 文档随包：`ARCHITECTURE.md`（架构 + 唯一权威开发规范）/ `UI-STANDARD.md` / `VISION.md`（项目全景先读层）。
