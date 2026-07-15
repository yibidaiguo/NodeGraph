# 节点编辑器框架 · 架构与开发指南（面向框架开发者）

> **UPM 边界（1.0）**：产品 Runtime/Editor 的发布源位于仓库根目录 `Packages/com.graphtest.nodeeditor/`；各 domain 位于对应 `Packages/com.graphtest.*`。`UnityProject/Assets/*/Data` 只保留 host 项目可变数据，默认消费者数据写入 `Assets/*Content`。本文后续的 `Runtime/`、`Editor/` 相对路径描述的是对应 package 内部边界。
>
> **这份讲的是可复用框架本身（`Packages/com.graphtest.nodeeditor/`）**——它的分层、核心类型、扩展缝、硬规则，以及"怎么在它之上搭一个新的领域层"。
>
> **先读层 · 项目全局视野**：动手前先读 [`VISION.md`](VISION.md)（本模块视野；框架这份**兼项目全景**：模块地图 + 依赖方向 + 跨模块接口契约/缝 + 读图导航）与下面这一节「⚠️ 开发规范」。VISION 给「视野与模块关系」，开发规范给硬规则；下面四份是按角色细分的指南。**开发纪律见开发规范 C17（先读后改、随设计同步更新 VISION）。**
>
> 四份文档分工，别看错：
> - 本文（`Packages/com.graphtest.nodeeditor/ARCHITECTURE.md`）：**框架开发者**——维护框架、给框架加图类型/基元类型、或基于框架做一个**全新领域编辑器**的人。
> - [`Packages/com.graphtest.dialogue/EXTENDING.md`](../com.graphtest.dialogue/EXTENDING.md)：**领域层开发者**——给**已有的对话编辑器**加节点/面板/校验的人（"想加 X → 改哪个文件"配方）。
> - [`Packages/com.graphtest.dialogue/INTEGRATION.md`](../com.graphtest.dialogue/INTEGRATION.md)：**集成者 / 外部模块开发者**——在别的游戏系统里**用**运行时产物（订阅事件、跑对话、存档）+ 调编辑器 UI 的人（消费者侧伴随文档；下文 §6 缝 / §8 是它在框架侧的来源）。
> - [`Packages/com.graphtest.dialogue/README.md`](../com.graphtest.dialogue/README.md)：**内容设计师**——用对话编辑器写剧情、不写代码的人。
>
> `Packages/com.graphtest.dialogue/` 是建立在本框架之上的一个**实例领域层**，可作活范本。

---

## 0. 一句话定位

本框架把"做一个 Unity 节点编辑器"抽象成**两件正交的事**：

1. **创作期数据 + 校验 + 运行时契约**（`Runtime/` 程序集，可进玩家构建）。
2. **基于 UI Toolkit / GraphView 的编辑器外壳**（`Editor/` 程序集，仅编辑器）。

领域层（对话 / 行为树 / 任务……）只需：① 写若干 `NodeDefinition` 子类声明节点；② 实现一个 `IRuntimeGraph` 跑语义；③ 注册一条校验扩展；④ 生成资产。**框架的画布、检视面板、变量面板、调试器、本地化、复制粘贴、撤销/重做全部复用，不用碰。**

---

## ⚠️ 开发规范（动手前必读 · 全工程唯一权威）

> **这一节是本工程所有硬规则、架构不变式、工程约束的单一权威来源。** 框架（本文件）、领域层（[`Dialogue/EXTENDING.md`](../com.graphtest.dialogue/EXTENDING.md)）、设计师文档（[`Dialogue/README.md`](../com.graphtest.dialogue/README.md)）的规则都以此处为准，别处只引用、不另立。改框架/领域层任何一处都要逐条守。

### A. 🔴 数据安全硬规则（违反 = 无声毁数据 / 资产打不开，最高优先级）

1. **每个具体 `ScriptableObject` / `MonoBehaviour` 一个文件、文件名 == 类名。** 否则 Unity 不给它绑 MonoScript，落盘的 `.asset` 全部 `m_Script: {fileID: 0}` 损坏、`AssetDatabase.FindAssets("t:Type")` 找不到、编辑器开窗即不可用。**纯 `[Serializable]` 数据类、enum、抽象基类不在此限**（抽象类永不作为资产实例化）。这就是为什么 `BlackboardAsset` / `NodeGraphAsset` / `NodeRegistry` / `LocalizationTable` / `EditorLocalizationConfig` 各占一个文件，而 `NodeDataTypes.cs` 只装纯数据类型 + 抽象的 `NodeDefinition`。详见 [NodeDataTypes.cs](Runtime/Graph/NodeDataTypes.cs) 顶部注释。坏资产 `m_Script:0` **就地改不掉**；Setup 必须失败关闭并报告路径，不能自动删除或覆盖归属不明的文件。作者确认后手工移走/删除，再重跑 Setup 创建带 Undo 的正确资产。
   - **`[SerializeReference]` 内联多态对象（如 `Unit` 子系统）的抽象基类必须标 `[Serializable]`。** 否则一旦它被嵌套引用（装饰器持有 `[SerializeReference] List<ConditionUnit>` 等），Unity 会把该字段判为不可序列化、**整段丢弃**——装饰器子树存盘即无声消失（编译绿、内存里也对，只有真正存盘→重载才暴露）。参照工作的先例 `Constraint`/`TypeRef`：基类都带 `[Serializable]`。`Unit`/`ConditionUnit`/… 同理（[NodeRuntime.cs](Runtime/Graph/NodeRuntime.cs)）。
2. **编辑器里任何对序列化数据的写入**：写前 `Undo.RegisterCompleteObjectUndo(owningAsset)`、写后 `EditorUtility.SetDirty(owningAsset)`。检视面板的编辑**不经过画布的 `graphViewChanged`**，不标脏就会在保存/关闭时被 Unity 跳过序列化、无声丢失，且不可 `Ctrl+Z`。框架已把这套收口到 `NodeInspectorEdits`（[InspectorPane.cs](Editor/Inspector/InspectorPane.cs)）；新写入路径必须走它或复刻它。
3. **GraphView 程序化重建视图时 `DeleteElements` 会触发 `graphViewChanged`**——若被误当"用户删节点"，会把 `Asset.instances` 清空并存盘（"节点一刷新就没了"）。框架在 `GraphCanvas.Load()`（[GraphCanvasView.cs:108](Editor/Window/GraphCanvasView.cs)）里用 `graphViewChanged = null` + `try/finally` 摘回调防住了。任何"程序化增删视图"的新代码都要照此摘回调。
4. **浮层 / 弹窗挂到有字体的容器**（GraphView 自身那层，与 banner/minimap 同级），别挂 `panel.visualTree`（那层 Label 继承不到字体 → 渲染成空黑框）；CJK 文本容器要**给固定宽度**（自动宽度会塌成 ~1 字宽）；`PopupField` 的当前值**必须在候选列表里**，否则抛 `ArgumentException` 刷 console。见 [NodeHoverTooltip.cs](Editor/Window/NodeHoverTooltip.cs)、`SafePopup`（[InspectorPane.cs:169](Editor/Inspector/InspectorPane.cs)）。

### B. 架构不变式（违反 = 破坏框架设计，评审逐条核）

