// SampleEnemyMotor2D.cs —— 样例：读黑板驱动 Rigidbody2D 的 2D 敌人电机（巡逻/追击）。
// 与 SamplePlayerMotor3D 同一解耦：电机不问当前状态——各状态在 onEnter 里配好 moveSpeed（巡逻慢、
// 追击快、攻击/眩晕停）与 chasePlayer（是否朝目标移动），电机只消费这两个参数；追不追、多快，
// 全由状态机图决定。FixedUpdate 写刚体速度，与敌人机的 FixedUpdate 驱动模式（2D 物理）配套。
// 仅供 StateMachine.Sample 演示程序集（一类一文件，硬规则 A1；无 UnityEditor 依赖）。

using UnityEngine;

namespace StateMachine.Sample
{
    [RequireComponent(typeof(StateMachinePlayer))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class SampleEnemyMotor2D : MonoBehaviour
    {
        [Tooltip("追击目标（样例场景里是可拖动的玩家方块）。chasePlayer 为真且目标非空时朝它移动。")]
        public Transform target;
        [Tooltip("巡逻半径：以起始位置为中心、沿 X 轴往返的半程距离（米）。")]
        public float patrolRange = 4f;

        StateMachinePlayer m_Player;
        Rigidbody2D m_Rb;
        float m_HomeX;   // 巡逻中心（起始 X）
        int m_Dir = 1;   // 巡逻方向（±1）

        void Awake()
        {
            m_Player = GetComponent<StateMachinePlayer>();
            m_Rb = GetComponent<Rigidbody2D>();
            m_HomeX = transform.position.x;
        }

        void FixedUpdate()
        {
            var bb = m_Player != null ? m_Player.Runner?.Blackboard : null;
            if (bb == null) return;

            float speed = bb.GetF("moveSpeed");
            bool chase = bb.Get("chasePlayer") is bool c && c;

            float vx = 0f;
            if (speed > 0.01f)
            {
                if (chase && target != null)
                {
                    // 追击：朝目标的 X 方向以状态给的速度移动（贴近后「攻击」状态会把 moveSpeed 置 0）。
                    vx = Mathf.Sign(target.position.x - transform.position.x) * speed;
                }
                else
                {
                    // 巡逻：以起始位置为中心沿 X 往返，越界即折返。
                    if (transform.position.x > m_HomeX + patrolRange) m_Dir = -1;
                    else if (transform.position.x < m_HomeX - patrolRange) m_Dir = 1;
                    vx = m_Dir * speed;
                }
            }
            m_Rb.linearVelocity = new Vector2(vx, m_Rb.linearVelocity.y);
        }
    }
}
