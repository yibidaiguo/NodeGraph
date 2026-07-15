# NodeEditor UI 标准（唯一权威 · 扩展必读）

> **给谁看**：任何要给本工程加/改编辑器 UI 的人（框架面板、领域面板、新模块、新数据源、新节点检视）。
> **地位**：本文是编辑器 UI 的**唯一权威规范**，由 `NodeEditor` 拥有；[`ARCHITECTURE.md`](ARCHITECTURE.md) 的 UI 标准条目与 [`VISION.md`](VISION.md) §3 缝表的 UI 行都指向这里。改本文所述契约必须**同一次改动里**同步：`NodeEditorStyles.uss` + 发布前 UI 契约闸门 + 本文（开发规范 C17）。
> **核心思想**：**扩展 = 组装现有组件，不是发明新样式。** 你的任务 99% 能用 §3 组件目录里的现成类/控件拼出来；拼不出来才走 §5 的「新增组件流程」。自由发挥（自造类名、自设颜色、自建样式表）会被契约测试直接判红。

---

## 1. 铁律（违反即打回，契约测试兜底）

| # | 铁律 | 锁定它的测试 |
|---|---|---|
| 1 | **单一样式表**：全工程只有 `NodeEditor/Editor/Resources/NodeEditorStyles.uss` 一份 USS；领域模块不得新建 `.uss`，不得 `styleSheets.Add` 别的表 | `UiStandard_SingleSharedStylesheet` |
| 2 | **类名必须注册**：C# 里 `AddToClassList/EnableInClassList` 的每个类名，必须在共享 USS 里有对应选择器（USS = 类名注册表）。自造类名 = 先在 USS 落样式规则 | `UiStandard_AllClassLiteralsRegisteredInStylesheet` |
| 3 | **C# 不设视觉色**：颜色/底色/描边色一律走 USS token；C# 内联 style 只准**纯布局**（flex/尺寸/间距/显隐/定位）。唯一豁免：Unity API 无法走 USS 的（当前仅 GraphView `Port.portColor`） | `AllEditorCode_DoesNotSetNormalVisualColorsInCSharp` |
| 4 | **窗口统一入口**：每个生产 `EditorWindow.CreateGUI` 开头必须调用 `EditorUi.ConfigureWindow(rootVisualElement)`；它统一挂共享 USS、绑定主题、安装主题化 tooltip，并给根挂 `ne-window-root`。需要清旧运行时挂接的可在这条统一入口之后、覆盖旧字段之前执行。内层非窗口样式挂载点仍须 `EditorUi.BindTheme(root)` | `UiStandard_StylesheetMountsBindThemeAndWindowsInstallTooltip` |
| 5 | **所有可见文案走 `Localizer.UI(key, 英文回退)`**，中文种子进各领域 Setup 的 `EnsureUI`；含校验/诊断/空态（开发规范 C11）。豁免：纯符号（▾ ✕ › + ●）与程序化数据（资产名/键名/类型名） | 人审 + 真窗口验收 |
| 6 | **命令按钮 / 导航 chrome 二分**（§2.3）：按钮质感只给"会执行动作"的命令按钮；列表行/页签/箭头/档位是导航，一律扁平 | `UsesWarmGreigeUnifiedButtonSkin` + `UsesFlatNavigationChrome` |
| 7 | **领域只消费不自建**：Dialogue/Task/新模块不得复制 token、自建平行控件、另起外壳；节点 cue、数据源、检视行全走框架 contract（VISION §3 缝表） | 架构审计 + 测试 1/2 兜底 |
| 8 | **编译 ≠ UI 完成**：改 UI 必须真开窗口验收（§6 清单），双主题都看 | 本文 §6 动态验收 |

---

## 2. 设计语言

### 2.1 双主题与 token

- 默认浅色 = **奶咖灰调**（`:root` 与 `.unity-theme-light`）；深色 = **暖炭铜金**（`.ne-theme-dark`，工具栏「深色」开关，`EditorUi.DarkTheme` 持久化）。
- **一切颜色写 `var(--ne-*)`**，规则里不出现字面 rgb（token 定义块除外）。新样式只引用现有 token；确需新 token → 两个主题块都要定义 + 契约测试断言。
- 表面阶梯（浅→深皆成立）：`--ne-bg-canvas`（画布）< `--ne-bg-window` < `--ne-bg-panel`（面板）< `--ne-bg-panel-alt/raised`（抬头/浮起）< `--ne-bg-row`（行）< `--ne-bg-control`（输入框）< `--ne-bg-node`。层级感靠这架梯子，不靠阴影堆砌。
- 几何节奏 token：普通控件圆角 `--ne-radius(-control)`、行高 `--ne-row-h`、标签宽 `--ne-label-w`、字号 `--ne-font-sm/md`、间距 `--ne-gap-*`/`--ne-pad-panel`。节点外轮廓由框架 Painter2D 统一绘制，不用领域 USS 的圆角矩形替代。

