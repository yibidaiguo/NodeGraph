# 状态机编辑器 · 拓展指南（面向开发者）

> 给**要给这个编辑器加新功能**的程序员。面向"搭状态机图"的设计师请看 [README.md](README.md)；想在**别的游戏模块里用状态机**（订阅事件、注入黑板、存档、开窗）而非扩展编辑器，请看 [INTEGRATION.md](INTEGRATION.md)——**扩展**编辑器看本文，**接入/使用**编辑器看那份。
> 框架（可复用）在 `Packages/com.graphtest.nodeeditor/`，状态机领域层在 `Packages/com.graphtest.statemachine/`。下面每个配方都标了改哪个文件。
>
> **⚠️ 动手前先读「开发规范」——它是本工程规则的唯一权威来源**：[`Packages/com.graphtest.nodeeditor/ARCHITECTURE.md`](../com.graphtest.nodeeditor/ARCHITECTURE.md) 开头的「⚠️ 开发规范」节（A 数据安全硬规则 / B 架构不变式 / C 工程规范 / D 完成定义）。**涉及任何 UI 时再读 [`NodeEditor/UI-STANDARD.md`](../com.graphtest.nodeeditor/UI-STANDARD.md)**。本模块最常踩的几条速查（完整理由以权威源为准）：
> - **一类一文件**（A1）：每个 `ScriptableObject` / `MonoBehaviour` 具体类一个文件、文件名 == 类名，否则生成的 `.asset` 全坏（`m_Script:0`）。六个节点定义各占一文件即因此。
> - **写序列化数据必标脏 + 可撤销**（A2）：写前 `Undo`、写后 `SetDirty`，面板写值走 `WriteOverride`。
> - **中文 + Localizer · 扩展即本地化**（C11）：所有可见字符串（含校验/拒绝消息）走 `Localizer.UI("…","英文缺省")`，中文在 [Editor/Setup/StateMachineSetup.cs](Editor/Setup/StateMachineSetup.cs) 的 `SetupLocalization` 里 `EnsureUI` 种（坑#12：校验横幅最容易漏）。
> - **完成定义**（D16）：改完真开窗真交互核一遍，"编译过"不算数。

---

## 1. 加一种状态机节点

举例加一个 `History`（历史回归点）节点：

1. **新建定义文件**（一类一文件）`Packages/com.graphtest.statemachine/Runtime/Nodes/HistoryNode.cs`，照 [Runtime/Nodes/StateNode.cs](Runtime/Nodes/StateNode.cs) 的形状：
   ```csharp
   using NodeEditor;
   namespace StateMachine
   {
       [NodeMenu("State Machine/Hierarchy/History")]
       [NodeDoc(Language.English, "History", "Re-enters the last active state of this layer.")]
       [NodeDoc(Language.Chinese, "历史", "回到本层上一次的活动状态。")]
       public class HistoryNode : StateMachineNodeDefinition
       {
           public override StateMachineNodeKind Kind => StateMachineNodeKind.History;
           protected override void Define()
           {
               Meta("History", NodeRole.Control);
               AddIn("in", Arity.Many);
           }
       }
   }
   ```
2. **登记 Kind**：在 [Runtime/Nodes/StateMachineNodes.cs](Runtime/Nodes/StateMachineNodes.cs) 的 `StateMachineNodeKind` 枚举里加 `History`（`StableId = "statemachine." + Kind` 自动派生，别手拼 id）。
3. **接运行时语义**：在 [Runtime/Runner/StateMachineRunner.cs](Runtime/Runner/StateMachineRunner.cs) 里消费新 Kind——转移目标的合法集合在 `FirstHit`，进入/退出语义在 `EnterNode`/`Fire`（参考相邻的 Exit/SubMachine 分支）。
4. **接连接矩阵**：在 [Editor/Validation/StateMachineConnectionRules.cs](Editor/Validation/StateMachineConnectionRules.cs) 的 `s_Matrix` 里补该 Kind 的出/入向规则（`ConnectionRuleEntry` 行），并在 `SetupLocalization` 种 `kind.History` 中文。
5. **重新生成资产**：在 `Tools/NodeGraph/Manager` 里点 State Machine 卡片的 **Setup Assets** 按钮（幂等）——`TypeCache` 自动发现新子类、生成 Def `.asset` 并合进注册表 `projectDomain` 档（只动本域条目，别域保留）。
6. **（可选）画布线索 / 校验**：见配方 4 / 2。补一条 EditMode 测试。

> **🔑 节点分层（开发规范 §3）**：上面是**状态机领域节点**（继承 `StateMachineNodeDefinition`、放本模块、进 `projectDomain` 档）。**跨领域通用节点**属于框架（直接继承 `NodeDefinition`、放 `NodeEditor/Runtime/`、进 `universal` 档）——别把通用节点塞进状态机模块，也别让框架认识状态机节点。

