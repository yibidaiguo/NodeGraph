// StateMachineSnapshot.cs —— 状态机运行快照（存档场景的可序列化载荷）。
// statePath = 捕获时的活动 HSM 栈路径（各层当前节点的 instanceId 以 '/' 连接；instanceId 跨会话稳定，
// 路径止于某个 SubMachine = 其子机已到 Exit 完结）；vars = 每个声明变量扁平化为「key + 不变量字符串」
//（照 DialogueRunner.Capture 的扁平化方式，恢复时按声明的 TypeRef 经 UnitValues 重新定型）。
// Restore 语义见 StateMachineRunner：只重建 HSM 栈与黑板值，不跑 OnEnter/OnExit、不发事件。
// 纯 [Serializable] 数据类（非 SO），一文件两个小类不违反硬规则 A1。Runtime 程序集。

using System;
using System.Collections.Generic;

namespace StateMachine
{
    [Serializable]
    public class StateMachineSnapshot
    {
        public string statePath;                 // 活动栈路径；空 = 捕获时机器未在运行
        public List<SnapshotVar> vars = new();   // 每个声明变量，扁平化为不变量字符串
    }

    // 一个扁平化的黑板变量（key + 不变量字符串值）。
    [Serializable]
    public class SnapshotVar { public string key; public string valueJson; }
}
