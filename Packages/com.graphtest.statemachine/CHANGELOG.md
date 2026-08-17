# 更新日志 / Changelog

## [0.1.8] - 2026-08-17

### 中文

**AI 和人写的是同一张图。** 新增创作访问层；既有 API 一个字节未改。

- **一个创作入口。** 想用脚本或提示词生成一张图，过去只能自己把 `NodeGraphAsset` 镜像成一份手写 JSON，而每份镜像都在同样三处丢东西：完整 Unit、分层黑板、编辑器布局。现在 `NodeEditor.EditorUI.GraphAuthoringAssetAccess` 提供 `List / Describe / Read / CreateDraft / Write / Validate`：资产仍是唯一真源，文档只是一次读改写事务途中的样子，不是需要同步的第二份资产。
- **只读的真的只读。** `Read`、`List`、`Describe`、`CreateDraft` 不会创建路径配置、目录、图或黑板，也不会因为「看了一眼」把已有资产标脏。新图不必再拿 seed 图克隆出来假装：`CreateDraft` 直接给出空 `graphId`、`MustNotExist` 修订，以及全局 → 模块 → 组的完整有效黑板闭包。
- **改名不动身份。** `authoringKey` 持久化在 `NodeInstance` 上，是图内的作者地址；`instanceId` 仍是连线与存档依赖的运行时身份，重命名只移动 key。老图缺 key 时只在返回文档里按 `instanceId` 确定性回填，不改资产；第一次成功 `Write` 才落盘。
- **图和黑板一起成功，或者一起不动。** 一次提交横跨图资产和至多三层黑板，前置条件因此是修订向量而不是单个修订：每个 owner 带 `ownerId`、规范 `ownerPath`、`contentHash` 和 `expectedState`。任一 owner 过期，整批按 owner 报冲突且零写入；过了 preflight 之后，图与所有黑板在同一个 Undo 组里，失败整体回滚，连新建的资产和目录都清掉。
- **先查目录，再写内容。** Unit 用稳定的 `[UnitAuthoringId]` 而不是 CLR 类型名，`Describe` 会把每个 Unit 的必填字段、标量类型、枚举候选和嵌套约束一并给出。目录和导入器共用同一个字段发现器——「目录说能写」和「导入器肯收」不会各说各话。
- **JSON 严进。** 注释、重复属性、未知属性、大小写不精确的属性名、单引号、尾随逗号、`NaN`/Infinity、整数形式的枚举、一个根值之后的多余内容，全部拒绝。数字在 Newtonsoft 解析之前先按 RFC 8259 做词法校验：`0x10`、`010`、`.5`、`1.` 会被 Newtonsoft 归一化成普通数值，事后按 token 检查已经分辨不出来。
- **领域模块自己登记。** 每个模块注册一份 `GraphAuthoringModuleDescriptor`，图根只调自己那份 `*AssetPathsLocator`，领域 Unit 留在领域运行时包里。NodeGraph 不反向引用任何领域程序集。
- **命令行是同一套调用。** `GraphAuthoringCommandLine` 把这六个调用搬到 Unity batchmode，输出统一 envelope（`command` / `data` / `diagnostics` / `succeeded`）。未知的、或不适用于当前命令的 `-graphAuthoring*` 参数会失败，不会被静默忽略。
- **新增 `AI-AUTHORING.md`**：工作流、身份规则、修订向量、JSON 约定与领域扩展步骤；三份模块 `EXTENDING.md` 都指向它。

### English

**AI and people edit the same graph.** A new authoring access layer; no existing API changed.

