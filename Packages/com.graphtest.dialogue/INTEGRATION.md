# 对话编辑器 · 集成/接入指南（面向外部模块开发者）

> **UPM 边界（1.0）**：Dialogue 产品源码位于 `Packages/com.graphtest.dialogue/`，通过 GraphTest Module Manager 安装；项目可变数据仍位于可配置的 `Assets/DialogueContent`。下文 `Runtime/`、`Editor/` 路径均相对于 Dialogue package。
>
> 给**别的游戏模块**（任务系统 / 存档 / HUD / 对话 UI / 音频…）的程序员：怎么在运行时**驱动**这套对话、怎么**和你的模块合作**、以及怎么从别的工具**调用编辑器 UI**。你只是**用**这个编辑器的产物，不改它、也不写对话内容。
>
> **四份文档分工，按你的角色选**：
> | 你想做的事 | 看哪份 |
> |---|---|
> | **在别的模块里用对话**（订阅事件、跑对话、存档、开窗）→ 本文 | `Dialogue/INTEGRATION.md`（本文） |
> | 给编辑器**加功能**（新节点/面板/校验/调试视图） | [`EXTENDING.md`](EXTENDING.md) |
> | **写对话内容**（不写代码） | [`README.md`](README.md) |
> | 维护**框架本身** / 在框架上搭新领域编辑器 | [`../com.graphtest.nodeeditor/ARCHITECTURE.md`](../com.graphtest.nodeeditor/ARCHITECTURE.md) |
>
> 规则以 [`ARCHITECTURE.md` 的「开发规范」节](../com.graphtest.nodeeditor/ARCHITECTURE.md) 为唯一权威。运行时类型在 `Packages/com.graphtest.dialogue/Runtime/`，可进玩家构建；编辑器类型在 `Packages/*/Editor/`，仅编辑器期可用。

---

## 1. 运行时接入：在你的模块里跑一段对话

对话的运行时入口是 `DialoguePlayer`（[Runtime/Runner/DialoguePlayer.cs](Runtime/Runner/DialoguePlayer.cs)，`MonoBehaviour`，Runtime 程序集、不引 `UnityEditor`）。它很薄——构建一个 `DialogueRunner` 并转发调用；所有对话逻辑在 [Runtime/Runner/DialogueRunner.cs](Runtime/Runner/DialogueRunner.cs)。

**第一步：场景里挂 `DialoguePlayer`，装配六个字段**（Inspector 或代码）：

| 字段 | 类型 | 作用 |
|---|---|---|
| `graph` | `NodeGraphAsset` | 要播放的对话图（从 Start 节点起的 control-flow 图） |
| `registry` | `NodeRegistry` | 把 `definitionId` 解析为节点定义（驱动 Kind/参数）；使用 `NodeEditorAssetPaths.registryPath` 当前指向的资产 |
| `blackboards` | `BlackboardAsset[]` | 按全局→模块→组排列的分层黑板引用；同名 key 就近覆盖（更专的层级胜出）；只放一块=仅全局；运行器构造走 `new BlackboardSet(blackboards)` |
| `database` | `DialogueDatabase` | 按 `lineKey`/`optionKey` 取本地化台词/说话人/立绘/配音（可选，缺则 key 原样显示） |
| `localizationConfig` | `RuntimeLocalizationConfig` | 运行时玩家语言。设了就按它的 `Language` 枚举取文本；留空才用 `DialoguePlayer.language` 的 `Language` 枚举字段，再回退 DB 的 `defaultLang` |

**第二步：拿 `player.Runner`，先订阅四个事件，再 `Begin()`。** `Runner` 在 `Awake` 里构建，所以你的订阅代码要在 `Begin()` 之前跑。Runner 自己**从不渲染**——它只在 Line/Choice 处挂起、抛事件，由你的 UI 模块渲染：

```csharp
using Dialogue;

DialoguePlayer player = /* 场景里的组件 */;

// 显示一句台词：speaker/text 已按当前语言解析好，portrait/voice 可能为 null
player.Runner.OnLine += (DialogueLineView line) => {
    hudView.Show(line.speaker, line.text, line.portrait, line.voice);
};

// 显示选项列表：opts[i].index == i（已过滤掉不可见的 Option），回传给 Choose
player.Runner.OnChoices += (IReadOnlyList<DialogueOptionView> opts) => {
    choiceView.Show(opts);   // 每个 opt 有 .index 和 .text
};

// 见 §2.1：Action 节点的「触发事件」单元抛给你的任务/成就/音频系统
player.Runner.OnEvent += (string eventId, string arg) => questSystem.Handle(eventId, arg);

// 对话结束
player.Runner.OnEnd += () => hudView.Hide();

player.Begin();   // 同步走到第一个 Line/Choice 并抛出其事件后返回
```

