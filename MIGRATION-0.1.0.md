# 0.0.6 → 0.1.0 迁移说明

0.1.0 把执行层从 Unity 上摘了下来。四个包的 `Runtime/` 现在是**零 UnityEngine 依赖的纯 C#**，
可以在 .NET 控制台 / 服务器 / `dotnet test` 下运行；Unity 侧只保留载体与编辑器。

**没有第二个执行器。** 三个领域执行器（DialogueRunner / StateMachineRunner / TaskRunner）是整体
搬进纯层的同一份实现，Unity 侧不留副本——语义不可能漂移。

---

## 你的资产要不要改？大概率不用

| 数据 | 是否需要迁移 |
|---|---|
| 节点位置 `position` | **不用**。`Vector2` 换成纯 C# 的 `Vec2`，字段名与顺序一致，**值**完全等价、双向读得通。注意文本形态会变：Unity 把 `Vector2` 写成内联 `{x: 20, y: 20}`，把自定义结构写成块状映射（`position:` 换行后 `x:` / `y:` 各一行）。首次保存图时这些行会重排，diff 上看着动了不少，但没有任何数据变化。 |
| 端口/参数类型（`TypeRef`） | **不用**。纯层保留了 `NodeEditor.Runtime` 这个程序集名，已烘好的 `[SerializeReference]` 记录（`asm: NodeEditor.Runtime`）继续解析。 |
| 节点定义 `.asset` | **不用**。`NodeDefinition` 仍是 ScriptableObject，`.cs.meta` 的 GUID 原样保留。 |
| 黑板、本地化表、注册表 | **不用**。 |
| **子图引用**（`SubDialogue.subGraph` / `SubMachine.graph` / `Task.stepGraph`） | **要**。见下。 |

### 子图引用：唯一需要动的数据

0.0.x 把子图存为 `UnityEngine.Object` 直连引用（`NodeInstance.objectOverrides`）。
这是数据模型里最后一处真实的 Unity 类型依赖，也是执行器进不了逻辑层的原因之一。
实测运行期四处读取**全部**强转成 `NodeGraphAsset`——它从来只表达"图指向图"，
所以 0.1.0 收敛为稳定 `graphId` 字符串（`NodeInstance.graphRefs`）。

**升级步骤**：打开工程，菜单 `NodeEditor / Migrate / Upgrade Graph References (0.1.0)`。
它会扫描所有 `NodeGraphAsset`，为缺失的图播种 `graphId`（取该 asset 的 GUID），
并把旧 `objectOverrides` 里的图引用转成 `graphRefs`。跑一次即可，可重复执行。

> 迁移工具直接读 `.asset` 的 YAML 文本，不经 Unity 反序列化——因为 `objectOverrides` 字段
> 在新代码里已不存在，走反序列化会读不到。所以**升级后先跑迁移、再保存任何图**，
> 否则保存会把尚未迁移的旧字段抹掉。

### 运行时构建要显式挂接子图

编辑期有 `AssetDatabase`，按 `graphId` 全库搜得到，所以**创作体验不变**——照旧右键选图。

但 player 构建没有 `AssetDatabase`。因此 `DialoguePlayer` / `StateMachinePlayer` 新增了
`subGraphs` 字段，需要把用到的子图挂上去——**和 `blackboards` 一直以来的做法完全一致**
（0.0.x 的注释就写着"运行时构建无 AssetDatabase，故各档在此显式引用"）。
菜单 `NodeEditor / Collect Sub Graphs` 可对选中的 Player 一键收集。

---

## 代码要改的地方

### 1. `ParamResolver.Resolve` 现在吃 `NodeSchema`

```csharp
// 0.0.6
ParamResolver.Resolve(inst, def, "lineKey");
// 0.1.0 —— def.Schema 带缓存，热路径上不会反复分配
ParamResolver.Resolve(inst, def.Schema, "lineKey");
```

`PortExists(def, port)` 同理改为 `PortExists(def.Schema, port)`，或直接用 `schema.PortExists(port)`。

### 2. `ResolveObject` → `ResolveGraphRef` / `GraphRefs.Resolve`

```csharp
// 0.0.6
var sub = ParamResolver.ResolveObject(inst, "subGraph") as NodeGraphAsset;
// 0.1.0，编辑器侧（经 AssetDatabase 解析）
var sub = GraphRefs.Resolve(inst, "subGraph");
// 0.1.0，运行时侧（经注入的 IGraphSource）
var sub = graphSource?.FindGraph(ParamResolver.ResolveGraphRef(inst, "subGraph"));
```

### 3. 执行器构造签名放宽为接口（**既有调用点无需修改**）

