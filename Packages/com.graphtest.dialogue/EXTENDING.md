# 对话编辑器 · 拓展指南（面向开发者）

> 给**要给这个编辑器加新功能**的程序员。面向"写对话内容"的设计师请看 [README.md](README.md)。想在**别的游戏模块里用对话**（订阅事件跑对话、和任务/存档系统合作、程序化调编辑器 UI）而非扩展编辑器，请看 [INTEGRATION.md](INTEGRATION.md)（集成/接入指南）——**扩展**编辑器看本文，**接入/使用**编辑器看那份。
> 框架（可复用）在 `Packages/com.graphtest.nodeeditor/`，对话领域层在 `Packages/com.graphtest.dialogue/`。下面每个配方都标了改哪个文件。
>
> **⚠️ 动手前先读「开发规范」——它是本工程规则的唯一权威来源**：[`Packages/com.graphtest.nodeeditor/ARCHITECTURE.md`](../com.graphtest.nodeeditor/ARCHITECTURE.md) 开头的「⚠️ 开发规范」节（A 数据安全硬规则 / B 架构不变式 / C 工程规范 / D 完成定义）。下面只列改对话编辑器最常踩的几条速查，完整理由 + 代码落点以那份为准：
> **涉及任何 UI 时再读 [`NodeEditor/UI-STANDARD.md`](../com.graphtest.nodeeditor/UI-STANDARD.md)**：领域只组装共享控件；命令按钮与扁平导航 chrome 分属两套 contract；不得自建 USS、颜色或平行控件。
> - **一类一文件**（A1）：每个 `ScriptableObject` / `MonoBehaviour` 具体类一个文件、文件名 == 类名，否则生成的 `.asset` 全坏（`m_Script:0`）、编辑器打不开。纯数据类 / enum / 抽象基类不在此限。
> - **写序列化数据必标脏 + 可撤销**（A2）：写前 `Undo.RegisterCompleteObjectUndo(asset)`、写后 `EditorUtility.SetDirty(asset)`，面板写值走 `WriteOverride`/`NodeInspectorEdits`，否则改了会无声丢失。
> - **浮层 / 弹窗坑**（A4）：挂到有字体的容器（GraphView 级，别挂 `panel.visualTree`）、CJK 文本容器给固定宽度、`PopupField` 当前值必须在候选里。
> - **中文 + Localizer · 扩展即本地化**（C11）：代码注释、文档、UI 文案一律中文；**所有可见字符串都走 `Localizer`**——chrome 用 `Localizer.UI("ui.xxx","英文缺省")`、节点/参数用 `[NodeDoc]`/`[ParamDoc]`、**校验/诊断/报错消息**用 `Localizer.UI("val.xxx"/"ui.errXxx","英文缺省")`（含占位的 `string.Format`）。**每次加功能都在同一次改动里把它的新文案一并本地化并种好中文**（别漏报错/横幅/弹窗这类非主路径文字）；判据：切中文后全 UI 无残留英文。
> - **完成定义**（D16）：改完一定真打开窗口、真交互核一遍（选中看检视面板、悬停看浮层、切语言看全 UI、重载后数据还在、console 干净）——"编译过、有节点"不算数。

---

## 1. 加一种对话节点（最常见）

举例加一个 `Wait`（等待）节点：

1. **新建定义文件**（一类一文件）`Packages/com.graphtest.dialogue/Runtime/Nodes/WaitNode.cs`：
   ```csharp
   using NodeEditor;
   namespace Dialogue
   {
       [NodeMenu("Dialogue/Flow/Wait")]
       [NodeDoc(Language.English, "Wait", "Pauses the dialogue for a number of seconds, then continues.")]
       [NodeDoc(Language.Chinese, "等待", "暂停对话若干秒后继续。")]
       [ParamDoc(Language.English, "seconds", "Seconds", "How long to wait.")]
       [ParamDoc(Language.Chinese, "seconds", "秒数", "等待的时长（秒）。")]
       public class WaitNode : DialogueNodeDefinition
       {
           public override DialogueNodeKind Kind => DialogueNodeKind.Wait;
           protected override void Define()
           {
               Meta("Wait", NodeRole.Control);
               AddParam("seconds", TypeRef.Float);
               AddIn("in", Arity.Many);
               AddOut("next", Arity.ExactlyOne);
           }
       }
   }
   ```
2. **登记 Kind**：在 `Packages/com.graphtest.dialogue/Runtime/Nodes/DialogueNodes.cs` 的 `DialogueNodeKind` 枚举里加 `Wait`。
3. **接运行时语义**：在 `Packages/com.graphtest.dialogue/Runtime/Runner/DialogueRunner.cs` 的 `Continue()` 大 `switch (kind)` 里加一支 `case DialogueNodeKind.Wait:`，写它的执行/挂起逻辑（参考相邻的 `Line`/`Action`/`Condition` 分支）。
4. **重新生成资产**：在 `Tools/NodeGraph/Manager` 里点 Dialogue 卡片的 **Setup Assets** 按钮（幂等）——会自动发现新子类，在 `DialogueAssetPaths.nodeDefinitionsDir` 当前指向的目录生成 `WaitNode.asset`，并加进注册表的 `projectDomain` 档。
5. **（可选）加可读性线索 / 运行时视图**：见配方 4 / 7。
6. 在产品程序集之外补一条覆盖对应运行器行为的 EditMode 测试。

