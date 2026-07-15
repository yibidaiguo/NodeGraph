# 状态机编辑器 · 集成/接入指南（面向外部模块开发者）

> **UPM 边界（1.0）**：StateMachine 产品源码位于 `Packages/com.graphtest.statemachine/`，通过 GraphTest Module Manager 安装；项目可变数据仍位于可配置的 `Assets/StateMachineContent`。下文 `Runtime/`、`Editor/` 路径均相对于 StateMachine package。
>
> 给**别的游戏模块**（动画 / 相机 / 音频 / 任务 / 存档 / AI…）的程序员：怎么在运行时**驱动**一台状态机、怎么**和你的模块合作**、以及怎么从别的工具**调用编辑器 UI**。你只是**用**这个编辑器的产物，不改它、也不画图。
>
> **四份文档分工，按你的角色选**：
> | 你想做的事 | 看哪份 |
> |---|---|
> | **在别的模块里用状态机**（订阅事件、注入黑板、存档、开窗）→ 本文 | `StateMachine/INTEGRATION.md`（本文） |
> | 给编辑器**加功能**（新节点/单元/校验/数据源） | [`EXTENDING.md`](EXTENDING.md) |
> | **搭状态机图**（不写代码） | [`README.md`](README.md) |
> | 维护**框架本身** / 在框架上搭新领域编辑器 | [`../com.graphtest.nodeeditor/ARCHITECTURE.md`](../com.graphtest.nodeeditor/ARCHITECTURE.md) |
>
> 规则以 [`ARCHITECTURE.md` 的「开发规范」节](../com.graphtest.nodeeditor/ARCHITECTURE.md) 为唯一权威。运行时类型在 `Packages/com.graphtest.statemachine/Runtime/`，可进玩家构建；编辑器类型在 `Packages/*/Editor/`，仅编辑器期可用。

---

## 1. 运行时接入：在你的场景里跑一台状态机

运行时入口是 `StateMachinePlayer`（[Runtime/Runner/StateMachinePlayer.cs](Runtime/Runner/StateMachinePlayer.cs)，`MonoBehaviour`，Runtime 程序集、不引 `UnityEditor`）。它很薄——构建一个 `StateMachineRunner` 并逐帧 tick；所有状态机逻辑在 [Runtime/Runner/StateMachineRunner.cs](Runtime/Runner/StateMachineRunner.cs)。

**第一步：场景里挂 `StateMachinePlayer`，装配字段**（Inspector 或代码）：

| 字段 | 类型 | 作用 |
|---|---|---|
| `graph` | `NodeGraphAsset` | 要运行的状态机图（`module="statemachine"`，须含 Entry 节点） |
| `registry` | `NodeRegistry` | 把 `definitionId` 解析为节点定义；使用 `NodeEditorAssetPaths.registryPath` 当前指向的共享资产（子机图共用同一个） |
| `blackboards` | `BlackboardAsset[]` | 按**全局→模块→组**排列的分层黑板引用，同名 key 就近覆盖；运行器构造走 `new BlackboardSet(blackboards)`。编辑期取各档资产用 `BlackboardLocator`（[../com.graphtest.nodeeditor/Editor/Support/BlackboardLocator.cs](../com.graphtest.nodeeditor/Editor/Support/BlackboardLocator.cs)） |
| `updateMode` | 枚举 | `Update`（默认）/ `FixedUpdate`（刚体/2D 物理驱动的机器选它）/ `Manual`（不自动 tick，见 §6） |
| `playOnStart` | `bool` | 勾选 = `Start()` 时自动 `Play()`。**要在代码里订阅 Runner 初始进入事件的场合请关掉、自己调 `Play()`**（见下） |

**第二步：先订阅、后启动。** `Play()` 里 `Runner.Start()` 会**同步**跑完初始 Enter 链并抛出初始 `OnStateEntered`——`Play()` 之后再订阅 Runner 的 C# 事件就抓不到初始进入了。两条路：

- **UnityEvent 字段（推荐给场景接线）**：`onStateEntered` / `onStateExited` / `onMachineEvent` / `onStopped` 是序列化的 `UnityEvent`，在检视面板拖接或 `AddListener` 后由 Player 转发——**它们在 `Play()` 前就已挂好**，初始事件不会漏。
- **代码订阅 Runner（要拿 C# 事件）**：关掉 `playOnStart`，自己按顺序装配：

```csharp
using StateMachine;

var player = GetComponent<StateMachinePlayer>();
player.playOnStart = false;                    // 或直接在 Inspector 里关
player.onStateEntered.AddListener(id => animAdapter.OnEnter(id));   // UnityEvent：Play 前挂好
player.Play();                                 // 构建 Runner + 同步初始 Enter 链
player.Runner.OnMachineEvent += name => audio.Handle(name);         // C# 事件：之后的事件都能收到
```

