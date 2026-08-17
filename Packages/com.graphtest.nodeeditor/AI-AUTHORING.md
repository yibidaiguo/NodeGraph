# NodeGraph AI 创作指南

这套接口让 AI、脚本与 Unity 节点编辑器编辑同一份图。Unity 的 `NodeGraphAsset` 和它引用的 `BlackboardAsset` 是唯一真源；JSON 是一次读写事务的可审阅投影，不是需要同步的第二份资产，也不应作为 sidecar 提交到内容目录。

## 最短工作流

1. `Describe(module)`：读取该模块可用的节点、端口、参数、Unit 和黑板变量。
2. `List(module)`：只在该模块注册的内容根下列出现有图，取得准确的 `AssetPath`。
3. 编辑现有图时调用 `Read(assetPath)`；创建新图时调用
   `CreateDraft(assetPath, module, group, graphType)`，直接取得当前有效黑板及所有 owner revision，禁止靠 seed 图拼草稿。
4. 只修改文档中的语义字段，保留所有未修改 owner 的 revision 项。
5. `Write(assetPath, document)`：由框架统一解析、校验、冲突检查并原子提交。
6. 使用返回的 `WriteResult.Document` 继续下一轮；不要继续复用写入前的 revision。

编辑器代码入口位于 `NodeEditor.EditorUI.GraphAuthoringAssetAccess`：

```csharp
var catalog = GraphAuthoringAssetAccess.Describe("dialogue");
var graphs = GraphAuthoringAssetAccess.List("dialogue");
var read = GraphAuthoringAssetAccess.Read(assetPath);
var draft = GraphAuthoringAssetAccess.CreateDraft(
    "Assets/Dialogue/New.asset", "dialogue", "chapter1", GraphType.ControlFlow);
var write = GraphAuthoringAssetAccess.Write(assetPath, read.Document);
```

这些调用都返回 `Succeeded`、结构化 `Diagnostics` 和各自的数据字段（`Catalog`、`Graphs` 或 `Document`）。另有 `Validate(assetPath, document)`，它与 `Write` 共用完整 preflight，但绝不提交。失败时先处理诊断，不要从日志文本猜测结果。

`Read`、`CreateDraft`、`List` 和 `Describe` 绝对只读：不会创建路径配置、目录、图或黑板，不会补写旧资产，也不会标脏已有资产。`CreateDraft` 只接受尚不存在且位于已注册 graph root 下的目标路径，返回空 `graphId`、图 owner 的 `MustNotExist` revision，以及 global → module → exact-group 的完整有效黑板快照和实时 revision。模块尚未配置时应返回诊断或空目录结果；调用方不得猜测 `Assets/` 落点。

## 唯一真源与往返

人工编辑直接改 Unity 资产；AI 写入也通过 `GraphAuthoringAssetAccess.Write` 回到同一批资产。服务使用 `NodeGraphAsset.ToData()` / `FromData()` 的同源契约，不维护平行的 Graph DTO 资产：

```text
Unity 图/黑板资产 -> Read -> GraphAuthoringDocument -> Write -> 同一 Unity 图/黑板资产
```

因此人工保存后的下一次 `Read` 会立刻看到变化，AI 成功 `Write` 后人工编辑器打开的也是同一份内容。若需要把 JSON 放进提示词或代码评审，只把它当作短期交换文件；写回后以服务返回的新文档为准。

## 身份规则

- `graphId` 是跨资产引用使用的稳定图身份。新图第一次成功写入时取其 Unity `.meta` GUID；后续写入必须保留。
- `instanceId` 是节点的稳定运行时身份，连线、存档和引用依赖它。节点改名或移动不能更改它。
- `authoringKey` 是持久化在 `NodeInstance` 上、图内按 `StringComparer.Ordinal` 唯一的作者地址；边与入口用它寻址。重命名只改 `authoringKey`，不能改 `instanceId`。
- 旧图缺少 `authoringKey` 时，`Read` 只在返回文档中按 `instanceId` 确定性回填，不修改资产；第一次成功 `Write`（或显式迁移）才把 key 落盘。
- `displayName`、`note` 和节点在列表中的位置都不是身份，禁止从它们派生 key。

创建新图时，给每个节点明确且有语义的 `authoringKey`，并为 `instanceId` 使用真正稳定的值。编辑现有图时保留 `Read` 返回的两个身份字段，除非操作本身就是显式重命名 key。

## Revision vector 与原子写入

一份文档可能同时拥有图资产和全局/模块/组黑板。`revisionVector.owners` 是整个提交的乐观并发前置条件，每项包含稳定 `ownerId`、规范 `ownerPath`、`contentHash` 和 `expectedState`：

- 编辑现有内容：完整保留 `Read` 返回的所有 owner 项和 hash。
- 创建新图或新黑板：为目标路径提供 `MustNotExist` owner，`contentHash` 留空。
- 不要删除“本次没改”的 owner；它仍参与同一次一致性检查。

`Write` 在任何 Unity 写入前完成 JSON/模型解析、资产解析、语义校验和所有 owner 的 revision 检查。任一 owner 过期都会按 owner 返回冲突诊断，整批提交不写入。通过检查后，图与所有黑板处于同一 Undo group；写入失败会整体回滚，连新建资产与目录也会清理。不要绕过服务逐个写 owner，否则会破坏图与黑板的原子性。

## JSON 约定

统一使用 `GraphAuthoringJson.SerializeDocument` 和 `DeserializeDocument`。序列化结果采用固定字段顺序、缩进格式、Invariant Culture、字符串 enum 和显式 null，便于稳定 diff。解析是严格的，会拒绝：

