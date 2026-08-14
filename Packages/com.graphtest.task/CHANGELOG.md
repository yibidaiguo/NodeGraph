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

## [0.0.6] - 2026-08-14

### 中文

- 节点换成统一圆角矩形：角色由整宽实色标题色带和标题上的语义图标表达，不再使用四种异形轮廓。
- 端口改为行内小圆点，连线改用冷色，整个编辑器去掉浮起金属质感，改为扁平分隔线。
- 新增图朝向：对话与任务默认横向，状态机默认纵向。朝向存在图资产上、由领域播种，已有资产无需迁移。

### English

- Nodes are now one rounded rectangle. Role reads from a full-width solid title band and the semantic icon on it, replacing the four outline shapes.
- Ports are inline dots, wires use the cool accent, and the editor drops its raised metal chrome for flat dividers.
- New graph orientation: dialogue and task default to horizontal, state machine to vertical. It is stored on the graph asset and seeded by the domain, so existing assets need no migration.

## [0.0.4] - 2026-07-16

### 中文

- 升级节点表面为平滑圆角与真实三段渐变，完整支持亮色与暗色主题。
- 为 Dialogue、Task、StateMachine 的 27 个节点新增 19 种简洁语义图标，并按具体语义区分节点轮廓。
- 运行中、成功、失败状态点亮整个节点；选择与校验轮廓可独立组合。

### English

- Upgraded node surfaces with smooth rounded silhouettes and true three-stop gradients in both light and dark themes.
- Added 19 concise semantic icons for 27 Dialogue, Task, and StateMachine nodes, with silhouettes selected by concrete node meaning.
- Running, success, and failure states illuminate the whole node while selection and validation outlines compose independently.

## [0.0.5] - 2026-07-17

### 中文

- 为任务节点注册简洁的共享语义图标，并自动继承框架的金属底座、双主题节点层次和整节点运行态照明。
- 将 Task Basics 合并到领域包的 `Samples~`，可从 Package Manager 或 NodeGraph Manager 直接导入。

### English

- Registered concise shared semantic icons for Task nodes, inheriting the framework metal base, dual-theme depth, and whole-node runtime illumination.
- Embedded Task Basics in this package's `Samples~` directory for direct Package Manager or NodeGraph Manager import.

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

- 首个公开版本，提供基于 NodeGraph 的任务图运行时与编辑器。
- 支持任务依赖 DAG，以及目标、条件、动作、等待事件、跳转、完成和失败等步骤节点。
- 提供 `TaskRunner`、任务日志、快照、领域校验和连接规则。
- 可通过 `Tools/NodeGraph/Task` 或模块管理器完成初始化。

### English

- First public release of the NodeGraph-based task runtime and editor.
- Supports task dependency DAGs and Objective, Condition, Action, Wait Event, Jump, Complete, and Fail step nodes.
- Provides `TaskRunner`, task journals, snapshots, domain validation, and connection rules.
- Supports setup through `Tools/NodeGraph/Task` or the module manager.
