# 状态机样例（State Machine Sample）

两个**可直接运行**的状态机场景：3D 玩家机（输入驱动）+ 2D 敌人机（HSM 感知驱动），演示「状态 = 配置黑板、外设 = 订阅事件/注入黑板」的零耦合协作。仿插件结构，自成一个文件夹：

```
Assets/Samples/<包显示名>/<版本>/State Machine Basics/
├─ Data/
│   ├─ Machines/                    # 三张仅供开发验收的样例图
│   └─ Blackboards/                 # 两个样例组黑板
├─ Scenes/                          # 随样例自带；亦可用下述菜单动作重新生成/打开
│   ├─ StateMachineSample3D.unity
│   └─ StateMachineSample2D.unity
├─ Scripts/
│   ├─ StateMachine.Sample.asmdef           # 运行时程序集（零 UnityEditor）
│   ├─ SampleBlackboardInputWriter.cs       # 输入 → 黑板（moveX/moveZ/jumpPressed）
│   ├─ SamplePlayerMotor3D.cs               # 黑板 → CharacterController（读 moveSpeed/canMove，写回 isGrounded）
│   ├─ SampleEnemyPerception2D.cs           # 感知 → 黑板（playerDistance）
│   ├─ SampleEnemyMotor2D.cs                # 黑板 → Rigidbody2D（读 moveSpeed/chasePlayer）
│   ├─ SampleAnimatorStateAdapter.cs        # onStateEntered → Animator SetTrigger（可选接线示例）
│   ├─ SampleCameraFollowAdapter.cs         # 跟随 + onMachineEvent 切偏移/阻尼（camera.near/far）
│   ├─ SampleMachineHud.cs                  # IMGUI HUD：DisplayPath + 黑板值 + 事件 + Manual 步进
│   └─ Editor/
│       ├─ StateMachine.Sample.Editor.asmdef   # 仅 Editor 平台（场景菜单不进构建）
│       └─ StateMachineSampleScenes.cs         # NodeGraph Manager 中创建 3D/2D 示例场景
└─ README.md
```

## 怎么开

1. 从 Package Manager 导入 **State Machine Basics**；三张样例图与两个样例组黑板随导入副本提供，不由产品 Setup 生成。
2. 在 `Tools/NodeGraph/Manager` 中点击 State Machine 的 `Create Sample Scene (3D)` 或 `(2D)`——场景不存在则生成到 `Assets/Samples/<包显示名>/<版本>/State Machine Basics/Scenes` 并打开，已存在则直接打开（不覆盖你的改动）。
3. 点 **Play**。左上角 HUD 实时显示活动路径（DisplayPath）、黑板关键值与最近事件。

## 3D 场景怎么玩（SamplePlayer3D：入口→待机⇄移动→跳跃）

- **WASD/方向键**移动、**空格**跳（旧输入管理器的 Horizontal/Vertical/Jump 轴）。
- 看 HUD：站住是 `待机`，动起来切 `移动`（条件是取值单元组合出的 |move|²>0.0001），跳起进 `跳跃`、落地回 `待机`（jumpPressed && isGrounded 的 AND 条件）。
- 相机在 待机/移动 间切近/远档——这不是相机读状态，而是状态 onEnter 发 `camera.near`/`camera.far` 事件、`SampleCameraFollowAdapter` 订阅切档（事件协作成例）。

## 2D 场景怎么玩（SampleEnemy2D：巡逻→战斗子机→脱战；FixedUpdate 示范）

- 敌人方块沿 X 往返**巡逻**；在 Scene 视图里把 `PlayerTarget` 方块**拖近**（距离 <8）→ 进 `战斗` **子状态机**（HUD 路径变成 `战斗/追击` 两层——HSM 压栈）；贴身（<1.5）切 `攻击` 停下，拉开又回 `追击`；**拖远**（>12）脱战回 `巡逻`。
- 敌人机的 `updateMode = FixedUpdate`：感知/电机也都在 FixedUpdate 读写黑板，与 2D 物理同步（物理驱动的机器都该这样配）。
- 试 **Any State**：play 中选中 Enemy，在编辑器图调试里或用代码把黑板 `stunned` 设 true → 无论当前在哪层，立即切 `眩晕`；设回 false 恢复巡逻。
- 子机 `SampleEnemyCombat` 里 追击→(>12)→出口 演示「子机到 Exit 回父层」的结构；父层的同阈值「脱战」转移按 HSM 外层先行语义会先命中——两条路都通向巡逻，打开两张图对照看最直观。

## Manual 模式怎么试

选中挂 `StateMachinePlayer` 的物体，把 `updateMode` 改成 **Manual** 再 Play：机器不再自动走，HUD 出现「步进 1 tick」按钮，点一下走一步（1/60s）——逐帧观察转移求值/onUpdate 顺序用。改回 Update/FixedUpdate 即恢复自动（其他模式下不要调 ManualTick，会双 tick）。

## 它依赖的数据

场景里的 `StateMachinePlayer` 按 GUID 引用资产。样例图和样例组黑板属于已导入样例；产品 Setup 只维护产品定义、注册表和项目配置，不生成或依赖这些样例：

| 字段 | 资产 | 位置 |
|---|---|---|
| `graph` | SamplePlayer3D / SampleEnemy2D（子机 SampleEnemyCombat 被 SubMachine 引用） | `Assets/Samples/<包显示名>/<版本>/State Machine Basics/Data/Machines/` |
| `registry` | NodeRegistry | 由项目的 `NodeEditorAssetPaths.registryPath` 决定 |
| `blackboards` | 全局 ⊕ 模块 ⊕ 组；两个样例组档随样例提供 | 全局/模块档由项目配置，样例组档位于已导入样例的 `Data/Blackboards/` |

> 若要制作自己的状态机，请在用户自选的项目资源目录中新建资产，不要把业务资产写回样例目录。

## 样例生命周期

从本包的 Package Manager **Samples** 页签或 NodeGraph Manager 导入后，Unity 会把样例复制到 `Assets/Samples/NodeGraph State Machine/0.0.5/State Machine Basics`。移除 State Machine 包不会自动删除这个已导入副本；不再需要时请手动删除该目录。