5. **依赖单向**：`Runtime` 不引用 `Editor`（否则破坏玩家构建）；框架数据契约不依赖编辑器或领域上层；编辑器只经 `IRuntimeGraph` 读状态、经可选 `IRuntimeGraphSource` 判断图所有权（§4），不碰领域内部。
6. **GraphView 依赖只能出现在框架 Editor 边界封装里，领域层不得直接引用。** 画布适配集中在 [`GraphCanvasView.cs`](Editor/Window/GraphCanvasView.cs)；Unity `SearchWindow`/拖拽辅助这类 GraphView API 只能在 `Editor/Window`、`Editor/Controls`、`Editor/Support` 的通用封装里出现。其它业务/领域文件只跟封装类型 `NodeView`/`PortView`/`EdgeView` 或共享控件打交道。
7. **身份与行为正交**：`Unit` 基类声明角色（`Role`，`sealed override`、子类不能改/混；基类保留 `*Unit` 后缀，裸名 `Action`/`Control` 撞 `System`）。Unit 有**两种用法、两套行为入口**，别混：①**当节点的逻辑基类**时——`Unit` 子类同时实现某 graph 类型的运行时接口（`ITickNode`/`IControlFlowNode` 等），行为由「连线」组合，基类**不绑**这些接口（保持身份/行为正交）。②**当内联可组合单元**（`[SerializeReference]` 存进 `NodeInstance.unitOverrides`、装饰器嵌套）时——四个族基类各带一个统一求值入口 `Evaluate/Get/Execute/Tick(NodeContext)`，行为由「装饰器对象树」组合（见 [Units.cs](Runtime/Units/Units.cs)、[UnitRegistry.cs](Editor/Inspector/UnitRegistry.cs)）。声明式 piece **不继承** `Unit`（§4）。
   - **节点需要条件/取值/副作用/编排时，持一个 Unit 槽（`AddUnitParam(name, family)`），不要把 `key/op/value`、`gateKey/gateOp/gateValue` 之类比较/门控/赋值参数烘到节点上。** 后者是本工程修过的真实设计偏差：每个节点各写一套门控、无法复用、条件一多就糊。正确做法——条件/取值/副作用一律做成可组合 Unit（全局通用单元放框架 `Runtime`、领域单元放领域 `Runtime`，都继承族基类即被 `UnitRegistry` 反射发现），检视面板渲染成「下拉选类型（全局/领域两级、按族过滤）+ 可折叠字段 + 装饰嵌套」。判断标准：若某参数复刻了另一类单元的职责（如 gateKey/op/value 复刻条件），就该用 Unit 槽组合，而非烘参数。
   - `UnitAttribute` 必须声明稳定本地化 key 与英文回退：`[Unit("unit.xxx.name", "English Name", "unit.group.xxx", "English Group")]`。`UnitRegistry` 按当前语言经 `Localizer.UI` 解析，类型身份不随语言改变。框架代码定义并拥有通用单元及共享分组的 key/fallback；领域扩展只定义并拥有自己的 `unit.<domain>.*`。物化入口与所有权一致：框架通用项由框架 `FrameworkSetup.EnsureCoreAssets/SeedFrameworkUI`（[Editor/Support/FrameworkSetup.cs](Editor/Support/FrameworkSetup.cs)）add-if-missing 播种——每个领域 Setup 先调它，Manager 框架卡片的 Setup Assets 也直达它；各领域只在自己的 Setup 里种 `unit.<domain>.*` 等领域 key。