> 本地化文案优先用上面的 `[NodeDoc]`/`[ParamDoc]` 属性（没写就回退英文）；也可在 `NodeEditorAssetPaths.localizationTablePath` 当前指向的表里按 `node.<id>.name` 等 key 补，见配方 8。
>
> **🔑 节点分层（开发规范 §3 节点分层规则）**：上面这是**对话领域节点**——继承 `DialogueNodeDefinition`、放本模块 `Dialogue/Runtime/Nodes/`，由 `DialogueSetup` 填进注册表的 **`projectDomain`** 档；数据窗口在「对话节点定义（只读）」(领域作用域) 下展示。若你要做的是**跨领域通用节点**（任何领域都能用、零对话语义），它属于**框架**：直接继承 `NodeDefinition` 放 `NodeEditor/Runtime/`、由框架侧装配填进 **`universal`** 档，在「全局节点定义（只读）」(项目作用域) 下展示——**别**把通用节点塞进对话模块，也**别**让框架认识对话节点。

---

## 1b. 让端口"只能单连线"还是"能多连线"

端口能连几条线由它的 **arity** 决定（在 `Define()` 里给每个端口设）。框架据此推导 GraphView 的连接容量，并**在画布上把两种口画得不一样 + 配中文 tooltip**，作者一眼可辨：

| 在 `Define()` 里写的 arity | 容量 | 画布外观 | 含义 |
|---|---|---|---|
| `Arity.ExactlyOne` / `Arity.Optional`（上界 ≤ 1） | Single（单连线） | **圆形 + 中性灰**连接点 | 只能连一条；**再连一条会自动替换旧的** |
| `Arity.Many` / `Arity.AtLeastOne` / 上界 > 1 的 Range/Exactly | Multi（多连线） | **方形 + 强调蓝**连接点 | 可连多条 |

- 选 arity 的经验：**流程"出口"通常单连线**（一个节点之后只接一个：`Start.next`、`Line.next`、`Condition.true/false` 都是 `ExactlyOne`）；**"入口"通常多连线**（多个节点都能流向它：各节点的 `in` 用 `Arity.Many`）；**要扇出**的出口用多连线（`Choice.options` 用 `AtLeastOne`）。
- ⚠️ **每个端口都要显式设 arity**。`PortDef` 不设时 struct 零值是 `Exactly-0`，会拒绝一切连接。
- 实现落点（一般不用碰）：容量推导 + 挂样式类 + tooltip 在 `PortView.Create`（`NodeEditor/Editor/Window/GraphCanvasView.cs`）；单连线"落新边挤掉旧边"的替换语义在 `GraphCanvas.EnforceSingleCapacity`（同文件）；**颜色**走 `Port.portColor`（Unity 用 C# 直接写连接点描边/填充色，USS 改不动），**形状**走 USS 的 `.ne-port-single` / `.ne-port-multi`（`NodeEditorStyles.uss`）。
- 校验：`GraphValidator.CheckArity` 会按 arity 校验实际连接数（连超/连缺出红黄框），与画布上的容量限制是两道关。
- 改完**真开窗悬停端口看 tooltip、看连接点形状/颜色**核验。

---


## 1c. 加一个条件 / 取值 / 副作用 / 编排单元（可组合 · 可装饰）

> **核心原则（NodeEditor ARCHITECTURE 规则 7）：节点不自带门控/比较/赋值参数，改持「Unit 槽」。** 条件、取值、副作用、编排都做成**可组合单元**，从「全局通用 + 对话领域」两级注册表里下拉选择、可层层装饰（And/Or/Not、Sequence/Conditional…）、检视面板可折叠编辑。这修正了早期"每个节点各写一套门控（gateKey/op/value）"的设计偏差——那样无法复用、条件一多就糊。

四个角色族基类（`NodeEditor/Runtime/Graph/NodeRuntime.cs`）：`ConditionUnit`（→bool）/`ProviderUnit`（→值）/`ActionUnit`（副作用）/`ControlUnit`（→Status，编排子单元）。求值上下文是框架的 `NodeContext`（`ctx.blackboard` 读写黑板）。

