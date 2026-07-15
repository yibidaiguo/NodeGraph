using System.Collections.Generic;
using NodeEditor;
using TaskEditor;
using UnityEngine;

namespace Task.Sample
{
    [DisallowMultipleComponent]
    public class TaskSampleUI : MonoBehaviour
    {
        public NodeRegistry registry;
        public NodeGraphAsset taskGraph;
        public BlackboardAsset moduleBlackboard;
        public BlackboardAsset sampleBlackboard;
        public string introTaskId = "sample.intro";
        public string scoutTaskId = "sample.scout";
        public string reportTaskId = "sample.report";
        public string objectiveId = "sample.collect";

        TaskRunner m_Runner;
        readonly List<string> m_Log = new();
        int m_ObjectiveCurrent;
        int m_ObjectiveRequired = 1;
        Vector2 m_LogScroll;

        GUIStyle m_AppTitle;
        GUIStyle m_SectionTitle;
        GUIStyle m_Text;
        GUIStyle m_Muted;
        GUIStyle m_Status;
        GUIStyle m_DetailLabel;
        GUIStyle m_DetailValue;
        GUIStyle m_Button;
        GUIStyle m_DisabledButton;
        GUIStyle m_LogText;
        Texture2D m_Backdrop;
        Texture2D m_Panel;
        Texture2D m_Header;
        Texture2D m_CardReady;
        Texture2D m_CardActive;
        Texture2D m_CardDone;
        Texture2D m_CardLocked;
        Texture2D m_LogBg;
        Texture2D m_AccentReady;
        Texture2D m_AccentActive;
        Texture2D m_AccentDone;
        Texture2D m_AccentLocked;
        Texture2D m_ButtonBg;
        Texture2D m_ButtonHover;
        Texture2D m_ButtonActive;
        Texture2D m_ButtonDisabled;
        Texture2D m_ProgressBg;

        void Start() => ResetRunner();

        void OnDestroy() => Unsubscribe();

        void ResetRunner()
        {
            Unsubscribe();
            m_Log.Clear();
            m_ObjectiveCurrent = 0;
            m_ObjectiveRequired = 1;
            if (registry == null || taskGraph == null)
            {
                m_Runner = null;
                m_Log.Add("Missing registry or task graph.");
                return;
            }

            m_Runner = new TaskRunner(registry, taskGraph, new BlackboardSet(moduleBlackboard, sampleBlackboard));
            m_Runner.OnTaskStarted += HandleTaskStarted;
            m_Runner.OnTaskCompleted += HandleTaskCompleted;
            m_Runner.OnTaskFailed += HandleTaskFailed;
            m_Runner.OnObjectiveUpdated += HandleObjectiveUpdated;
            m_Runner.OnCustomEvent += HandleCustomEvent;
            m_Log.Add("Runner ready.");
        }

        void Unsubscribe()
        {
            if (m_Runner == null) return;
            m_Runner.OnTaskStarted -= HandleTaskStarted;
            m_Runner.OnTaskCompleted -= HandleTaskCompleted;
            m_Runner.OnTaskFailed -= HandleTaskFailed;
            m_Runner.OnObjectiveUpdated -= HandleObjectiveUpdated;
            m_Runner.OnCustomEvent -= HandleCustomEvent;
            m_Runner.Dispose();
            m_Runner = null;
        }

        void HandleTaskStarted(string taskId) => AddLog("Started", taskId);
        void HandleTaskCompleted(string taskId) => AddLog("Completed", taskId);
        void HandleTaskFailed(string taskId, string reasonKey) => AddLog("Failed", taskId + " (" + reasonKey + ")");
        void HandleObjectiveUpdated(string taskId, string id, int current, int required)
        {
            m_ObjectiveCurrent = current;
            m_ObjectiveRequired = required;
            AddLog("Objective", $"{taskId} / {id} {current}/{required}");
        }
        void HandleCustomEvent(string taskId, string eventId, object payload) =>
            AddLog("Event", $"{taskId} / {eventId} / {payload}");

        void AddLog(string label, string value)
        {
            m_Log.Add(label + ": " + value);
            if (m_Log.Count > 24) m_Log.RemoveAt(0);
        }

