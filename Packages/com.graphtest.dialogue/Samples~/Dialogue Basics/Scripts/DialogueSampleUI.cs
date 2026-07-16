// DialogueSampleUI.cs — 测试场景用的最小 IMGUI 驱动器：把 DialoguePlayer 的运行时表现事件画到屏幕上。
// 订阅同物体上 DialoguePlayer.Runner 的 OnLine/OnChoices/OnEvent/OnEnd：渲染当前台词与选项，
// 点「继续」推进、点选项选分支、结束后可重播。纯 IMGUI（OnGUI/GUILayout，UnityEngine 核心模块），
// 不依赖 UI 包；仅供 Dialogue.Sample 演示程序集，不进运行时核心（DialoguePlayer 才是引擎侧句柄）。

using System.Collections.Generic;
using UnityEngine;

namespace Dialogue.Sample
{
    [RequireComponent(typeof(DialoguePlayer))]
    public class DialogueSampleUI : MonoBehaviour
    {
        DialoguePlayer m_Player;

        // 当前呈现状态（三态：停在某行 / 等待选择 / 已结束）。
        DialogueLineView m_Line;
        bool m_HasLine;
        IReadOnlyList<DialogueOptionView> m_Choices;
        bool m_Ended;
        bool m_Subscribed;
        readonly List<string> m_Events = new();

        void Start()
        {
            if (!TryGetReadyPlayer(out var player)) return;
            Subscribe(player);
            player.Begin();   // 同步走到第一行/选择并抛出事件
        }

        void OnDestroy() => Unsubscribe();

        bool TryGetReadyPlayer(out DialoguePlayer player)
        {
            if (m_Player == null) m_Player = GetComponent<DialoguePlayer>();
            player = m_Player;
            return player != null && player.Runner != null;
        }

        void Subscribe(DialoguePlayer player)
        {
            if (m_Subscribed) return;
            var r = player != null ? player.Runner : null;
            if (r == null) return;
            r.OnLine += HandleLine;
            r.OnChoices += HandleChoices;
            r.OnEvent += HandleEvent;
            r.OnEnd += HandleEnd;
            m_Subscribed = true;
        }

        void Unsubscribe()
        {
            if (!m_Subscribed) return;
            var r = m_Player != null ? m_Player.Runner : null;
            if (r == null) { m_Subscribed = false; return; }
            r.OnLine -= HandleLine;
            r.OnChoices -= HandleChoices;
            r.OnEvent -= HandleEvent;
            r.OnEnd -= HandleEnd;
            m_Subscribed = false;
        }

        // 任何新的 Line/Choices 事件都意味着「未结束」——End 态完全由事件驱动，故此处一并清掉 m_Ended，
        // 这样无论经由 Replay 按钮还是直接 Begin() 重启，呈现态都不会卡在结束画面上。
        void HandleLine(DialogueLineView line) { m_Line = line; m_HasLine = true; m_Choices = null; m_Ended = false; }
        void HandleChoices(IReadOnlyList<DialogueOptionView> choices) { m_Choices = choices; m_HasLine = false; m_Ended = false; }
        void HandleEvent(string id, string arg) { m_Events.Add(string.IsNullOrEmpty(arg) ? id : id + ": " + arg); }
        void HandleEnd() { m_HasLine = false; m_Choices = null; m_Ended = true; }

        void Replay()
        {
            if (!TryGetReadyPlayer(out var player)) return;
            m_Ended = false;
            m_HasLine = false;
            m_Choices = null;
            m_Events.Clear();
            player.Begin();
        }

        // ---- IMGUI 渲染（大号自适应版：字号随屏高缩放，半透明压暗背景，便于查看）----

        GUIStyle m_Title, m_Speaker, m_Body, m_Btn;
        Texture2D m_Dim, m_PanelBg;

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (m_Title != null) return;
            m_Dim     = Solid(new Color(0f, 0f, 0f, 0.55f));            // 压暗 3D 场景
            m_PanelBg = Solid(new Color(0.10f, 0.12f, 0.16f, 0.96f));  // 对话框底
            m_Title   = new GUIStyle(GUI.skin.label)  { fontStyle = FontStyle.Bold,
                                                        normal = { textColor = new Color(0.7f, 0.8f, 1f) } };
            m_Speaker = new GUIStyle(GUI.skin.label)  { fontStyle = FontStyle.Bold,
                                                        normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            m_Body    = new GUIStyle(GUI.skin.label)  { wordWrap = true,
                                                        normal = { textColor = Color.white } };
            m_Btn     = new GUIStyle(GUI.skin.button) { padding = new RectOffset(16, 16, 10, 10) };
        }

        void OnGUI()
        {
            EnsureStyles();

            // 字号按屏幕高度自适应（参考 1080p），设下限保证小窗也看得清。
            m_Title.fontSize   = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.026f));
            m_Speaker.fontSize = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.044f));
            m_Body.fontSize    = Mathf.Max(22, Mathf.RoundToInt(Screen.height * 0.050f));
            m_Btn.fontSize     = Mathf.Max(18, Mathf.RoundToInt(Screen.height * 0.038f));

            // 半透明压暗整屏，让对话框从场景里凸出来。
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_Dim);

            // 大对话框：宽 88% 屏宽、高 ~44% 屏高、底部留边。
            float pad = Screen.height * 0.03f;
            float w = Screen.width * 0.88f;
            float h = Mathf.Max(300f, Screen.height * 0.44f);
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height - h - Screen.height * 0.05f, w, h);
            GUI.DrawTexture(box, m_PanelBg);

            GUILayout.BeginArea(new Rect(box.x + pad, box.y + pad, box.width - pad * 2f, box.height - pad * 2f));

            var hasPlayer = TryGetReadyPlayer(out var player);
            GUILayout.Label("对话系统测试 · Dialogue Test   [lang: " + (player != null ? player.language.ToString() : "-") + "]", m_Title);
            GUILayout.Space(pad * 0.6f);

            float btnH = Mathf.Max(40f, Screen.height * 0.085f);
            if (!hasPlayer)
            {
                GUILayout.Label("等待 DialoguePlayer / Waiting for DialoguePlayer", m_Body);
            }
            else if (m_Ended)
            {
                GUILayout.Label("（对话结束 / End）", m_Body);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("重播 / Replay", m_Btn, GUILayout.Height(btnH))) Replay();
            }
            else if (m_Choices != null)
            {
                var choices = m_Choices;
                GUILayout.Label("请选择 / Choose:", m_Speaker);
                GUILayout.Space(pad * 0.4f);
                for (int i = 0; i < choices.Count; i++)
                {
                    var c = choices[i];
                    if (GUILayout.Button("  " + (i + 1) + ".  " + c.text, m_Btn, GUILayout.Height(btnH))) player.Choose(c.index);
                    GUILayout.Space(pad * 0.25f);
                }
            }
            else if (m_HasLine)
            {
                if (!string.IsNullOrEmpty(m_Line.speaker)) GUILayout.Label(m_Line.speaker, m_Speaker);
                GUILayout.Space(pad * 0.3f);
                GUILayout.Label(m_Line.text ?? string.Empty, m_Body);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("继续 ▸ / Continue", m_Btn, GUILayout.Height(btnH))) player.Advance();
            }
            else
            {
                GUILayout.Label("…", m_Body);
            }

            GUILayout.EndArea();
        }
    }
}