1. **写单元类**：继承对应族基类，实现其入口（`Evaluate`/`Get`/`Execute`/`Tick`），用稳定 key + 英文回退标注，例如 `[Unit("unit.dialogue.fireEvent.name", "Fire Event", "unit.group.action", "Action")]`。`UnitRegistry` 会按当前语言经 `Localizer.UI` 解析名称与分组；类型身份保持不变。框架代码定义/拥有通用单元及共享分组 key，对话扩展只定义/拥有 `unit.dialogue.*`。在本 GraphTest 项目中，物化入口与所有权一致：框架通用 key 由框架 `FrameworkSetup.SeedFrameworkUI` add-if-missing 播种（各领域 Setup 先调 `FrameworkSetup.EnsureCoreAssets`），`unit.dialogue.*` 由 `DialogueSetup.SetupLocalization` 播种。
   - **全局通用**（只用 `ctx.blackboard`）→ 放框架 `NodeEditor/Runtime/Units/Units.cs`，会出现在下拉的「全局通用」分组。
   - **对话专属**（要领域能力，如触发事件）→ 放 `Dialogue/Runtime/Units/DialogueUnits.cs`，强转 `ctx.blackboard as IDialogueEventSink` 取领域能力；出现在「对话」分组。
   - **装饰器** = 单元里持 `[SerializeReference] 基类 inner` 或 `[SerializeReference] List<基类> items`，递归求值（见 `NotCondition`/`AndCondition`/`SequenceAction`）。检视面板会自动为这些字段出子下拉+子折叠。
   - 黑板键字段标 `[BlackboardKey]`，检视面板渲染成已声明 key 的下拉。
2. **节点持槽**：在节点 `Define()` 里 `AddUnitParam("predicate", "Condition")`（family ∈ Condition/Provider/Action/Control）。运行时用 `ParamResolver.ResolveUnit(inst, "predicate") as ConditionUnit` 取出求值。
3. ⚠️ **族基类必须保持 `[Serializable]`**（硬规则 A#1 子条）：否则装饰器的 `List<基类>` 字段存盘被整段丢弃、子树无声消失。
4. 改完**真开窗**：选中节点→检视面板里给 Unit 槽下拉选类型（确认全局/领域分组、按族过滤）、套个装饰器、收起展开、保存关闭重开看数据还在；进 play 看运行生效。

实现落点：注册表 `NodeEditor/Editor/Inspector/UnitRegistry.cs`（反射发现 + 全局/领域分级）；检视面板 `NodeEditor/Editor/Inspector/UnitInspector.cs`（递归折叠编辑器，`InspectorPane.EditorFor` 的 `TypeKind.Unit` 分支调它）。

---


## 2. 加一条校验规则（红框/黄框 + 画布横幅）

校验通过 `GraphValidator.RegisterExtension(id, fn)` 钩子接入，**不用改框架**。看 `Packages/com.graphtest.dialogue/Editor/Validation/DialogueValidation.cs`：

```csharp
[InitializeOnLoad]
public static class DialogueValidation
{
    static DialogueValidation() => GraphValidator.RegisterExtension("dialogue", CheckAll);   // 静态构造里按稳定 id 注册
    static IEnumerable<ValidationIssue> CheckAll(NodeGraphAsset g, NodeRegistry reg, BlackboardSet bb)
    {
        // 消息走本地化助手 L（见文件顶部）：命中兜底表则用之，否则回退内联英文；带 {0} 占位用 L(key, enFmt, args)。
        // yield return ValidationIssue.Error(inst.instanceId, L("val.myRule", "..."));               // 节点级 → 画在该节点上（红框）
        // yield return ValidationIssue.Warn (inst.instanceId, L("val.myWarn", "... {0}", x));         // 节点级（黄框，带参数）
        // yield return ValidationIssue.Error(GraphValidator.GraphIssueTarget, L("val.myGraph", "...")); // 图级 → 进画布顶部横幅
    }

    // 校验消息也是可见文案，必须本地化（开发规范 C11）——和连接规则消息（配方 2b）一视同仁。
    static string L(string key, string en) => NodeEditor.EditorUI.Localizer.UI(key, en);
    static string L(string key, string enFormat, params object[] args) => string.Format(NodeEditor.EditorUI.Localizer.UI(key, enFormat), args);
}
```
加规则 = 在 `CheckAll` 里多 `yield` 几条 issue。节点级 issue 的 `target` 填 `inst.instanceId`，图级填 `GraphValidator.GraphIssueTarget`（会走横幅、不刷 console）。校验是**边编辑边自动跑**的，无需手点。
> **本地化校验消息（C11，别漏——这是实例真踩过的坑 #12）**：`ValidationIssue.*(target, message)` 的 `message` 是会显示在红黄框 / 横幅里的**可见文案**，不能硬编码英文。用上面的 `L("val.xxx","English"[,args])` 包裹，中文在 `DialogueSetup.SetupLocalization` 里 `EnsureUI(table, "val.xxx", "中文")` 种、跑一次 `Tools/NodeGraph/Manager` 的 Dialogue **Setup Assets** 落表。框架 `GraphValidator` 与对话 `DialogueValidation` 都已照此本地化（各自顶部就有同款 `L` 助手），照抄即可。改完切中文核一遍红黄框/横幅都是中文。