**第三步：玩家交互回驱。** 确认当前台词调 `player.Advance()`（没停在 Line 上时是 no-op，双击/误触安全）；选了第 `i` 个可见选项调 `player.Choose(i)`（`i` 是 `OnChoices` 列表里的下标，越界 no-op）。

> 视图结构：`DialogueLineView { string speaker; string text; Sprite portrait; AudioClip voice; }`、`DialogueOptionView { int index; string text; }`（[DialogueRunner.cs:27](Runtime/Runner/DialogueRunner.cs)）。`text` 都已按当前语言解析，渲染时**无需**自己碰 `database`。

---

## 2. 和别的模块合作

### 2.1 抛事件 → 任务 / 成就 / 音频系统（最常用的协作点）

对话里放一个 **Action 节点**，在它的 `actions` 槽里选「触发事件（`FireEventAction`）」单元、填 `eventId`/`arg`；运行到该 Action 时 Runner **不阻塞**地抛 `OnEvent(eventId, arg)` 然后继续往下走（已无独立的 Event 节点——抛事件改由 Action 节点的可组合动作单元承担）。这是对话与外部系统**解耦**的标准缝——对话不直接调你的系统，只抛事件，你订阅：

```csharp
player.Runner.OnEvent += (id, arg) => {
    switch (id) {                              // 约定一套 eventId 命名空间
        case "quest.start":  quests.Start(arg);     break;   // arg = 任务 id
        case "achieve.grant": achievements.Grant(arg); break;
        case "sfx.play":     audio.PlayOneShot(arg); break;
    }
};
```

设计师在编辑器里加 Action 节点、在 `actions` 槽选「触发事件」单元、填 `eventId`/`arg`，无需改代码——你只要约定好命名空间即可对接。

### 2.2 黑板：注入 / 读出对话状态

`player.Runner.Blackboard`（[DialogueBlackboard](Runtime/Data/DialogueBlackboard.cs)）是每实例的变量store，开始时按各档 `BlackboardAsset` 的默认值播种。你的模块可在 `Begin()` 前或两次交互之间读写它，把外部状态喂给对话的 `Condition` 分支 / `Option` 门控：

```csharp
player.Runner.Blackboard.Set("trustedHero", true);   // Begin 前注入剧情状态
player.Runner.Blackboard.SetF("gold", 120);          // 数值用 SetF/GetF
// ...对话里 Condition 的 predicate 条件单元读 trustedHero 分两路、Option 的 gate 条件单元按 gold 决定是否可见
bool trusted = (bool)player.Runner.Blackboard.Get("trustedHero");
```

API：`object Get(string)` / `void Set(string,object)` / `float GetF(string)` / `void SetF(string,float)`。键名用变量面板里声明过的 key——门控/赋值**不烘节点参数**（红线 #13），走可组合 Unit 槽（`[SerializeReference]` 存进 `unitOverrides`）：设变量=Action 节点 + `SetVariableLiteralAction` 单元；条件门控=Option/Condition 的条件 Unit 槽（`gate`/`predicate`）里的 `[BlackboardKey]` 字段，都从这些 key 的下拉里选。

### 2.3 SubDialogue：嵌套别的对话图

`SubDialogue` 节点跳进另一张 `NodeGraphAsset`、跑完返回，**对你的模块透明**：子图里的 Action（触发事件）/Condition/台词照常触发同一套事件，你不用特殊处理。用于共享对话片段、条件子剧情。

### 2.4 存档系统：`Save()` / `Load()`

`DialogueState Save()` 捕获当前指针 + 黑板 + 子对话栈；`void Load(DialogueState state)` 还原。**注意两点**：

- **跨会话持久化**：`Save()` 记的是图的**对象引用**，同一会话内可经 `Load()` 完整往返；要存到磁盘跨整个应用重启，你的存档模块需把图对象映射成稳定的资产 id（GUID）再序列化（`instanceId`/`labelName` 字符串跨会话稳定，可作锚点）。详见 [DialogueRunner.cs](Runtime/Runner/DialogueRunner.cs) 的 `Capture` 注释。
- **`Load()` 会重新呈现当前节点**（重新抛 `OnLine`/`OnChoices`），所以**先订阅事件、再 `Load()`**，否则读档后界面是空的。

---

## 3. UI 调用：从别的工具 / 代码打开编辑器窗口