- **One authoring entry point.** Writing a graph from a script or a prompt used to mean mirroring `NodeGraphAsset` into a hand-rolled JSON shape, and every mirror lost the same three things: full Unit payloads, layered blackboards, and editor layout. `NodeEditor.EditorUI.GraphAuthoringAssetAccess` now offers `List / Describe / Read / CreateDraft / Write / Validate`. The assets stay the only source; the document is what one read-modify-write transaction looks like on the way through, not a second asset to keep in sync.
- **Read-only really is read-only.** `Read`, `List`, `Describe` and `CreateDraft` never create a path configuration, folder, graph or blackboard, and never dirty an existing asset just because something looked at it. A new graph no longer has to be faked by cloning a seed: `CreateDraft` returns an empty `graphId`, a `MustNotExist` revision, and the full global → module → group blackboard closure.
- **Renaming does not move identity.** `authoringKey` persists on `NodeInstance` as the in-graph author address; `instanceId` stays the runtime identity that connections and saves depend on, and renaming moves the key only. Graphs written before this get a deterministic backfill by `instanceId` in the returned document, not in the asset; the key reaches disk on the first successful `Write`.
- **The graph and its blackboards commit together or not at all.** One commit spans the graph asset and up to three blackboard layers, so the precondition is a revision vector rather than a single revision: each owner carries `ownerId`, canonical `ownerPath`, `contentHash` and `expectedState`. One stale owner fails the batch with a per-owner conflict and writes nothing; past preflight, the graph and every blackboard share one Undo group and roll back together, created assets and folders included.
- **Query the catalog, then author.** Units carry stable `[UnitAuthoringId]` values instead of CLR type names, and `Describe` returns the required fields, scalar types, enum values and nesting constraints for each one. The catalog and the importer share a single field discoverer, so "the catalog says you can write it" and "the importer accepts it" cannot drift apart.
- **Strict JSON on the way in.** Comments, duplicate properties, unknown properties, case-inexact names, single quotes, trailing commas, `NaN`/Infinity, integer-valued enums and any content after the root value are rejected. Numbers are lexed against RFC 8259 before Newtonsoft parses them: it normalises `0x10`, `010`, `.5` and `1.` into ordinary values, and a token-level check afterwards can no longer tell.
- **Domain modules register themselves.** One `GraphAuthoringModuleDescriptor` per module, graph roots read from that domain's own `*AssetPathsLocator`, domain Units declared in the domain's runtime package. NodeGraph references no domain assembly.
- **The same calls from batchmode.** `GraphAuthoringCommandLine` exposes all six through one envelope (`command` / `data` / `diagnostics` / `succeeded`). Unknown or inapplicable `-graphAuthoring*` arguments fail instead of being silently ignored.
- **New `AI-AUTHORING.md`** covering the workflow, identity rules, revision vector, JSON contract and domain extension steps; all three module `EXTENDING.md` files link to it.

## [0.1.7] - 2026-08-16

### 中文

**装只占一个目录，卸干净得掉。** 两处行为变更，已有工程不受影响。

