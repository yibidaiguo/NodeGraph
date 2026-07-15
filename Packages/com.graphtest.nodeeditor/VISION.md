# 项目视野（VISION）—— 节点编辑器框架 · 兼全项目全景 hub

> **这是「先读层」。** 动手开发任何模块前，先读本文（全项目入口） + 你要动的那个模块自己的 `VISION.md` + [`ARCHITECTURE.md`](ARCHITECTURE.md) 的「⚠️ 开发规范」A/B/C/D。
>
> **为什么有这份文档**：各模块过去是一个个会话、一次次叠加开发出来的，规则也是一次次加的——缺一个「遍历整个项目」的视野，于是模块各自演化、容易偏离原设计思想。本文补上这一层：它**只给「视野与模块关系」**（模块地图 / 依赖方向 / 跨模块接口契约 / 读图导航 / 已知偏差），**硬规则一律指向 [`ARCHITECTURE.md` 开发规范](ARCHITECTURE.md#-开发规范动手前必读--全工程唯一权威)，不在本文另立或复述**。
>
> **文档分层**：`VISION`（先读 / 全景 / 模块关系）→ `ARCHITECTURE`（框架架构 + 唯一权威开发规范）→ `EXTENDING` / `INTEGRATION` / `README`（按角色细分的指南）。
>
> **维护纪律（开发规范 C17，硬性）**：① 每次开发**先读**本文 + 本模块 VISION + 开发规范，再动手；② 每次改动若动了**模块边界 / 对外接口（缝）/ 依赖关系 / 设计意图**，必须在**同一次改动里**更新对应 `VISION.md`。判据：任一模块的实现依赖图与对外接口，与其 VISION 声明一致；新接手者只读 VISION 就能拿到全项目视野，不必逐文件考古。

---

## 0. 一句话

本工程 = **一套可复用的节点编辑器框架基座（`NodeEditor/`）** + **建立在其上的若干领域实例（当前：`Dialogue/`、`Task/`）**。框架把「做一个 Unity 节点编辑器」抽象成两件正交的事（创作期数据/校验/契约 vs 编辑器外壳），领域层只填策略、复用框架的全部机制。

---

## 1. 项目模块地图

| 模块 | 路径 | 职责（一句话） | 对外提供 | 受众 | 状态 |
|---|---|---|---|---|---|
| **NodeEditor（框架基座 / hub）** | `Packages/com.graphtest.nodeeditor/` | 数据/校验/运行时契约 + GraphView 编辑器外壳 + 横切（黑板/本地化/数据窗口/单元体系）。一切领域复用它 | 见 §3 全部缝 | 框架开发者 / 领域开发者 | 已实现，活范本 |
| **Dialogue（领域实例）** | `Packages/com.graphtest.dialogue/` | 在框架上实现「对话编辑器」：对话节点、运行器、对话数据库、领域校验/连接规则、模块入口 | `IRuntimeGraph` 实例 + 事件 + `OpenModule` 入口（见 `Dialogue/VISION.md`） | 领域开发者 / 集成者 / 设计师 | 已实现，活范本 |
| **Task（领域实例）** | `Packages/com.graphtest.task/` | 在框架上实现「任务/目标依赖编辑器」：任务节点、运行器、任务数据源、领域校验/连接规则、模块入口 | `IRuntimeGraph` 实例 + `OpenModule` 入口（同 §3 缝表） | 领域开发者 / 集成者 / 设计师 | 已实现 |
| **StateMachine（领域实例）** | `Packages/com.graphtest.statemachine/` | 在框架上实现「分层状态机（HSM）编辑器」：六种状态机节点、逐帧运行器、快照存档、领域校验/连接矩阵、模块入口；可运行样例仅位于本地开发包 | `IRuntimeGraph` 实例 + 事件/黑板 + `OpenModule` 入口（见 `StateMachine/VISION.md`） | 领域开发者 / 集成者 / 设计师 | 已实现 |
| _（未来）行为树 / 科技树…_ | `Assets/<领域>/` | 各自领域语义；**照 Dialogue/Task/StateMachine 的样子**只填策略 | 同上模式 | — | 占位：新增模块即在此登记 + 建该模块 `VISION.md` |

> **声明式 / planner 栈（GOAP/HTN/utility）** 是与 wire-graph 正交的第二栈：卡片/表格而非画布，**两栈隔离**（见 §2、§4）。当前实例未落地声明式领域，但框架契约层为它预留。

---

## 2. 依赖方向（谁可以依赖谁，谁绝不可）

```
玩家构建可见 ────────────────────────────────┐
  NodeEditor.Runtime   ← 不依赖任何东西（不引 Editor！）
        ▲
        │ 单向
  NodeEditor.Editor    ← 只依赖 NodeEditor.Runtime
        ▲
        │ 单向
  <领域>.Runtime ──▶ NodeEditor.Runtime
  <领域>.Editor  ──▶ NodeEditor.Editor + NodeEditor.Runtime + <领域>.Runtime

  领域 A  ✗  领域 B     ← 领域之间【绝不直接引用】，只经运行时契约 / 事件协作
  声明式栈 ✗ wire-graph 图结构（NodeGraphAsset/Connection/GraphValidator/GraphType/IRuntimeGraph）
```

铁律（细节与代码落点见开发规范 **B5–B10**）：
- **Runtime 不引用 Editor**（否则破坏玩家构建）。
- **框架 → 领域是单向的**：框架绝不认识任何领域语义（不把「对话」写进框架），领域经「框架出机制、领域填策略」的缝接入。
- **领域之间不直接依赖**：跨领域协作只经运行时契约 / 黑板 / 事件（解耦）。
- **声明式两栈隔离**：声明式只复用 4a 的 `TypeRef`/`AuthoringFamily`/`IAuthoringAsset` + 4b 的 `IScopedBlackboard`，绝不碰 wire-graph 图结构。
- **GraphView 依赖只在框架 Editor 边界封装里**（`Editor/Window`·`Controls`·`Support`），领域层只跟封装类型 `NodeView`/`PortView`/`EdgeView` 打交道（B6）。

---

## 3. 模块间接口契约（缝）—— 跨模块只准走这些

> 这是本框架的核心解耦机制：**框架出机制（mechanism），领域填策略（policy）**。领域要扩什么，都从下面的缝接入，**框架代码一行不动**。新增领域时照抄这张表。

| 缝 | 类型 | 提供方 | 消费方 | 不变式 / 注意 |
|---|---|---|---|---|
| `IRuntimeGraph` + 可选 `IRuntimeGraphSource` + `RuntimeGraphRegistry` | 运行时↔编辑器桥 | 框架 Runtime（只读状态/图所有权契约）/ 领域 Runtime（实现） | 编辑器（`[InitializeOnLoad]` 桥订阅） | 运行时抛事件，Editor 侧桥订阅；注册表保存全部 live runner，窗口按当前 `NodeGraphAsset` 匹配 → Runtime 不引 Editor |
| `NodeRegistry` / 全局 `BlackboardAsset` / `LocalizationTable` / `*AssetPaths` | 资产 locator | 框架 | 生成器 / 启动器 / 各面板 | `*AssetPaths` 只负责提供唯一项目配置；注册表、全局黑板和本地化资产只按其中的精确 `Assets/` 路径解析，缺失、占用或配置歧义均失败关闭。例外：模块/组黑板按 `module`/`group` 标签解析，可有多块 |
| `GraphValidator.RegisterExtension` | 校验扩展点 | 框架 4c | 领域校验（如 `DialogueValidation`） | 领域按 id 追加 Check（同 id 覆盖告警 B10），框架按 graphType 分派 |
| `ConnectionRules.RegisterRule` + `ConnectionRuleMatrix<TDef,TKind>`（+ `CheckConnectionRules` / `GetCompatiblePorts`） | 连接合法性矩阵 | 框架（含判定引擎与 `val.conn*` 拒绝消息） | 领域矩阵数据（如 `DialogueConnectionRules` 的 `s_Matrix`，`[InitializeOnLoad]` 里 `RegisterRule(id, s_Matrix.Evaluate)`） | 按「节点种类」约束连接；按 id 保序短路、同 id 覆盖告警；拖拽实时拦截 + 校验兜底共用同一规则源（准则#10） |
| `DataSourceRegistry`（+ 通用数据窗口） | 数据编辑缝 | 框架 | 领域数据源（如 `DialogueDataSources`） | 按 `DataScope` 三档（项目/领域/单图）；已有 `[CustomEditor]` 直接 `InspectorElement` 嵌入（准则#12） |
| `GraphCreationRegistry` + `NodeGraphAsset.module` + `OpenModule` | 模块入口 / 新图创建配方 | 框架 | 领域入口（如 `DialogueEditorLauncher`） | 领域可为同一模块注册一个或多个显式配方（标签、目录、文件名、初始化器）；框架按 `module` 字符串过滤，不认领域语义；旧图须回填 `module` 标签（准则#11、坑#11） |
| `ParamChoiceProviders` + `ParamDef.choiceSource` | 候选反向注入 | 框架 | 领域（注册标签→候选） | key/引用类参数走可搜索下拉，框架不认识领域候选（坑#10） |
| `Unit` 四族 + `UnitRegistry`（全局/领域两级） | 可组合单元 | 框架 Runtime/Editor | 领域单元（如 `DialogueUnits`） | 条件/取值/副作用/编排做成 `[SerializeReference]` 内联槽，**节点不烘 key/op/value**（准则#13、红线#13/#14） |
| 分层 `BlackboardAsset` + `BlackboardLocator` + `BlackboardSet` + `RuntimeBlackboard` | 黑板作用域 / 运行存储 | 框架 | 领域 / 图 | 作用域 = 资产档别（全局/模块/组），就近覆盖；运行存储每实例、统一为框架 `RuntimeBlackboard`（领域薄继承，准则#15） |
| `Localizer` + `Language` 枚举 + 配置 SO + `FrameworkSetup`（种子入口） | 本地化横切 | 框架（框架 key 由 `FrameworkSetup.SeedFrameworkUI` 播种） | 所有可见文案；各领域 Setup 先调 `EnsureCoreAssets` 再种领域 key | 所有 UI 文案走它，含校验/诊断/报错（开发规范 C11）；种子所有权 = key 所有权 |
| `DomainSetupPipeline`（`SetupDefinitions<TDef>` / `MergeIntoRegistry<TDef>`） | 领域 Setup 管线 | 框架 | 各领域 Setup | 一类型一 .asset、坏资产失败关闭、registry 只接管本域条目——领域只供 TDef/目录/名称（机制在框架、策略在领域） |
| `NodeEditorStyles.uss` + `EditorUi` + `NodeCueControl`（完整规范见 [`UI-STANDARD.md`](UI-STANDARD.md)） | UI 标准 / 共享控件 | 框架 Editor | 领域 Editor（Dialogue/Task 等） | NodeEditor 拥有 tokens、toolbar/cue/badge/form-row/banner/tooltip/data 三列 contract；Graphite Premium Tool palette 保留深画布与黑钛/石墨面板，节点统一深色标题，角色色只走 4px 窄脊线和少量语义 accent；命令按钮统一走 premium button tokens（底色、hover、active、disabled、描边、高光、阴影），覆盖 toolbar、添加/删除、组合框箭头与 hover-bar，禁止领域另起平面黑矩形或亮蓝按钮皮肤；导航 chrome（面包屑路径条、折叠箭头、数据源导航行、图列表行、作用域分段选择器 ne-seg-*）走扁平导航契约——透明底、hover 软底、选中强调色软底 + 常驻透明脊线占位（选中不位移），契约由 NodeEditorStyles_UsesFlatNavigationChrome 锁定；本地化文本统一用语言下拉 + 全宽高文本区，只显示当前选择语言；普通视觉颜色走 USS，C# 设色仅限 Unity API 例外 |
| 事件（事件节点 → 外部系统） | 跨模块解耦 | 领域 Runtime | 别的游戏系统（任务/存档/HUD…） | 事件解耦、不阻塞；跨会话图引用映射成稳定 GUID（见 `Dialogue/INTEGRATION.md`） |

---

## 4. 框架自身的设计意图与边界（它作为 hub 的职责）

完整架构见 [`ARCHITECTURE.md`](ARCHITECTURE.md)（§1 程序集、§2 分层、§3–§8 数据模型/契约/校验/装配/本地化）。这里只记**不可动摇的正交轴**——偏离它们就是「跑偏」：

1. **创作期（数据 + 校验 + 运行时契约，进玩家构建）** 与 **编辑器外壳（UI Toolkit/GraphView，仅编辑器）** 正交。
2. **身份（`Role`）与行为（运行时接口）正交**：`Unit` 基类只声明 `Role`（`sealed override`），不绑图运行时接口；同一个 `ActionUnit` 能用于任何图类型。
3. **声明分层（黑板按资产档别）** 与 **运行每实例存储** 正交（两轴独立）。
4. **wire-graph 栈 与 声明式栈 隔离**（§2）。
5. **框架出机制、领域填策略**（§3 全部缝）：框架绝不认识领域语义。

**框架边界**：框架做「画布/检视/变量面板/调试器/本地化/复制粘贴/撤销重做/数据窗口/单元体系/连接规则机制/模块入口机制/UI 标准」。节点表面、亮暗主题、运行态整面照明、选择轮廓、语义图标绘制与金属底座也由框架统一拥有；领域只通过 `[NodeIcon]` 选择抽象 `NodeIconKind`。框架**不做**任何领域语义（对话台词、任务目标…那是领域的事）。Dialogue/Task/StateMachine 的节点 cue、图标声明、数据源和工具栏入口都只消费框架共享 UI contract。

> **共享 shell（C12 有意例外）**：C12 的强默认是「每模块单独 EditorWindow」，但 Dialogue 当前**复用框架 shell**（`OpenModule("dialogue", …)` 进同一 `NodeEditorWindow`，即模块模式），是经文档化的有意取舍——对话数据可嵌套、设计师需在多张图间频繁来回，故并进同一外壳；框架仍按 `module` 字符串过滤，不认识领域语义。

---

## 5. 读图导航（我要做 X → 读哪份）

| 我要… | 先读 | 再读 |
|---|---|---|
| 给框架加图类型 / 基元 / 校验，或基于框架搭一个**全新领域** | 本文 §1–§4 | [`ARCHITECTURE.md`](ARCHITECTURE.md) 全文 + 开发规范 |
| 给**已有领域**（如对话）加节点 / 面板 / 校验 | 本文 §3 缝表 | 该领域 `VISION.md` + `<领域>/EXTENDING.md` |
| 在别的游戏系统里**用**运行时产物 / 调编辑器 UI | 本文 §2–§3 | `<领域>/INTEGRATION.md` |
| 不写代码、用编辑器写内容 | — | `<领域>/README.md` |
| 新增一个领域模块 | 本文 §1（登记）+ §3（照缝表接入） | 本文末尾「新增模块清单」 |

### 新增模块清单（照此即可，框架零改动）
1. 在 §1 模块地图登记该模块（路径 / 职责 / 受众）。
2. 在该模块目录建一份 `VISION.md`（领域模块模板见 `Dialogue/VISION.md`）。
3. 按 §3 缝表接入：`NodeDefinition` 子类声明节点 → 实现 `IRuntimeGraph`；需要 play 调试时同时实现 `IRuntimeGraphSource.OwnsGraph` 声明 root/活动子图 → 注册 `GraphValidator.RegisterExtension` / `ConnectionRules` / `DataSourceRegistry` / `GraphCreationRegistry` / `ParamChoiceProviders` → 出 `OpenModule` 菜单入口 → 路径集中进 `<Domain>AssetPaths`。
4. 产出该领域的四份角色文档（EXTENDING / INTEGRATION / README + 在本文/该 VISION 反映关系）。
