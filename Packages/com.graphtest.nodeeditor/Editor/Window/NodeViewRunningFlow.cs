using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public partial class NodeView
    {
        const double RunningFlowPeriodSeconds = 2.0;
        const long RunningFlowFrameMilliseconds = 33;

        IVisualElementScheduledItem m_RunningFlowSchedule;
        bool m_RunningFlowEnabled;
        double m_RunningFlowStartedAt;
        float m_RunningFlowPhase;

        void InitializeRunningFlowLifecycle()
        {
            RegisterCallback<AttachToPanelEvent>(_ => ResumeRunningFlowAnimation());
            RegisterCallback<DetachFromPanelEvent>(_ => PauseRunningFlowAnimation());
        }

        void SetRunningFlowEnabled(bool enabled)
        {
            if (m_RunningFlowEnabled == enabled) return;

            m_RunningFlowEnabled = enabled;
            m_RunningFlowPhase = 0f;
            if (enabled) ResumeRunningFlowAnimation();
            else PauseRunningFlowAnimation();
        }

        void ResumeRunningFlowAnimation()
        {
            if (!m_RunningFlowEnabled || panel == null || m_RunningFlowSchedule != null) return;

            m_RunningFlowStartedAt = EditorApplication.timeSinceStartup
                - m_RunningFlowPhase * RunningFlowPeriodSeconds;
            m_RunningFlowSchedule = schedule.Execute(TickRunningFlowAnimation)
                .Every(RunningFlowFrameMilliseconds);
        }

        void PauseRunningFlowAnimation()
        {
            m_RunningFlowSchedule?.Pause();
            m_RunningFlowSchedule = null;
        }

        void TickRunningFlowAnimation()
        {
            if (!m_RunningFlowEnabled)
            {
                PauseRunningFlowAnimation();
                return;
            }

            var elapsed = EditorApplication.timeSinceStartup - m_RunningFlowStartedAt;
            m_RunningFlowPhase = Mathf.Repeat((float)(elapsed / RunningFlowPeriodSeconds), 1f);
            MarkDirtyRepaint();
        }

        static float RunningFlowCenter(float phase, float halfWidth)
            => Mathf.Lerp(-halfWidth, 1f + halfWidth, Mathf.Clamp01(phase));

        static float RunningFlowOpacityAt(float position, float center, float halfWidth, float peak)
        {
            if (halfWidth <= 0f || peak <= 0f) return 0f;
            return peak * Mathf.Clamp01(1f - Mathf.Abs(position - center) / halfWidth);
        }
    }
}
