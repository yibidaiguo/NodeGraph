# 更新日志 / Changelog

## [0.1.6] - 2026-08-16

### 中文

**运行时契约新增有序执行轨迹；三个纯逻辑层程序集声明零引擎依赖。** 既有 API 一个字节未改。

- **新增可选契约 `IRuntimeGraphTrace`**（`NodeEditor` 命名空间，`NodeEditor.Runtime` 程序集）：

  ```csharp
  public readonly struct GraphVisit { public readonly string GraphId, InstanceId; }

  public interface IRuntimeGraphTrace
  {
      IReadOnlyList<GraphVisit> Trace { get; }   // 真实执行顺序；重复访问重复出现
      void ClearTrace();
  }
  ```

  `IRuntimeGraph` 仍然只有 `StatusOf` / `RuntimeNodeOf` 两个方法——新契约单独开接口，
  **只实现 `IRuntimeGraph` 的第三方执行器一个字都不用改**。

  **下游可以怎么用**：拿本套件的执行器与自己的执行器做对拍时，过去只能比「走过哪些节点」
  这个集合，顺序错了照样绿；现在 `((IRuntimeGraphTrace)runner).Trace` 给的是**有序列表**，
  循环走了几圈、哪个节点被重复进入，逐条可比。`GraphVisit` 带 `GraphId`，所以下钻子图
  （`SubDialogue` / `SubMachine` / Task 的 `stepGraph`）时也分得清是哪张图的节点。
  编辑器侧的调试着色同理——路径与顺序直接读得到，无需从快照倒推。

  三个自带执行器都实现了它。轨迹的清空时机：

  | 执行器 | 清空点 |
  | --- | --- |
  | `DialogueRunner` | `Run()` / `Restore()` |
  | `StateMachineRunner` | `Start()` / `Restore()` |
  | `TaskRunner` | `StartTask()` / `Restore()` / 任务结束 |

  调用方也可以随时 `ClearTrace()` 只观察某一段。

  一处刻意的语义：`Restore()` 之后轨迹只含恢复出来的那个当前指针，**不会**把存档里的
  visited 集合灌回轨迹——那个集合本身是无序的，灌回去等于凭空捏造一个执行顺序。

- **`Dialogue.Runtime` / `StateMachine.Runtime` / `Task.Runtime` 三个程序集加上
  `"noEngineReferences": true`。** 下游若有「纯逻辑层零 `UnityEngine` 依赖」的规矩，
  这三个程序集现在能被带同样标志的程序集直接引用。
  （Unity 6000.3.11f1 实测编译 0 错误；产出的 DLL 里已无 `UnityEngine` 引用。
  它们仍然引用 `NodeEditor.Runtime`——被引用方带引擎引用并不妨碍引用方声明零引擎依赖，
  因为 `NodeEditor.Runtime` 的公开 API 里没有任何 `UnityEngine` 类型。）

- **`NodeEditor.Runtime` 没有加这个标志，这是结论而非遗漏。** 该程序集的
  `Runtime/Graph/NodeDataTypes.cs` 与 `Runtime/Units/Units.cs` 共 66 处 `[SerializeReference]`，
  那是 `UnityEngine` 的特性。加上标志后 Unity 实测报 138 个 `CS0246`
  （66 处特性 × 两种报法，外加 2 行 `using UnityEngine;`）。
  而 `[SerializeReference]` 正是 `TypeRef` 与 `Unit` 这两棵多态树能存进 `.asset` 的唯一原因：
  去掉它，图数据里的参数类型与单元树会被 Unity 判为不可序列化而整段丢弃。
  「加了标志但数据存不住」比「没加标志」糟得多，所以这一条到此为止。
  真要让核心层也零引擎依赖，正确做法是把这两个文件的序列化载体下沉到 `NodeEditor.Unity`
  程序集——那是一次数据模型改造，不是一个 asmdef 开关。

- **编辑器调试器的节点着色开始读这条轨迹。** 新增状态 class `status-visited`：
  快照说「现在不在这个节点」、但轨迹说「本次运行走到过」的节点，不再和从未到达的节点同色。
  **对状态机尤其明显**——`StateMachineRunner.StatusOf` 只把当前活动路径报成 `Running`，
  已退出的状态一律 `None`，在此之前一次运行的历史在画布上整段不可见。
  刻意与 `status-success` 分开：走过不等于成功，状态机的历史状态既非成功也非失败，
  所以 `status-visited` 只染描边、不换填充。
  另外 `GraphDebugger` 公开了 `VisitCountOf(instanceId)`——轨迹能回答「这个节点被绕了几圈」，
  `StatusOf` 回答不了；检视/提示要用这个数字时有现成接缝。

