// SampleCameraFollowAdapter.cs —— 样例：相机协作适配器——平滑跟随目标 + 订阅 StateMachinePlayer.onMachineEvent
//（UnityEvent，也可在检视面板直接拖接本类的 HandleMachineEvent，零代码接线），按状态机抛出的自定义事件
// 切换偏移/阻尼档位（如 待机态抛 camera.near 拉近、移动态抛 camera.far 拉远）。展示「相机协作 = 订阅事件」：
// 状态机核心对相机零耦合——图里的动作单元 FireMachineEventAction 只发事件名，相机是订阅方。
// 仅供 StateMachine.Sample 演示程序集（一类一文件，硬规则 A1；无 UnityEditor 依赖）。

using UnityEngine;

namespace StateMachine.Sample
{
    public class SampleCameraFollowAdapter : MonoBehaviour
    {
        [Tooltip("要订阅的状态机 Player（事件来源）。为空则不切档，仅按默认档跟随。")]
        public StateMachinePlayer player;
        [Tooltip("跟随目标（样例场景里是玩家胶囊体）。为空则本组件不做任何事。")]
        public Transform target;

        [Tooltip("默认档相机偏移（目标位置 + 此向量 = 相机期望位置）。")]
        public Vector3 defaultOffset = new Vector3(0f, 5f, -8f);
        [Tooltip("默认档跟随阻尼（越大跟得越紧）。")]
        public float defaultDamping = 8f;
        [Tooltip("切到默认档的事件名（状态 onEnter 里用「触发状态机事件」单元发出）。")]
        public string defaultEvent = "camera.near";

        [Tooltip("远档相机偏移（如移动中拉远视野）。")]
        public Vector3 farOffset = new Vector3(0f, 8f, -12f);
        [Tooltip("远档跟随阻尼（松一点，移动镜头更柔）。")]
        public float farDamping = 3f;
        [Tooltip("切到远档的事件名。")]
        public string farEvent = "camera.far";

        Vector3 m_Offset;
        float m_Damping;

        void Awake()
        {
            m_Offset = defaultOffset;
            m_Damping = defaultDamping;
        }

        void OnEnable()
        {
            if (player != null && player.onMachineEvent != null)
                player.onMachineEvent.AddListener(HandleMachineEvent);
        }

        void OnDisable()
        {
            if (player != null && player.onMachineEvent != null)
                player.onMachineEvent.RemoveListener(HandleMachineEvent);
        }

        // public：既供代码 AddListener，也供检视面板把 onMachineEvent 直接拖到本方法（无代码接线）。
        // 未知事件名直接忽略——同一事件流上还可能有别的订阅方（音效/任务），相机只认自己的两档。
        public void HandleMachineEvent(string eventName)
        {
            if (eventName == farEvent) { m_Offset = farOffset; m_Damping = farDamping; }
            else if (eventName == defaultEvent) { m_Offset = defaultOffset; m_Damping = defaultDamping; }
        }

        // LateUpdate：等目标本帧移动完再跟，避免抖动。指数平滑对帧率不敏感（比裸 Lerp(t*dt) 稳）。
        void LateUpdate()
        {
            if (target == null) return;
            var desired = target.position + m_Offset;
            float k = 1f - Mathf.Exp(-m_Damping * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, k);
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}
