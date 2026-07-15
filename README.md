# NodeGraph

Unity 6 模块化节点图编辑框架。

A modular node-graph editor framework for Unity 6.

## 中文

### 简介

NodeGraph 提供可复用的节点图运行时、编辑器框架和模块管理器，并包含可选的对话、任务、状态机与示例包。

### 环境要求

- Unity 6（`6000.0` 或更高版本）
- 系统 `PATH` 中可用的 Git 2.14 或更高版本

### 安装

在 Unity 中打开 **Window > Package Management > Package Manager**，选择 **Install package from git URL**，然后输入：

```text
https://github.com/yibidaiguo/NodeGraph.git?path=/Packages/com.graphtest.nodeeditor#v0.0.3
```

安装后打开 **Tools > NodeGraph > Manager**，即可从同一仓库和版本安装可选模块。

首次安装框架或任一正式模块后，Unity 会自动弹出该模块的双语路径设置窗口。路径配置先保留在内存中；只有点击 **保存并生成 / Save & Generate** 后，才会把配置写入 `Assets/NodeEditorSettings/` 并生成对应资产。多个模块同时安装时会按框架、Dialogue、Task、State Machine 的顺序逐个设置。选择 **稍后 / Later** 或关闭窗口不会创建资产，下次重启 Unity 会再次提示。

### 包列表

| 包 | 用途 |
| --- | --- |
| `com.graphtest.nodeeditor` | 节点图运行时、编辑器框架与模块管理器 |
| `com.graphtest.dialogue` | 对话图运行时与编辑器 |
| `com.graphtest.task` | 任务图运行时与编辑器 |
| `com.graphtest.statemachine` | 状态机运行时与编辑器 |
| `com.graphtest.*.samples` | 各领域的可选示例内容 |

每个包目录中都包含更详细的使用、扩展和集成文档。

## English

### Overview

NodeGraph provides a reusable graph runtime, editor framework, and module manager, with optional Dialogue, Task, State Machine, and sample packages.

### Requirements

- Unity 6 (`6000.0` or newer)
- Git 2.14 or newer available on `PATH`

### Installation

In Unity, open **Window > Package Management > Package Manager**, choose **Install package from git URL**, and enter:

```text
https://github.com/yibidaiguo/NodeGraph.git?path=/Packages/com.graphtest.nodeeditor#v0.0.3
```

After installation, open **Tools > NodeGraph > Manager** to install optional modules from the same repository and revision.

The first time the framework or any production module is installed, Unity automatically opens that module's bilingual path setup window. The configuration remains in memory until **Save & Generate** is chosen; only then is it saved under `Assets/NodeEditorSettings/` and used to generate the module assets. Simultaneous installs are configured one at a time in Framework, Dialogue, Task, then State Machine order. Choosing **Later** or closing the window creates nothing and prompts again after Unity restarts.

### Packages

| Package | Purpose |
| --- | --- |
| `com.graphtest.nodeeditor` | Graph runtime, editor framework, and module manager |
| `com.graphtest.dialogue` | Dialogue graph runtime and editor |
| `com.graphtest.task` | Task graph runtime and editor |
| `com.graphtest.statemachine` | State machine runtime and editor |
| `com.graphtest.*.samples` | Optional sample content for each domain |

Detailed usage, extension, and integration documentation is included in each package directory.

## 许可证 / License

MIT，详见 [LICENSE](LICENSE)。

MIT. See [LICENSE](LICENSE).
