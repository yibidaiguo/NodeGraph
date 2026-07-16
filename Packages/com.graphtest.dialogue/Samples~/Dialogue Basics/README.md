# 对话系统样例（Dialogue Sample）

一个**可直接运行**的对话播放样例，演示如何用 `DialoguePlayer` 驱动一张对话图并把台词/选项画到屏幕上。仿插件结构，自成一个文件夹：

```
Assets/Samples/NodeGraph Dialogue/0.0.5/Dialogue Basics/
├─ Scenes/
│   └─ DialogueTest.unity      # 测试场景：Camera + Light + DialogueTest 物体
├─ Scripts/
│   ├─ Dialogue.Sample.asmdef  # 程序集（引用 Dialogue.Runtime + NodeEditor.Runtime）
│   └─ DialogueSampleUI.cs      # 纯 IMGUI 驱动器：订阅 Runner 事件、画对话框、推进/选择/重播
└─ README.md
```

## 怎么跑

打开 `Scenes/DialogueTest.unity`，点 **Play**：
- 「继续 ▸」推进台词；选择页点对应选项按钮；结束后「重播」。
- 语言在 `DialogueTest` 物体的 `DialoguePlayer.Language` 下拉框切换（English / Chinese）。

## 它依赖的共享数据（不在本文件夹）

场景里的 `DialoguePlayer` 通过引用（GUID）接到项目的共享对话数据，这些数据**刻意**留在各自的受众档里，由 `Tools/NodeGraph/Manager` 中 Dialogue 的 `Setup Assets` 幂等生成：

| 字段 | 资产 | 位置 |
|---|---|---|
| `graph` | SampleDialogue | `Assets/Dialogue/Data/Runtime/Dialogues/SampleDialogue/` |
| `registry` | DialogueNodeRegistry | `Assets/NodeEditor/Data/Runtime/Nodes/` |
| `blackboards` | 全局⊕模块⊕组 三档黑板 | `NodeEditor/Data` + `Dialogue/Data/Runtime/Blackboards/` |
| `database` | DialogueDatabase | `Assets/Dialogue/Data/Runtime/Databases/` |

> 若场景里的引用为空（缺数据），先在 `Tools/NodeGraph/Manager` 中跑一次 Dialogue 的 `Setup Assets` 重新生成，再把它们拖回 `DialoguePlayer`。

## 演示的对话流程

Start → Line(问候) → Choice ⟨友善 / 冷淡⟩
- 友善 → Action(置 trustedHero=true) → Label(reunite) → Condition 真 → 信任结局
- 冷淡 → Jump(reunite) → Label → Condition 假 → 警惕结局
→ End

即覆盖 Start/Line/Choice·Option/Action/Jump·Label/Condition 分支/双结局/End 全部节点种类。

## 样例生命周期

从本包的 Package Manager **Samples** 页签或 NodeGraph Manager 导入后，Unity 会把样例复制到 `Assets/Samples/NodeGraph Dialogue/0.0.5/Dialogue Basics`。移除 Dialogue 包不会自动删除这个已导入副本；不再需要时请手动删除该目录。