> ⚠️ **先分清两种 "UI"**：本节说的是**编辑器窗口**（`Tools/NodeGraph` 那个画布），在 `NodeEditor.Editor` 程序集、**仅编辑器期**可用。**游戏运行时玩家看到的对话 UI 不走这个窗口**——它走 §1 的事件渲染（你自己的 HUD）。**别在 gameplay 运行时代码里调 `NodeEditorWindow`**，那是编辑期工具。

编辑期想从你自己的编辑器工具/菜单里打开并定位某张对话图（[NodeEditorWindow.cs](../com.graphtest.nodeeditor/Editor/Window/NodeEditorWindow.cs)）：

```csharp
using NodeEditor.EditorUI;

NodeEditorWindow.Open();                                         // 开窗（= NodeGraph Manager 的 Open Node Editor）
EditorWindow.GetWindow<NodeEditorWindow>().LoadGraph(myGraph);   // 程序化加载某张图
```

- **菜单**：`Tools/NodeGraph/Manager` 的 **Open Node Editor**（`NodeEditorWindow.Open()`，[NodeEditorWindow.cs:37](../com.graphtest.nodeeditor/Editor/Window/NodeEditorWindow.cs)）。
- **双击资产**：在 Project 里双击任意 `NodeGraphAsset` 经 `[OnOpenAsset]` 自动用本窗口打开（[NodeEditorWindow.cs:63](../com.graphtest.nodeeditor/Editor/Window/NodeEditorWindow.cs)），无需你写代码。
- **程序化加载**：`LoadGraph(NodeGraphAsset)`（[NodeEditorWindow.cs:184](../com.graphtest.nodeeditor/Editor/Window/NodeEditorWindow.cs)，public）——它会经 locator 找注册表/黑板并重建画布。
- **drill-in / 面包屑导航**：窗口内的导航历史 `NavigationHistory`（[NavigationHistory.cs](../com.graphtest.nodeeditor/Editor/Support/NavigationHistory.cs)）`Push`/`Back`/`Forward`/`ClimbTo`。SubDialogue 钻入子图就走这套；做关联工具时复用。

---

## 4. 框架级可复用缝（换个领域编辑器照走这套）

下面这些是**框架**（`NodeEditor`）提供的、**不限对话**的接入缝——你做行为树/任务编辑器的运行时对接时，照样用同一套。对话层只是它们的一个实例。

### 4.1 运行时 ↔ 编辑器调试器：`IRuntimeGraph`（唯一的缝）

编辑器的 play 模式调试器**只**经 `IRuntimeGraph`（[NodeRuntime.cs:82](../com.graphtest.nodeeditor/Runtime/Graph/NodeRuntime.cs)）读运行时，**绝不碰**你的运行时内部：

- `Status StatusOf(string instanceId)` —— 节点当前状态（`Running`/`Success`/`None`），调试器据此着色。
- `object RuntimeNodeOf(string instanceId)` —— 逐节点内联调试视图用（对话层返回 `null`）。

你的 runner 实现它即可（`DialogueRunner : IRuntimeGraph`，[DialogueRunner.cs:33](Runtime/Runner/DialogueRunner.cs)）。

### 4.2 让调试器找到活动 runner：`RuntimeGraphRegistry`

play 时把 runner 注册进去，调试器经 `RuntimeGraphLocator.Find()` 取（[RuntimeGraphLocator.cs](../com.graphtest.nodeeditor/Editor/Support/RuntimeGraphLocator.cs)）：

- `RuntimeGraphRegistry.Register(IRuntimeGraph)` / `Unregister(IRuntimeGraph)`（[RuntimeGraphRegistry.cs](../com.graphtest.nodeeditor/Editor/Support/RuntimeGraphRegistry.cs)，**覆盖会警告**，套件反对无防护全局状态）。

**但 Register 在 Editor 程序集，运行时不能直接调**。对话层的解法是一个 `[InitializeOnLoad]` 桥（[Editor/Support/DialogueRuntimeBridge.cs](Editor/Support/DialogueRuntimeBridge.cs)）：`DialoguePlayer` 在 Runtime 里抛 `static OnRunnerCreated`/`OnRunnerDestroyed`（`Action<IRuntimeGraph>`，[DialoguePlayer.cs:37](Runtime/Runner/DialoguePlayer.cs)），桥在编辑器侧订阅并转给 `RuntimeGraphRegistry`。**这样 Runtime 不带任何 Editor 依赖**（红线：运行时不引 Editor）。你做新领域层照抄这个桥即可。