- **单一安装根。** 设置资产曾固定去写死的 `Assets/NodeEditorSettings/`，各模块内容固定去 `Assets/<模块>Content/`——装齐四个包，`Assets/` 根目录平白多出五个文件夹，工程无从干预。现在只有一个可配置的安装根，默认 `Assets/NodeGraph/`，设置在 `Settings/`、各模块内容在同名子目录下。安装根存在 `ProjectSettings/NodeGraphInstall.json`，在 **安装路径 / Install Paths** 窗口最上面那一栏改。
- **已有工程不动。** 老位置**真有设置资产**、且没写过 `NodeGraphInstall.json` 的工程，路径与升级前逐字一致。判据不是「老目录还在」——空掉的 `Assets/NodeEditorSettings/` 到处都是，拿它当判据会把干净工程也永久钉在分散布局上。否则「升级即换根」会让配置定位器在旧资产之外再造一份，直接撞上「找到多个配置资产」的关门错误。
- **卸载先清理。** `Remove` 曾经只做一件事——移包；Setup 生成的资产、空掉的目录、`NodeEditor.*` 那批 EditorPrefs 全部留在工程里。现在 Remove 先弹 **卸载清理 / Uninstall Cleanup**，逐条列出后确认才删，再移包。残留面直接读模块自己的 `*AssetPaths` 配置，不另开一份会漂移的清单。
- **被引用的资产默认不删。** 清理窗口单列「被工程其它资产引用」一栏，删了会在别处留下丢失引用，要删得显式勾选。引用扫描被中断时，「无人引用」这个结论作废并显式告警。
- **框架自己的账本不算「外部引用」。** 共享 `NodeRegistry` 当然引用着各模块的节点定义；把它算成外部引用，卸对话时那 10 个定义会一个都删不掉——清理等于没做。引用检查现在排除安装根（legacy 时排除各模块声明落点）下的资产，只有真正的业务引用才拦得住。
- **清理后摘掉注册表空槽。** 节点定义被删后共享注册表会留下 Missing 槽位；清理末尾一并摘除，注册表才真回到没装这个模块之前的样子。
- **框架的清理入口。** 框架不能在 Manager 里移除自己，其卡片新增 **Clean Up Generated Files**：清完生成物再去 Package Manager 移包。
- **菜单栏不再多出一栏。** 「升级子图引用」「收集子图」两条维护命令原先挂在顶层 `NodeEditor/` 下——菜单路径没有现成的父级就会自己开一个，于是为两条命令在 Tools 旁边单立了一栏。现在它们在 **Tools/NodeGraph/Maintenance/** 下，和 Manager、各模块编辑器同处一棵树。Assets ▸ Create 里的 `NodeEditor/` 分组不变。

### English

**One folder to install into, and a clean way out.** Two behavior changes; existing projects are untouched.

- **Single configurable install root**, `Assets/NodeGraph/` by default, replacing the hardcoded `Assets/NodeEditorSettings/` plus one `Assets/<Module>Content/` folder per module. Stored in `ProjectSettings/NodeGraphInstall.json`; edited at the top of the **Install Paths** window.
- **Projects that still have `Assets/NodeEditorSettings/` keep their existing paths** until a root is pinned explicitly, so upgrading cannot strand the assets they already generated.
- **Remove now runs Uninstall Cleanup first**, listing generated assets, emptied folders, editor preferences, and project settings before deleting them and then removing the package. The residue set is read from each module's own `*AssetPaths` configuration.
- **Assets still referenced elsewhere are kept by default** and listed separately; a cancelled reference scan invalidates the "unreferenced" grouping.
- **`Clean Up Generated Files`** on the framework card, since the framework cannot remove itself from the Manager.
- **No more top-level `NodeEditor` menu.** The two maintenance commands now live under **Tools/NodeGraph/Maintenance/** with the rest of the entry points, instead of opening a menu-bar heading of their own for the sake of two items. The `NodeEditor/` grouping under Assets ▸ Create is unchanged.

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

- 为状态机节点注册简洁的共享语义图标，并自动继承框架的金属底座、双主题节点层次和整节点运行态照明。
- 将 State Machine Basics 合并到领域包的 `Samples~`，可从 Package Manager 或 NodeGraph Manager 直接导入。

### English

- Registered concise shared semantic icons for State Machine nodes, inheriting the framework metal base, dual-theme depth, and whole-node runtime illumination.
- Embedded State Machine Basics in this package's `Samples~` directory for direct Package Manager or NodeGraph Manager import.

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

- 首个公开版本，提供基于 NodeGraph 的分层状态机运行时与编辑器。
- 包含入口、状态、转换、任意状态、子状态机和出口节点。
- 提供逐帧运行、子状态机栈、事件、快照、领域校验和连接规则。
- 可通过 `Tools/NodeGraph/State Machine` 或模块管理器完成初始化。

### English

- First public release of the NodeGraph-based hierarchical state machine runtime and editor.
- Includes Entry, State, Transition, Any State, Sub-Machine, and Exit nodes.
- Provides frame-based execution, nested state-machine stacks, events, snapshots, domain validation, and connection rules.
- Supports setup through `Tools/NodeGraph/State Machine` or the module manager.
