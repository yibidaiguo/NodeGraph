# Changelog

## [0.0.1] - 2026-07-15

首个正式版。基于 com.graphtest.nodeeditor 的分层状态机（HSM）编辑器领域模块（Runtime + Editor）。

### 内容
- 六种节点（Entry/State/Transition/AnyState/SubMachine/Exit），State→Transition→State 三段式 + 生命周期三 Unit 槽 + AnyState 防抖 + 子机压栈。
- 运行时：`StateMachineRunner`（逐帧 tick + HSM 层栈）+ `StateMachinePlayer`（Update/FixedUpdate 驱动模式）+ 快照存档（不重触发 OnEnter）。
- 领域校验（`StateMachineValidation`：转移接线/子机环/死端）与连接矩阵（经框架 `ConnectionRuleMatrix`）。
- 模块入口 `Tools/NodeGraph/State Machine` + Manager 卡片。
- 文档随包：README / EXTENDING / INTEGRATION / VISION。