### 4.3 资产 locator（每项目假设一个，发现多个会警告）

编辑器创作数据使用 `DialogueDatabaseLocator`：零个数据库时给出创建原因；一个时自动采用；多个时
必须在 `DialogueAssetPaths.authoringDatabase` 显式选择，绝不静默选择扫描结果中的第一个。
这只决定编辑器数据窗口与参数候选的 authoring 数据库；运行时仍显式装配
`DialoguePlayer.database`。

- `NodeRegistryLocator.Find()` / `BlackboardLocator.FindGlobal()` / `EditorLocalizationLocator.Config()` 从唯一的 `NodeEditorAssetPaths` 读取精确项目路径；目标缺失、被其他类型占用或配置资产重复时失败关闭并列出候选，绝不取首个扫描结果。`Resolve(module,group)` / `ResolveFor(graph)` 仅对模块/组黑板按 `module`/`group` 标签枚举并合并成 `BlackboardSet`；同一标签重复同样失败关闭。

### 4.4 运行时安全的查询 API

- `NodeRegistry.Find(string id)`（[NodeRegistry.cs](../com.graphtest.nodeeditor/Runtime/Graph/NodeRegistry.cs)）—— 按定义 id 取节点定义。
- `ParamResolver.Resolve(inst, def, paramName)` / `ResolveObject(inst, paramName)`（[NodeDataTypes.cs](../com.graphtest.nodeeditor/Runtime/Graph/NodeDataTypes.cs)）—— **版本安全**取参（实例覆盖优先、缺失回填定义默认，定义升版不会无声破图）。
- 运行时玩家语言：`RuntimeLocalizationConfig.language`（[RuntimeLocalizationConfig.cs](../com.graphtest.nodeeditor/Runtime/Localization/RuntimeLocalizationConfig.cs)，枚举字段）。

---

## 5. 跨模块协作原则（接入时守这几条）

1. **外部只经 `IRuntimeGraph` / 事件读运行时，绝不碰内部状态**——编辑器调试器也守这条，你的模块同理。
2. **事件解耦**：对话不直接调你的系统，只抛 `OnEvent` / `OnLine` / `OnChoices` / `OnEnd`；你订阅。别反过来让对话硬依赖某个具体系统。
3. **运行时程序集不引 `UnityEditor`**：`DialoguePlayer`/`DialogueRunner` 在 Runtime、可进玩家构建；要跨 Runtime↔Editor 协作就用"抛事件 + Editor 侧桥订阅"（§4.2），不要在运行时 `using UnityEditor`。
4. **资产 locator 按归属保证唯一性**：`NodeRegistry`、全局黑板与本地化配置每项目一个；模块/组黑板按 `module`/`group` 标签可有多块，只有同一档重复才警告。
5. 规则细节一律以 [`ARCHITECTURE.md` 的「开发规范」节](../com.graphtest.nodeeditor/ARCHITECTURE.md) 为准。

---

## 6. 速查：你的系统 → 用什么 API → 在哪调

| 你的系统 | API | 调用点 |
|---|---|---|
| 对话 UI / HUD | `Runner.OnLine` / `OnChoices` / `OnEnd` | 订阅后渲染 `DialogueLineView` / `DialogueOptionView` |
| 玩家输入 | `player.Advance()` / `player.Choose(index)` | 确认台词 / 选了第 index 个可见选项（越界 no-op） |
| 启动对话 | `player.Begin()` | **订阅完事件后**调；同步走到第一个挂起点 |
| 任务 / 成就 / 音频 | `Runner.OnEvent(eventId, arg)` | 订阅、按 eventId 命名空间分发（不阻塞对话） |
| 剧情状态注入/读出 | `Runner.Blackboard.Get/Set/GetF/SetF` | `Begin()` 前或交互间；喂给 Condition/Option 门控 |
| 存档 | `player.Save()` → `DialogueState` / `player.Load(state)` | 跨会话需把图引用映射成 GUID；**先订阅再 Load** |
| 运行时语言 | `RuntimeLocalizationConfig.language` | 拖到 `player.localizationConfig` |
| 编辑期开窗/定位图 | `NodeEditorWindow.Open()` + `LoadGraph(asset)` | **仅编辑器工具**，别在 gameplay 调 |
| 调试器找 runner（新领域层） | `RuntimeGraphRegistry.Register/Unregister` + `[InitializeOnLoad]` 桥 | 参考 `DialogueRuntimeBridge` |

接好后，**进 play 真跑一遍**（订阅四事件 → Begin → Advance/Choose → 触发 Event → 存读档）确认行为，再算接入完成。