- **状态机模块补齐了自己的外壳配置。** 过去它走的是图列表的 legacy 兜底注册，
  「新建」按钮只能显示框架通用文案，空态还会显示别的领域的措辞。现在它像对话/任务一样
  在 `GraphCreationRegistry` 登记显式创建配方（`statemachine.graph`，自带图目录与黑板目录），
  并种齐 `ui.noGraphs.statemachine` / `ui.newGraphPrompt.statemachine` / `ui.newStateMachineGraph`。
  **给扩展作者**：新模块照 `StateMachineGraphScaffold` / `DialogueGraphScaffold` 的形状登记配方即可；
  漏配会被 `ModuleChromeContractTests` 那组契约测试抓住，而不是等用户截图。

- **框架通用文案自愈。** `ui.noGraphs` / `ui.newGraphPrompt` 这两个框架通用键，在只有对话模块的
  年代被写成了对话措辞（「项目中暂无对话组」）。`EnsureUI` 是 add-if-missing，永远不会把它改回来，
  于是任何一个老工程里，没有自己覆盖文案的模块都会显示其他领域的词。
  现在 Setup 会检查：**通用键的值恰好等于某个 `<key>.<module>` 覆盖值**时（这是领域措辞漏进通用键的
  确证），把它重置为框架措辞并打一条警告。条件收得这么窄是为了不碰作者自己改过的通用文案。
  **升级到本版后跑一次任一模块的 Setup Assets 即可自愈**，无需手改本地化表。

### English

**The runtime contract gains an ordered execution trace; three pure-logic assemblies now declare no engine references.** No existing API changed.

- **New optional contract `IRuntimeGraphTrace`** (namespace `NodeEditor`, assembly `NodeEditor.Runtime`):

  ```csharp
  public readonly struct GraphVisit { public readonly string GraphId, InstanceId; }

  public interface IRuntimeGraphTrace
  {
      IReadOnlyList<GraphVisit> Trace { get; }   // real execution order; revisits repeat
      void ClearTrace();
  }
  ```

  `IRuntimeGraph` still has exactly `StatusOf` / `RuntimeNodeOf` — the new contract is a separate
  interface, so **third-party runtimes that implement only `IRuntimeGraph` need no change**.

  **How to use it**: when differential-testing your own runtime against these runners, a set of
  visited nodes cannot catch an ordering bug. `((IRuntimeGraphTrace)runner).Trace` is an ordered
  list, so loop counts and repeated entries compare element by element. `GraphVisit` carries
  `GraphId`, so nodes remain distinguishable when a run descends into a sub-graph
  (`SubDialogue`, `SubMachine`, a Task `stepGraph`). Editor-side debug highlighting reads the same
  data instead of inferring a path from snapshots.

  All three bundled runners implement it. The trace is cleared by
  `DialogueRunner.Run()`/`Restore()`, `StateMachineRunner.Start()`/`Restore()`, and
  `TaskRunner.StartTask()`/`Restore()`/task completion. Callers may also call `ClearTrace()`
  at any point to scope the observation.

  One deliberate semantic: after `Restore()` the trace holds only the restored pointer. The
  visited set in a snapshot is unordered, so replaying it into the trace would fabricate an
  execution order that never happened.

