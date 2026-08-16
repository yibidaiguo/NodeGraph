# NodeGraph Node Editor

NodeGraph 的可复用节点编辑器框架，也是模块化安装的唯一手动入口。框架包只包含 Runtime、Editor 和 Module Manager，不会自动安装模块，也不会导入示例场景、示例数据或示例脚本。

## 安装根 / Install Root

节点图在工程里只占一个顶级目录，默认 `Assets/NodeGraph/`：设置资产在 `Settings/`，各模块生成的内容在同名子目录下。

```text
Assets/NodeGraph/
  Settings/                     NodeEditorAssetPaths、DialogueAssetPaths…
  NodeEditor/ Dialogue/ Task/ StateMachine/
```

安装根写在 **安装路径 / Install Paths** 窗口最上面那一栏，改完点 **应用 / Apply**，下面各模块的落点会跟着重算。它存在 `ProjectSettings/NodeGraphInstall.json`——工程级配置，随仓库提交，不占 `Assets/`，卸载清理会把它一并带走。

The framework occupies a single top-level folder, `Assets/NodeGraph/` by default. Change it in the **Install Root** field at the top of the **Install Paths** window; it is stored in `ProjectSettings/NodeGraphInstall.json`, outside `Assets/`.

**已有工程不动。** `Assets/NodeEditorSettings/` 还在、且没写过 `NodeGraphInstall.json` 的工程，路径与升级前逐字一致（`Assets/NodeEditorSettings/`、`Assets/<模块>Content/`）。要收进单一安装根，先手工把资产搬到新根下，再在窗口里填新根——框架不会自动搬运作者资产。

Projects that still have `Assets/NodeEditorSettings/` keep their pre-upgrade paths untouched until an install root is pinned explicitly.

## 卸载清理 / Uninstall Cleanup

在 `Tools/NodeGraph/Manager` 点模块的 **Remove**，会先弹出 **卸载清理 / Uninstall Cleanup**，逐条列出这个模块生成的资产、会变空的目录、编辑器偏好与工程配置文件，确认后才删，然后才移除包。顺序不能反：包一移走，认识这批生成物的代码就没了，残留从此没人能清。

被工程其它资产引用的条目单独一栏、默认保留——删掉它们会在别处留下丢失引用；要删得显式勾选。选 **保留文件，只移除包 / Keep Files** 就只移包不删文件。

框架自己不能在这个窗口里移除（Manager 就住在它里面）。清理框架用它卡片上的 **Clean Up Generated Files**，清完再去 Package Manager 移包。

Removing a module opens **Uninstall Cleanup** first: it lists the generated assets, folders, editor preferences, and project settings that module owns, and deletes them only after confirmation — before the package is removed. Assets still referenced elsewhere are listed separately and kept by default. The framework itself is cleaned from **Clean Up Generated Files** on its own card.

## 首次安装路径向导 / First-install Path Wizard

其他用户首次安装本包时，Unity 会自动弹出 **安装路径 / Install Paths** 窗口，显示 `NodeEditorAssetPaths` 的全部可配置路径。窗口里的配置只是内存草稿；点击 **保存并生成 / Save & Generate** 后才会创建 `Assets/NodeGraph/Settings/NodeEditorAssetPaths.asset`，随后生成注册表、全局黑板和本地化等框架资产。所有字符串路径必须位于规范化的 `Assets/` 子目录中。

For package consumers, the first installation automatically opens **Install Paths** with every `NodeEditorAssetPaths` field. The configuration is an in-memory draft: `Assets/NodeGraph/Settings/NodeEditorAssetPaths.asset` and framework assets are created only after **Save & Generate**. Every string path must be a normalized location below `Assets/`.

同时安装多个正式模块时，窗口会按框架、Dialogue、Task、State Machine 顺序逐个出现。选择 **稍后 / Later** 或直接关闭不会保存或生成，并在下次启动 Unity 时再次提示。已有路径配置的项目不会被自动修改；之后仍可在 `Tools/NodeGraph/Manager` 使用 **Open Asset Paths** 与 **Setup Assets**。

When several production modules arrive together, their windows are queued in Framework, Dialogue, Task, then State Machine order. **Later** or closing the window writes nothing and defers the queue until the next Unity session. Existing configurations are never changed automatically; **Open Asset Paths** and **Setup Assets** remain available in `Tools/NodeGraph/Manager`.

## 安装与使用

1. 在 Unity Package Manager 中添加一次框架 Git URL：

   ```text
   <repository>.git?path=/Packages/com.graphtest.nodeeditor#v0.0.6
   ```

2. 打开 `Tools/NodeGraph/Manager`。
3. 按需安装 Dialogue、Task 或 State Machine。Manager 会复用框架包的仓库和 revision，不需要再次输入 Git URL。
4. 在 Manager 中运行模块的 `Setup Assets`、`Open Asset Paths` 等操作。
5. 仅在需要示例时安装对应的 `.samples` 包，再在 Manager 中显式导入 sample。

`Development/` 仅用于本地测试、验证日志和迁移材料，不属于产品包或安装入口。

## 文档

框架文档随包发布（就在本目录）：

- [`VISION.md`](VISION.md) —— **先读层**·全项目全景：模块地图 / 依赖方向 / 跨模块接口契约（缝）/ 读图导航。
- [`ARCHITECTURE.md`](ARCHITECTURE.md) —— 框架架构 + **全工程唯一权威开发规范**（A 数据安全 / B 架构不变式 / C 工程规范 / D 完成定义）。
- [`UI-STANDARD.md`](UI-STANDARD.md) —— 编辑器 UI 唯一权威规范：组件目录 / 铁律 / 扩展流程（契约测试强制）。

每个领域模块各自随包带一套角色文档 **README**（设计师）/ **EXTENDING**（领域开发）/ **INTEGRATION**（集成）/ **VISION**：Dialogue 见 `com.graphtest.dialogue/`，Task 见 `com.graphtest.task/`，State Machine 见 `com.graphtest.statemachine/`。
