# 模块视野（VISION）—— 对话编辑器（领域实例）

> **先读层。** 开发本模块前，先读 [项目全景 `NodeEditor/VISION.md`](../com.graphtest.nodeeditor/VISION.md) → 本文 → [`ARCHITECTURE.md` 开发规范](../com.graphtest.nodeeditor/ARCHITECTURE.md#-开发规范动手前必读--全工程唯一权威) → [`EXTENDING.md`](EXTENDING.md)。
>
> 本文**只给「本模块的视野与对外关系」**，硬规则一律指向开发规范，不复述。**维护纪律（C17，硬性）**：动了本模块的**边界 / 对外接口 / 依赖 / 设计意图**，就在同一次改动里更新本文（必要时同步更新项目全景 VISION 的 §1/§3）。

---

## 0. 一句话

在节点编辑器框架（`NodeEditor/`）之上实现的**对话编辑器领域层**——它只声明对话节点、跑对话语义、配对话数据，**复用框架的画布/检视/校验/黑板/本地化/数据窗口全部机制**，是「框架出机制、领域填策略」的活范本。

---

## 1. 职责与边界

**做**：对话节点定义（Start/Line/Choice/Option/Condition/Action/Jump/Label/SubDialogue/End，共 10 种）；对话运行器（`IRuntimeGraph` 实现）；对话数据库（按语言存多语台词）；领域校验 + 连接规则（如 `Choice.options` 只接 `Option`）；对话单元（领域 `Unit`）；模块入口（`Tools/NodeGraph/Dialogue`）+ 资产生成器。

**不做**（属框架，不要在本模块重造）：画布/GraphView 适配、检视面板、变量面板、调试器、本地化引擎、数据编辑窗口、复制粘贴/撤销、单元体系机制、连接规则机制、模块过滤机制——这些**全部复用框架**。

---

## 2. 依赖（依赖什么 / 绝不依赖什么）

- **依赖**：`Dialogue.Runtime → NodeEditor.Runtime`；`Dialogue.Editor → NodeEditor.Editor + NodeEditor.Runtime + Dialogue.Runtime`（单向）。
- **绝不依赖**：① 任何**别的领域模块**（将来有任务/状态机模块也不直接引用——只经事件/黑板/契约协作）；② `Dialogue.Runtime` **绝不引用任何 Editor**（保玩家构建）；③ 不绕开框架缝去直接碰 GraphView（B6）。
- **接入框架走的缝**（与项目全景 §3 一致）：`NodeDefinition` 子类、`IRuntimeGraph`、`GraphValidator.RegisterExtension`、`ConnectionRules`、`DataSourceRegistry`、`GraphCreationRegistry` + `NodeGraphAsset.module`、`ParamChoiceProviders`、`UnitRegistry`（领域级）、分层 `BlackboardAsset`、`Localizer`、`DialogueAssetPaths`（领域路径 SO）。对话只注册 `dialogue.graph` 一种 `ControlFlow` 创建配方，落在对话图目录并播种钉住的 Start/End。

---

## 3. 对外提供（别的模块 / 集成者怎么用本模块）

| 提供 | 给谁 | 怎么用 | 详见 |
|---|---|---|---|
| 对话运行时产物（`IRuntimeGraph` 实例 + 播放器组件 + 事件） | 别的游戏系统（任务/存档/HUD/音频） | **先订阅事件再启动**；事件节点 → 外部系统（解耦、不阻塞）；存档把图引用映射成稳定 GUID | [`INTEGRATION.md`](INTEGRATION.md) |
| 黑板读写 | 外部系统 | 经黑板注入/读出状态，不直接调内部 | INTEGRATION |
| 编辑器入口 `OpenModule("dialogue", …)` | 工具/菜单 | 程序化开窗 / 双击 / drill-in；**开窗 API 仅编辑器期可用，运行时玩家 UI 走事件渲染** | INTEGRATION |

---

## 4. 与其他模块的关系

- **与框架（NodeEditor）**：纯消费者 + 策略填充方。框架升级缝、本模块跟随；本模块**不得**反向要求框架认识「对话」语义。本模块当前**复用框架 shell**（`DialogueEditorLauncher.OpenModule("dialogue", …)` 进同一 `NodeEditorWindow`，即「模块模式」），是 C12 强默认「每模块单独 EditorWindow」的**有意例外**——对话数据可嵌套、设计师需在多张图间频繁来回，故并进同一外壳。
- **与未来领域模块**：平级、隔离。协作只经**运行时契约 / 黑板 / 事件**（见项目全景 §2 铁律）。新领域应**照本模块的形状**接入，而非依赖本模块。
- **与声明式栈**：无关（两栈隔离）。

---

## 5. 设计意图（为什么这样建 / 有意取舍）

- 选 **control-flow（wire-graph）** 家族：对话是设计师画出的执行结构。
- 节点**不烘门控参数**，条件/取值/副作用一律走**可组合 Unit 槽**（修过的真实偏差，红线#13；见 EXTENDING 配方）。
- 事件副作用不立独立节点：走 **Action 节点 + `FireEventAction` 单元**（与「副作用一律走 Unit 槽」一致；旧 Event 节点已折叠）。
- 连接合法性按**节点种类**约束（`Choice→Option`），做成第一类规则而非靠约定（准则#10）。
- 多语台词存**对话数据库**（按 lang），与 UI 本地化表分离。

---

## 6. 内部结构速览（详见 [`EXTENDING.md`](EXTENDING.md)）

| 子模块 | 路径 | 一句话 |
|---|---|---|
| 节点定义 | `Editor/Nodes`、`Runtime/Nodes` | 各对话节点的 `NodeDefinition` 子类（一类一文件）+ 运行时行为 |
| 单元 | `Runtime/Units` | 对话领域 `Unit`（领域级注册） |
| 运行器 | `Runtime/Runner` | `IRuntimeGraph` 实现 + 播放器 |
| 数据 | `Runtime/Data` + `DialogueAssetPaths` 当前配置的项目目录 | 对话数据库、对话图、模块/组黑板与项目路径配置；具体资源目录由用户决定 |
| 装配 | `Editor/Setup` | `DialogueSetup` 生成产品节点定义、注册表、空黑板和本地化（幂等，不生成演示） |
| 校验 | `Editor/Validation` | `DialogueValidation` + `DialogueConnectionRules`（注册到框架缝） |
| 入口 | `Editor/Launcher` | `DialogueEditorLauncher`（`OpenModule`）+ `DialogueGraphScaffold`（`dialogue.graph` 创建配方） |
| 数据源 | `Editor/Data` | `DialogueDataSources`（注册到 `DataSourceRegistry`） |