        void EnsureStyles()
        {
            if (m_AppTitle != null) return;
            m_Backdrop = Solid(new Color(0.07f, 0.075f, 0.08f, 1f));
            m_Panel = Solid(new Color(0.12f, 0.125f, 0.135f, 0.98f));
            m_Header = Solid(new Color(0.16f, 0.18f, 0.19f, 1f));
            m_CardReady = Solid(new Color(0.13f, 0.16f, 0.16f, 1f));
            m_CardActive = Solid(new Color(0.08f, 0.22f, 0.23f, 1f));
            m_CardDone = Solid(new Color(0.09f, 0.20f, 0.13f, 1f));
            m_CardLocked = Solid(new Color(0.13f, 0.13f, 0.14f, 1f));
            m_LogBg = Solid(new Color(0.08f, 0.085f, 0.095f, 1f));
            m_AccentReady = Solid(new Color(0.95f, 0.70f, 0.32f));
            m_AccentActive = Solid(new Color(0.22f, 0.74f, 0.78f));
            m_AccentDone = Solid(new Color(0.40f, 0.78f, 0.45f));
            m_AccentLocked = Solid(new Color(0.46f, 0.47f, 0.50f));
            m_ButtonBg = Solid(new Color(0.18f, 0.46f, 0.50f, 1f));
            m_ButtonHover = Solid(new Color(0.22f, 0.58f, 0.62f, 1f));
            m_ButtonActive = Solid(new Color(0.13f, 0.36f, 0.40f, 1f));
            m_ButtonDisabled = Solid(new Color(0.17f, 0.18f, 0.18f, 1f));
            m_ProgressBg = Solid(new Color(0.055f, 0.065f, 0.07f, 1f));

            m_AppTitle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            m_SectionTitle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            m_Text = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = Color.white } };
            m_Muted = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(0.75f, 0.78f, 0.78f) } };
            m_Status = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            m_DetailLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.58f, 0.64f, 0.65f) } };
            m_DetailValue = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(0.92f, 0.94f, 0.93f) } };
            m_Button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, padding = new RectOffset(12, 12, 8, 8) };
            m_Button.normal.background = m_ButtonBg;
            m_Button.hover.background = m_ButtonHover;
            m_Button.active.background = m_ButtonActive;
            m_Button.focused.background = m_ButtonBg;
            m_Button.normal.textColor = Color.white;
            m_Button.hover.textColor = Color.white;
            m_Button.active.textColor = Color.white;
            m_Button.focused.textColor = Color.white;
            m_DisabledButton = new GUIStyle(m_Button);
            m_DisabledButton.normal.background = m_ButtonDisabled;
            m_DisabledButton.hover.background = m_ButtonDisabled;
            m_DisabledButton.active.background = m_ButtonDisabled;
            m_DisabledButton.normal.textColor = new Color(0.58f, 0.60f, 0.60f);
            m_LogText = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(0.84f, 0.86f, 0.86f) } };
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        void OnGUI()
        {
            EnsureStyles();
            ScaleStyles();

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_Backdrop);
            var panel = new Rect(Screen.width * 0.06f, Screen.height * 0.06f, Screen.width * 0.88f, Screen.height * 0.88f);
            GUI.DrawTexture(panel, m_Panel);

            var pad = Mathf.Max(18f, Screen.height * 0.025f);
            var header = new Rect(panel.x + pad, panel.y + pad, panel.width - pad * 2f,
                Mathf.Clamp(Screen.height * 0.12f, 92f, 132f));
            DrawHeader(header);

            if (m_Runner == null)
            {
                DrawMissingState(new Rect(header.x, header.yMax + pad, header.width, panel.yMax - header.yMax - pad * 2f));
                return;
            }

            var cardTop = header.yMax + pad;
            var logHeight = Mathf.Clamp(panel.height * 0.30f, 190f, 360f);
            var cardHeight = panel.yMax - cardTop - logHeight - pad * 2f;
            var gap = Mathf.Max(12f, panel.width * 0.012f);
            var cardWidth = (header.width - gap * 2f) / 3f;

            DrawTaskCard(new Rect(header.x, cardTop, cardWidth, cardHeight),
                1, "Intro", "Runs a step graph and waits on objective progress.", introTaskId, objectiveId);
            DrawTaskCard(new Rect(header.x + cardWidth + gap, cardTop, cardWidth, cardHeight),
                2, "Scout", "Instant branch. Completes as soon as it starts.", scoutTaskId, null);
            DrawTaskCard(new Rect(header.x + (cardWidth + gap) * 2f, cardTop, cardWidth, cardHeight),
                3, "Report", "Unlocks only after Intro and Scout are both done.", reportTaskId, null);

            DrawLog(new Rect(header.x, panel.yMax - logHeight - pad, header.width, logHeight));
        }

        void ScaleStyles()
        {
            m_AppTitle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.044f), 24, 42);
            m_SectionTitle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.027f), 16, 26);
            m_Text.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.021f), 13, 20);
            m_Muted.fontSize = m_Text.fontSize;
            m_Status.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.018f), 12, 16);
            m_DetailLabel.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.017f), 11, 15);
            m_DetailValue.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.019f), 12, 17);
            m_Button.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.021f), 13, 19);
            m_DisabledButton.fontSize = m_Button.fontSize;
            m_LogText.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.018f), 12, 16);
        }

        void DrawHeader(Rect rect)
        {
            GUI.DrawTexture(rect, m_Header);
            var x = rect.x + 22f;
            var y = rect.y + 16f;
            GUI.Label(new Rect(x, y, rect.width * 0.52f, 42f), "Task System Sample", m_AppTitle);
            GUI.Label(new Rect(x, y + 46f, rect.width * 0.70f, 34f), NextActionText(), m_Muted);

            var completed = CompletedCount();
            var statusRect = new Rect(rect.xMax - 190f, y + 4f, 150f, 30f);
            var statusTexture = completed == 3 ? m_AccentDone :
                m_Runner == null || m_Runner.ActiveTaskId == null ? m_AccentReady : m_AccentActive;
            DrawStatusPill(statusRect, completed == 3 ? "COMPLETE" : m_Runner == null || m_Runner.ActiveTaskId == null ? "READY" : "ACTIVE",
                statusTexture);
            GUI.Label(new Rect(rect.xMax - 210f, y + 50f, 170f, 30f), completed + " / 3 tasks done", m_Muted);
        }

        void DrawMissingState(Rect rect)
        {
            GUI.DrawTexture(rect, m_LogBg);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 18f, rect.width - 40f, 32f), "Scene references are missing", m_SectionTitle);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 58f, rect.width - 40f, 60f),
                "Assign registry and task graph on TaskSample, then retry.", m_Muted);
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 128f, 160f, 42f), "Retry", m_Button))
                ResetRunner();
        }

        void DrawTaskCard(Rect rect, int index, string title, string description, string taskId, string objective)
        {
            var state = StateOf(taskId);
            GUI.DrawTexture(rect, TextureFor(state));

            var accent = AccentTextureFor(state);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 5f), accent);
            DrawStatusPill(new Rect(rect.x + 18f, rect.y + 18f, 92f, 26f), StateLabel(state), accent);

            GUI.Label(new Rect(rect.x + 18f, rect.y + 56f, rect.width - 36f, 34f), index + ". " + title, m_SectionTitle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 96f, rect.width - 36f, 64f), description, m_Muted);

            var detailY = rect.y + 174f;
            detailY += DrawDetailRow(new Rect(rect.x + 18f, detailY, rect.width - 36f, 30f), "Task ID", taskId);
            detailY += DrawDetailRow(new Rect(rect.x + 18f, detailY, rect.width - 36f, 30f), "Unlock", UnlockText(taskId));
            detailY += DrawDetailRow(new Rect(rect.x + 18f, detailY, rect.width - 36f, 42f), "Signal", SignalText(taskId, objective));

            if (taskId == introTaskId && state == TaskVisualState.Active)
            {
                GUI.Label(new Rect(rect.x + 18f, rect.yMax - 138f, rect.width - 36f, 28f),
                    "Objective " + Mathf.Min(m_ObjectiveCurrent, m_ObjectiveRequired) + "/" + m_ObjectiveRequired, m_Text);
                DrawProgressBar(new Rect(rect.x + 18f, rect.yMax - 100f, rect.width - 36f, 16f),
                    m_ObjectiveRequired <= 0 ? 1f : Mathf.Clamp01((float)m_ObjectiveCurrent / m_ObjectiveRequired));
                if (GUI.Button(new Rect(rect.x + 18f, rect.yMax - 64f, rect.width - 36f, 42f),
                        "Report " + objective, m_Button))
                    m_Runner.ReportObjective(objective, 1);
                return;
            }

            var canStart = state == TaskVisualState.Available && m_Runner.ActiveTaskId == null;
            using (new GUIEnabledScope(canStart))
            {
                var label = state switch
                {
                    TaskVisualState.Done => "Completed",
                    TaskVisualState.Locked => "Locked",
                    TaskVisualState.Active => "Running",
                    _ => "Start " + taskId
                };

                if (GUI.Button(new Rect(rect.x + 18f, rect.yMax - 64f, rect.width - 36f, 42f), label,
                        canStart ? m_Button : m_DisabledButton))
                {
                    var started = m_Runner.StartTask(taskId);
                    AddLog(started ? "Start requested" : "Could not start", taskId);
                }
            }
        }

        void DrawLog(Rect rect)
        {
            GUI.DrawTexture(rect, m_LogBg);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 12f, rect.width * 0.5f, 28f), "Runner events", m_SectionTitle);
            if (GUI.Button(new Rect(rect.xMax - 138f, rect.y + 12f, 120f, 34f), "Reset", m_Button))
                ResetRunner();

            var list = new Rect(rect.x + 18f, rect.y + 52f, rect.width - 36f, rect.height - 64f);
            m_LogScroll = GUI.BeginScrollView(list, m_LogScroll, new Rect(0, 0, list.width - 20f, Mathf.Max(list.height, m_Log.Count * 24f)));
            for (int i = 0; i < m_Log.Count; i++)
                GUI.Label(new Rect(0, i * 24f, list.width - 24f, 24f), m_Log[i], m_LogText);
            GUI.EndScrollView();
        }

        float DrawDetailRow(Rect rect, string label, string value)
        {
            GUI.Label(new Rect(rect.x, rect.y, 84f, rect.height), label, m_DetailLabel);
            GUI.Label(new Rect(rect.x + 88f, rect.y, rect.width - 88f, rect.height), value, m_DetailValue);
            return rect.height + 8f;
        }

        void DrawProgressBar(Rect rect, float normalized)
        {
            GUI.DrawTexture(rect, m_ProgressBg);
            if (normalized <= 0f) return;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * normalized, rect.height), m_AccentActive);
        }

        string NextActionText()
        {
            if (m_Runner == null) return "Missing scene references.";
            if (m_Runner.ActiveTaskId == introTaskId) return "Next: report one objective to finish Intro.";
            if (m_Runner.ActiveTaskId != null) return "Task is running.";
            if (!m_Runner.Journal.IsCompleted(introTaskId)) return "Next: start Intro.";
            if (!m_Runner.Journal.IsCompleted(scoutTaskId)) return "Next: start Scout.";
            if (!m_Runner.Journal.IsCompleted(reportTaskId)) return "Next: start Report.";
            return "All sample tasks are complete. Reset to replay.";
        }

        int CompletedCount()
        {
            if (m_Runner == null) return 0;
            var count = 0;
            if (m_Runner.Journal.IsCompleted(introTaskId)) count++;
            if (m_Runner.Journal.IsCompleted(scoutTaskId)) count++;
            if (m_Runner.Journal.IsCompleted(reportTaskId)) count++;
            return count;
        }

        string UnlockText(string taskId)
        {
            if (taskId == introTaskId) return "Available at start";
            if (taskId == scoutTaskId) return "Available at start";
            return "Intro + Scout completed";
        }

        string SignalText(string taskId, string objective)
        {
            if (taskId == introTaskId) return "Step graph waits for " + objective;
            if (taskId == scoutTaskId) return "Completes immediately";
            return "Completes after dependency gate";
        }

        TaskVisualState StateOf(string taskId)
        {
            if (m_Runner.ActiveTaskId == taskId) return TaskVisualState.Active;
            if (m_Runner.Journal.IsCompleted(taskId)) return TaskVisualState.Done;
            return m_Runner.IsAvailable(taskId) ? TaskVisualState.Available : TaskVisualState.Locked;
        }

        Texture2D TextureFor(TaskVisualState state) => state switch
        {
            TaskVisualState.Active => m_CardActive,
            TaskVisualState.Done => m_CardDone,
            TaskVisualState.Locked => m_CardLocked,
            _ => m_CardReady
        };

        Texture2D AccentTextureFor(TaskVisualState state) => state switch
        {
            TaskVisualState.Active => m_AccentActive,
            TaskVisualState.Done => m_AccentDone,
            TaskVisualState.Locked => m_AccentLocked,
            _ => m_AccentReady
        };

        static string StateLabel(TaskVisualState state) => state switch
        {
            TaskVisualState.Active => "ACTIVE",
            TaskVisualState.Done => "DONE",
            TaskVisualState.Locked => "LOCKED",
            _ => "READY"
        };

        void DrawStatusPill(Rect rect, string text, Texture2D texture)
        {
            GUI.DrawTexture(rect, texture);
            var previous = m_Status.normal.textColor;
            m_Status.normal.textColor = Color.black;
            GUI.Label(rect, text, m_Status);
            m_Status.normal.textColor = previous;
        }

        enum TaskVisualState
        {
            Available,
            Active,
            Done,
            Locked
        }

        readonly struct GUIEnabledScope : System.IDisposable
        {
            readonly bool m_Previous;

            public GUIEnabledScope(bool enabled)
            {
                m_Previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose() => GUI.enabled = m_Previous;
        }
    }
}