---

## 2a. 限制节点可用于哪种图

`DialogueValidation` 的静态构造函数通过 `NodeDefinitionAvailability.Register("dialogue", ...)` 注册对话领域的节点可用性谓词。领域层拥有 `module` 与 `graphType` 的语义：当前对话模块只有一种 ControlFlow 图，因此谓词在 `module == DialogueGraphScaffold.Module` 时只接受 `DialogueNodeDefinition`。

搜索、程序化创建、兼容端口过滤和保存后 `GraphValidator.ValidateAll` 都消费这一个谓词。新增节点或图类型时只改领域注册规则与测试，不在这些框架消费者里复制 `module` / `graphType` 分支。可见拒绝原因走 `Localizer.UI`，中文 key 在 `DialogueSetup.SetupLocalization` 播种。

---

## 2b. 限制哪种节点能接哪种（连接规则 include/exclude，双向）

端口**类型**（`TypeRef`）和**数量**（`Arity`）之外，还能按**节点种类**限制连线——防止策划靠约定乱接。规则集中写在 `Packages/com.graphtest.dialogue/Editor/Validation/DialogueConnectionRules.cs` 的一张矩阵 `s_Matrix` 里（**不用改框架**；判定引擎是框架泛型 `ConnectionRuleMatrix<TDef,TKind>`——出向/入向、端口专属优先、本地化拒绝消息 `val.conn*` 都在框架，领域只填矩阵数据；注册钩子仍是 `ConnectionRules.RegisterRule(id, s_Matrix.Evaluate)`，与 `GraphValidator.RegisterExtension` 平行；按 id 注册、保序短路、同 id 覆盖告警）。两道关共用这份规则：

- **拖拽实时拦截**：非法目标端口直接不高亮、连不上（`GraphCanvasView.GetCompatiblePorts`）。
- **事后校验报错**：复制粘贴 / 老图 / 拖端点重连留下的非法边会被标红，消息说清两端为何不能连（`GraphValidator.CheckConnectionRules`）。

一条规则 = 一行：

```csharp
// Out=约束“本节点输出能连到谁”；In=约束“本节点输入只接受谁”。port 为 null = 该侧所有端口。
new ConnectionRuleEntry<DialogueNodeKind> { node = DialogueNodeKind.Choice, port = "options", side = ConnectSide.Out, mode = ConnectMode.Include, kinds = new[]{ DialogueNodeKind.Option } },  // options 只能接 Option
new ConnectionRuleEntry<DialogueNodeKind> { node = DialogueNodeKind.Option, port = "in",      side = ConnectSide.In,  mode = ConnectMode.Include, kinds = new[]{ DialogueNodeKind.Choice } },  // Option 只接受来自 Choice
// 排除模式示例：禁止某出口接某些种类，其余放行
// new ConnectionRuleEntry<DialogueNodeKind> { node = DialogueNodeKind.Start, port = "next", side = ConnectSide.Out, mode = ConnectMode.Exclude, kinds = new[]{ DialogueNodeKind.End } },
```

- **Include**=仅允许列出的种类；**Exclude**=禁止列出的种类（其余放行）。
- **双向**：出向（源端口能连到谁）+ 入向（目标端口只接受谁）独立判定，任一否决即不让连。把“Option 只能挂在 Choice 下”这种约束**写在 Option 的入向**一条就够，不用给每个别的节点都加排除。
- 同侧同端口最具体的规则生效（端口专属 > 整节点 `port=null`）。
- 消息按当前编辑器语言产出、点名两端节点与端口（见 `OutMessage`/`InMessage`）。
- 默认空矩阵 = 不限制，与改动前行为一致；非对话图不受影响。

---

## 3. 加检视面板里某个参数的编辑器

参数编辑器由 `Packages/com.graphtest.nodeeditor/Editor/Inspector/InspectorPane.cs` 的 `EditorFor(pd, node)` 按 `pd.type.kind` 分派（下拉/滑块/勾选/文本/对象选择）。多数类型已自动处理：
- `TypeRef.BBKey()` → 黑板 key **可搜索下拉**（列全部 key，见坑：别用元类型过滤）。
- `TypeRef.BBValue("键参数名")` → "值"跟随所引用键的类型：Bool 键→true/false 下拉、Int/Float 键→数字框、String/未设置→文本框（用于"比较值/赋值"这类值域由另一个键决定的参数，别再用裸 `TypeRef.String`）。
- `AddParam("名", TypeRef.String, "候选来源标签")` → **可搜索下拉**：候选由领域层经 `ParamChoiceProviders.Register("标签", ctx => keys…)` 提供（本套已注册 `dialogue.dbKeys`=数据库行 key、`dialogue.labels`=本图 Label 名）。key/标签类参数一律用它，别让人手填（容易填错）。点分 key 会折成分类树，候选很多也好选。注册时传 `allowCustom: true` 则该字段变**可编辑组合框**（既能从候选里选，也能临时键入新名字——如还没建出来的 Label 目标；本套 `dialogue.labels` 已开启）。
- `TypeRef.Enum(typeof(X).FullName)` → 枚举下拉。
- `TypeRef.Object(typeof(T).FullName)` → 对象选择框。
- `TypeRef.Bool` → 勾选开关（Toggle）。
- 带 `hasBounds` 的 Float → 滑块。
- 其它 → 文本框。

