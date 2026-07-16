# 状态机编辑器使用指南

面向用这套工具**搭状态机**的人（设计师/关卡/AI 策划），零代码，不涉及框架内部实现。

> 想给编辑器**加新功能**的**开发者**请看 [EXTENDING.md](EXTENDING.md)；想在**别的游戏模块里用状态机**（订阅事件、存档、接动画相机）的**程序员**请看 [INTEGRATION.md](INTEGRATION.md)。搭图不需要看这些。

## 0. 安装

独立可安装、编译、卸载的 State Machine Runtime/Editor 模块，只依赖 `com.graphtest.nodeeditor`，不依赖 Dialogue 或 Task。通过 `Tools/NodeGraph/Manager` 安装本模块，再按 §1 生成资产。生成的状态机图、黑板与 `StateMachineAssetPaths` 属于项目，默认写入 `Assets/StateMachineContent`。需要演示时，直接从本包的 Package Manager **Samples** 页签或 Manager 的 State Machine 卡片导入 **State Machine Basics**。

其他用户首次安装 State Machine 时会自动看到路径设置窗口，可先修改节点定义、状态机组和黑板目录。只有点击 **保存并生成 / Save & Generate** 后，才会保存 `StateMachineAssetPaths` 并生成对应资产。选择 **稍后 / Later** 或关闭不会写入工程，下次重启 Unity 会再次提示。

Package consumers automatically receive State Machine path setup on first install and can edit the node-definition, machine-group, and blackboard directories first. `StateMachineAssetPaths` and generated assets are written only after **Save & Generate**. **Later** or closing writes nothing and prompts again after Unity restarts.

## 1. 初始化（第一次用先跑这个）

在 `Tools/NodeGraph/Manager` 里点 State Machine 卡片的 **Setup Assets** 按钮。它只生成状态机节点定义、共享注册表条目、模块黑板和本地化；重复跑是幂等的，不会重建样例或覆盖作者内容。生成目录全部来自项目级 `StateMachineAssetPaths`，可用 `Tools/NodeGraph/Manager` 的 State Machine **Open Asset Paths** 定位并自由修改。首次默认值（例如 `Assets/StateMachineContent/Machines`）只是示例。

## 2. 打开编辑器 / 新建一张图

菜单 `Tools/NodeGraph/State Machine`：左侧是本模块的**图列表**（可搜索、单击切换）、其下 **变量** 面板（黑板）、中间画布、右侧**检视面板**、顶部工具条。在 Project 窗口双击任意状态机图也会自动打开。

**新建**：点图列表底部的「新建」。新图自动带一个**钉住的入口（Entry）节点**（固定存在、删不掉），并自动配好该图的组黑板。图落在当前 `StateMachineAssetPaths.machineGroupsDir` 下，代码安装路径不参与决定。

## 3. 六种节点

> 编辑器里的节点名/参数名/说明按所选语言显示（工具栏语言下拉切换）；下表用英文 Kind 名标识，方便对照。

| 节点 | 作用 | 关键参数 |
|---|---|---|
| Entry | 状态机唯一入口（每图一个，自动生成、钉住） | 无；唯一出口指向初始状态 |
| State | 普通状态 | `onEnter`/`onUpdate`/`onExit` 三个**动作单元**槽（进入时/每帧/退出时做什么）+ `tags` |
| Transition | 状态间的**显式转移节点**（边不带逻辑，条件住在这里） | `condition`（**条件单元**，留空=恒真）+ `priority`（同源多转移，数小者先，首真生效） |
| Any State | 任意状态转移源：无论当前在哪个状态，它的转移都参与判定（如 →死亡/眩晕） | 无入边；目标已是当前状态时自动跳过（防抖） |
| Sub Machine | 子状态机（分层）：进入即运行子图，子图到 Exit 后回本层继续 | `graph`（拖一张状态机图）+ 同 State 的三个生命周期槽 |
| Exit | 返回点：子图到它=回父层；顶层图到它=整机停机 | 无 |

**连线只有一种走法**：状态（State/Any State/Sub Machine）的 `transitions` 出口 → Transition 的入口；Transition 的 `to` 出口 → 目标（State/Sub Machine/Exit）。连不对的地方拖拽时就连不上（连接规则实时拦截）；一个 Transition 可以被多个状态共用（多源同一转移）。

## 4. 条件与动作：都是「单元」，下拉选、可组合

选中 Transition，在检视面板给 `condition` 槽下拉选条件（如「黑板比较」：选变量、选比较符、填值）；要组合就先选装饰器（与 AND / 或 OR / 非 NOT）再往里加子条件。选中 State，给 `onEnter` 等槽选动作（如「设置变量（字面量）」）；要一次做多件事用「顺序」装饰串起来，「触发状态机事件」可以发一个事件名给外面的动画/相机/音效。引用黑板变量的字段都是已声明变量的下拉，不用手打。

## 5. 黑板变量

在左侧 **变量** 面板点 `+ 变量`（作用域=所在档：全局/模块/组，顶部档位切换；新建图会自动配好组黑板）。每个变量可直接编辑默认值；悬停变量名显示注释。转移条件读的、动作写的都是这些声明过的变量——运行时由程序把输入/感知等数据写进来（那是程序员的事，见 INTEGRATION）。

## 6. 校验

边编辑边自动跑：红框=错误（如 Transition 没有目标、Any State 有入边、子图引用成环）、黄框=警告（如状态没有任何出向转移「死驻留」、引用未声明变量、被恒真转移挡死的转移）；图级问题显示在画布顶部横幅。改好即消。

## 7. 看运行：Play 高亮

给场景中的 `StateMachinePlayer` 装配图、注册表与黑板，进入 Play 后打开对应图（先运行后开、关闭重开也可以）：**只有当前活动状态路径高亮**（含子机下钻），状态退出即变暗；转移/Entry/Exit 可保留路径着色。仓库本地另有可运行演示，但产品发布不包含也不依赖它。

## 8. 语言

工具栏右侧语言下拉（English / 中文）切换整个编辑器界面语言，节点名/参数说明/校验消息即时跟随。默认中文。