- 空内容、JSON 注释、重复属性、未知属性、大小写不精确的属性名、缺失的必需属性；
- 单引号/未加引号的属性或字符串、尾随逗号、`undefined`、构造器及 `NaN`/Infinity；
- 整数形式的 enum、类型不兼容的值、`null` 根值；
- 一个根值之后的任何额外内容。

解析失败返回 `GraphAuthoringDiagnostic`，不会产生可写文档。不要用 `JsonUtility`、默认 Newtonsoft 设置或自建宽松 DTO 解析同一格式。

## Catalog：先查询，再创作

`Describe(module)` 返回当前项目真实可用的能力目录，而不是示例清单：

- `Definitions`：稳定 definition id、所属模块、端口及 arity、参数类型/default、版本；
- `Units`：稳定 Unit id、角色/族、所有必填字段、scalar 类型、enum 值和嵌套 Unit 约束；
- `BlackboardVariables`：全局、模块、组作用域中当前可见的变量；
- `UnitIds`：旧只读调用方的兼容视图，新代码优先读取完整 `Units`。

只使用 catalog 中存在且属于当前模块的 id。参数、端口、Unit 字段和黑板引用都应按 catalog 生成；不要从 CLR 类型名、菜单显示名或本地化文本推断稳定 id。

## 命令行

Unity batchmode 的入口是 `NodeEditor.EditorUI.GraphAuthoringCommandLine.Run`：

```powershell
Unity.exe -batchmode -quit -projectPath <Project> `
  -executeMethod NodeEditor.EditorUI.GraphAuthoringCommandLine.Run `
  -graphAuthoringCommand describe -graphAuthoringModule dialogue `
  -graphAuthoringOutput <catalog.json>

Unity.exe -batchmode -quit -projectPath <Project> `
  -executeMethod NodeEditor.EditorUI.GraphAuthoringCommandLine.Run `
  -graphAuthoringCommand read -graphAuthoringAsset Assets/Dialogue/MyGraph.asset `
  -graphAuthoringOutput <graph.json>

Unity.exe -batchmode -quit -projectPath <Project> `
  -executeMethod NodeEditor.EditorUI.GraphAuthoringCommandLine.Run `
  -graphAuthoringCommand draft -graphAuthoringModule dialogue `
  -graphAuthoringGroup chapter1 -graphAuthoringGraphType ControlFlow `
  -graphAuthoringAsset Assets/Dialogue/NewGraph.asset `
  -graphAuthoringOutput <draft.json>

Unity.exe -batchmode -quit -projectPath <Project> `
  -executeMethod NodeEditor.EditorUI.GraphAuthoringCommandLine.Run `
  -graphAuthoringCommand write -graphAuthoringAsset Assets/Dialogue/MyGraph.asset `
  -graphAuthoringInput <graph.json> -graphAuthoringOutput <result.json>
```

命令为 `list`、`describe`、`read`、`draft`、`write`、`validate`。`list/describe` 可带 `-graphAuthoringModule`；省略时查询 NodeGraph 通用/core 能力。`read` 需要 `-graphAuthoringAsset`。`draft` 必须带 asset/module，可选 group，graphType 缺省为 `ControlFlow` 且提供时必须使用区分大小写的精确枚举名。`write` 需要 asset 和 UTF-8 input。`validate` 必须带目标 asset，可选 input；省略 input 时先读取目标资产，再走与 `Write` 相同的只读 preflight。省略 output 时结果写入 Unity 日志。任何未知 `-graphAuthoring*` 参数或不适用于当前命令的已知参数都会失败，不会被静默忽略。

所有命令输出同一 envelope：`command`、`data`、`diagnostics`、`succeeded`。成功退出码为 0，参数、JSON、校验、冲突或写入失败均为 1。自动化必须同时检查退出码与 `succeeded`。

## 诊断处理

每条诊断包含稳定 `code`、机器可定位的 `path`、`severity` 和供人阅读的 `message`。控制流应只依赖 code/path，不要解析或匹配 message。常见类别：

- `json.*`：JSON 边界错误；修正文档后重试。
- `command.*`：命令或文件参数错误。
- `semantic.*` / codec 诊断：节点、端口、参数、Unit、黑板或引用不符合 catalog。
- revision/conflict 诊断：至少一个 owner 已变化；重新 `Read`，在新文档上合并，不要强行覆盖。
- asset/path/config 诊断：目标不在注册根、项目配置不唯一或资产类型不匹配；先修正项目状态。

任何失败都应被视为“零提交”。若观察到失败后有部分图或黑板变化，这是框架缺陷，应保留输入和诊断并报告。

## 扩展一个领域模块

通用机制只放 `com.graphtest.nodeeditor`；领域路径和领域 Unit 注册跟随自己的模块包：

1. 在领域 Editor 程序集中注册唯一 `GraphAuthoringModuleDescriptor(moduleId, graphRoots, unitTypes)`。
2. `graphRoots` 只调用该领域现有 `<Domain>AssetPathsLocator.Find()`，返回配置中的图根；缺配置返回空列表，不能创建配置或硬编码默认目录。
3. 领域专属 Unit 留在领域 Runtime 包，并声明稳定 `[UnitAuthoringId("domain.name")]`；注册具体类型。通用 Unit 仍归 NodeGraph。
4. 节点能力继续进入现有 `NodeRegistry`；不要另建 AI 专用节点表、GraphAdapter 镜像或领域 JSON 真源。
5. 为注册、目录隔离、catalog 和完整读写往返补 EditMode 测试，并确保测试只清理自己的精确临时子树。

领域细节见 [Dialogue 扩展指南](../com.graphtest.dialogue/EXTENDING.md)、[Task 扩展指南](../com.graphtest.task/EXTENDING.md) 和 [State Machine 扩展指南](../com.graphtest.statemachine/EXTENDING.md)。