要给某个 string 参数加一组动态候选（如新的资源 key 类型）：① 在领域 Editor 程序集的 `[InitializeOnLoad]` 静态类里 `ParamChoiceProviders.Register("我的标签", ctx => 候选, allowCustom: false)`（`ctx` 给到当前图/节点/注册表/黑板；`allowCustom` 决定是否允许临时键入候选外的新值）；② 在 `Define()` 里 `AddParam("名", TypeRef.String, "我的标签")`；③ 跑 `Tools/NodeGraph/Manager` 的 Dialogue **Setup Assets** 重烘焙 Def。框架层不认识领域候选，全靠这条反向注入。下拉控件本身是可复用的 `NodeEditor.EditorUI.SearchableDropdownField`（标签 + 选择按钮 / 可编辑组合框，内部复用 `StringSearchWindow`）——任何编辑器面板都能直接 `new` 出来用，不限于检视面板。

要加新控件：在 `EditorFor` 的 `switch` 里加一个 `case`。**写值一律走 `WriteOverride(node, pd.name, value)`**（它经 `NodeInspectorEdits` 标脏 + 记 Undo）。下拉构造用 `SafePopup`（保证当前值在候选里，否则抛异常刷 console）。标签/tooltip 用 `Localizer.ParamName/ParamDesc`。

---

## 4. 加节点上的可读性线索（画布上不展开也能看出参数）

走共享 `NodeCueControl` 扩展点（`Packages/com.graphtest.nodeeditor/Editor/Window/NodeViewControl.cs`），对话域的实现见 `Packages/com.graphtest.dialogue/Editor/Nodes/DialogueNodeViews.cs`。领域只提供文案，标签、刷新、两行截断和样式由框架统一负责：

```csharp
[NodeViewControl(typeof(WaitNode))]
public class WaitNodeCue : NodeCueControl
{
    protected override string Describe(NodeInstance inst, NodeDefinition def) =>
        string.Format(Localizer.UI("ui.cue.wait", "Wait {0}s"), Param(inst, def, "seconds"));
}
```
框架在创建每个 `NodeView` 时通过 `NodeViewControlRegistry.AttachIfAny` 自动挂上（`GraphCanvasView.cs`）。同时在 Setup 里种好 `ui.cue.wait` 中文。**约束：不新增图片资源、不自建 cue USS**，只用共享 contract。

---

## 5. 加工具栏按钮 / 快捷键

当前框架**没有领域级 toolbar/shortcut 注册缝**。因此：

- 若动作对所有领域都通用，才在 `NodeEditorWindow` 实现，并同步 `UI-STANDARD.md` 与契约测试；命令按钮走 `EditorUi` 的 toolbar helper。
- 若动作只属于 Dialogue，不要把 `"dialogue"` 分支写进框架。先在 NodeEditor 增加一个按 `module` 注册 label/callback 的显式缝，再由 Dialogue.Editor 注册策略；在该缝落地前，把动作放进领域自己的数据源/检视面板入口。

快捷键同理：通用快捷键可由共享 shell 拥有；领域快捷键须先有 NodeEditor-owned、按 module 分派的注册缝，不能直接在共享 `CreateGUI()` 里写领域判断。

---

## 6. 加一个面板

领域数据/工具面板优先通过 `DataSourceRegistry` 接入通用三栏数据窗口（见 6b），或通过已有 Inspector/引用数据扩展缝接入。不要直接在 `NodeEditorWindow.CreateGUI()` 套新的 `TwoPaneSplitView`：那会让共享 shell 认识 Dialogue。确有跨领域通用的新面板时，才把它设计成 NodeEditor-owned module，并同步 `UI-STANDARD.md`、契约测试与真实双主题验收；确有领域专用 shell 插槽需求时，先增加按 `module` 注册内容的显式缝。

---

## 6b. 把数据接进通用数据编辑窗口（数据源）

