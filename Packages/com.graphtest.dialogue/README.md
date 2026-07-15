# 对话编辑器使用指南

面向用这套工具**写对话内容**的人（设计师/对话作者），不涉及框架内部实现。

> 想给编辑器**加新功能**（新节点类型、面板、工具栏按钮、校验规则、调试视图、本地化/语言…）的**开发者**，请看 [EXTENDING.md](EXTENDING.md)（编辑器拓展指南）——动手前先读它指向的[「开发规范」](../com.graphtest.nodeeditor/ARCHITECTURE.md)（本工程规则的唯一权威来源）。写对话内容不需要看这些。

## 0. 安装

独立可安装、编译、卸载的 Dialogue Runtime/Editor 模块，只依赖 `com.graphtest.nodeeditor`，不依赖 Task 或 State Machine。通过 `Tools/NodeGraph/Manager` 安装本模块（Manager 复用框架包的仓库与 revision，无需再输 Git URL），再按 §2 生成资产。生成的对话图、黑板、数据库与 `DialogueAssetPaths` 属于项目，默认写入 `Assets/DialogueContent`。产品包不含场景、示例数据或示例脚本；需要演示时在 Manager 里单独安装 `com.graphtest.dialogue.samples` 并显式导入 **Dialogue Basics**。

其他用户首次安装 Dialogue 时会自动看到 **Dialogue 路径设置 / Path Setup**。可先修改节点定义、对话组和黑板目录；只有点击 **保存并生成 / Save & Generate** 后，才会保存 `DialogueAssetPaths` 并创建这些目录、节点定义和注册表条目。选择 **稍后 / Later** 或关闭不会创建任何 Dialogue 资产，下次重启 Unity 会再次提示。

Package consumers automatically receive **Dialogue Path Setup** on first install. They can edit the node-definition, dialogue-group, and blackboard directories before anything is written. `DialogueAssetPaths`, its configured directories, node definitions, and registry entries are created only after **Save & Generate**. **Later** or closing creates no Dialogue assets and prompts again after Unity restarts.

## 1. 打开编辑器

菜单 `Tools/NodeGraph/Dialogue` 打开对话模块窗口；`Tools/NodeGraph/Manager` 的 **Open Node Editor** 打开不限定模块的通用窗口。在 Project 窗口双击任意 `NodeGraphAsset`（比如样例对话）也会自动用共享窗口打开。

窗口分几块：左侧**对话组**列表（项目里所有对话图，可搜索、单击切换、双击在 Project 里定位）、其下的 **Variables** 面板（黑板变量）、中间画布（拖节点连线）、右侧 **Inspector**（选中节点后改参数）、顶部工具条。左侧两块与右侧检视面板的边界都可拖动调整大小。

`Tools/NodeGraph/Manager` 的 **Node Editor Data** 使用三栏布局：左侧选数据源，中间选具体条目，右侧编辑该条目的字段。

**新建一张对话图**：点对话组列表底部的「新建对话」。这一个 `ControlFlow` 创建配方会在当前 `DialogueAssetPaths.dialogueGroupsDir` 下按图名创建独立文件夹，播种钉住的 Start/End，并立即打开。首次默认值（例如 `Assets/DialogueContent/Dialogues`）只是示例；可在 `Tools/NodeGraph/Manager` 的 Dialogue **Open Asset Paths** 自由修改。Project 里的 `Create ▸ NodeEditor/Graph` 只创建未归属模块的裸图，不会套用对话配方。

## 2. 初始化/重新生成资产

在 `Tools/NodeGraph/Manager` 里点 Dialogue 卡片的 **Setup Assets** 按钮。

- 第一次跑：在 `Assets/NodeEditorSettings/` 创建或读取项目级 `NodeEditorAssetPaths` / `DialogueAssetPaths`，再按其中的当前值创建共享注册表、全局黑板、本地化、语言配置和对话节点定义。`Assets/NodeEditorContent/` / `Assets/DialogueContent/` 只是可编辑默认示例，不是强制路径。
- 之后重复跑：是幂等的，只会重建节点定义并补齐共享配置/本地化种子，**不会**替你创建或覆盖业务数据库、演示图和手写内容。重复路径配置会失败并列出所有候选，不会静默取第一个。产品发布只包含创作能力，不附带业务数据库或演示内容。

## 3. 10 种节点

> 编辑器里这些节点名、参数名、功能说明都会按所选编辑器语言显示（见 §8）；下表用英文 Kind 名标识，方便对照代码。