**第三步（可选）：状态 id → 显示名。** 事件参数是节点的 `instanceId`（跨会话稳定）；要显示名可在图数据里查 `displayName`，整条活动路径用 `Runner.DisplayPath`。

> **重启语义**：运行中再调 `Play()`（或 `Runner.Start()`）= **先干净停机再重启**——旧 Runner 自内向外跑完 onExit 并**先抛一次 `OnStopped`**，然后才构建新 Runner 进初始态。你的 `onStopped` 订阅方要能区分「真结束」与「重启前的收尾」（最简单：重启入口自己置个标志位）。

---

## 2. 黑板：注入 / 读出状态机变量

`player.Runner.Blackboard`（[Runtime/Runner/StateMachineBlackboard.cs](Runtime/Runner/StateMachineBlackboard.cs)）是**每次运行一份**的变量实例，`Play()` 时按各档 `BlackboardAsset` 声明的默认值播种；未 `Play`/已 `Stop` 时 `Runner` 为 null——写入方要判空。

外部系统把状态喂给转移条件的标准姿势 = **每帧写黑板**（系统不认识状态机内部，只写声明过的 key）：

```csharp
var bb = player.Runner?.Blackboard;
if (bb != null)
{
    bb.SetF("playerDistance", dist);   // 感知 → 黑板；条件单元下一 tick 即可读到
    bb.Set("stunned", true);           // Bool/任意值用 Set；数值快路径用 SetF/GetF
}
```

输入与感知组件通过 `Runner.Blackboard.Set(...)` 注入数据；反向由状态动作配置黑板、游戏组件读取黑板。键名一律使用黑板里**声明过的**变量（没声明的 key 校验会黄框告警）。

---

## 3. 事件 → 动画 / 相机 / 音频 / 任务（最常用的协作点）

图里的动作单元「触发状态机事件」（[Runtime/Units/FireMachineEventAction.cs](Runtime/Units/FireMachineEventAction.cs)，装在状态的 onEnter/onUpdate/onExit 槽里）发一个事件名，经 `Runner.OnMachineEvent` → `player.onMachineEvent` 流到场景；进/出状态本身另有 `onStateEntered`/`onStateExited`。状态机**不认识**你的系统，只发事件——两种订阅方式：

- **UnityEvent 无代码接线**：在检视面板把 `onMachineEvent` 拖到你组件的 public 方法上，例如按事件名切换相机参数或触发 Animator。
- **代码订阅**：`player.onMachineEvent.AddListener(...)` 或 `Runner.OnMachineEvent +=`，按事件名命名空间分发（`"sfx.xxx"` / `"camera.xxx"` / `"quest.xxx"`…），设计师在图里填事件名即可对接、无需改代码。

---

## 4. 与行为树（BT）结合的三种方式

1. **HSM（本模块一等能力）**：SubMachine 节点嵌套子图（[Runtime/Nodes/SubMachineNode.cs](Runtime/Nodes/SubMachineNode.cs)），宏观切换 + 微观子机编排，多数「分层决策」诉求到此为止。
2. **FSM ⊃ BT，今天就可用**：把框架控制族单元（Selector/Sequence/Parallel/Inverter，[../com.graphtest.nodeeditor/Runtime/Units/Units.cs](../com.graphtest.nodeeditor/Runtime/Units/Units.cs)）装进状态的 `onUpdate` 槽 = 状态内每 tick 跑一小棵内联行为树。将来若落地独立的 TickTree 行为树领域，它与状态机**互不引用**，照下面第 3 条的契约协作。
3. **并行多机 / 跨模块协作 = 事件 + 黑板**：一个角色挂多个 `StateMachinePlayer`（移动机/武器机…）或「状态机 + 行为树」并行——共享**分层黑板的声明**（全局/模块档）作为契约，各自的每实例黑板独立；相互影响走「A 写黑板 → B 条件读」或「A 抛事件 → 宿主转写进 B 的黑板」。领域之间**绝不直接引用**（项目全景 §2 铁律）。

---

## 5. 存档系统：`Capture()` / `Restore()`

```csharp
StateMachineSnapshot snap = player.Runner.Capture();   // 活动栈路径 + 每个声明变量的当前值
// …序列化 snap（[Serializable]，见 Runtime/Runner/StateMachineSnapshot.cs）…
player.Play();                        // 先有 Runner（Play 会正常跑初始 Enter）
player.Runner.Restore(snap);          // 再覆盖成快照的栈 + 黑板
```

- **`Restore` 只重建 HSM 栈与黑板值，不跑 OnEnter/OnExit、不发任何事件**——存档恢复不得重触发进入动作（否则读档 = 重复发奖励/重播音效）。恢复后你的表现层要自己按 `Runner.CurrentStatePath`/`DisplayPath` 对齐显示。
- **`statePath` 跨会话稳定**：路径由各层当前节点的 `instanceId` 以 `/` 连接，`instanceId` 是存在图资产里的 GUID 串，跨会话/跨机器不变；图被改到路径解析不动时 `Restore` 告警并安全落停机态（不抛异常）。
- 跨整个应用重启持久化时，`graph` 引用本身由你的存档模块映射成稳定资产 id（快照只存路径与变量，不存图引用）。

