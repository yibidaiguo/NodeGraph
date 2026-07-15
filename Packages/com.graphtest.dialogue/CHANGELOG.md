# Changelog

## [0.0.1] - 2026-07-15

首个正式版。基于 com.graphtest.nodeeditor 的对话编辑器领域模块（Runtime + Editor）。

### 内容
- 10 种对话节点（Start/Line/Choice/Option/Condition/Action/Jump/Label/SubDialogue/End，一类一文件）+ 对话单元（FireEvent）。
- 运行时：`DialogueRunner`（行/分支/跳转/子对话调用栈）+ `DialoguePlayer`（MonoBehaviour host，OnLine/OnChoices/OnEvent/OnEnd 事件）+ 存/读档快照。
- 对话数据库 `DialogueDatabase`（说话人/头像/语音/多语言文本，条目用途分类）+ 数据窗口领域源。
- 领域校验（`DialogueValidation`）与连接矩阵（Choice↔Option 双向锁，经框架 `ConnectionRuleMatrix`）。
- 模块入口 `Tools/NodeGraph/Dialogue` + Manager 卡片（Setup Assets / Open Asset Paths）。
- 文档随包：README（设计师）/ EXTENDING（领域开发）/ INTEGRATION（集成）/ VISION。