数据层（黑板变量 / 本地化表 / 对话数据库 / 节点定义…这些纯数据 SO）的查看与编辑统一在**通用数据编辑窗口** `DataEditorWindow`（框架，`NodeEditor/Editor/Data/DataEditorWindow.cs`）里。它按左源 / 中列表 / 右明细三栏工作：左侧按归属作用域分三档——**项目 / 领域 / 单图**——列出注册进来的数据源；集合型源在中间列出具体记录；右侧编辑选中记录。两个入口同一窗口：`Tools/NodeGraph/Manager` 的 **Node Editor Data** 按钮是看全项目的「总数据中心」；节点编辑器工具栏的「数据」按钮经 `DataEditorWindow.Open("dialogue", 当前图)` 打开**领域窗口**（绑当前图、只显示 项目级 + 对话领域 + 该图 的源）。

**框架已自带这几源**（`NodeEditor/Editor/Data/FrameworkDataSources.cs`，任何基于本框架的编辑器都白拿）：**全局变量**（项目，全局档黑板，复用 `VariablePane`）、**组变量**（单图，当前图所属「模块+组」那档黑板；图无组则不显示）、本地化表（项目，`LocalizationTablePane`）、图参数总览（单图，只读）。黑板的**模块档**是领域概念，由领域侧注册（见 `DialogueDataSources` 的 `dialogue.blackboard` 源，复用框架的 `FrameworkDataSources.BuildLayerPane`）。**你只需注册对话领域自己的数据**。

加一个数据源 = 在领域 Editor 程序集的 `[InitializeOnLoad]` 静态类里 `DataSourceRegistry.Register`，看 `Packages/com.graphtest.dialogue/Editor/Data/DialogueDataSources.cs`。如果一个对话数据资产包含可编辑记录列表，注册 `DelegateListDataSource`，不要把整个资产塞成一个旧式下拉/嵌套列表；把记录发现放在 `Items(ctx)`，把单条编辑放在 `BuildDetail(ctx,item)`，数据窗口就能稳定显示左侧数据源、中间记录列表、右侧字段编辑：

```csharp
[InitializeOnLoad]
public static class DialogueDataSources
{
    static DialogueDataSources()
    {
        // 用 DelegateListDataSource 免去自写类：id、标题（走 Localizer.UI）、作用域、领域标签、Items、BuildDetail。
        DataSourceRegistry.Register("dialogue.database", ctx =>
            new DelegateListDataSource("dialogue.database", Localizer.UI("ui.dialogueData", "对话数据库"),
                DataScope.Domain, "dialogue",
                c => DialogueDatabaseEditor.Items(DialogueDatabaseLocator.Resolve(out _)),
                (c, item) => DialogueDatabaseEditor.BuildEntryDetail(
                    DialogueDatabaseLocator.Resolve(out _), item)));
    }
}
```

- **`DataScope`**：`Project`（项目全局，如黑板/本地化）/ `Domain`（领域专属，给领域标签如 `"dialogue"`，会被领域窗口的 `DomainFilter` 过滤）/ `Graph`（依赖当前图，未选图时工厂返回 `null` 即不显示）。
- **单面板 vs 列表明细**：没有自然记录列表的源可继续用 `DelegateDataSource` + `BuildUI`；有记录列表的源用 `DelegateListDataSource` / `IListDataSource`。右侧详情只编辑当前 `DataItem`，不要再放一个“选择条目”的下拉框。
- **只读视图**：生成类数据（节点定义/注册表）做成只读卡片即可，别在窗口里改（它们由 Setup 生成）。
- **本地化表**：key×语言 的编辑用现成可复用面板 `new LocalizationTablePane(LocalizationTableLocator.Find())`（按 key 前缀分组、可搜索、增删）。
- 写值一律 `SerializedObject` → `Undo` + `SetDirty`（硬规则 A2），与 `DialogueDatabaseEditor` 同 idiom。
- 对话数据库统一经 `DialogueDatabaseLocator.Resolve(out reason)` 解析：零个返回原因；一个自动使用；
  多个必须由 `DialogueAssetPaths.authoringDatabase` 显式选择，禁止 `FindAssets(...).FirstOrDefault()`。
  解析失败时参数候选返回空列表；数据窗口显示原因和选择字段，字段回调必须 `Undo` + `SetDirty`。
- ⚠️ Unity 6 的 UI Toolkit 也有个同名 `DataSourceContext`：领域文件同时 `using UnityEngine.UIElements` 和 `NodeEditor.EditorUI` 时，加一行 `using DataSourceContext = NodeEditor.EditorUI.DataSourceContext;` 消歧。
- UI 文案走 `Localizer.UI(key, 英文缺省)`，中文在 `DialogueSetup.SetupLocalization` 的 `EnsureUI` 里种。
- 改完**真开 `Tools/NodeGraph/Manager` 的 Node Editor Data 走一遍**：三档作用域都出源、选中能编、改完重载数据还在、console 干净。

---

## 7. 加 play 模式调试视图 / 运行时状态

