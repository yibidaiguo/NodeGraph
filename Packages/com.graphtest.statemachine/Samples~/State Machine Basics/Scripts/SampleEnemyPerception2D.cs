// SampleEnemyPerception2D.cs —— 样例：2D 敌人感知——把与目标 Transform 的距离每物理步写进黑板
// playerDistance。这是「感知系统经黑板注入」的成例（与 SampleBlackboardInputWriter 同一姿势）：
// 感知不认识状态机内部，只写声明过的变量；敌人图的转移条件（巡逻→战斗、追击⇄攻击、脱战）
// 读它决策。用 FixedUpdate 与敌人机的 FixedUpdate 驱动模式（2D 物理）配套。
// 仅供 StateMachine.Sample 演示程序集（一类一文件，硬规则 A1；无 UnityEditor 依赖）。

using UnityEngine;

namespace StateMachine.Sample
{
    [RequireComponent(typeof(StateMachinePlayer))]
    public class SampleEnemyPerception2D : MonoBehaviour
    {
        [Tooltip("感知目标（样例场景里是可拖动的玩家方块）。为空则不写黑板。")]
        public Transform target;

        StateMachinePlayer m_Player;

        void Awake() => m_Player = GetComponent<StateMachinePlayer>();

        void FixedUpdate()
        {
            var bb = m_Player != null ? m_Player.Runner?.Blackboard : null;
            if (bb == null || target == null) return;

            bb.SetF("playerDistance", Vector2.Distance(transform.position, target.position));
        }
    }
}
