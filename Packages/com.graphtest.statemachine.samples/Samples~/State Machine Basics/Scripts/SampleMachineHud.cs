// SampleMachineHud.cs —— 样例：最小 IMGUI 调试 HUD——把同物体 StateMachinePlayer 的运行状态画到屏幕上：
// 当前活动路径（Runner.DisplayPath，HSM 各层以 '/' 连接）、若干黑板关键值（watchKeys）、最近的状态机
// 自定义事件，以及 Manual 驱动模式下的手动步进按钮（ManualTick——仅 Manual 模式提供，其他模式 Player
// 自己在 Update/FixedUpdate 里 tick，再手动步进会造成双 tick）。纯 IMGUI（OnGUI/GUILayout，UnityEngine
// 核心模块），照 DialogueSampleUI 的成例风格；仅供 StateMachine.Sample 演示程序集，不进运行时核心
//（一类一文件，硬规则 A1；无 UnityEditor 依赖）。

using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace StateMachine.Sample
{
    [RequireComponent(typeof(StateMachinePlayer))]
    public class SampleMachineHud : MonoBehaviour
    {
        [Tooltip("要在 HUD 上实时显示的黑板变量键名（须是黑板里声明过的 key）。")]
        public string[] watchKeys = { "moveSpeed" };
        [Tooltip("事件日志最多保留几条（新事件把最旧的挤掉）。")]
        public int maxEvents = 5;

        StateMachinePlayer m_Player;
        readonly List<string> m_Events = new();

        void Awake() => m_Player = GetComponent<StateMachinePlayer>();

        void OnEnable()
        {
            if (m_Player == null) m_Player = GetComponent<StateMachinePlayer>();
            if (m_Player != null && m_Player.onMachineEvent != null)
                m_Player.onMachineEvent.AddListener(HandleMachineEvent);
        }

        void OnDisable()
        {
            if (m_Player != null && m_Player.onMachineEvent != null)
                m_Player.onMachineEvent.RemoveListener(HandleMachineEvent);
        }

        // public：既供代码 AddListener，也供检视面板把 onMachineEvent 直接拖到本方法（无代码接线）。
        public void HandleMachineEvent(string eventName)
        {
            m_Events.Add(eventName);
            while (m_Events.Count > Mathf.Max(1, maxEvents)) m_Events.RemoveAt(0);
        }

        // ---- IMGUI 渲染（左上角小面板：字号随屏高缩放，半透明底，照 DialogueSampleUI 的样式手法）----

        GUIStyle m_Title, m_Body, m_Btn;
        Texture2D m_PanelBg;

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (m_Title != null) return;
            m_PanelBg = Solid(new Color(0.10f, 0.12f, 0.16f, 0.85f));
            m_Title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold,
                                                     normal = { textColor = new Color(0.7f, 0.8f, 1f) } };
            m_Body  = new GUIStyle(GUI.skin.label) { wordWrap = true,
                                                     normal = { textColor = Color.white } };
            m_Btn   = new GUIStyle(GUI.skin.button) { padding = new RectOffset(12, 12, 6, 6) };
        }

        // 黑板值 → 短字符串（float 两位小数，null 显示 -）。
        static string Fmt(object v) => v switch
        {
            null => "-",
            float f => f.ToString("0.00", CultureInfo.InvariantCulture),
            double d => d.ToString("0.00", CultureInfo.InvariantCulture),
            _ => System.Convert.ToString(v, CultureInfo.InvariantCulture)
        };

        void OnGUI()
        {
            EnsureStyles();
            m_Title.fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.022f));
            m_Body.fontSize  = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.020f));
            m_Btn.fontSize   = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.020f));

            float pad = 10f;
            float w = Mathf.Max(320f, Screen.width * 0.30f);
            float h = Mathf.Max(200f, Screen.height * 0.38f);
            var box = new Rect(pad, pad, w, h);
            GUI.DrawTexture(box, m_PanelBg);
            GUILayout.BeginArea(new Rect(box.x + pad, box.y + pad, box.width - pad * 2f, box.height - pad * 2f));

            var runner = m_Player != null ? m_Player.Runner : null;
            GUILayout.Label("状态机样例 · State Machine Sample", m_Title);

            // 活动路径（HSM 栈各层显示名，'/' 连接；进了子机能看到 战斗/追击 两层）。
            GUILayout.Label("路径 Path:  " + (runner != null && runner.IsRunning ? runner.DisplayPath : "（已停机 / stopped）"), m_Body);

            // 黑板关键值。
            var bb = runner?.Blackboard;
            if (bb != null)
                foreach (var key in watchKeys ?? System.Array.Empty<string>())
                    GUILayout.Label("  " + key + " = " + Fmt(bb.Get(key)), m_Body);

            // 最近的状态机自定义事件（FireMachineEventAction 发出，经 onMachineEvent 到这里）。
            if (m_Events.Count > 0)
            {
                GUILayout.Label("事件 Events:", m_Body);
                foreach (var e in m_Events) GUILayout.Label("  · " + e, m_Body);
            }

            GUILayout.FlexibleSpace();

            // Manual 模式专属：手动步进（其他模式 Player 已自动 tick，再步进 = 双 tick，故不提供）。
            if (m_Player != null && m_Player.updateMode == StateMachineUpdateMode.Manual && runner != null && runner.IsRunning)
            {
                if (GUILayout.Button("步进 1 tick / Step (1/60s)", m_Btn)) m_Player.ManualTick(1f / 60f);
            }
            // 停机后可重启（重复 Play = 重启语义：先干净停机再重建 Runner）。
            if (m_Player != null && (runner == null || !runner.IsRunning))
            {
                if (GUILayout.Button("重新启动 / Restart", m_Btn)) m_Player.Play();
            }

            GUILayout.EndArea();
        }
    }
}