调试器 `Packages/com.graphtest.nodeeditor/Editor/Support/GraphDebugger.cs` 在 play 模式按节点状态着色/高亮。它读的是运行时实现的 `IRuntimeGraph`（`DialogueRunner` 已实现 `StatusOf`/`RuntimeNodeOf`）。要让调试器找到你的 runner：参考 `Packages/com.graphtest.dialogue/Editor/Support/DialogueRuntimeBridge.cs`（`[InitializeOnLoad]` 订阅 `DialoguePlayer.OnRunnerCreated`/`OnRunnerDestroyed` 转给 `RuntimeGraphRegistry`）。逐节点的运行时小部件（如进度条）走配方 4 的 `OnRuntimeUpdate`。

---

## 8. 加 / 改本地化文案、加一种语言

本地化是横切层（`Packages/com.graphtest.nodeeditor/Runtime/Localization/Localization.cs` + `Localizer.cs`）。解析优先级：**属性 → 兜底表 → 英文缺省**。
- **节点/参数文案**：优先用节点类上的 `[NodeDoc]`/`[ParamDoc]`（配方 1）；或在 `NodeEditorAssetPaths.localizationTablePath` 当前指向的表里按 key（`node.<defId>.name`/`.desc`、`param.<defId>.<paramName>.name`/`.desc`）补。
- **编辑器界面文案（chrome）**：调用处写 `Localizer.UI("ui.xxx", "English default")`，中文在 `LocalizationTable`（`DialogueSetup.SetupLocalization` 里 `EnsureUI` 种）。
- **校验 / 诊断 / 报错文案**：节点红黄框、画布横幅、弹窗表单错误、检视面板明细行标签**也是可见文案**——走 `Localizer.UI("val.xxx"（校验/诊断）/ "ui.errXxx"（表单错误）, "English")`，带 `{0}` 占位的用 `string.Format`；中文同样在 `DialogueSetup.SetupLocalization` 里 `EnsureUI` 种。框架 `GraphValidator` 与对话 `DialogueValidation` 顶部各有一个 `L(key,en[,args])` 助手统一包裹（见配方 2）。**这一类最容易在持续拓展中漏成英文（坑 #12），加任何校验/报错时务必同步本地化。**
- **黑板变量注释（变量面板 tooltip）**：在 `DialogueSetup.SetupLocalization` 里用 `EnsureVarDesc(table, "变量键名", "中文注释", "English")` 种一条 `var.<键名>.desc`（**刻意极简**）；没种的变量 tooltip 自动回退成键名。变量的**默认值**直接在变量面板里按类型内联编辑（无需写代码，自动标脏 + 可撤销）。
- **运行时台词**：数据库是显式创作资产，`DialogueSetup` 不会自动生成。可用 `Create ▸ Dialogue ▸ Database` 在用户选择的 `Assets/` 目录建立生产库。entry 的 `texts` 使用当前语言下拉选择的语言；通用数据窗口与自定义 Inspector 都复用共享“语言下拉 + 全宽多行文本”控件。
- **语言码候选**：`en`/`zh` 等运行时 `lang` 字符串由 `NodeEditorAssetPaths.languageOptionsPath` 当前指向的资产统一定义，`DialogueDatabaseEditor` 的默认语言和每条本地化文本都从这里下拉选择；不要手填语言码。
- **加一种语言**：① `Language` 枚举加值；② `LanguageCodes.Code` 加映射（→ DB 的 lang 串）；③ 在 `LanguageOptions.asset` 里补该 code；④ 给节点补 `[NodeDoc(Language.新语言, …)]` / 表里补该语言条目 / DB 文本补该 lang。
- 切语言：编辑器靠工具栏语言下拉（写 `EditorLocalizationConfig.language` + `Invalidate` + 重建）；运行时靠 `RuntimeLocalizationConfig.language`。

---

## 9. 加悬停浮层 / 自定义浮窗

参考 `Packages/com.graphtest.nodeeditor/Editor/Window/NodeHoverTooltip.cs`（悬停 1 秒弹节点说明）。**坑（坑 #10）**：浮层要挂到 **GraphView 自身**（`node.GetFirstAncestorOfType<GraphCanvas>()`，与 banner/minimap 同级，字体/主题就绪），别挂 `panel.visualTree`（那层无字体 → 框在字不显示）；坐标用 `WorldToLocal` 换算；容器**给固定宽度**（CJK 自动宽度会塌成 1 字宽）。

---

## 10. 固定（不可删）节点 / 新建对话组自动播种

新建一张对话组时会**自动带一个进入节点（Start）和一个退出节点（End）**，二者**钉住**（pinned）固定存在、不可删除。这套是"框架给机制、领域定策略"：

