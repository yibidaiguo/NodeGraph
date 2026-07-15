# 模块视野（VISION）—— 状态机编辑器（领域实例）

> **先读层。** 开发本模块前，先读 [项目全景 `NodeEditor/VISION.md`](../com.graphtest.nodeeditor/VISION.md) → 本文 → [`ARCHITECTURE.md` 开发规范](../com.graphtest.nodeeditor/ARCHITECTURE.md#-开发规范动手前必读--全工程唯一权威) → [`EXTENDING.md`](EXTENDING.md)。
>
> 本文**只给「本模块的视野与对外关系」**，硬规则一律指向开发规范，不复述。**维护纪律（C17，硬性）**：动了本模块的**边界 / 对外接口 / 依赖 / 设计意图**，就在同一次改动里更新本文（必要时同步更新项目全景 VISION 的 §1/§3）。

---

## 0. 一句话

在节点编辑器框架（`NodeEditor/`）之上实现的**分层状态机（HSM）领域层**——声明六种状态机节点、逐帧解释运行、经事件/黑板与游戏系统零耦合协作，**复用框架的画布/检视/校验/黑板/本地化/数据窗口全部机制**（照 Dialogue 的形状接入，第三个领域实例）。

---

## 1. 职责与边界

**做**：状态机节点定义（Entry/State/Transition/AnyState/SubMachine/Exit，共 6 种，[Runtime/Nodes/](Runtime/Nodes/StateMachineNodes.cs)）；逐帧运行器（`IRuntimeGraph` 实现，[Runtime/Runner/StateMachineRunner.cs](Runtime/Runner/StateMachineRunner.cs)）+ 场景宿主（[StateMachinePlayer.cs](Runtime/Runner/StateMachinePlayer.cs)）；快照存档（[StateMachineSnapshot.cs](Runtime/Runner/StateMachineSnapshot.cs)）；领域校验 + 连接矩阵（[Editor/Validation/](Editor/Validation/StateMachineValidation.cs)）；领域动作单元（[FireMachineEventAction](Runtime/Units/FireMachineEventAction.cs)）；模块入口（`Tools/NodeGraph/State Machine`）+ 资产生成器（[Editor/Setup/StateMachineSetup.cs](Editor/Setup/StateMachineSetup.cs)）。

**不做**（属框架，不要在本模块重造）：画布/GraphView 适配、检视面板、变量面板、调试器、本地化引擎、数据编辑窗口、单元体系机制、连接规则机制、模块过滤机制——**全部复用框架**。

---

## 2. 依赖（依赖什么 / 绝不依赖什么）

- **依赖**：`StateMachine.Runtime → NodeEditor.Runtime`；`StateMachine.Editor → NodeEditor.Editor + NodeEditor.Runtime + StateMachine.Runtime`（单向）。产品程序集不引用测试、验收或演示程序集。
- **绝不依赖**：① 任何**别的领域模块**（Dialogue/Task/未来行为树——只经事件/黑板/契约协作）；② `StateMachine.Runtime` **绝不引用任何 Editor**（保玩家构建；调试器经静态事件 + [Editor/Support/StateMachineRuntimeBridge.cs](Editor/Support/StateMachineRuntimeBridge.cs) 桥接）；③ 不绕开框架缝直接碰 GraphView（B6）。
- **接入框架走的缝**（与项目全景 §3 一致）：`NodeDefinition` 子类、`IRuntimeGraph`、`GraphValidator.RegisterExtension`、`ConnectionRules`、`DataSourceRegistry`、`GraphListPane.RegisterModuleInitializer/RegisterModuleAssetFolders` + `NodeGraphAsset.module`、`UnitRegistry`（领域级）、分层 `BlackboardAsset`、`Localizer`、`StateMachineAssetPaths`（领域路径 SO，[Runtime/Data/StateMachineAssetPaths.cs](Runtime/Data/StateMachineAssetPaths.cs)）。

---

## 3. 对外提供（别的模块 / 集成者怎么用本模块）

| 提供 | 给谁 | 怎么用 | 详见 |
|---|---|---|---|
| 状态机运行时产物（`IRuntimeGraph` 实例 + `StateMachinePlayer` 组件 + 事件） | 动画/相机/音频/任务/存档 | **先订阅事件再启动**；`onStateEntered/onStateExited/onMachineEvent/onStopped` 既可 UnityEvent 拖接也可代码订阅 | [`INTEGRATION.md`](INTEGRATION.md) |
| 黑板读写（`Runner.Blackboard`） | 输入/感知等外部系统 | 经黑板注入变量、转移条件读它决策 | INTEGRATION §2 |
| 快照存档（`Capture()`/`Restore()`） | 存档系统 | statePath 经 instanceId 跨会话稳定；Restore 只重建栈+黑板，不跑生命周期不发事件 | INTEGRATION §5 |
| 编辑器入口 `OpenModule("statemachine", …)` | 工具/菜单 | 程序化开窗 / 双击 / drill-in；**仅编辑器期可用** | INTEGRATION §8 |

---

## 4. 与其他模块的关系

- **与框架（NodeEditor）**：纯消费者 + 策略填充方。框架升级缝、本模块跟随；本模块**不得**反向要求框架认识「状态机」语义。本模块**复用框架 shell**（`OpenModule("statemachine", …)` 进同一 `NodeEditorWindow`，即「模块模式」），是 C12 强默认「每模块单独 EditorWindow」的**有意例外**——HSM 的 SubMachine 引用子图，作者要在父图/子图间频繁 drill-in 来回（与对话嵌套同理由），故并进同一外壳复用其导航历史/面包屑。
- **与 Dialogue / Task**：平级、隔离，互不引用。协作只经**运行时契约 / 黑板 / 事件**（如对话结束抛事件→状态机黑板置位）。
- **与未来行为树（BT）领域**：见 §5「与行为树结合的三种方式」——今天就有两条路可用，第三条为将来的 TickTree 领域预留。
- **与声明式栈**：无关（两栈隔离）。

---

## 5. 设计意图（为什么这样建 / 有意取舍）

- **选 control-flow（wire-graph）家族**：状态机是设计师画出的「驻留 + 转移」执行结构——节点是驻留点、边是控制权移交，与对话同族；但它是**强运行时变体**（逐帧 tick、活动路径持续存在），而非对话那种「走一步停一步」的推进器。
- **Transition 是显式节点，不是边上的属性**（照 options rule：边不带逻辑）：条件、优先级都要有编辑界面和校验落点，挂在边上就成了「点边弹属性面板」的隐形数据。State→Transition→State 三段式让一条转移可被多源共享（样例里 待机/移动 共用「→ 跳跃」）、可被单独选中检视、可被连接矩阵约束（[Editor/Validation/StateMachineConnectionRules.cs](Editor/Validation/StateMachineConnectionRules.cs)）。
- **生命周期 = 可组合 Unit 槽**（红线#13）：State/SubMachine 的 onEnter/onUpdate/onExit 与 Transition 的 condition 一律是 `AddUnitParam` 槽——绝不烘成 key/op/value 参数。条件/动作从「全局通用（[../com.graphtest.nodeeditor/Runtime/Units/Units.cs](../com.graphtest.nodeeditor/Runtime/Units/Units.cs)）+ 状态机领域」两级注册表下拉选择、可层层装饰（And/Or、Sequence…）。
- **HSM = SubMachine 节点引用子图**：分层不是新图类型，而是「节点持一个 `NodeGraphAsset` 引用」（照 SubDialogue 成例）——子图仍是普通状态机图，可单独编辑/校验/复用；运行时压栈解释（[StateMachineRunner.cs](Runtime/Runner/StateMachineRunner.cs) 的 Layer 栈），外层转移覆盖内层（HSM 标准语义），子图到 Exit 回父层。
- **调试高亮表达当前活动路径，不是状态访问史**：State/SubMachine 进入为 Running，退出立即为 None；Entry/Transition/Exit 才可保留已通过的结构历史。runner 同时通过通用 `IRuntimeGraphSource` 声明 root + 当前活动子图，使编辑器即使晚开/重开也能按资产追上，框架无需硬编码 StateMachine。
- **与行为树结合的三种方式**（有意分层，各取所长）：
  1. **HSM（本期一等实现）**——SubMachine 嵌套已覆盖「分层决策」的大部分诉求：宏观状态切换 + 微观子机编排。
  2. **FSM ⊃ BT：ControlUnit 轻量编排，今天就可用**——框架的控制族单元（Selector/Sequence/Parallel/Inverter，见 [Units.cs](../com.graphtest.nodeeditor/Runtime/Units/Units.cs)）就是内联的迷你行为树；把它装进状态的 onUpdate 槽，即得「状态内每 tick 跑一小棵 BT」。将来若落地完整的 **TickTree 行为树领域**（独立模块、严格树校验框架已备），它与状态机**互不引用**，照 §4 铁律经事件/黑板协作（状态机置黑板→行为树读；行为树抛事件→状态机转移条件消费）。
  3. **并行多机 + 分层黑板**——一个角色挂多个 `StateMachinePlayer`（移动机/武器机/表情机…），共享「全局→模块→组」分层黑板声明作为协作契约，各机每实例黑板独立运行、互不锁步。
- **协作 = 事件 + 黑板，零耦合**：状态机核心不认识动画/相机/音频——图里只发事件名（[FireMachineEventAction](Runtime/Units/FireMachineEventAction.cs)）、只写黑板；外部系统订阅事件并注入黑板。
- **快照不重放生命周期**：`Restore` 只重建 HSM 栈与黑板值——存档恢复不得重触发 onEnter 副作用/事件（否则读档 = 重复发奖励）。

---

## 6. 内部结构速览（详见 [`EXTENDING.md`](EXTENDING.md)）

| 子模块 | 路径 | 一句话 |
|---|---|---|
| 节点定义 | [Runtime/Nodes/](Runtime/Nodes/StateMachineNodes.cs) | 六种节点的 `NodeDefinition` 子类（一类一文件）+ Kind 枚举/基类 |
| 单元 | [Runtime/Units/](Runtime/Units/FireMachineEventAction.cs) | 状态机领域 `Unit`（领域级注册） |
| 运行器 | [Runtime/Runner/](Runtime/Runner/StateMachineRunner.cs) | `IRuntimeGraph` 解释器 + `StateMachinePlayer` 宿主 + 黑板/上下文/快照 |
| 数据 | [Runtime/Data/StateMachineAssetPaths.cs](Runtime/Data/StateMachineAssetPaths.cs) | 项目级路径 SO；所有生成目录由用户配置，`Assets/StateMachineContent/` 仅是首次默认示例 |
| 装配 | [Editor/Setup/StateMachineSetup.cs](Editor/Setup/StateMachineSetup.cs) | 产品生成器（节点定义/注册表/黑板/本地化，幂等，不生成样例） |
| 校验 | [Editor/Validation/](Editor/Validation/StateMachineValidation.cs) | `StateMachineValidation` + `StateMachineConnectionRules`（注册到框架缝） |
| 入口 | [Editor/Launcher/](Editor/Launcher/StateMachineEditorLauncher.cs) | `OpenModule` 菜单 + 新图播种（钉住 Entry） |
| 数据源 | [Editor/Data/StateMachineDataSources.cs](Editor/Data/StateMachineDataSources.cs) | 模块变量 / 节点定义（只读）接进通用数据窗口 |
| 调试桥 | [Editor/Support/StateMachineRuntimeBridge.cs](Editor/Support/StateMachineRuntimeBridge.cs) | play 模式 runner → 调试器（Runtime 零 Editor 依赖） |
| 演示 | 不随产品发布 | 本地验证可使用独立演示包；产品不包含也不依赖 |

---

## 7. 已知偏差 / 待收敛（_审计填_）

> _由「全项目规范化审计」流程填写：对照 §2 依赖、§3/§4 对外关系、开发规范 A/B/C/D，列出本模块当前实现的漂移点。当前无已登记项。_