---

## 6. Manual 模式与 `ManualTick`

`updateMode = Manual` 时 Player **不自动 tick**，由你调 `player.ManualTick(dt)` 手动步进——锁步网络、回放、测试、逐帧调试用。**仅 Manual 模式可调**：`Update`/`FixedUpdate` 模式下 Player 已经在对应回调里 tick，再手动步进 = **同帧双 tick**（转移求值两次、onUpdate 两次）。

2D/3D 刚体驱动的机器选 `FixedUpdate`（感知/电机也放 FixedUpdate，同步物理步）；普通逻辑机用默认 `Update`。

---

## 7. play 模式调试器

编辑器的图调试器只经 `IRuntimeGraph` 观察状态、经 `IRuntimeGraphSource` 匹配当前打开的 root/活动子图：当前 State/SubMachine 为 Running，收到退出后立即回到 None（变暗）；Entry/Transition/Exit 可保留路径历史。`StateMachinePlayer` 在 Runtime 侧抛 `OnRunnerCreated/OnRunnerDestroyed` 静态事件，[Editor/Support/StateMachineRuntimeBridge.cs](Editor/Support/StateMachineRuntimeBridge.cs)（`[InitializeOnLoad]`）转注册进 `RuntimeGraphRegistry`——**Runtime 程序集零 Editor 依赖**。runner 已经运行后再开/重开窗口也会按当前资产追上；你无需重启状态机。

---

## 8. UI 调用:从别的工具 / 代码打开编辑器窗口

> ⚠️ 本节说的是**编辑器窗口**（仅编辑器期）。游戏运行时玩家看到的表现走 §1–§3 的事件/黑板，**别在 gameplay 代码里调 `NodeEditorWindow`**。

- **菜单**：`Tools/NodeGraph/State Machine`（[Editor/Launcher/StateMachineEditorLauncher.cs](Editor/Launcher/StateMachineEditorLauncher.cs)）——模块模式：左侧只列状态机图。
- **程序化**：`NodeEditorWindow.OpenModule("statemachine", 标题, 图)`（框架 [../com.graphtest.nodeeditor/Editor/Window/NodeEditorWindow.cs](../com.graphtest.nodeeditor/Editor/Window/NodeEditorWindow.cs)）；双击任意 `NodeGraphAsset` 也会自动用共享窗口打开；SubMachine 检视面板可 drill-in 子图（导航历史/面包屑由框架管）。
- **生成/重生产品资产**：`Tools/NodeGraph/Manager` 的 State Machine **Setup Assets**（[Editor/Setup/StateMachineSetup.cs](Editor/Setup/StateMachineSetup.cs)，幂等）；它不会创建或依赖演示场景。

---

## 9. 速查：你的系统 → 用什么 API → 在哪调

| 你的系统 | API | 调用点 |
|---|---|---|
| 动画 / 表现层 | `onStateEntered` / `onStateExited`（instanceId） | 检视面板拖接或 `AddListener`；按图数据把 id 映射到显示名 |
| 相机 / 音频 / 任务 | `onMachineEvent`（事件名） | 订阅、按事件名命名空间分发（图里 FireMachineEventAction 发） |
| 输入 / 感知 | `Runner.Blackboard.Set/SetF/Get/GetF` | 每帧写声明过的 key；Runner 为 null（未 Play/已 Stop）时跳过 |
| 启动 / 重启 | `player.Play()` | 重复调 = 先干净停机（**会先抛一次 OnStopped**）再重启；订阅要在 Play 前挂好 |
| 停机 | `player.Stop()` | 自内向外 onExit + OnStopped；幂等 |
| 整机结束通知 | `onStopped` / `Runner.OnStopped` | 顶层到 Exit 或调用 Stop 时触发 |
| 手动步进（锁步/回放/测试） | `player.ManualTick(dt)` | **仅 `updateMode == Manual`**；其他模式会双 tick |
| 存档 | `Runner.Capture()` → `StateMachineSnapshot` / `Runner.Restore(snap)` | Restore 不跑生命周期不发事件；statePath 经 instanceId 稳定 |
| 当前状态查询 | `Runner.CurrentStatePath` / `DisplayPath` / `IsRunning` | HUD、断言或存档锚点 |
| 多机并行 | 多个 `StateMachinePlayer` + 共享分层黑板声明 | 协作走事件/黑板，领域间不引用（§4） |
| 编辑期开窗 | `NodeEditorWindow.OpenModule("statemachine", …)` | **仅编辑器工具**，别在 gameplay 调 |

接好后，**进 play 真跑一遍**（订阅事件 → Play → 触发几次转移/事件 → Capture/Restore → 重启看 OnStopped 次序）确认行为，再算接入完成。
