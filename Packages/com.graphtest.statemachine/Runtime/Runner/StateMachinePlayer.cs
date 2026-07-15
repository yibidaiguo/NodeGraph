// StateMachinePlayer.cs —— 面向场景的 MonoBehaviour 宿主，驱动一个 StateMachineRunner（照 DialoguePlayer 成例）。
// 在 inspector 中接入图/注册表/分层黑板 + 驱动模式；Play() 构建 Runner 并启动，Update/FixedUpdate 按模式
// 逐帧 tick，Manual 模式由调用方 ManualTick(dt) 手动步进（2D 物理机器选 FixedUpdate，回放/测试选 Manual）。
// 设计上很薄：所有状态机逻辑都在 StateMachineRunner 中——这里只是引擎侧的句柄 + 事件转发。
// Runtime 程序集 —— 不引用 UnityEditor / RuntimeGraphRegistry（红线 §6），编辑器调试器经静态事件桥接。

using System;
using UnityEngine;
using UnityEngine.Events;
using NodeEditor;

namespace StateMachine
{
    // Runner 的驱动模式：Update = 普通逐帧；FixedUpdate = 物理步（刚体/2D 物理驱动的机器选它）；
    // Manual = 不自动 tick，由调用方 ManualTick(dt) 手动步进（回放、锁步、测试用）。
    public enum StateMachineUpdateMode { Update, FixedUpdate, Manual }

    public sealed class StateMachinePlayer : MonoBehaviour
    {
        [Tooltip("要运行的状态机图（module=\"statemachine\" 的 control-flow NodeGraphAsset，须含 Entry 节点）。")]
        public NodeGraphAsset graph;
        [Tooltip("把每个实例的 definitionId 解析为对应节点定义的注册表（驱动节点种类/参数/Unit 槽）。子机图共用同一个。")]
        public NodeRegistry registry;
        [Tooltip("黑板变量声明的分层引用，按「全局→模块→组」由外到内排列；用默认值初始化 runner 的每实例黑板，" +
                 "同名 key 就近覆盖（更专的层级胜出）。运行时构建无 AssetDatabase，故各档在此显式引用；" +
                 "外部系统（输入/感知）经 Runner 侧黑板注入，转移条件读它。")]
        public BlackboardAsset[] blackboards;
        [Tooltip("驱动模式：Update=普通逐帧；FixedUpdate=物理步（2D/3D 刚体驱动选它）；Manual=不自动 tick，调用方自己 ManualTick(dt)。")]
        public StateMachineUpdateMode updateMode = StateMachineUpdateMode.Update;
        [Tooltip("勾选后场景 Start 时自动 Play()。需要在代码里先订阅 Runner 事件再启动的场合请关掉、自己调 Play()。")]
        public bool playOnStart = true;

        [Tooltip("进入某状态（State/SubMachine）时触发，参数 = 该节点的 instanceId。供相机/动画等适配器在检视面板拖接、无代码接线。")]
        public UnityEvent<string> onStateEntered;
        [Tooltip("退出某状态时触发，参数 = 该节点的 instanceId。")]
        public UnityEvent<string> onStateExited;
        [Tooltip("状态机动作单元「触发状态机事件」发出自定义事件时触发，参数 = 事件名。供音效/任务等系统无代码接线。")]
        public UnityEvent<string> onMachineEvent;
        [Tooltip("状态机整机结束（顶层到 Exit 或调用 Stop）时触发。")]
        public UnityEvent onStopped;

        // 此组件的解释器。Play() 中构建；未 Play 或已 Stop 时为 null。
        public StateMachineRunner Runner { get; private set; }

        // 编辑器调试器的挂接缝（照 DialoguePlayer 成例）：此 Runtime 程序集不能引用 Editor 侧的
        // RuntimeGraphRegistry，改为抛静态事件，由 StateMachine.Editor 的小桥（[InitializeOnLoad]）
        // 订阅并转发给 registry——Runtime 不带任何 Editor 依赖（红线 §6）。
        public static event Action<IRuntimeGraph> OnRunnerCreated;
        public static event Action<IRuntimeGraph> OnRunnerDestroyed;

        void Start() { if (playOnStart) Play(); }

        // 构建 Runner 并启动状态机（重复调用 = 重启：先干净销毁上一个 Runner）。
        // 需要代码订阅 Runner 事件的调用方：Play() 后立即经 Runner 属性订阅（Start 已同步跑完初始 Enter 链，
        // 初始进入事件已抛出；要抓初始进入事件请用本组件的 UnityEvent 字段或关 playOnStart 自行装配）。
        public void Play()
        {
            Stop();
            var ctx = new StateMachineRunContext(new StateMachineBlackboard(new BlackboardSet(blackboards)));
            Runner = new StateMachineRunner(graph, registry, ctx);
            Runner.OnStateEntered += id => onStateEntered?.Invoke(id);
            Runner.OnStateExited  += id => onStateExited?.Invoke(id);
            Runner.OnMachineEvent += name => onMachineEvent?.Invoke(name);
            Runner.OnStopped      += () => onStopped?.Invoke();
            OnRunnerCreated?.Invoke(Runner);
            Runner.Start();
        }

        // 停机（自内向外逐层 OnExit + OnStopped，Runner.Stop 幂等）并销毁 Runner。无 Runner 时为空操作。
        public void Stop()
        {
            if (Runner == null) return;
            Runner.Stop();
            OnRunnerDestroyed?.Invoke(Runner);
            Runner = null;
        }

        // Manual 模式的手动步进入口（锁步/回放/测试驱动）。未 Play 时为空操作。
        public void ManualTick(float dt) => Runner?.Tick(dt);

        void Update()      { if (updateMode == StateMachineUpdateMode.Update)      Runner?.Tick(Time.deltaTime); }
        void FixedUpdate() { if (updateMode == StateMachineUpdateMode.FixedUpdate) Runner?.Tick(Time.fixedDeltaTime); }

        void OnDestroy() => Stop();
    }
}