- **`Dialogue.Runtime`, `StateMachine.Runtime` and `Task.Runtime` now set
  `"noEngineReferences": true`.** If your project requires pure-logic assemblies to carry no
  `UnityEngine` dependency, these three can now be referenced directly from assemblies with the
  same flag. (Verified by a real compile on Unity 6000.3.11f1: 0 errors, and the produced DLLs
  contain no `UnityEngine` reference. They still reference `NodeEditor.Runtime`; a referenced
  assembly having engine references does not prevent the referencing assembly from declaring
  none, because no `UnityEngine` type appears in `NodeEditor.Runtime`'s public API.)

- **`NodeEditor.Runtime` deliberately does not set the flag.** `Runtime/Graph/NodeDataTypes.cs`
  and `Runtime/Units/Units.cs` carry 66 `[SerializeReference]` attributes, which come from
  `UnityEngine`. Setting the flag produces 138 `CS0246` errors in Unity. `[SerializeReference]`
  is the only reason the polymorphic `TypeRef` and `Unit` trees can be persisted into an
  `.asset` at all — without it Unity treats those fields as non-serializable and drops them.
  A flag that costs you your graph data is worse than no flag. Making the core assembly
  engine-free properly means moving the serialization carriers in those two files down into
  `NodeEditor.Unity`, which is a data-model change rather than an asmdef switch.

- **The editor debugger now colours nodes from that trace.** A new `status-visited` class
  separates "walked earlier in this run" from "never reached" — previously both rendered as
  `status-inactive`. This matters most for state machines: `StateMachineRunner.StatusOf` reports
  `Running` only for the active path and `None` for every exited state, so a run's history was
  entirely invisible on the canvas. It is deliberately distinct from `status-success` — walked is
  not succeeded, and a state machine's past states are neither — so `status-visited` tints the
  outline without changing the fill. `GraphDebugger.VisitCountOf(instanceId)` is public for
  inspectors that want the loop count, which `StatusOf` cannot answer.

- **The state machine module now owns its own shell configuration.** It previously fell back to
  the graph list's legacy registration, so its create button could only show the framework's
  generic label and its empty state borrowed another domain's wording. It now registers an
  explicit recipe in `GraphCreationRegistry` (`statemachine.graph`, with its own graph and
  blackboard folders) and seeds `ui.noGraphs.statemachine`, `ui.newGraphPrompt.statemachine`
  and `ui.newStateMachineGraph`. **For extension authors**: register a recipe the way
  `StateMachineGraphScaffold` and `DialogueGraphScaffold` do; a module that skips it is caught by
  the `ModuleChromeContractTests` contract tests rather than by a user's screenshot.

- **Framework wording heals itself.** `ui.noGraphs` and `ui.newGraphPrompt` had been written with
  dialogue wording back when dialogue was the only module. `EnsureUI` is add-if-missing, so it
  never corrected them, and in any existing project every module without its own override
  inherited that vocabulary. Setup now resets a generic key when its value is *exactly equal* to
  one of its `<key>.<module>` overrides — the signature of domain wording leaking into the generic
  slot — and logs a warning. The condition is deliberately narrow so wording an author edited on
  purpose is left alone. **Run any module's Setup Assets once after upgrading** and the table
  repairs itself; no manual editing required.

## [0.1.5] - 2026-08-15

### 中文

**编辑器外壳重构：固定三栏 → 画布优先 + 贴角浮层。** 界面动作有变，数据与运行时 API 不变。

- 左栏（图列表 + 变量）整条删除。图列表收进顶栏胶囊点开的**切换器弹层**（搜索 / 新建 / 定位 / 删除都在里面）；
  分组标题只在真有多个模块时才出——模块模式下只有一组，「对话组 / 对话 (2)」那两层标题是纯噪音。
- 变量与检视变成画布上的**贴角浮层**（可拖、可折叠、位置与显隐跨会话保留）。**检视只在选中节点时出现**：
  旧外壳为一句「选中一个节点即可在此编辑」常驻 320px。
- 顶栏重排成三区：左「在哪张图 / 怎么走」，中留白，右「看什么 / 有没有问题」。深色、语言、整理、全览
  这类长尾开关进「···」溢出菜单，不再和主命令平铺同权重。
- 新增**画布坞**（画布左下）：缩放读数、全览、**整理**（按连接方向分层重排全图，新功能）、缩略图、加节点。
- 校验状态由文字改成**可点的 chip**：点它逐个跳到出问题的节点，不再只报「2 错误」却不说错在哪。
- 变量作用域档位从一排按钮改成页签；单档变量面板不再自带标题（标题由浮层给）。
- 数据窗口顶栏换用同一条 AppBar（三栏浏览结构不变）。

**给扩展作者**：`EditorUi.ToolbarClass`（`.ne-toolbar`）已移除，窗口顶栏一律用 `AppBar`；
`ne-seg-bar/-btn` 由 `ne-tabs/-tab` 取代；`graphlist-*` 由 `ne-picker-*` 取代；`ui.graphs` 文案键已废弃。
`GraphListPane` 的公开静态 API（`RegisterModuleInitializer` / `RegisterModuleAssetFolders` 等）不变。
新组件（`AppBar` / `OverlayPanel` / `Popover` / `PickerPill` / `PanelToggleBar` / `StatusChip` / `CanvasDock`）
的用法见 NodeEditor 包内的 UI-STANDARD.md。

### English

**Editor shell rebuilt: fixed three columns → canvas-first with corner overlays.** UI only; data and runtime APIs are unchanged.

- The left column is gone. The graph list now lives in a picker popover behind the app-bar pill
  (search / create / reveal / delete included); group headers only appear when more than one module is listed.
- Variables and the inspector are draggable, collapsible canvas overlays whose position and visibility persist.
  **The inspector only exists while a node is selected** — it no longer holds 320px to say "select a node".
- The app bar is now three zones (where you are · spacer · what you are looking at + status). Theme, language,
  tidy and frame-all moved into the "···" overflow menu.
- New canvas dock: zoom readout, frame all, **tidy** (new layered auto-layout), minimap, add node.
- The validation readout is a clickable chip that walks you through the problem nodes.
- Variable scope tiers are tabs; the embedded variable pane no longer carries its own title.
- The data window uses the same AppBar (its three-column browsing layout is unchanged).

**For extension authors**: `EditorUi.ToolbarClass` (`.ne-toolbar`) is removed — window headers use `AppBar`.
`ne-seg-bar/-btn` → `ne-tabs/-tab`; `graphlist-*` → `ne-picker-*`; the `ui.graphs` wording key is retired.
`GraphListPane`'s public static API is unchanged. The new controls (`AppBar`, `OverlayPanel`, `Popover`,
`PickerPill`, `PanelToggleBar`, `StatusChip`, `CanvasDock`) are documented in the NodeEditor package's UI-STANDARD.md.