`NodeRegistry` 实现了 `ISchemaSource`，`DialogueDatabase` 实现了 `IDialogueTextSource`，
`BlackboardAsset` 实现了 `IBlackboardDecl`。所以下面这行 0.0.6 的代码在 0.1.0 里原样编译：

```csharp
var runner = new DialogueRunner(registry, new BlackboardSet(blackboards), database, lang);
```

新增的 `graphs` / `log` 参数都有默认值。要跑子对话就传 `IGraphSource`。

### 4. `Run(...)` / `OwnsGraph(...)` 接收 `GraphData`

```csharp
runner.Run(graphAsset.ToData());
```

`ToData()` **有缓存且返回同一实例**，所以拿它做字典键、做引用相等判断（`OwnsGraph`、子图栈帧比较）
的语义与 0.0.x 直接用 asset 时完全一致。它与 asset **共享同一个 `instances` 列表**，不是拷贝——
编辑器改了执行器立刻可见。

### 5. `DialogueLineView` 去掉了 `portrait` / `voice`

执行器搬运引擎资产引用，正是它进不了逻辑层的原因。改为携带 `lineKey`，由表现层回查：

```csharp
// 0.0.6
image.sprite = view.portrait;
// 0.1.0
var entry = database.Find(view.lineKey);
image.sprite = entry?.portrait;
```

### 6. `DialogueState` 的图引用变成 `graphId` 字符串

这其实**修好了 0.0.x 自己承认的一个缺陷**——原注释写着"DialogueState 的原始 JSON 不会往返
复原图指针，跨会话必须由调用方把图引用映射为稳定 asset id"。现在存的本来就是稳定 id，
读档经 `IGraphSource` 解析回来，跨会话直接可用。

`DialogueState.graph`（`NodeGraphAsset`）→ `DialogueState.graphId`（`string`）；
`Frame2.graph` → `Frame2.graphId`。旧存档需要用图的 asset GUID 回填这两个字段。

### 7. 日志改为注入

纯层不能引用 `UnityEngine.Debug`。执行器改用 `IGraphLog`；Unity 侧的 `UnityGraphLog`
会在进入播放模式时自动装上（`[RuntimeInitializeOnLoadMethod]`），**行为无变化**。
`dotnet test` 下默认是 Null 实现，控制台干净；测试要断言报错就传 `CollectingGraphLog`。

---

## 一处需要调用方配合的语义差异

Unity 为 `UnityEngine.Object` 重载了 `operator ==`，使**已销毁/丢失的资产**比较为 `null`。
**通过接口引用比较时该重载不生效**（走普通引用相等）。

因此 `BlackboardSet` 里的 `!= null` 过滤不再能剔除"已销毁的 `BlackboardAsset`"。
约定：Unity 侧调用方（`BlackboardLocator` / 各 `Player`）在构造前自行剔除——
它们持有强类型引用，那里才判得准。`NodeGraphSource` 已经这么做了。

正常工程碰不到这条（要资产在运行中被销毁才会），但如果你有动态卸载资产的逻辑，请注意。

---

## 程序集变化

| 0.0.6 | 0.1.0 |
|---|---|
| `NodeEditor.Runtime`（纯层 + 载体混在一起） | `NodeEditor.Runtime`（**纯层**）+ `NodeEditor.Unity`（载体） |
| `Dialogue.Runtime` | `Dialogue.Runtime`（纯层）+ `Dialogue.Unity` |
| `StateMachine.Runtime` | `StateMachine.Runtime`（纯层）+ `StateMachine.Unity` |
| `Task.Runtime` | `Task.Runtime`（纯层）+ `Task.Unity` |

**纯层刻意保留了原程序集名**。已发布资产里有 90 条 `[SerializeReference]` 记录写死了
`asm: NodeEditor.Runtime`（`TypeRef` / `Constraint` / `Unit` 全家）。若把这些类型挪进一个新名字的
程序集，每个节点定义的端口和参数都会**静默**反序列化成 null。所以搬走的是载体不是纯层——
载体靠 MonoScript GUID 绑定，只要 `.cs.meta` 跟着文件走就安全。

如果你的代码有自己的 asmdef 引用了这些包，请补上对应的 `*.Unity` 引用（类型仍在 `NodeEditor`
等原命名空间下，**代码里的 using 不用改**）。

---

## 怎么验证纯度

```
dotnet build Packages/NodeEditor.Runtime.csproj
dotnet run --project Tools/PureCheck -- <编出来的 dll> --shim NodeEditor.UnityShim
```

`PureCheck` 同时验证三件事：纯程序集不引用真 UnityEngine；shim 没被编进纯程序集
（否则"引用列表里没有 UnityEngine"会假通过）；shim 里只有 Attribute——
往里加任何带运行时语义的类型都会红。

跑纯 C# 门禁：

```
dotnet test Tests/NodeGraph.Core.Tests
```