### 2.2 状态样式三规则

1. **状态不改尺寸**：hover/selected/focus/disabled 只换色，不改 width/height/padding/border-width。
2. **选中脊线常驻占位**：需要左脊线的选中态，平时就画 `border-left: 3px 透明`，选中只换色（`graphlist-row`、`data-source-row` 即范本）。
3. **状态类命名**：持续状态用 `.is-selected`；变体用 BEM 双横线（`data-source-row--selected`、`breadcrumb-crumb--current`）。

### 2.3 命令按钮 vs 导航 chrome（本标准的核心二分）

| | 命令按钮（点了会执行动作） | 导航 chrome（点了是"去哪/看哪"） |
|---|---|---|
| 质感 | premium button tokens：`--ne-button-*` 底色+高光顶边+阴影底边+5px 圆角 | 扁平：透明底，hover 软底（`--ne-bg-row-hover`），选中 `--ne-accent-soft` + 脊线 |
| 成员 | toolbar 命令/图标钮、`add-button`（新建/删除/+变量/+添加）、`choice-arrow`、`unit-list-del`、hover-bar 钮 | `breadcrumb-*`、`collapsible-arrow`、`graphlist-row`、`data-scope-title`+`data-source-row`、`ne-seg-bar/-btn`、`ne-master-list-row`（卡片行例外：有软卡底） |
| 禁止 | 平面黑矩形、亮蓝皮肤 | 把导航行做回凸起按钮 |

`Button` 控件默认**居中**继承给子元素——列表型行必须显式 `-unity-text-align: middle-left`。

---

## 3. 组件目录（先查这里，能拼则拼）

> 列出「场景 → 用什么」。类都在 `NodeEditorStyles.uss`，helper 都在 `Editor/Controls/EditorUi.cs` 等共享控件里。

### 3.1 窗口骨架

| 场景 | 用法 |
|---|---|
| 新 EditorWindow | `CreateGUI` 开头调用 `EditorUi.ConfigureWindow(rootVisualElement)`；工具栏 `Toolbar` + `EditorUi.ToolbarClass` |
| 工具栏命令 | `EditorUi.ApplyToolbarTextButton/ApplyToolbarIconButton`（文本/图标）、开关 `ApplyToolbarToggle`；分隔 `toolbar-sep`；对象框 `toolbar-graphfield` |
| 多图路径条 | `Breadcrumb` 控件（自带 `breadcrumb-crumb/-sep/--current`） |
| 分栏 | `TwoPaneSplitView` 嵌套；分栏底色/分隔线画在**滚动容器**上（`data-pane-scroll(--left)`），画在内容元素上会露底 |
| Manager 窗口标题/分节标题 | `ne-manager-title` / `ne-manager-heading`（字号/字重在 USS；margin 属布局留内联） |

### 3.2 面板解剖（左右栏）

| 部位 | 类/控件 |
|---|---|
| 面板根 | `inspector-root`（右检视）/ `variable-root`（左变量）/ `graphlist-root`（左列表） |
| 面板标题条 | `inspector-header`（一个面板一条，嵌入的子面板**不再自带标题**——`VariablePane` 教训） |
| 分节卡 | `inspector-section` + 小号淡色 `inspector-section-title`（不许彩色竖条/大字标题） |
| 底部操作栏 | `graphlist-actions`（整宽条，内部 `add-button` 均分）|
| 互斥档位切换 | `ne-seg-bar` + `ne-seg-btn`(+`.is-selected`)（不许一排散落按钮） |
| 空态 | `EditorUi.EmptyState(Localizer.UI(...))`——列表/面板空时必须给空态，不留空白 |

### 3.3 表单与字段