## [0.1.1] - 2026-08-15

### 中文

**修复 0.1.0 的打包缺陷。用 git URL 安装过 0.1.0 的请升到本版。**

- 0.1.0 的包里有 12 个 `.cs` 缺少配套 `.meta`。这些文件是在一个 Unity 从未打开过的
  工作树里新建的，因而没有生成 meta 就被发布了。从 git URL 安装的包对 Unity 是只读的，
  它无法补写 meta——于是 GUID 每次导入重新生成，ScriptableObject 的 `m_Script` 绑不上，
  表现为编辑器界面文案退回英文、菜单项错乱。本版补齐全部 meta。
  （`Shim~/UnityShim.cs` 没有 meta 是正确的：Unity 忽略 `~` 目录。）
- 修复模块作用域：`NodeAdmission` 过去只在"图已打开"时才按模块裁剪候选节点，
  模块模式下还没建图时不做任何约束——这就是状态机编辑器里弹出对话节点的成因。
  现在准入上下文携带 `moduleScope`，无图时用外壳锁定的模块。自由模式无图仍不裁剪，行为不变。

### English

**Packaging fix for 0.1.0. Upgrade if you installed 0.1.0 from a git URL.**

- 0.1.0 shipped 12 `.cs` files without their `.meta` companions. They were created in a
  worktree Unity never opened, so no meta was generated before publishing. A package
  installed from a git URL is immutable, so Unity cannot write the missing metas: GUIDs get
  regenerated per import and ScriptableObject `m_Script` bindings fail, which showed up as
  editor chrome falling back to English and stray menu entries. All metas are now included.
- Node admission is scoped to the shell's module even before a graph exists, which is what
  let dialogue nodes appear in the state machine editor.

## [0.1.0] - 2026-08-15

### 中文

**破坏性变更。升级前请读 [MIGRATION-0.1.0.md](MIGRATION-0.1.0.md)。**

执行层从 Unity 上摘了下来。四个包的 `Runtime/` 现在是零 UnityEngine 依赖的纯 C#，可在 .NET
控制台 / 服务器 / `dotnet test` 下运行；Unity 侧只保留载体与编辑器。三个领域执行器是**整体搬迁**
的同一份实现，Unity 侧不留副本——不存在两套执行器、也就不会有语义漂移。

- 每个包拆成两个程序集：`X.Runtime`（纯层，**沿用原名**）+ `X.Unity`（载体）。纯层刻意保留原
  程序集名——已发布资产里 90 条 `[SerializeReference]` 记录写死了 `asm: NodeEditor.Runtime`，
  换名会让每个节点定义的端口和参数静默变成 null。
- 新增纯 C# 类型：`NodeSchema`（`NodeDefinition` 的数据投影）、`GraphData`、`Vec2`、
  `BlackboardDecl`、`IGraphSource`、`IGraphLog`。`Vec2` 与 `Vector2` 值等价，但序列化文本形态不同
  （内联 `{x, y}` → 块状映射），首次保存图会重排这几行，无数据变化。
- 载体实现纯接口（`NodeRegistry : ISchemaSource`、`BlackboardAsset : IBlackboardDecl`、
  `DialogueDatabase : IDialogueTextSource`），因此**多数既有调用点无需修改**。
- 子图引用由 `UnityEngine.Object` 直连改为稳定 `graphId`。附一次性迁移：
  菜单 `NodeEditor / Migrate / Upgrade Graph References (0.1.0)`。