| 节点 | 作用 | 关键参数 |
|---|---|---|
| Start | 对话入口，每张图只能有一个 | 无 |
| Line | 显示一句台词 | `lineKey`（去本地化数据库查文本/说话人） |
| Choice | 给玩家若干选项 | 无参数；`options` 出口接若干 Option，`fallback` 出口在所有选项都不可见时兜底（不接则直接结束） |
| Option | 一个可选分支 | `optionKey`（选项文案）；`gate`（可选**条件单元**）——选了才按条件决定是否可见，留空则总是可见 |
| Condition | 按一个**条件单元**分两路 | `predicate`（条件单元）；成立走 `true`、否则走 `false` 出口 |
| Action | 执行副作用（设变量、抛事件…） | `actions`（**动作单元**；要一次做多件用「顺序」装饰，按条件做用「条件执行」装饰） |
| Jump | 跳转到某个 Label | `targetLabel`（按名字匹配，不依赖连线） |
| Label | Jump 的落点 | `labelName`，必须唯一 |
| SubDialogue | 跳进另一张对话图，结束后返回 | `subGraph`（拖一个 NodeGraphAsset） |
| End | 对话分支结束 | 无 |

> **条件 / 取值 / 动作都是「单元」——在检视面板里下拉选、可折叠、可装饰。** 选中 Option/Condition/Action 节点，在右侧 Inspector 里点对应槽的**类型下拉**：候选分「全局通用」（比较、与/或/非、读黑板、算术、设置变量…）和「对话」（触发事件…）两组，选一个即展开它的字段编辑；想组合多个就先选装饰器（与 AND / 或 OR / 非 NOT / 顺序 / 条件执行）再往里加子单元，可层层嵌套。不需要时把它收起来。比黑板键的字段会是已声明变量的下拉，不用手打。

### 连线：单连线口 vs 多连线口

从一个节点的**下方出口**拖到另一个节点的**上方入口**就连上了。端口上的小连接点形状/颜色告诉你它能连几条：

- **圆形 · 灰色 = 单连线口**：只能连**一条**线。已经连了一条还往上连新的，**旧的会被自动顶掉**（替换，不是叠加）。多数"下一步"出口是这种——比如 Start、Line、Action 的 `next`，Condition 的 `true`/`false`。
- **圆形 · 蓝色粗描边 = 多连线口**：能连**多条**线。比如各节点的**入口**（多个节点都能汇流进来）、以及 Choice 的 `options` 出口（接好几个 Option）。容量仍由 arity 决定，形状/描边以当前共享 UI 标准为准。

把鼠标停在连接点上会弹出中文提示（"单连线…/多连线…"）。连超了/连缺了会被校验出来（见 §5），在节点上画红/黄框。

## 4. 黑板变量 / 本地化文案

- **黑板变量**：在节点编辑器窗口的 Variables 面板里点 `+ 变量`，弹窗里填名字（key）、类型（Bool/Int/Float/String），点创建即加入，重名会拦下提示。**不用再选作用域**——变量的作用域由它**所在的那块黑板**决定：黑板分全局 / 模块 / 组三层；实际资产位置分别来自 `NodeEditorAssetPaths.globalBlackboardPath` 和当前模块配方的 `blackboardLayersDir`，没有固定目录。在哪块里编辑就是哪个作用域。Variables 面板顶部可切换档位，新建图时会按当前配置自动配好模块/图黑板。一张图实际可见的变量 = 全局 + 模块 + 组合并，同名以更具体的一档为准。
- **本地化文案**：`Line.lineKey` / `Option.optionKey` 对应显式创建并装配给 `DialoguePlayer.database` 的 `DialogueDatabase` entry。`DialogueSetup` 不会自动创建业务数据库；可用 `Create ▸ Dialogue ▸ Database` 在用户选择的任意 `Assets/` 目录新建。开发样例库不属于发布内容。Inspector 和数据窗口都使用语言下拉 + 单个全宽多行文本框编辑当前语言。
- **作者数据库选择**：项目中没有 `DialogueDatabase` 时，数据窗口显示创建提示；恰好一个时自动使用；
  多于一个时绝不静默取第一个，必须在 `DialogueAssetPaths.authoringDatabase` 显式选择。数据窗口
  的对象字段写入带 Undo 并标脏。参数候选在未能唯一解析数据库时返回空列表。

## 5. 校验

不需要手动点什么——**编辑图的同时就在跑**。节点编辑器窗口监听画布变化，每次改动（加节点/连线/删节点）后自动重新校验并把问题画在节点上（红框=Error，黄框=Warning）。图级别的问题（比如整张图没有单一入口）显示在画布顶部的横幅里，不会刷 Console。

会检查的内容：
- 必须有且只有一个 Start 节点；它默认就是图的入口（无需手动指定 entry）。只要全图从这个 Start 往下连成一条流程，就满足"单入口"。
- 每个 Jump 的 `targetLabel` 必须能在图里找到对应的 Label；重名的 Label 会警告（运行时按名字取第一个匹配，重名是隐患）。
- 节点单元槽里引用黑板 key 的字段（如设变量、比较条件选用的变量），若指向一个没声明过的 key 会警告。