| 场景 | 用法 |
|---|---|
| 标签+字段行 | `EditorUi.DetailRow/FormRow`（标签宽=`--ne-label-w`，字段 `minWidth:0` 可收缩，CJK/长 key 不挤出） |
| 有限固定值（枚举/bool/语言/用途） | `EnumDropdownField`（原生 PopupField 皮）。**bool 一律 true/false 下拉，禁用 Toggle**（重皮后无 ✓） |
| 候选多/动态（库 key、黑板 key、类型路径） | `SearchableDropdownField`（`choice-*` 系；allowCustom 视缝定义） |
| 数字 | `IntegerField/FloatField`；带范围 → `field-row` + Slider + `field-num` |
| 本地化文本 | `EditorUi.CurrentLanguageTextRow`（语言下拉 + 全宽多行框；一次只显一种语言，禁语言 chip+窄框） |
| 帮助/警告/错误文本 | `ne-form-help / ne-form-warning / ne-form-error`；斜体小注 `field-note` |
| 徽章/标签 | `EditorUi.Badge`（中性）/ `EditorUi.Chip`（金色强调，如作用域 `scope-tag`） |

### 3.4 列表与导航

| 场景 | 用法 |
|---|---|
| 树状资产列表 | `graphlist-*` 系 + `CollapsibleCard` 分组（组头样式由 `.graphlist-group .collapsible-header` 提供） |
| 数据窗口左列 | `data-scope-group/-title` + `data-source-row(--selected)` |
| 条目列表+详情 | `MasterDetailList`（`ne-master-list-*`；行=软卡片、文字左对齐、带搜索/分组/空态/选中同步） |
| 弹出搜索选单 | `StringSearchWindow`（`string-search-*`；**禁用原生 SearchWindow**——编辑器铬不可主题化） |
| 折叠区块 | `CollapsibleCard`（结构）+ 叠视觉类：数据卡/引用数据 → `entry-card`；检视面板内内容自动缩进（`.inspector-body .collapsible-content`） |

### 3.5 节点/画布/单元

| 场景 | 用法 |
|---|---|
| 节点视觉 | 框架 `node-base` 全家。四类节点必须使用真实且互异的整节点轮廓：Provider=椭圆侧边胶囊，Condition=六边形/菱形侧边，Action=右箭头卡片，Control=八边形流程卡；后三种多边形的所有顶点统一走框架 rounded-polygon helper，以约 7px 且受相邻边长限制的圆滑转角替代硬尖角，不能退回统一圆角矩形或只写 USS `border-radius`。轮廓表面由同一路径叠绘上沿高光、下沿阴影、主填充与细描边，形成与共享命令按钮一致的精密压边层次；亮暗主题分别提供 token。节点角色仅由轮廓形状表达，普通态使用中性描边，不得叠加角色彩色 chrome。标题行固定 34px 最小高度，左侧统一使用 `NodeIconControl` 的 24px、7px 圆角金属小底座和 Painter2D 1.6px 语义线图标；所有图标浅色主题统一陶土色，暗色主题统一暖铜色。领域节点只用 `[NodeIcon]` 注册 `NodeIconKind`，不得带位图/SVG 或自建皮肤，未注册时才按 Role 回退。原生 `Node/#node-border` 矩形背景和边框在 normal/hover/selected 全部保持透明。running/success/failure 必须改变整个轮廓的填充、上沿高光、下沿阴影、描边与克制辉光；selected 只叠加向外偏移 3px 的独立陶土色轮廓环，不得替换运行态填充；validation error/warn 走独立内轮廓，不能露出矩形选择框。颜色必须来自 `var(--ne-*)`，命中区域使用同一组圆角曲线采样。领域**不得**自定义节点皮肤、不得给标题上底色 |
| 节点 cue（卡片下两行摘要） | 继承 `NodeCueControl`，只提供文案（两行截断由框架管）。`#extra-content` 必须保留角色轮廓的安全内边距：Provider 22/22、Condition 19/19、Action 12/22、Control 14/14（左/右）；这组值由真实 Condition/Action/Jump 与两行 cue 的四角命中验收锁定，领域不得覆写、不得用 `overflow: clip` 掩盖越界。动态高度时 cue 的完整 border-box 仍须落在 `NodeView.ContainsPoint` 的同一圆角轮廓内 |
| 端口 | `PortView.Create` 自动挂 `ne-port-single/multi`；连接点颜色是 portColor 豁免点 |
| 图级横幅 | `EditorUi.BannerClass(+--issue)` |
| 节点悬浮卡 | `node-hover-tip/-title/-desc/-param` |
| 可组合单元槽 | `UnitInspector`（`unit-slot/-body/-list/-list-row/-list-del`），扩展单元时不要自己画槽 |
| 自绘 tooltip | 自动生效（窗口根 InstallTooltip + `ne-tooltip`），元素只管设 `.tooltip` 文本 |