---

## 1b. 加一个条件 / 动作 / 取值 / 编排单元（可组合 · 可装饰）

> **核心原则（红线#13）**：节点不烘门控/比较/赋值参数，改持「Unit 槽」——State/SubMachine 的 `onEnter`/`onUpdate`/`onExit`（Action 族）与 Transition 的 `condition`（Condition 族）都是 `AddUnitParam` 槽。

1. **写单元类**：继承族基类（`ConditionUnit`/`ProviderUnit`/`ActionUnit`/`ControlUnit`，定义在 [../com.graphtest.nodeeditor/Runtime/Graph/NodeRuntime.cs](../com.graphtest.nodeeditor/Runtime/Graph/NodeRuntime.cs)），实现入口（`Evaluate`/`Get`/`Execute`/`Tick`），标 `[Unit("显示名","分组")]`。
   - **全局通用**（只用 `ctx.blackboard`）→ 放框架 [../com.graphtest.nodeeditor/Runtime/Units/Units.cs](../com.graphtest.nodeeditor/Runtime/Units/Units.cs)。
   - **状态机专属**（要领域能力）→ 放 `Packages/com.graphtest.statemachine/Runtime/Units/`（一单元一文件不强制——单元是普通 `[Serializable]` 类，非 SO），照 [Runtime/Units/FireMachineEventAction.cs](Runtime/Units/FireMachineEventAction.cs)：领域能力经 `ctx.blackboard as IMachineEventSink` 强转取得（接口在 [Runtime/Runner/StateMachineRunContext.cs](Runtime/Runner/StateMachineRunContext.cs)），别让框架的 `IScopedBlackboard` 认识领域概念。
   - 黑板键字段标 `[BlackboardKey]` → 检视面板渲染成已声明 key 的下拉。
   - ⚠️ **族基类必须保持 `[Serializable]`**（A1 子条）：否则装饰器的 `List<基类>` 存盘整段丢失。
2. **节点持槽**：`Define()` 里 `AddUnitParam("condition", "Condition")`；运行时 `ParamResolver.ResolveUnit(inst, "condition") as ConditionUnit` 取出求值（成例见 Runner 的 `RunAction`/`ConditionTrue`）。
3. 改完**真开窗**：Unit 槽下拉确认分组/族过滤，套个装饰器存盘重开看数据还在，进 play 看生效。

单元可按公开组合契约嵌套：`CompareCondition` 可组合算术/读黑板取值树，`AndCondition` 组合多个条件，`SequenceAction` 串联多项动作。具体业务组合属于项目资产，由使用方按自己的资源路径和状态机需求创建。

---

## 2. 加一条校验规则（红框/黄框 + 画布横幅）

经 `GraphValidator.RegisterExtension(id, fn)` 钩子接入，**不改框架**——看 [Editor/Validation/StateMachineValidation.cs](Editor/Validation/StateMachineValidation.cs)：`[InitializeOnLoad]` 静态构造里按稳定 id `"statemachine"` 注册 `CheckAll`；加规则 = 在 `CheckAll` 里多 `yield` 几条 `ValidationIssue`（节点级 target 填 `inst.instanceId`，图级填 `GraphValidator.GraphIssueTarget` 走横幅）。域判定：只在图里含本域节点时插手。
> **校验消息也是可见文案（C11 / 坑#12）**：用文件顶部的 `L("val.sm.xxx","English"[,args])` 助手包裹，中文在 `StateMachineSetup.SetupLocalization` 里 `EnsureUI` 种、重跑一次 Setup 落表。改完切中文核一遍红黄框/横幅。

---

## 3. 加一条连接规则（哪种节点能接哪种，include/exclude，双向）

规则集中在 [Editor/Validation/StateMachineConnectionRules.cs](Editor/Validation/StateMachineConnectionRules.cs) 的矩阵 `s_Matrix`（一条规则一行：node/port/side/mode/kinds；判定引擎在框架 `ConnectionRuleMatrix<TDef,TKind>`，拒绝消息统一 `val.conn*` 键），经框架钩子 `ConnectionRules.RegisterRule("statemachine", s_Matrix.Evaluate)` 注册。两道关共用这份规则：拖拽期直接连不上（`GetCompatiblePorts`）+ 已存在的非法边校验标红（`CheckConnectionRules`）。加新 Kind 时在此补出/入向两行；拒绝消息的节点种类名走 `kind.<Kind>` 本地化键。当前矩阵即 State→Transition→State 三段式的完整约定（`Entry.out→{State,SubMachine}`、`Transition.to→{State,SubMachine,Exit}` 等，见文件头注释）。

