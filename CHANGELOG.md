# 更新日志 / Changelog

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
