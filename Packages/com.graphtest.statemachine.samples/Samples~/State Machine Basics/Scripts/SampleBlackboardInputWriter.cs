// SampleBlackboardInputWriter.cs —— 样例：把旧输入系统（Input.GetAxis/GetButton）的输入每帧写进
// 同物体 StateMachinePlayer 的运行时黑板（Runner.Blackboard）。这是「外部系统经黑板注入」的标准姿势
//（见 StateMachine/INTEGRATION.md §2）：输入系统不认识状态机内部，只写 moveX/moveZ/jumpPressed 三个
// 声明过的变量；转移条件（如 待机⇄移动、跳跃）下一 tick 即可读到。仅供 StateMachine.Sample 演示程序集，
// 不进运行时核心（一类一文件，硬规则 A1；无 UnityEditor 依赖）。

using UnityEngine;

namespace StateMachine.Sample
{
    [RequireComponent(typeof(StateMachinePlayer))]
    public class SampleBlackboardInputWriter : MonoBehaviour
    {
        StateMachinePlayer m_Player;

        void Awake() => m_Player = GetComponent<StateMachinePlayer>();

        void Update()
        {
            // Runner 未 Play / 已 Stop 时为 null——黑板是每次运行一份的实例，没在跑就没得写。
            var bb = m_Player != null ? m_Player.Runner?.Blackboard : null;
            if (bb == null) return;

            bb.SetF("moveX", Input.GetAxis("Horizontal"));
            bb.SetF("moveZ", Input.GetAxis("Vertical"));
            bb.Set("jumpPressed", Input.GetButton("Jump"));
        }
    }
}