节点视觉轮廓的 Provider/Condition/Action/Control 是**视觉分类**，不等于底层执行 `NodeRole`。`NodeView` 必须通过已注册的 `NodeIconKind` 选择具体语义的轮廓：Dialogue/Label/Objective/State 使用胶囊，Choice/Option/Condition/Gate/Transition 使用六边形，Action/Jump/Task/Complete/Failure 使用右箭头，Entry/Terminal/SubGraph/WaitEvent/AnyState 使用八边形。未注册节点才回退到执行 Role 对应的轮廓。这条规则保证不同具体节点不会因共用执行角色而被画成同一形状。

圆角与渐变修订（覆盖上表早期的“约 7px”表述）：Condition/Action/Control 轮廓统一使用 13px 目标切入半径，短边上自适应限制为相邻边长的 42%，不得露出硬尖角。节点主体必须由 `MeshGenerationContext` 绘制与轮廓同源的顶点色网格，从左上 highlight 经 55% fill 过渡到右下 shadow；半透明的普通态高光/暗边必须先与不透明 face 合成，避免节点发灰或露出画布。不得用纯色、矩形图片或可见分带代替批准设计稿的真实渐变。
网格必须在 55% 中间色等值线上切分为两个凸区域后分别三角化，禁止使用跨越中间色的单中心三角扇，避免径向色差或辐条。

运行时着色表示“当前图的真实执行状态”，不是全局最后注册 runner 的历史：窗口按当前 `NodeGraphAsset` 匹配 runtime；晚开、关闭重开、play 中切图都必须在有限帧内追上。匹配 runtime 消失时立即清空状态类并继续查找。状态机的 State/SubMachine 仅活动路径用 `status-running`，退出即回 `status-inactive`；不得用 `status-success` 把所有访问过的状态永久点亮。

---

## 4. 布局硬约定

- 内联 style 只准：`flexGrow/Shrink/Basis/Direction`、`alignItems/Self`、`width/height/min*`、`margin*/padding*`、`display`、`position/left/top/right`、`whiteSpace`。出现颜色/字号/字重/对齐 → 挪进 USS。
- 滚动区：列表放 `ScrollView` 内、外层 `flexGrow:1`；固定操作（`add-button`/操作栏）钉在 ScrollView **外**的面板底部。
- 多档竖叠会嵌套滚动塌成 0 高——一次只挂一个可滚面板（`LayeredVariablePane` 教训）。
- 双击/单击行为：列表行单击=选中/加载，双击=Ping 资产（`GraphListPane` 范本）。

## 5. 扩展流程（照抄清单）

**A. 新领域模块要出编辑器**：走 `NodeEditorWindow.OpenModule(module, title, initial)` + `GraphCreationRegistry.Register(GraphCreateRecipe)`；同一 module 可注册多个显式配方（Task 的任务线/步骤图是双配方范本），仍不自建窗口或 USS。数据编辑走 `DataSourceRegistry` 三档，UI 自动进数据窗口三栏。

**B. 新面板/新数据源 UI**：只用 §3 目录里的类与 helper 组装；文案全 `Localizer.UI` + Setup 种中文；空态、选中态、CJK 长文本都要处理。

**C. 确需新组件/新类名**（目录真没有）：
1. 类名循例：`ne-<组件>-<部位>`（新组件）或沿用既有前缀家族；状态按 §2.2 规则 3。
2. 样式落 `NodeEditorStyles.uss`（新建注释分节，颜色只引 token，按 §2.3 判定按钮/导航）。
3. 在产品外的 UI 契约闸门中增加新类关键属性断言。
4. 本文 §3 目录登记一行。
5. 双主题真窗口验收。
—— 五步缺一即回滚到"用现有组件拼"。

**D. 改既有视觉标准**（如本次按钮/导航二分）：USS + 契约测试 + 本文 + ARCHITECTURE/VISION 摘要行，同一提交完成（C17）。

## 6. 验收清单（改 UI 的完成定义）

1. Unity 产品编译绿 + EditMode UI 契约闸门全过。
2. 真开窗口：任务/对话编辑器 + 数据窗口，浅色**和**深色各看一遍（工具栏「深色」开关）。
3. 过一遍：hover/选中/禁用不位移；列表行左对齐；空态有文案；中文长 key 不挤出不丢字；tooltip 是主题化自绘（不是系统深灰）。
4. Console 无新错误/警告。