---

## 4. 加节点上的可读性线索（cue，画布上不展开也能看出参数）

走共享 `NodeCueControl` 扩展点（框架 [../com.graphtest.nodeeditor/Editor/Window/NodeViewControl.cs](../com.graphtest.nodeeditor/Editor/Window/NodeViewControl.cs)），本域实现见 [Editor/Nodes/StateMachineNodeViews.cs](Editor/Nodes/StateMachineNodeViews.cs)——每种节点一个 `[NodeViewControl(typeof(XxxNode))]` 类，只重写 `Describe(inst, def)` 返回一行文案（如 Transition 显示「? 条件摘要 [p1]」、State 显示「进/更/出」已配槽位）。领域只给文案；标签/刷新/截断/样式由框架统一。文案走 `Localizer.UI("ui.sm.cue.xxx", "English")` + Setup 种中文。**不新增图片资源、不自建 cue USS。**

---

## 5. 把数据接进通用数据编辑窗口（数据源）

在领域 Editor 程序集的 `[InitializeOnLoad]` 静态类里 `DataSourceRegistry.Register`，看 [Editor/Data/StateMachineDataSources.cs](Editor/Data/StateMachineDataSources.cs)：本域已注册「状态机模块变量」（领域档黑板，复用框架 `VariablePane`/`BuildLayerPane`）与「状态机节点定义（只读）」两源。加新源照抄——集合型用 `DelegateListDataSource`（Items + BuildDetail 分离），单面板用 `DelegateDataSource`；`DataScope` 选 项目/领域/单图；生成类数据做只读卡片。⚠️ Unity 6 的 UI Toolkit 有同名 `DataSourceContext`，文件顶部加 `using DataSourceContext = NodeEditor.EditorUI.DataSourceContext;` 消歧（本文件已示范）。标题文案走 `Localizer.UI` + Setup 种中文。

---

## 6. 其他常用扩展落点速查

| 想加什么 | 改哪里 | 机制 |
|---|---|---|
| 状态机节点 | `Runtime/Nodes/<Kind>Node.cs` + [StateMachineNodes.cs](Runtime/Nodes/StateMachineNodes.cs) + [StateMachineRunner.cs](Runtime/Runner/StateMachineRunner.cs) | NodeDefinition 子类 + RebuildFromCode（配方 1） |
| 条件/动作/取值/编排单元 | 框架 [Units.cs](../com.graphtest.nodeeditor/Runtime/Units/Units.cs)（通用）或 `Runtime/Units/`（领域） | `[SerializeReference]` 内联多态 + `UnitRegistry` 反射发现（配方 1b） |
| 校验规则 | [Editor/Validation/StateMachineValidation.cs](Editor/Validation/StateMachineValidation.cs) | `GraphValidator.RegisterExtension`（配方 2） |
| 连接规则 | [Editor/Validation/StateMachineConnectionRules.cs](Editor/Validation/StateMachineConnectionRules.cs) 的 `s_Matrix` | `ConnectionRules.RegisterRule`（配方 3；引擎在框架 `ConnectionRuleMatrix`） |
| 节点线索 cue | [Editor/Nodes/StateMachineNodeViews.cs](Editor/Nodes/StateMachineNodeViews.cs) | `[NodeViewControl(typeof(Def))]`（配方 4） |
| 数据源 | [Editor/Data/StateMachineDataSources.cs](Editor/Data/StateMachineDataSources.cs) | `DataSourceRegistry.Register`（配方 5） |
| 新图播种 / 钉住节点 | [Editor/Launcher/StateMachineGraphScaffold.cs](Editor/Launcher/StateMachineGraphScaffold.cs) | `GraphListPane.RegisterModuleInitializer` + `NodeInstance.pinned` |
| 调试器接活动 runner | [Editor/Support/StateMachineRuntimeBridge.cs](Editor/Support/StateMachineRuntimeBridge.cs) | `[InitializeOnLoad]` + `RuntimeGraphRegistry`（照 [DialogueRuntimeBridge](../com.graphtest.dialogue/Editor/Support/DialogueRuntimeBridge.cs)） |
| 生成/再生领域定义与配置资产 | [Editor/Setup/StateMachineSetup.cs](Editor/Setup/StateMachineSetup.cs) | `Tools/NodeGraph/Manager` → State Machine **Setup Assets**（幂等，不依赖开发样例） |
| 领域资产落点 | [Runtime/Data/StateMachineAssetPaths.cs](Runtime/Data/StateMachineAssetPaths.cs)（SO） | 挪目录只改 SO 不动代码（准则#14） |

加完任何东西，**真打开 `Tools/NodeGraph/State Machine` 走一遍真实操作**（连线、检视、校验、切语言、play 调试着色）再算完成。
