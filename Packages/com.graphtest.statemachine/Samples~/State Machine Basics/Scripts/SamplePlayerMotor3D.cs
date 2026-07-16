// SamplePlayerMotor3D.cs —— 样例：读黑板驱动 CharacterController 的 3D 玩家电机（移动/重力/跳），
// 并把 isGrounded 写回黑板。展示「状态 = 配置黑板，电机 = 消费黑板」的解耦：电机完全不依赖状态机
// 内部（不问当前是哪个状态）——各状态在 onEnter 里用「设置变量」单元改 moveSpeed/canMove 等参数，
// 电机只按参数干活；输入（moveX/moveZ/jumpPressed）由 SampleBlackboardInputWriter 注入。
// 仅供 StateMachine.Sample 演示程序集（一类一文件，硬规则 A1；无 UnityEditor 依赖）。

using UnityEngine;

namespace StateMachine.Sample
{
    [RequireComponent(typeof(StateMachinePlayer))]
    [RequireComponent(typeof(CharacterController))]
    public class SamplePlayerMotor3D : MonoBehaviour
    {
        [Tooltip("重力加速度（米/秒²，负值向下）。")]
        public float gravity = -20f;
        [Tooltip("起跳瞬间的竖直初速度（米/秒）。")]
        public float jumpSpeed = 6.5f;

        StateMachinePlayer m_Player;
        CharacterController m_Cc;
        float m_VelY;   // 竖直速度（重力/跳跃积分）

        void Awake()
        {
            m_Player = GetComponent<StateMachinePlayer>();
            m_Cc = GetComponent<CharacterController>();
        }

        void Update()
        {
            var bb = m_Player != null ? m_Player.Runner?.Blackboard : null;
            if (bb == null) return;

            // 消费状态配好的参数：canMove 门控水平移动，moveSpeed 是当前状态给的速度（待机=0、移动=4、跳跃=空中机动）。
            bool canMove = bb.Get("canMove") is bool cm && cm;
            float speed = bb.GetF("moveSpeed");
            var horizontal = canMove ? new Vector3(bb.GetF("moveX"), 0f, bb.GetF("moveZ")) : Vector3.zero;
            if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();   // 斜向不加速

            // 重力 + 跳：贴地时把竖直速度压到小负值（保持贴地检测稳定）；黑板 jumpPressed 且贴地 → 起跳。
            bool grounded = m_Cc.isGrounded;
            if (grounded && m_VelY < 0f) m_VelY = -2f;
            if (grounded && bb.Get("jumpPressed") is bool jp && jp) m_VelY = jumpSpeed;
            m_VelY += gravity * Time.deltaTime;

            m_Cc.Move((horizontal * speed + Vector3.up * m_VelY) * Time.deltaTime);

            // 写回黑板：起跳当帧竖直速度已为正，isGrounded 立即翻 false——状态机的
            // 「跳跃→(isGrounded==true)→待机」转移不会在起跳同一 tick 误触发。
            bb.Set("isGrounded", m_Cc.isGrounded && m_VelY <= 0f);
        }
    }
}