8. **声明式两栈隔离**：声明式栈只复用 Runtime 的 `TypeRef`/`AuthoringFamily`/`IAuthoringAsset` 与 `IScopedBlackboard` 契约，**绝不碰** wire-graph 图结构（`NodeGraphAsset`/`Connection`/`GraphValidator`/`GraphType`/`IRuntimeGraph`）。
9. **资产 locator 服从项目路径配置并在歧义时失败关闭**（`NodeRegistry`/**全局** `BlackboardAsset`/本地化配置）：配置路径存在就只取该资产；路径缺失而别处有候选时列出候选并返回空，绝不取第一个（§6 缝）。⚠️ 例外：`BlackboardAsset` 是**分层**的——全局档每项目一块，**模块/组档按 `module`/`group` 标签可有多块**（这是设计，不是歧义）；`BlackboardLocator.FindLayer/Resolve` 按标签精确取档，同一标签出现多块也失败关闭。
10. **禁止全局可变 bag**：组件间传数据走构造参数 / 显式 API，不用 `public static` 可变字段（多窗口会互相覆盖）。需要的全局注册表（如 `RuntimeGraphRegistry`）必须是幂等、可注销的显式 live collection，不是裸 static 或单槽 last-writer-wins。

### C. 工程规范（约束所有开发工作）

11. **中文 + Localizer（扩展即本地化）**：代码注释、文档、面向设计师的说明一律中文；**所有面向用户的可见字符串都走 `Localizer`**（属性 → 表 → 英文回退，§7），绝不硬编码可见字符串。**"可见字符串"完整含义——这四类一类都不能漏**：① 节点/参数的名与说明（`[NodeDoc]`/`[ParamDoc]`，没写回退英文）；② 界面 chrome（标题/按钮/标签/提示/空状态，`Localizer.UI("ui.xxx","English")`）；③ **校验 / 诊断 / 报错消息**（节点红黄框、画布横幅、弹窗错误——`Localizer.UI("val.xxx"/"ui.errXxx","English")`，带 `{0}` 占位的用 `string.Format`）；④ 检视面板明细行标签等零散文字。**时机是硬性的：每次开发或拓展编辑器功能，必须在同一次改动里把它新增的可见文案一并本地化**——写 `Localizer.UI` 调用 *并且* 在生成器里种好中文（框架 key 进 `FrameworkSetup.SeedFrameworkUI`、领域 key 进该领域 Setup 的 `SetupLocalization`/`SeedLocalization`，都用共享 `FrameworkSetup.EnsureUI`/`EnsureVarDesc`；跑 `Setup Assets` 落进 `LocalizationTable.asset`）；加节点/参数 *并且* 标 `[NodeDoc]`/`[ParamDoc]`。不留"以后再补"——遗漏只会无声回退英文、不报错，最难发现（坑 #12：校验/诊断消息正是这样长期漏成英文的）。**完成判据**：把编辑器语言切到中文后，整套 UI——连同报错、横幅、悬停浮层——无任何残留英文。
12. **每模块单独 `EditorWindow`（强默认）**：只有数据真嵌套、设计师需频繁来回时才并进同一 shell。
13. **真实 Unity 验证优先**：必须在实际支持版本中编译、运行 EditMode、检查 Console 并完成真实窗口交互；环境确实无法运行 Unity 时只能报告未验证，静态检查不能冒充动态验收。
14. **节点线索 / 运行时小部件不新增图片资源**：用文字 + 既有 USS（`NodeViewControl` 扩展点）。
15. **四份文档分工，改代码同步改文档**：框架架构指南（本文）/ 领域拓展指南（EXTENDING）/ 集成接入指南（INTEGRATION）/ 设计师 README 各司其职；动了契约/接口/扩展点，对应文档同步更新（坑 #5：跨文件跨时间的 stale 是后期主要 bug 源）。
16. **代码结构固定，项目资源位置由用户配置**：① 产品代码在各 asmdef 的 `Runtime/` / `Editor/` 根下按职责分文件夹；移动 `.cs` 必须保留 `.meta` / GUID。② 节点定义、图、黑板、数据库、本地化与注册表的实际落点不是架构常量；每个生成/装载入口都从唯一的项目级 `NodeEditorAssetPaths` 或领域 `<Domain>AssetPaths` 读取。Setup 只播种 `Assets/<Module>Content/...` 一类可编辑默认值，用户可改成任意规范化、项目自有的 `Assets/` 子目录；改默认值不会自动搬动或覆盖现有作者资产。③ 配置资产首次引导到 `Assets/NodeEditorSettings/`，但 locator 按类型在项目 `Assets/` 中解析；缺失时带 Undo 创建，重复时失败并列出候选，绝不静默取第一个。领域配置不得持有框架全局路径，框架配置不得认识领域目录。④ 产品与集成测试从当前配置解析资源；只有明确创建/销毁自身临时资产的隔离测试可使用测试目录常量。范本见 [Runtime/Data/NodeEditorAssetPaths.cs](Runtime/Data/NodeEditorAssetPaths.cs)、[Editor/Support/NodeEditorAssetPathsLocator.cs](Editor/Support/NodeEditorAssetPathsLocator.cs) 与 [ProjectAssetPaths.cs](Editor/Support/ProjectAssetPaths.cs)。

17. **每个模块写一份「视野文档」`VISION.md`，先读后改、随设计同步（项目全局视野层）。** 各模块过去一个个会话叠加开发、规则一次次加，缺一个「遍历整个项目」的视野——模块各自演化就会偏离原设计思想。故每个模块（框架 `NodeEditor` 兼**项目全景 hub**、各领域如 `Dialogue`、将来每个新模块）各产出一份 [`VISION.md`](VISION.md)，作为四份角色文档（架构/拓展/集成/README）**之上的「先读层」**，覆盖：① **模块地图**（全项目模块 + 职责/受众/状态）；② **依赖方向**（谁可依赖谁——单向、领域之间只经契约/事件、声明式两栈隔离）；③ **模块间接口契约（缝）**（跨模块只准走这些：`IRuntimeGraph`/`RuntimeGraphRegistry`、各 locator（每项目一个）、`GraphValidator.RegisterExtension`/`ConnectionRules`/`DataSourceRegistry`/`GraphCreationRegistry`/`ParamChoiceProviders`、`Localizer`、事件解耦）；④ **本模块设计意图与边界**；⑤ **读图导航**（「我要做 X → 读哪份角色文档/哪个 skill」）；⑥ **已知偏差/待收敛**。框架那份兼项目全景（模块地图 + 依赖 DAG + 跨模块缝清单都汇在它这里）。**VISION 只给「视野与关系」，硬规则一律指向本节「开发规范」A/B/C/D，不在 VISION 另立或复述。** **纪律（硬性）：每次开发先读 项目全景 [`NodeEditor/VISION.md`](VISION.md) + 本模块 `VISION.md` + 本节开发规范，再动手；每次改动若动了模块边界 / 对外接口（缝）/ 依赖关系 / 设计意图，必须在同一次改动里更新对应 `VISION.md`**——与 C15「改代码同步改文档」同源（VISION 是其中最易漂的「全局视野」那层）。判据：任一模块的实现依赖图、对外接口，与其 VISION 声明一致；新接手者只读 VISION 即可拿到全项目视野、不必逐文件考古。

### D. 完成定义（Definition of Done）

18. **编辑器改动未经"真开窗 + 真交互核验"不算完成**：至少逐节点选中看检视面板各参数渲染、悬停看浮层有内容、切语言看全 UI 翻过去、**重选/刷新图后数据还在**，且 Console 零产品 error/warning。"编译绿 + 有节点"远不等于"用起来对"；只有真交互能暴露布局、刷新与生命周期问题。

---

## 1. 程序集结构

| asmdef | 平台 | 引用 | 装什么 |
|---|---|---|---|
| `NodeEditor.Runtime` | 全平台（含玩家构建） | 无 | 数据类型、运行时接口/契约、Unit 体系、校验器、本地化数据 SO + 属性 |
| `NodeEditor.Editor` | 仅 Editor | `NodeEditor.Runtime` | GraphView 适配、检视/变量面板、调试器、外壳窗口、本地化解析服务 |

**铁律：依赖单向。** Runtime **不引用** Editor（否则破坏玩家构建）。命名空间：Runtime 用 `NodeEditor`，Editor 用 `NodeEditor.EditorUI`。

> 校验器 `GraphValidator` 放在 `Editor/`（创作期关注点），当前还直接通过 Editor 侧 `Localizer` 生成本地化诊断，因此是 **Editor-only**。若将来确有加载期/runtime 校验需求，应先把“规则结果 token”与“Editor 本地化呈现”拆开，不能直接把现文件移进 Runtime。

> **模块文件夹（开发规范 C16）**：每个 asmdef 根下按模块开子文件夹，不平铺——`Runtime/` 分 `Graph`·`Units`·`Blackboard`·`Localization`；`Editor/` 分 `Window`·`Inspector`·`Data`·`Validation`·`Controls`·`Support`。asmdef 递归编译，子文件夹不影响编译/命名空间/GUID。下表「文件」列即按此布局给路径。

---

## 2. 分层架构

下表编号只用于本文内部表达依赖顺序：**数据契约是根 → runtime/validation 依赖数据 → editor canvas 依赖数据 → interaction/debug 依赖 canvas → shell 最终装配**。

| 层 | 文件 | 职责 |
|---|---|---|
| **4a 数据** | [Runtime/Graph/NodeDataTypes.cs](Runtime/Graph/NodeDataTypes.cs) | `TypeRef`/`Arity`/`PortDef`/`ParamDef`、`NodeDefinition`(抽象)、`NodeInstance`/`Connection`/`ParamOverride`/`ObjectOverride`、`GraphType`/`AuthoringFamily`/`IAuthoringAsset`、`ParamResolver`、`NodeMenuAttribute` |
| 4a 资产 | [Graph/NodeGraphAsset.cs](Runtime/Graph/NodeGraphAsset.cs) · [Graph/NodeRegistry.cs](Runtime/Graph/NodeRegistry.cs) · [Blackboard/BlackboardAsset.cs](Runtime/Blackboard/BlackboardAsset.cs) | 三个 SO，各一文件（硬规则 #1） |
| **4b 运行时** | [Runtime/Graph/NodeRuntime.cs](Runtime/Graph/NodeRuntime.cs) · [Units/Units.cs](Runtime/Units/Units.cs) | `Status`/`NodeContext`/`StepResult`、`ITickNode`/`IDataflowNode`/`IControlFlowNode`、`IRuntimeGraph` + 可选 `IRuntimeGraphSource` 图所有权、`Unit` 四族基类体系（含内联可组合入口）；`Units.cs` = 全局通用具体单元（比较/逻辑/算术/设变量/装饰器）+ `UnitValues` 值助手 |
| **4c 校验** | [Editor/Validation/GraphValidator.cs](Editor/Validation/GraphValidator.cs) | `ValidationIssue` + 8 个 Check + `ValidateAll` + `Extensions` 钩子 |
| **5a 画布** | [Editor/Window/GraphCanvasView.cs](Editor/Window/GraphCanvasView.cs) · [Window/NodeViewControl.cs](Editor/Window/NodeViewControl.cs) | GraphView 画布适配层、`NodeView`/`PortView`/`EdgeView`、剪贴板编解码、节点自定义视图扩展点 |
| **5b 交互** | [Editor/Window/AddNodeSearchWindow.cs](Editor/Window/AddNodeSearchWindow.cs) · [Inspector/InspectorPane.cs](Editor/Inspector/InspectorPane.cs) · [Window/GraphListPane.cs](Editor/Window/GraphListPane.cs) · [Support/InteractionSupport.cs](Editor/Support/InteractionSupport.cs) | 空格加节点搜索、类型化检视面板、分层变量面板 `LayeredVariablePane`（全局/模块/组 档位 + 按类型内联编辑变量默认值）、图列表面板（按 module 分组 + 搜索过滤 + 新建（盖组并配齐黑板）/删除 + 双击定位，点行回调让窗口加载该图）、各 Locator/弹窗 |
| **5c 调试** | [Editor/Support/GraphDebugger.cs](Editor/Support/GraphDebugger.cs) | play 模式状态着色/高亮、断点、运行时小部件、编辑期校验标记绘制 |
| **5d 外壳** | [Editor/Window/NodeEditorWindow.cs](Editor/Window/NodeEditorWindow.cs) · `Editor/Support/` 单类型文件（[NavigationHistory](Editor/Support/NavigationHistory.cs)·[Breadcrumb](Editor/Support/Breadcrumb.cs)·各 locator·[RuntimeGraphLocator](Editor/Support/RuntimeGraphLocator.cs)·[RuntimeGraphRegistry](Editor/Support/RuntimeGraphRegistry.cs)——原 EditorSupport.cs 已按一类型一文件拆分） | 多面板 `EditorWindow`（图列表/变量 ｜ 画布 ｜ 检视 + 工具栏/面包屑）、装配 5a/5b/5c、导航历史/面包屑、locator、domain-reload 安全的运行时挂接 |
| **横切** | [Runtime/Localization/Localization.cs](Runtime/Localization/Localization.cs) · [Localization/LocalizationTable.cs](Runtime/Localization/LocalizationTable.cs) · [Localization/EditorLocalizationConfig.cs](Runtime/Localization/EditorLocalizationConfig.cs) · [Localization/RuntimeLocalizationConfig.cs](Runtime/Localization/RuntimeLocalizationConfig.cs) · [Editor/Support/Localizer.cs](Editor/Support/Localizer.cs) | i18n（见 §7） |

---

## 3. 核心数据模型（4a）

- **`NodeDefinition`（抽象 SO）= 节点的"类型"。** 子类**用代码声明接口**：重写 `Define()`，里头调 `Meta(name, role)` / `AddIn` / `AddOut` / `AddParam` / `AddParam` 等。编辑器工具实例化每个子类、调 `RebuildFromCode()` 把它烘成 `.asset`。可选重写 `StableId` 给一个确定性 id（由类型派生），这样定义资产重建后图仍能解析到同一个 id。
- **`NodeInstance` = 节点的"实例"**（画布上的一个节点）。持有 `definitionId`、`position`、`connections`、`parameterOverrides`（字符串型覆盖）、`objectOverrides`（真实 `UnityEngine.Object` 引用，构建安全）、每节点 `displayName`/`note`、以及 `pinned`（钉住=固定不可删节点，如对话图的进入/退出；机制在框架、策略在领域层——见 §6 缝）。
- **`NodeGraphAsset`** = 一张图：`instances` + `entryInstanceIds`（入口侧列表）+ `graphType`。实现 `IAuthoringAsset`（报告 `WireGraph`）。
- **`NodeRegistry`** = 节点池：`universal` + `projectDomain` **两档定义（节点分层）**，`Find(id)` 跨两档解析。**每项目假设只有一个**（`NodeRegistryLocator` 发现多个会警告）。
  - **🔑 节点分层规则（解耦，逐条核）**：`universal` = **框架通用节点**（领域无关、任何领域可复用），其 `NodeDefinition` 子类放**框架** `NodeEditor/Runtime`、由框架侧装配填入 `universal`；`projectDomain` = **领域/模块自有节点**，其子类放**各自模块**（如对话节点 `DialogueNodeDefinition` 放 `Dialogue/Runtime/Nodes`），由该模块的生成器填入 `projectDomain`（如 `DialogueSetup` **只掌管 `projectDomain`、绝不动 `universal`**）。**框架不得定义任何领域节点；领域不得把自己的节点塞进 `universal`。** 通用的「节点定义只读查看器」是框架基建（零领域语义），放 `FrameworkDataSources`（`NodeDefItems`/`BuildNodeDefDetail`/`BuildNodeDefsView`）；框架与各领域的节点数据源**各自复用它、按所在档分层展示**——框架源「全局节点定义」(项目作用域 = `universal`)、领域源如「对话节点定义」(领域作用域 = `projectDomain` 中本领域的节点)，互不耦合。
- **`BlackboardAsset`** = 变量声明表（分层）。**作用域 = 这块 asset 在层级里的位置**，由两个标签字段表达：`module`/`group` 皆空=**全局**（每项目一块）｜带 `module`=**模块**档｜带 `module`+`group`=**组**档。`VariableDef` 不再带 scope 字段——在哪块 SO 里编辑就是哪个作用域。一张图的「有效黑板」由 **`BlackboardSet`** 把适用各档按「全局→模块→组」合并（同名 key **就近覆盖**，更专一档胜出）；编辑期 `BlackboardLocator.ResolveFor(graph)` 按图的 `module`/`group` 标签 `FindAssets` 出各档，运行期由 `DialoguePlayer.blackboards`（显式引用，无 AssetDatabase）传入。运行播种 / 校验 / 检视面板「键」下拉都读这个合并视图。**运行期每实例存储**统一为框架 [`RuntimeBlackboard`](Runtime/Blackboard/RuntimeBlackboard.cs)（Dictionary + `BlackboardSet` 播种 + `Declared` 声明视图；快照字符串化经 `UnitValues.ToInvariantString`），三领域黑板为薄继承（保类型名兼容存档）。
- **`TypeRef`** = 类型词汇表（Primitive/Enum/BlackboardKeyRef/Object/List/Any）。`element` 用 `[SerializeReference]`（否则 Unity 触发"Serialization depth limit 7"并把每个 TypeRef 深拷 7 层）。`TypeRefCompat.Compatible(a,b)` 是连线/边类型校验的判据（数值互通、Any 通配）。
- **`Arity`** = 端口连接数量约束（Exactly/AtLeast/Range/Optional/Many）。⚠️ `PortDef` 默认给 `Many`——**作者每个端口都要显式设 arity**，否则 struct 零值是 Exactly-0，会拒绝一切连接。**arity 同时决定单/多连线**：上界 ≤1（`ExactlyOne`/`Optional`）→ `Capacity.Single`（单连线，落新边会挤掉旧边）；其余（`Many`/`AtLeastOne`/上界>1）→ `Capacity.Multi`（多连线）。画布据此把两种口画成不同形状/颜色（见 §6）。
- **`ParamResolver`** = 参数解析：**实例覆盖优先；缺失则从定义当前默认值回填**（实现版本回填契约，定义升版不会无声破图）。`ResolveObject` 取 Object 型引用。

---

## 4. 运行时契约（4b）—— 身份与行为正交

**这是全套最重要的设计点。**

- **`Unit` 基类体系声明"身份/角色"**：`ActionUnit`（副作用）/`ConditionUnit`（返回 bool）/`ProviderUnit`（取值无副作用）/`ControlUnit`（编排子单元）。`Role` 是 `sealed override`——子类不能改/混角色，这是"一个节点一种角色"的语言层强制。
- **运行时接口声明"行为词汇"**：`ITickNode`（行为树）/`IDataflowNode`（数据流）/`IControlFlowNode`（控制流，Enter/Execute/Exit + `StepResult`）。**当前 Dialogue/Task 产品并未用这三组接口分派节点**，而是各自的 runner 按领域 Kind 解释图；因此它们是预留 contract，不应被文档描述成已经接通的执行引擎。
- **正交原则仍成立**：若未来某个真实 runner 采用这些接口分派，同一个 `ActionUnit` 可在 tick-tree 里实现 `ITickNode`、在 control-flow 里实现 `IControlFlowNode`；**基类绝不绑运行时接口**（否则一个 Action 就只能用于一种图）。在出现真实 consumer + contract tests 之前，不要围绕这些预留接口再增加新的抽象。
- **运行时与编辑器调试器只走框架契约**：`IRuntimeGraph` 提供 `StatusOf(instanceId)` + `RuntimeNodeOf(instanceId)`；独立的可选 `IRuntimeGraphSource.OwnsGraph(NodeGraphAsset)` 提供图身份，不破坏已有第三方 `IRuntimeGraph`。窗口必须按当前资产选择 runner，不能拿最后注册者猜测。领域层 runner 实现其真实 root/活动/调用栈子图所有权，编辑器永不触碰领域内部。
- `Status` 有 4 个值，**永远别塌成 bool**：`None` 表示"本次运行还没轮到/没判断"，调试器把它画成暗淡的 `status-inactive`，区别于已运行出结果的 Success。

---

## 5. 校验引擎（4c）

`GraphValidator.ValidateAll(graph, registry, blackboard)` 返回 `List<ValidationIssue>`（Error/Warn + `target` + message）。**按 `graph.graphType` 分派**，这是框架"一套校验器服务四种图类型"的关键：

| Check | 跑在哪些 graphType | 作用 |
|---|---|---|
| `CheckArity` | 全部 | 端口连接数 vs `Arity`；同时消费 `ChildArity`/`PortType` 声明式约束 |
| `CheckReachability` | ControlFlow / TickTree / DependencyDag | 从 entry（或根节点）BFS，播种集并上定义声明了 `ReachabilitySeed` 约束的实例（source-only 播种源，如状态机 AnyState）；`RequiresEntryReachable` 节点不可达 = Error，否则 Warn |
| `CheckBlackboardKeys` | 全部（需传 bb） | 节点读/写的 key 未在黑板声明 = Warn |
| `CheckSingleRole` | **除** ControlFlow | Provider/Condition/Control 不得写黑板；Condition 恰好一个 Bool 输出；等等 |
| `CheckEntry` | ControlFlow / TickTree | 入口存在性；control-flow 可由单根隐式推导 |
| `CheckCycle` | Dataflow / TickTree / DependencyDag | 三色 DFS 查环（无环类型） |
| `CheckTickTreeShape` | TickTree | 恰好一个根 + 严格树（每个非根恰一父） |
| `CheckEdgeTypes` | Dataflow | 每条边源输出类型与目标输入类型兼容 |

- **图级问题**（如"没有入口"）用哨兵 `target = GraphValidator.GraphIssueTarget`——编辑器把它路由到**画布顶部横幅**（`GraphCanvas.SetBanner`），不刷 console。节点级问题则 `target = instanceId`，画成红/黄框。
- **领域扩展点**：`GraphValidator.RegisterExtension(id, fn)`，`fn(graph, reg, bb) → IEnumerable<ValidationIssue>`，在内置检查后跑（按稳定 id 注册，同 id 覆盖告警——B10）。**领域层加规则不用改框架**（对话层 `DialogueValidation` 在 `[InitializeOnLoad]` 静态构造里注册）。
- **连接规则扩展点**：`ConnectionRules.RegisterRule(id, fn)`（`Editor/Validation/ConnectionRules.cs`；按 id 注册、保序短路、同 id 覆盖告警，`UnregisterRule(id)` 供测试隔离）。领域侧不复制判定器——用框架泛型 **`ConnectionRuleMatrix<TDef,TKind>`**（[Editor/Validation/ConnectionRuleMatrix.cs](Editor/Validation/ConnectionRuleMatrix.cs)：出向/入向、端口专属优先、Include/Exclude、本地化拒绝消息统一 `val.conn*` 键）持矩阵数据，把 `s_Matrix.Evaluate` 注册进钩子，`fn(ConnectionContext) → ConnectionVerdict`，按**节点种类**层面判“哪种能接哪种”（类型 `TypeRefCompat`、数量 `CheckArity` 之外的第三轴）。与 `GraphValidator.RegisterExtension` 平行——机制在框架、规则在领域。两个消费方共用一份规则源：实时拖拽 `GraphCanvasView.GetCompatiblePorts`（被拒端口连不上）+ 事后兜底 `GraphValidator.CheckConnectionRules`（粘贴/老图/重连留下的非法边照样报错）。空表 = 不限制（默认行为不变）。对话层落点见 [`Dialogue/EXTENDING.md`](../com.graphtest.dialogue/EXTENDING.md) 配方 2b。

---

## 6. 编辑器装配（5a–5d）

- **`NodeEditorWindow`（5d）** = 多面板：工具栏 + 面包屑 +（左列竖切「图列表面板 / **分层变量面板** `LayeredVariablePane`」｜ 画布 ｜ 检视面板，用**嵌套 `TwoPaneSplitView`** 装配——`outer[inner|检视]`、`inner[leftColumn|画布]`、`leftColumn` 上下切「图列表/变量」）。检视面板与调试器吃当前图的合并黑板 `BlackboardLocator.ResolveFor(graph)`（`BlackboardSet`），变量面板按档编辑。菜单 `Tools/NodeGraph/<模块>`（或 `Tools/NodeGraph/Manager` 的 **Open Node Editor** 按钮进自由模式）开窗；Project 里双击 `NodeGraphAsset` 经 `[OnOpenAsset]` 打开。图列表面板（`GraphListPane`）的选行与工具栏对象选择框走**同一条加载路径**（`m_Nav.Push` + `LoadGraph`）；它按当前 module 从 `GraphCreationRegistry` 渲染全部显式创建配方按钮；没有显式配方时才回退一个通用「新建」，并始终带「删除」按钮（不放刷新——资产增删自动 Reload）。每个 `GraphCreateRecipe` 独立声明 id/文案/默认文件名、图目录、黑板目录和初始化器；Dialogue 注册一个配方，Task 注册任务线与步骤图两个配方。配方创建时**给图盖组标签、find-or-create 模块与组黑板**（准则 #15），落盘后立即打开；删除会确认，双击行在 Project 里定位资产。
  - **domain-reload 安全**：`m_Asset` 用 `[SerializeField]` 跨 reload 保留；`m_Registry`/`m_Blackboard`/普通字段会被清空，所以 `CreateGUI` 里走 `LoadGraph` 重新解析、并 `m_Nav ??= new()` 重建非序列化字段。
  - **运行时挂接靠有界持续轮询**：进 play 的 reload/runner 注册时机不确定，`PollForRuntime` 在窗口存活且处于 play 时按当前 `m_Asset` 调 `RuntimeGraphLocator.Find(asset)`；匹配者新增、注销或窗口切图时即解绑/重绑，退出 play / 关窗才拆轮询，绝不对错误 runner 永久停查。`CreateGUI` 允许同窗重建：首行仍须 `EditorUi.ConfigureWindow`，随后在覆盖字段前必须 `StopRuntimePoll()` → `DetachRuntimeBinding()`，清掉旧 debugger 的 update 订阅与脱树节点状态；末尾若仍在 play 再启动新轮询。
- **`GraphCanvas`（5a）= GraphView 画布适配层。** 业务侧不直接碰 GraphView；只有画布、搜索窗口、拖拽辅助等框架边界封装可引用 `UnityEditor.Experimental.GraphView`。`GetCompatiblePorts` 用 `TypeRefCompat` 实时做连线类型检查。复制/粘贴经 `serializeGraphElements`/`canPasteSerializedData`/`unserializeAndPaste` 三委托，纯逻辑在 `ClipboardCodec`：复制的 `NodeInstance` 快照包含标量参数、Object 引用、选中子图的出向连接和完整多态 Unit 树；粘贴重映射 id，并创建相互独立的 Unit 对象图。回归测试：`ClipboardCodecTests.BuildPasted_RoundTripsNestedUnitTree_WithoutSharingReferences`。⚠️ **Delete/Backspace 键删除必须显式赋值 `deleteSelection` 委托**（构造里接到内置 `DeleteSelection()`）——不赋值则按键删除命令静默无效（右键菜单的"删除"走 `DeleteSelection()` 方法、不经此委托，会让人误以为删除是通的，其实连线按 Delete 删不掉）。
  - **单连线 vs 多连线**：`PortView.Create` 依 arity 把端口建成 `Capacity.Single`/`Multi`，并挂 `ne-port-single`/`ne-port-multi` 类 + 本地化 tooltip（`ui.port.single`/`.multi`）。**形状**走 USS（`NodeEditorStyles.uss`：单=圆、多=方+粗边）；**颜色**走 `Port.portColor`（单=中性灰、多=强调蓝）——因为 Unity 的 `Port` 在 C# 里直接写连接点描边色，USS 的 `border-color` 改不动它，只能用 `portColor`（USS 仍能改 border-radius/width）。连线建立/重连统一走 `IEdgeConnectorListener.OnDrop → OnEdgeDropped`（不经 `graphViewChanged`），其中 `EnforceSingleCapacity` 实现单连线口"落新边挤掉旧边"的替换语义。⚠️ **拖拽落点不会自动 `Port.Connect`**——`OnEdgeDropped` 写完数据后必须 `EnsurePortConnected` 显式连两端，否则数据上连了、但 Unity 仍认端口未连接、连接点 cap 不点亮（"连上但没亮"，要等重载经 `CreateEdgeView/ConnectTo` 才亮）。
- **`InspectorPane`（5b）** 的 `EditorFor(pd, node)` 按 `pd.type.kind` 分派控件（黑板 key 可搜索下拉 / 枚举下拉 / Object 选择 / 带 bounds 的滑块 / 勾选 / 文本）。**写值一律走 `WriteOverride` → `NodeInspectorEdits`**（标脏 + 记 Undo，硬规则 #2）。有限固定集合走 `EnumDropdownField`（会把 legacy 当前值插回候选首位，避免旧图崩），大量/动态候选走 `SearchableDropdownField` + `StringSearchWindow`（CJK/长 key 保留完整原值）。
  - **有限集合一律用下拉 / 搜索下拉，不用手填字符串。** 候选来自 enum、SO、注册表或数据表时，都按有限值处理：黑板 key、对话行 key、Unit 类型、语言码（`LanguageOptions` 里的 `en`/`zh`…）等都不能裸 `TextField`。运行时字段即便仍是 `string`（为了兼容存档/数据库契约），编辑器也必须从单一来源 SO/registry 取候选；旧数据里的未知值只作为迁移期当前值插入列表首位，新增值必须从候选里选。
  - **本地化文本编辑一律用共享「语言下拉 + 全宽高文本区」控件。** 只显示当前下拉选中的一种语言，不把多语言并排塞进同一详情区，也不用 `Chinese (zh)` 这类 chip/标签挤占文本宽度；语言候选来自 `LanguageOptions`，切换语言只替换文本区内容，写入只落到当前选中的语言。
- **`VariablePane`（5b）** 声明**单档**黑板变量，列表放进 `ScrollView`（"+ 变量"钉底）。每行 = 键名 + **按类型内联编辑该变量的默认值**（`VariableDef.defaultJson`：Bool→`Toggle` / Int→`IntegerField` / Float→`FloatField` / 其余→`TextField`，数值用 `InvariantCulture`），写值经 `WriteDefault` 标脏 + 记 Undo（硬规则 #2）；键名 tooltip 走 `Localizer.VariableDesc`（`var.<key>.desc`，无则回退键名）。创建逻辑 `VariableCreatePopup.TryCreate` 纯静态（可单测）——**不再选作用域**，作用域 = 所绑那块黑板的档别（准则 #15）。
- **`LayeredVariablePane`（5b）** 是节点编辑器**左栏**的变量面板（取代单档 `VariablePane`）：顶部按当前图的 `module`/`group` 出「全局 / 模块 / 组」档位钮，下方一次只挂选中那一档的 `VariablePane`（默认最专档），让"编辑这张图的变量"直达它自己的图黑板。各档面板复用 `FrameworkDataSources.BuildLayerPane`（缺该档黑板给「新建」按钮）。
- **`GraphDebugger`（5c）** 干两件事：① `RevalidateAndPaint`——跑 `ValidateAll`、清旧标记、画节点红/黄框 + 图级横幅（**每次结构性编辑后由 `OnGraphChanged` 触发**）；② `AttachRuntime` 后在 `EditorApplication.update` 里按 `IRuntimeGraph.StatusOf` 着色、刷 `NodeViewControl.OnRuntimeUpdate`、命中断点 `Debug.Break()`。断点存 `SessionState`（跨进 play 的 reload 存活）。
- **`DataEditorWindow`（5d，第二个框架级窗口）** = 通用「数据层编辑窗口」，专职查看 / 改数据层（4a 的纯数据 SO，与画布/节点正交）。本身不认识任何具体数据：左列按归属作用域**项目 / 领域 / 单图**分组列出注册进来的 `IDataSource`；集合型源实现 `IListDataSource` 后，中列显示该源的条目列表，右列渲染选中条目的明细编辑；简单源仍可只实现 `IDataSource`，直接走单面板详情。两个入口、同一窗口、只差上下文：`Tools/NodeGraph/Manager` 的 **Node Editor Data** 按钮 = 总数据中心（看全项目所有数据、可新建）；`DataEditorWindow.Open(domain, graph)` = 从某编辑器工具栏开的领域窗口（绑当前图、过滤到 项目+该领域+该图）。数据源经 `DataSourceRegistry` 注册（见下方缝）——**框架填框架数据**（**全局变量**[项目] + **组变量**[单图，当前图的组档] + 本地化 + 图概览，`FrameworkDataSources`；黑板按档分源、共用 `BuildLayerPane`）、**领域填领域数据**（**模块变量**[领域，模块档] + 对话数据库 / 定义只读，`DialogueDataSources`）。三档黑板（全局/模块/组）由此分落 项目/领域/单图 三列，缺档给「新建」按钮。本地化表、黑板变量、对话数据库、节点定义都优先用 source / item list / detail 的三栏心智模型，避免在右侧详情里再塞一层下拉或小列表。共享编辑控件统一放 `Editor/Controls`，保持领域无关。写值同守硬规则 #2（`Undo`+`SetDirty`）。
- **NodeEditor 拥有 UI 标准（2026-07-09；完整规范见 [`UI-STANDARD.md`](UI-STANDARD.md)——扩展 UI 前必读，含组件目录/铁律/扩展清单，契约测试强制执行）**：`NodeEditorStyles.uss` 定义跨模块 tokens 与 shared classes（toolbar command/text/icon/toggle、`node-cue`、badge/chip、form row、banner、hover tooltip、data 三列状态）；`EditorUi` 暴露这些 class 常量和小型 helper；`NodeCueControl` 负责节点 cue 的标签、刷新、截断和两行稳定化。Graphite Premium Tool palette 保留深色画布和黑钛/石墨编辑面板，用统一深色节点标题、4px 角色窄脊线、细描边、选中描边与少量语义 accent 改善可读性，避免大面积高亮、彩色标题块或糖果色节点。**命令按钮**（toolbar 文本/图标按钮、添加/删除按钮、组合框箭头、hover-bar 按钮）必须走共享 premium button tokens（底色/hover/active/disabled/描边/高光/阴影）；**导航 chrome**（面包屑路径条 `breadcrumb-crumb/-sep/--current`、折叠箭头 `collapsible-arrow`、数据窗口左列 `data-scope-title`+`data-source-row`、图列表行 `graphlist-row`、作用域分段选择器 `ne-seg-bar/-btn`）走扁平导航契约——透明底、hover 软底、选中强调色软底 + 常驻透明脊线占位（选中不位移），契约由 `NodeEditorStyles_UsesFlatNavigationChrome` 锁定；领域模块不得自建平面黑矩形或亮蓝按钮皮肤，也不得把导航行重新做成凸起按钮。Dialogue/Task 等领域只继承/消费这些 contracts，不复制 USS token 或自建平行控件。本地化文本统一用 `EditorUi.CurrentLanguageTextRow` 的语言下拉 + 全宽高文本区，候选来自 `LanguageOptions`，禁止回退到语言 chip + 窄文本框或多语言挤在同一区。普通视觉颜色必须在 USS；C# 直接设色只允许 Unity API 例外（当前明确例外：GraphView `Port.portColor`）。所有可见 UI 文案、校验、报错走 `Localizer.UI`，CJK/长 key 通过共享 dropdown/detail row contract 保持不挤出、不丢值。

### 框架与领域层之间的缝（Locator / Registry）

框架不写死任何项目资产，全靠 `Editor/` 里的轻量 locator：

- `NodeRegistryLocator.Find()` / `EditorLocalizationLocator.Config()` / `LocalizationTableLocator.Find()` / `BlackboardLocator.FindGlobal()`：读取 `NodeEditorAssetPaths` 的对应路径；路径缺失、越界或存在歧义时失败关闭并列出候选。`NodeDefinitionLocator.ForType()` 只从该配置所指注册表解析具体类型，不扫描、猜测旧定义资产。
- `BlackboardLocator`（**分层**，准则 #15）：`FindGlobal()`（由配置指定）/ `FindLayer(module,group)` / `Resolve(module,group)`→`BlackboardSet` / `ResolveFor(graph)` / `CreateLayer(module,group,folder)` / `LayerFolder(module)`。模块/组档按 `module`/`group` 标签取档；不同标签可有多块（设计），同一标签出现多块则失败关闭。**新建档按分层落盘**：模块/组黑板进**该模块资产区**（`folder` 由调用方传入 = 该模块图所在文件夹，或 `LayerFolder` 推断），不进框架目录（准则 #14c / #15）。
- `RuntimeGraphRegistry`：它位于 `NodeEditor.Editor`，**领域 Runtime 不得直接引用或调用它**。正确接法是领域 Runtime 的 host 抛出 `IRuntimeGraph` 创建/销毁事件，领域 Editor 用 `[InitializeOnLoad]` bridge 订阅并转发到幂等 `Register/Unregister`；编辑器再经 `RuntimeGraphLocator.Find(currentAsset)` 从全部 live runner 中匹配 `IRuntimeGraphSource`。`Current` 只为旧消费者兼容，窗口不得依赖它。Dialogue/Task/StateMachine 均接通该模式；新增领域必须提供同类 host/bridge与所有权实现。
- `GraphCreationRegistry.Register(GraphCreateRecipe)` 是图创作的主接缝：同一 module 可注册多个显式配方，各自拥有文案、默认名、图/黑板目录与初始化器；`GraphListPane` 按稳定注册顺序渲染按钮并调用所选配方。`GraphListPane.RegisterModuleInitializer(module, init)` 只保留旧模块兼容；一旦 module 有显式配方，legacy 配方不再出现。Dialogue 有一个显式配方，Task 有 `task.line` / `task.steps` 两个。配方初始化器可在图落盘前设置 graphType、播种 pinned/entry 节点；`GraphListPane` 随后按配方的 `blackboardFolder` 创建分层黑板。`module` 同时驱动列表分组与 `OpenModule` 过滤（见准则 #11）。
- `NodeDefinitionAvailability.Register(id, rule)`（保序短路的节点可用性注册表）：NodeEditor 只拥有机制，领域在自己的 Editor 程序集中按 `module` / `graphType` 注册谓词。节点搜索、`GraphCanvas.CreateNode`、兼容端口过滤和 `GraphValidator.ValidateAll` 全部调用同一个 `Evaluate(graph, definition)`，所以交互创建、程序化创建、连接与已保存图校验不会漂移；消费者不得另写 module/graphType 分支。
- `DataSourceRegistry.Register(id, ctx => IDataSource)`（`Editor/Data/DataSources.cs`）：通用数据编辑窗口（`DataEditorWindow`，§6 上）的**数据源缝**，与 `GraphValidator.RegisterExtension` / `ParamChoiceProviders` 同款「框架留缝、各方填充」。每个源带一个 `DataScope`（项目/领域/单图）+ 领域标签；`Sources(ctx)` 按 `DomainFilter` 过滤（总中心显示全部，领域窗口只留项目级 + 本领域源，单图源未选图时工厂返回 null 即跳过）。框架自注册全局/组变量 + 本地化 + 图概览（`FrameworkDataSources`），领域注册模块变量 + 自己的数据（`DialogueDataSources`）。常见单面板源可用 `DelegateDataSource`；包含可编辑记录列表的源用 `DelegateListDataSource` / `IListDataSource`，把记录发现放进 `Items(ctx)`，把单条编辑放进 `BuildDetail(ctx,item)`，让数据窗口稳定呈现左源 / 中列表 / 右明细，不在详情里再套下拉。⚠️ Unity 6 的 UI Toolkit 也有个同名 `DataSourceContext`，领域文件同时 `using` 两个命名空间时加 `using` 别名消歧。
- `DataSourceRegistry`、`ParamChoiceProviders`、`ParamReferenceEditors` 的同 id 注册统一为「发出 warning 后由后注册者覆盖」，并各自提供按 id 精确 `Unregister`；测试和热重载清理由调用方只注销自己拥有的 id，禁止 `Clear()` 抹掉别的模块注册。

---

## 7. 本地化横切层

解析优先级（`Localizer`）：**属性(当前语言) → 表(当前语言) → 属性(英文) → 表(英文) → 英文缺省**。

- **节点/参数文案**：优先用节点类上的 `[NodeDoc(Language, name, desc)]` / `[ParamDoc(Language, param, name, desc)]`（[Localization.cs](Runtime/Localization/Localization.cs)）；没写则查 `LocalizationTable` 的 key（`node.<id>.name`/`.desc`、`param.<id>.<p>.name`/`.desc`）；再没有回退英文（`Meta()` 的名字 / 调用方给的缺省）。
- **编辑器界面 chrome 文案**：调用处写 `Localizer.UI("ui.xxx", "English default")`，中文在 `LocalizationTable` 里种。
- **黑板变量注释**：变量面板里每个变量的 tooltip 走 `Localizer.VariableDesc(key)`——**仅查表**（key = `var.<key>.desc`，当前语言 → 英文回退，无属性来源）；领域层在 Setup 里种（对话层用 `EnsureVarDesc`）。
- **配置**：`EditorLocalizationConfig.language`（编辑器显示语言，默认中文）+ `RuntimeLocalizationConfig.language`（运行时玩家可见文案）。`language` 是枚举字段→检视面板自动渲染下拉。
- **切语言**：工具栏语言下拉写回 `EditorLocalizationConfig.language` + `EditorLocalizationLocator.Invalidate()` + 重建窗口，整套 UI 即时本地化。
- **加一种语言**：① `Language` 枚举加值；② `LanguageCodes.Code` 加映射；③ 给节点补 `[NodeDoc(新语言, …)]` / 表里补该语言条目 / 运行时内容库补该 lang。

---

## 8. 在框架之上搭一个新领域层（最常见的"用框架"姿势）

以"做一个行为树编辑器"为例，**不改框架**，只在新目录（如 `Assets/BehaviorTree/`）里：

1. **节点定义**：每种节点一个 `<X>Node.cs`（一类一文件，硬规则 #1），继承 `NodeDefinition`（或经领域基类），重写 `Define()` 声明端口/参数/角色，挂 `[NodeMenu]` + `[NodeDoc]`/`[ParamDoc]`。`TypeCache` 让它**自动出现在空格搜索框**，无需手配菜单。
2. **运行时语义**：写一个 runner 实现 `IRuntimeGraph`；要接 play 调试还应实现 `IRuntimeGraphSource`，让 `OwnsGraph` 覆盖真实 root、当前图与活动/调用栈子图。若该 runner 明确选择接口分派，再让节点实现对应行为接口（如 tick-tree 的 `ITickNode`）；当前 Dialogue/Task 的 Kind-switch 解释器不消费这些预留接口。Runtime host 只抛 runner 创建/销毁事件；领域 Editor 的 `[InitializeOnLoad]` bridge 转发到 `RuntimeGraphRegistry.Register/Unregister`。参考 `DialoguePlayer` + `DialogueRuntimeBridge`，不要让 Runtime 直接引用 Editor registry。
3. **生成/再生资产（幂等）**：`Tools/NodeGraph/Manager` 里各模块卡片的 **Setup Assets** 按钮。定义/注册表管线直接用框架 **`DomainSetupPipeline`**（[Editor/Support/DomainSetupPipeline.cs](Editor/Support/DomainSetupPipeline.cs)：`SetupDefinitions<TDef>` 反射发现 + 一类型一 .asset + 坏资产失败关闭 + 永远 `RebuildFromCode()`；`MergeIntoRegistry<TDef>` 只接管本域 projectDomain 条目），并先调 **`FrameworkSetup.EnsureCoreAssets`**（[Editor/Support/FrameworkSetup.cs](Editor/Support/FrameworkSetup.cs)：本地化表/语言选项/双配置/全局黑板 + 框架种子——领域 Setup 自足，不依赖别的模块先跑）。所有资源目的地来自唯一项目级 `NodeEditorAssetPaths` / `<Domain>AssetPaths`；默认目录只是首次建议，不是契约。用户可把每项改到任意规范化的项目 `Assets/` 子目录，Setup 不自动迁移或覆盖既有作者资产。配置重复、路径越界、类型错误或坏资产 `m_Script:0` 时失败关闭并报告；作者确认并手工移开冲突文件后再运行。创建与重建都必须 Undo + SetDirty。产品测试跟随当前配置，临时测试资产例外。产品 Setup 不生成或引用本地演示内容。
4. **图类型**：在 `NodeGraphAsset.graphType` 选 `TickTree`（行为树）——校验器会自动启用环检测 + 严格树形状检查（§5）。
5. **校验扩展**：`GraphValidator.RegisterExtension(id, ...)` 加领域规则（§5）。
6. **（可选）节点线索 / 运行时小部件**：`[NodeViewControl(typeof(XNode))]` 子类，`OnAttach` 加画布上的内联 UI，`OnRuntimeUpdate` 在 play 模式每帧刷新。
7. **（可选）数据源**：把领域自己的数据资产接进通用数据编辑窗口（`DataEditorWindow`）——`[InitializeOnLoad]` 里 `DataSourceRegistry.Register(id, ctx => IDataSource)`，选 `DataScope`（项目/领域/单图）。单面板源用 `DelegateDataSource`；有记录列表的源用 `DelegateListDataSource` / `IListDataSource`，按 `Items(ctx)` + `BuildDetail(ctx,item)` 拆成中列列表和右侧明细。框架已自带 全局变量/组变量 + 本地化 + 图概览，领域补模块变量 + 自己的（参考 `Dialogue/Editor/DialogueDataSources.cs`：模块变量复用 `FrameworkDataSources.BuildLayerPane`，对话数据库/节点定义走列表明细）。
8. **领域层自己的文档**：内容设计师 README + 开发者 EXTENDING（工程准则 #2）+ 面向外部模块的 INTEGRATION 接入指南（工程准则 #9——怎么从别的系统驱动运行时、调编辑器 UI、与别的模块协作；范本见 `Dialogue/INTEGRATION.md`）。运行时对接缝（本节第 2 步的 `IRuntimeGraph`/`RuntimeGraphRegistry`）就是 INTEGRATION「框架级可复用缝」一节的来源。

**强默认：每个领域/模块是它自己独立的 `EditorWindow`**（工程准则 #4）；只有数据真嵌套、需频繁来回时才并进同一 shell。

---

## 9. 给框架本身扩功能（改框架时）

| 想给框架加什么 | 改哪里 | 注意 |
|---|---|---|
| 一种新基元类型（如 `Vector4`） | `PrimitiveType` enum + `TypeRefCompat.Compatible` + `InspectorPane.EditorFor` + `VariablePane.ValueEditorFor` 各加 case | 四处都要动，否则有类型但参数/变量编不了 / 不参与兼容判定 |
| 一种新图类型 | `GraphType` enum + `GraphValidator.ValidateAll` 的分派 + 必要的新 Check | 想清楚它是 entry 驱动还是 pull 模型（影响可达性/入口规则） |
| 一条新的内置校验 | `GraphValidator` 加 `CheckXxx` + 在 `ValidateAll` 按 graphType 接上 | 节点级填 `instanceId`，图级填 `GraphIssueTarget`（走横幅） |
| 一类连接规则缝（按节点种类挡连线） | `Editor/Validation/ConnectionRules.cs`（注册表 + `Evaluate`）；消费方 `GetCompatiblePorts` + `CheckConnectionRules` | 与 `GraphValidator.RegisterExtension` 平行；领域层只注册规则、不碰框架；空表=不限制 |
| 一个新的检视面板控件 | `InspectorPane.EditorFor` 的 `switch` 加 case | 写值走 `WriteOverride`；下拉用 `SafePopup` |
| 一个工具栏按钮 / 快捷键 | `NodeEditorWindow.BuildToolbar()` / `CreateGUI()` 里 `RegisterCallback<KeyDownEvent>` | 工具栏在 `m_Canvas` 之前构建，回调里引用 `m_Canvas` 要判空/延到点击时取 |
| 一个新面板 | `NodeEditorWindow.CreateGUI()` 多套一层 `TwoPaneSplitView` | 面板 UI 走 `Localizer`，写数据走 `SetDirty`+`Undo` |
| 一种新数据源（进通用数据窗口） | `[InitializeOnLoad]` 里 `DataSourceRegistry.Register(id, ctx => IDataSource)`（`Editor/Data/DataSources.cs`；单面板用 `DelegateDataSource`，列表明细用 `DelegateListDataSource`） | 与 `GraphValidator.RegisterExtension` 平行；选 `DataScope` 项目/领域/单图；集合型源实现 `IListDataSource`，让窗口按左源 / 中列表 / 右明细渲染；同名 `DataSourceContext` 用别名消歧 |
| 一种新的运行时接口 | `NodeRuntime.cs` 加接口 | **基类绝不绑它**（保持身份/行为正交，§4） |

---

## 10. 关键不变式（评审 / 改框架时逐条核）

**评审清单即开头「⚠️ 开发规范」的 A/B/C/D 四组——逐条核对那里，不在此重复罗列。** 改框架时高频踩的几条速记：依赖单向（B5）、GraphView 只在框架边界封装里（B6）、一类一文件（A1）、序列化写入 SetDirty+Undo（A2）、`Role` 保持 `sealed override`（B7）、locator 每项目一个（B9）、UI 文案走 `Localizer`（C11）。每条的完整理由 + 代码落点见「开发规范」节。

---

## 11. 验证约定

- 发布内容只包含 Runtime、Editor 与现行文档，不依赖任何测试程序集。集成项目应为自己的扩展建立 EditMode 覆盖，并把它放在产品程序集之外。
- **优先把"棘手但纯"的逻辑抽成不依赖 panel / GraphView 的静态类**再测：`TypeRefCompat`、`ParamResolver`、`GraphValidator.*`、`ClipboardCodec`、`NodeInspectorEdits`、`FindDialog.TitleMatches`、`VariableCreatePopup.TryCreate` 都是这么设计的。
- ⚠️ **内存测试 / 编译看不见两类产物级缺陷**：① MonoScript 绑定与资产保存/重载；② 编辑器面板写数据是否标脏。这两类**只有真建资产 + 真开窗 + 真交互**才暴露；发布前必须补真实 Unity 资产往返、Undo/Redo、双主题窗口与 Play Mode 生命周期验收。

---

## 12. 速查：从需求到一个能用的编辑器

```
需求 → 选 AuthoringFamily（连线图 / 声明式）→ 选 GraphType（4 选 1）
     → 写 NodeDefinition 子类（[NodeMenu]/[NodeDoc]，一类一文件）
     → 实现 IRuntimeGraph + 运行时接口
     → Setup 菜单生成 Registry/Blackboard/Defs/本地化（幂等，删后现造）
     → GraphValidator.RegisterExtension 加领域校验
     → 真开 Tools/NodeGraph/<模块> 窗口，真交互核验（选中/悬停/切语言/重载/console 干净）
     → 写四份指南（设计师 README + 开发者 EXTENDING + 集成者 INTEGRATION + 框架 ARCHITECTURE）
```

改完任何东西，**真打开窗口走一遍真实操作**再算完成。