## 6. Play 模式下跑一个对话

> 这节是给设计师快速试跑的最小步骤。**程序员**要把对话接进游戏（订阅事件、和任务/存档系统合作、程序化开窗）请看 [INTEGRATION.md](INTEGRATION.md)（集成/接入指南），那里有完整 API 与代码示例。

1. 场景里随便建一个空 GameObject，挂 `DialoguePlayer` 组件。
2. Inspector 里拖好这些字段：`graph`（要播放的对话图）、`registry`（`NodeRegistry.asset`）、`blackboards`（黑板**数组**——可放多块，按**全局 → 模块 → 组**分层合并，同名以更具体的一档为准；只放一块=仅用全局，比如 `GlobalBlackboard.asset`）、`database`（显式创建的 `DialogueDatabase.asset`）。语言：**推荐**把 `RuntimeLocalizationConfig.asset` 拖到 `localizationConfig` 字段（按它的 `language` 枚举取文本）；留空才用 `DialoguePlayer.language` 的 `Language` 枚举字段。对话数据库里的 `en`/`zh` 等语言码从 `LanguageOptions.asset` 下拉选择，避免手填拼错。
3. 代码里拿到这个组件后，**先订阅事件再 Begin**：
   ```csharp
   player.Runner.OnLine += line => ...;       // 显示台词
   player.Runner.OnChoices += opts => ...;     // 显示选项列表
   player.Runner.OnEvent += (id, arg) => ...;  // 处理事件（由 Action 节点的「触发事件」单元抛出）
   player.Runner.OnEnd += () => ...;           // 对话结束
   player.Begin();
   ```
4. 玩家确认台词调 `player.Advance()`；选了某个选项调 `player.Choose(index)`（index 是当前可见选项里的下标，不可见的 Option 已经被过滤掉）。
5. 存档/读档用 `player.Save()` / `player.Load(state)`，跨会话保存需要自己把存档里的图引用换成稳定的资产 id（详见 `DialogueRunner.Capture` 的注释）。

## 7. 创建第一张对话图

在工具栏选择“新建对话”，至少连接 Start → Line → End，再在数据窗口创建并指定自己的 `DialogueDatabase`。
需要分支时加入 Choice/Option；需要复用流程时用 Label/Jump；条件与副作用通过可组合 Unit 配置。图、黑板和数据库都写入当前路径配置指定的项目目录，不依赖随包演示资产。

## 8. 编辑器本地化 / 语言切换

- **编辑器语言**：工具栏右侧有语言下拉（English / 中文），切换后整个编辑器界面立即按所选语言显示。配置位置来自 `NodeEditorAssetPaths.editorLocalizationConfigPath`，可在路径配置里自由修改。
- **内容语言码**：`NodeEditorAssetPaths.languageOptionsPath` 指向的 `LanguageOptions.asset` 定义 `DialogueDatabase` 可选的 `lang` code（默认 `en`/`zh`）。数据窗口/数据库 Inspector 都用下拉选择这些 code。
- **节点/参数的本地化文案**来自两处，按优先级回退：① 代码属性 `[NodeDoc]` / `[ParamDoc]`（写在节点定义类上，每种语言一条，英文+中文）→ ② 兜底表 `LocalizationTable.asset`（`key → 各语言文本`，非程序员也能在它的 Inspector 里补/改）→ ③ 英文缺省。没写中文就回退英文，绝不空白。
- **单元下拉的名称/分组**同样按编辑器语言切换：代码只保存稳定 `unit.*` key 与英文回退；框架定义/拥有通用单元和共享分组 key，对话只定义/拥有 `unit.dialogue.*`。框架通用种子由框架 `FrameworkSetup` 播种、`unit.dialogue.*` 由 `DialogueSetup` 播种（都 add-if-missing）；切语言不会改变已经保存的单元类型。
- **运行时语言**：游戏里玩家看到的台词语言，由 `RuntimeLocalizationConfig.asset` 的 `language` 枚举决定（见 §6 把它拖到 `DialoguePlayer.localizationConfig`）。它对应 `DialogueDatabase` 每条 entry 里的多语言文本（`en` / `zh`）。

## 9. 节点备注与悬停说明

- **备注**：选中节点，Inspector 的"备注"字段填内容——**非空时画布上该节点的标题就显示这条备注**（方便给节点起业务名，如"开场问候"）。留空则显示节点的本地化默认名。旁边的"名称"字段是自定义名（优先级次于备注）。
- **悬停说明**：鼠标停在任意节点上**约 1 秒**，弹出该节点的**功能说明 + 各参数（名称 / 说明 / 当前值）**，全部按当前编辑器语言本地化。移开鼠标即消失。

## 10. 已知问题

目前没有已知的编译警告——之前的 `CS0618`（过时 API）与 `CS0108`（成员隐藏）警告都已修掉，正常编译应是干净的 Console。
