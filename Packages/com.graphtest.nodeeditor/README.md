# NodeGraph Node Editor

NodeGraph 的可复用节点编辑器框架，也是模块化安装的唯一手动入口。框架包只包含 Runtime、Editor 和 Module Manager，不会自动安装模块，也不会导入示例场景、示例数据或示例脚本。

## 安装与使用

1. 在 Unity Package Manager 中添加一次框架 Git URL：

   ```text
   <repository>.git?path=/Packages/com.graphtest.nodeeditor#v0.0.1
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
