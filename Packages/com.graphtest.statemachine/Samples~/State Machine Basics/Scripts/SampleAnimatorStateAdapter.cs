// SampleAnimatorStateAdapter.cs —— 样例：动画协作适配器——订阅 StateMachinePlayer.onStateEntered
//（UnityEvent，也可在检视面板直接拖接本类的 HandleStateEntered，零代码接线），把进入状态的显示名
// 映射成 Animator 的 SetTrigger。展示「动画协作 = 订阅事件」：状态机核心对 Animator 零耦合——
// runner 只抛事件，动画是订阅方；Animator 可空防护，没挂 Animator 也能跑（Debug.Log 代替，便于观察）。
// 仅供 StateMachine.Sample 演示程序集（一类一文件，硬规则 A1；无 UnityEditor 依赖）。

using System.Collections.Generic;
using NodeEditor;
using UnityEngine;

namespace StateMachine.Sample
{
    public class SampleAnimatorStateAdapter : MonoBehaviour
    {
        [Tooltip("要订阅的状态机 Player。为空则取同物体上的组件。")]
        public StateMachinePlayer player;
        [Tooltip("接收状态触发器的 Animator。可空——没有就只 Debug.Log，样例场景不带动画资源也能跑。")]
        public Animator animator;

        void OnEnable()
        {
            if (player == null) player = GetComponent<StateMachinePlayer>();
            if (player != null && player.onStateEntered != null)
                player.onStateEntered.AddListener(HandleStateEntered);
        }

        void OnDisable()
        {
            if (player != null && player.onStateEntered != null)
                player.onStateEntered.RemoveListener(HandleStateEntered);
        }

        // public：既供代码 AddListener，也供检视面板把 onStateEntered 直接拖到本方法（无代码接线）。
        public void HandleStateEntered(string instanceId)
        {
            var stateName = ResolveDisplayName(instanceId);
            if (string.IsNullOrEmpty(stateName)) return;

            if (animator != null) animator.SetTrigger(stateName);   // 约定：动画控制器按状态显示名建 Trigger
            else Debug.Log($"[SampleAnimatorStateAdapter] SetTrigger(\"{stateName}\")（未挂 Animator，仅日志演示）", this);
        }

        // instanceId → 状态显示名：在图（含 SubMachine 引用的子图，防环）里找该实例的 displayName；
        // 找不到回退 instanceId。这是纯数据查找（NodeGraphAsset 是运行时安全资产），不碰状态机内部。
        string ResolveDisplayName(string instanceId)
        {
            var visited = new HashSet<NodeGraphAsset>();
            return FindName(player != null ? player.graph : null, instanceId, visited) ?? instanceId;
        }

        static string FindName(NodeGraphAsset g, string instanceId, HashSet<NodeGraphAsset> visited)
        {
            if (g == null || !visited.Add(g)) return null;
            foreach (var inst in g.instances)
            {
                if (inst.instanceId == instanceId)
                    return string.IsNullOrEmpty(inst.displayName) ? null : inst.displayName;
                // 顺带下钻子机引用（objectOverrides 里的 "graph"），HSM 子图里的状态也能解析出名字。
                var sub = ParamResolver.ResolveObject(inst, "graph") as NodeGraphAsset;
                if (sub == null) continue;
                var found = FindName(sub, instanceId, visited);
                if (found != null) return found;
            }
            return null;
        }
    }
}
