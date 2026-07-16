# 任务系统样例（Task Sample）

一个可直接运行的任务系统样例，用来展示 `TaskRunner` 如何消费任务图、步骤图和黑板。

```
Assets/Samples/NodeGraph Task/0.0.5/Task Basics/
├─ Data/
│  ├─ Localization/SampleLocalizationTable.asset  # 仅开发样例使用的 task.sample.* 文案
│  ├─ Tasks/SampleTaskLine.asset
│  ├─ Steps/SampleTaskLineSteps.asset
│  └─ Blackboards/Blackboard_task_SampleTaskLine.asset
├─ Scenes/TaskTest.unity
└─ Scripts/
   ├─ Task.Sample.asmdef
   └─ TaskSampleUI.cs
```

## 怎么跑

打开 `Scenes/TaskTest.unity`，点 Play：

- `Start intro task` 启动 `sample.intro`，它会停在目标 `sample.collect`。
- `Report objective: sample.collect` 上报一次目标进度，完成 intro。
- `Start scout task` 完成第二条前置分支。
- 两条前置分支都完成后，`Start report task` 解锁并完成后续任务。
- `Reset sample` 会重建 runner 和任务日志。

样例场景只依赖运行时资产引用，不需要打开任务编辑器即可运行。
`SampleLocalizationTable.asset` 只保存开发样例文案，不会被产品 `NodeEditorAssetPaths` 自动选择，也不会进入干净发布包。

## 样例生命周期

从本包的 Package Manager **Samples** 页签或 NodeGraph Manager 导入后，Unity 会把样例复制到 `Assets/Samples/NodeGraph Task/0.0.5/Task Basics`。移除 Task 包不会自动删除这个已导入副本；不再需要时请手动删除该目录。