- `DialogueLineView` 移除 `portrait`/`voice`，改携 `lineKey` 由表现层回查。
- `DialogueState` 的图引用改为 `graphId`——顺带修好了 0.0.x 自陈的"存档无法跨会话往返图指针"。
- 新增 `Tools/PureCheck` 纯度门禁与 `Tests/NodeGraph.Core.Tests`（15 项，无 Unity 运行）。

### English

**Breaking. Read [MIGRATION-0.1.0.md](MIGRATION-0.1.0.md) before upgrading.**

The execution layer is off Unity. Every package's `Runtime/` is now pure C# with zero
UnityEngine dependency and runs under .NET console / server / `dotnet test`; Unity keeps only
the asset carriers and the editor. The three domain runners were moved wholesale — there is no
second executor implementation, so the two sides cannot drift apart.

- Each package splits into `X.Runtime` (pure, **keeps its original name**) and `X.Unity`
  (carriers). The pure layer deliberately keeps the old assembly name: 90 `[SerializeReference]`
  records in shipped assets hard-code `asm: NodeEditor.Runtime`, and renaming would silently
  null out every node definition's ports and params.
- New pure types: `NodeSchema`, `GraphData`, `Vec2`, `BlackboardDecl`, `IGraphSource`, `IGraphLog`.
- Carriers implement the pure contracts, so most existing call sites compile unchanged.
- Sub-graph references move from `UnityEngine.Object` to a stable `graphId`, with a one-shot
  migration under `NodeEditor / Migrate`.
- `DialogueLineView` drops `portrait`/`voice` for `lineKey`; `DialogueState` stores `graphId`.
- Adds `Tools/PureCheck` (purity gate) and `Tests/NodeGraph.Core.Tests` (15 tests, no Unity).

## [0.0.6] - 2026-08-14

### 中文

- 节点换成统一圆角矩形：角色由整宽实色标题色带和标题上的语义图标表达，不再使用四种异形轮廓。
- 端口改为行内小圆点，连线改用冷色，整个编辑器去掉浮起金属质感，改为扁平分隔线。
- 新增图朝向：对话与任务默认横向，状态机默认纵向。朝向存在图资产上、由领域播种，已有资产无需迁移。

### English

- Nodes are now one rounded rectangle. Role reads from a full-width solid title band and the semantic icon on it, replacing the four outline shapes.
- Ports are inline dots, wires use the cool accent, and the editor drops its raised metal chrome for flat dividers.
- New graph orientation: dialogue and task default to horizontal, state machine to vertical. It is stored on the graph asset and seeded by the domain, so existing assets need no migration.

## [0.0.5] - 2026-07-17

### 中文

- 节点运行态新增主题色流动高光，标题宽度限制为 240px，超长标题自动省略。
- 修复运行时切换对话组可能卡死的问题，并缓存高频编辑器资源查询。
- Dialogue、Task 与 State Machine 示例已合并到各自领域包的 `Samples~`，不再作为独立包发布。
- 修复 NodeGraph Manager 模块卡片与示例行重叠，示例现在可从领域包内直接导入。

### English

- Added theme-aware flowing runtime highlights, capped node titles at 240px, and ellipsized overflow.
- Fixed a possible editor freeze when switching dialogue groups at runtime and cached hot editor resource lookups.
- Embedded Dialogue, Task, and State Machine samples in their domain package `Samples~` directories instead of publishing standalone sample packages.
- Fixed overlapping module and sample rows in NodeGraph Manager and enabled direct domain-package sample import.

## [0.0.4] - 2026-07-16

### 中文

- 升级节点表面为平滑圆角与真实三段渐变，完整支持亮色与暗色主题。
- 为 Dialogue、Task、StateMachine 的 27 个节点新增 19 种简洁语义图标，并按具体语义区分节点轮廓。
- 运行中、成功、失败状态点亮整个节点；选择与校验轮廓可独立组合。

### English

- Upgraded node surfaces with smooth rounded silhouettes and true three-stop gradients in both light and dark themes.
- Added 19 concise semantic icons for 27 Dialogue, Task, and StateMachine nodes, with silhouettes selected by concrete node meaning.
- Running, success, and failure states illuminate the whole node while selection and validation outlines compose independently.

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

- NodeGraph 首个公开发布版本。
- 包含节点编辑器、对话、任务与状态机；可选样例内嵌在对应领域包的 `Samples~` 中。

### English

- First public release of NodeGraph.
- Includes the Node Editor, Dialogue, Task, State Machine, and optional sample packages.