- **机制（框架）**：`NodeInstance.pinned`（`NodeEditor/Runtime/Graph/NodeDataTypes.cs`，构建安全数据字段）。`NodeView` 构造时对 pinned 节点去掉 GraphView 的 `Capabilities.Deletable`（灰掉右键删除、排除 Delete 键/框选删除/剪切），`GraphCanvasView.OnGraphViewChanged` 再防御性地把混入 `elementsToRemove` 的 pinned 节点剔除。pinned 节点仍可移动/选中/改参数，只是删不掉。
- **新图配方缝（框架，按模块）**：`GraphCreationRegistry.Register(GraphCreateRecipe)`。每个配方显式声明按钮文案、默认文件名、图目录、黑板目录和落盘前初始化器；同一模块可注册多个配方，未注册模块仍显示一个创建裸图的「新建」。
- **策略（对话层）**：`Dialogue/Editor/Launcher/DialogueGraphScaffold.cs`（`[InitializeOnLoad]`）只注册 `dialogue.graph` 这一种 `ControlFlow` 配方——落到对话图目录，加一个钉住的 Start（登记进 `entryInstanceIds`）+ 一个钉住的 End（经 `NodeDefinitionLocator.ForType` 解析定义资产，没跑过 Setup 时给提示并跳过）。

**想钉住别的节点**：在创建该 `NodeInstance` 时设 `pinned = true` 即可。**想换新图的初始节点**：改 `DialogueGraphScaffold.Seed`。

---

## 11. 拓展点速查

| 想加什么 | 改哪里 | 机制 |
|---|---|---|
| 对话节点 | `Dialogue/Runtime/<Kind>Node.cs` + `DialogueNodeKind` + `DialogueRunner.Continue` | NodeDefinition 子类 + RebuildFromCode |
| **条件 / 取值 / 副作用 / 编排单元** | 写个类继承 `ConditionUnit`/`ProviderUnit`/`ActionUnit`/`ControlUnit`，标 `[Unit("unit.<domain>.<name>.name", "English Name", "unit.group.<family>", "English Group")]`；全局通用放 `NodeEditor/Runtime/Units/Units.cs`、对话专属放 `Dialogue/Runtime/Units/DialogueUnits.cs`（见配方 1c） | `[SerializeReference]` 内联多态 + `UnitRegistry` 反射发现（全局/领域两级、按族过滤）；节点用 `AddUnitParam(name, family)` 持槽，检视面板自动出「下拉+折叠+装饰嵌套」 |
| 端口单连线 / 多连线 | 节点 `Define()` 里给端口设 `Arity`（见配方 1b） | arity → 容量；圆灰=单、方蓝=多；`PortView.Create` + `EnforceSingleCapacity` |
| 校验规则 | `Dialogue/Editor/Validation/DialogueValidation.cs` | `GraphValidator.RegisterExtension(id,…)` |
| 连接规则（哪种接哪种，include/exclude，双向） | `Dialogue/Editor/Validation/DialogueConnectionRules.cs` 的 `s_Matrix`（见配方 2b；引擎在框架 `ConnectionRuleMatrix`） | `ConnectionRules.RegisterRule(id,…)`；实时挡 `GetCompatiblePorts` + 兜底报 `CheckConnectionRules` |
| 节点线索 / 运行时小部件 | `Dialogue/Editor/Nodes/DialogueNodeViews.cs` | `[NodeViewControl(typeof(Def))]` |
| 参数编辑器 | `NodeEditor/Editor/Inspector/InspectorPane.cs` `EditorFor` | TypeKind 分派；写值走 `WriteOverride` |
| 工具栏 / 快捷键 / 面板 | `NodeEditor/Editor/Window/NodeEditorWindow.cs` | `BuildToolbar` / `RegisterCallback` / `CreateGUI` |
| 数据源（进通用数据窗口） | `Dialogue/Editor/Data/DialogueDataSources.cs`（`[InitializeOnLoad]`） | `DataSourceRegistry.Register`；单面板用 `DelegateDataSource`，列表明细用 `DelegateListDataSource`；选 `DataScope` 项目/领域/单图；框架自带黑板/本地化/图概览三源（见配方 6b） |
| 调试器接活动 runner | `Dialogue/Editor/Support/DialogueRuntimeBridge.cs` | `[InitializeOnLoad]` + `RuntimeGraphRegistry` |
| 本地化 / 语言 | `NodeEditor/Runtime/Localization.cs`、`Localizer.cs`、`LocalizationTable` | 属性 → 表 → 英文回退 |
| 浮层 | `NodeEditor/Editor/Window/NodeHoverTooltip.cs` | 挂 GraphView + 固定宽度 |
| 固定/不可删节点 · 新图播种 | `NodeInstance.pinned` + `Dialogue/Editor/Launcher/DialogueGraphScaffold.cs` | `GraphCreationRegistry` 注册 `dialogue.graph`；pinned 去掉 `Capabilities.Deletable` |
| 生成/再生资产 | `Dialogue/Editor/Setup/DialogueSetup.cs` | `Tools/NodeGraph/Manager` → Dialogue **Setup Assets**（幂等） |

加完任何东西，**真打开 `Tools/NodeGraph/Dialogue` 窗口走一遍真实操作**再算完成。
