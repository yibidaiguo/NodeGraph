using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeEditor.EditorUI
{
    public partial class NodeView
    {
        const double RunningFlowPeriodSeconds = 2.0;
        const long RunningFlowFrameMilliseconds = 33;
        const float RunningFlowHalfWidth = 0.14f;

        static readonly CustomStyleProperty<Color> s_RunningFlowColor =
            new("--ne-node-running-flow-color");
        [System.ThreadStatic] static List<Vector2> s_RunningFlowClipScratchA;
        [System.ThreadStatic] static List<Vector2> s_RunningFlowClipScratchB;

        IVisualElementScheduledItem m_RunningFlowSchedule;
        bool m_RunningFlowEnabled;
        double m_RunningFlowStartedAt;
        float m_RunningFlowPhase;
        Color m_RunningFlowColor;

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
            if (!ShouldAdvanceRunningFlow(m_RunningFlowEnabled,
                    InternalEditorUtility.isApplicationActive)) return;

            var elapsed = EditorApplication.timeSinceStartup - m_RunningFlowStartedAt;
            m_RunningFlowPhase = Mathf.Repeat((float)(elapsed / RunningFlowPeriodSeconds), 1f);
            MarkDirtyRepaint();
        }

        static bool ShouldAdvanceRunningFlow(bool enabled, bool applicationActive)
            => enabled && applicationActive;

        static float RunningFlowCenter(float phase, float halfWidth)
            => Mathf.Lerp(-halfWidth, 1f + halfWidth, Mathf.Clamp01(phase));

        static float RunningFlowOpacityAt(float position, float center, float halfWidth, float peak)
        {
            if (halfWidth <= 0f || peak <= 0f) return 0f;
            return peak * Mathf.Clamp01(1f - Mathf.Abs(position - center) / halfWidth);
        }

        void ResolveRunningFlowStyle(CustomStyleResolvedEvent evt)
        {
            evt.customStyle.TryGetValue(s_RunningFlowColor, out m_RunningFlowColor);
        }

        void DrawRunningFlow(MeshGenerationContext context, NodeRole role, Rect bounds)
        {
            if (!m_RunningFlowEnabled || m_RunningFlowColor.a <= 0f) return;

            var perimeter = s_RoundedSampleScratch ??= new List<Vector2>(64);
            BuildRoleSilhouetteSamples(role, bounds, perimeter);
            if (perimeter.Count < 3) return;

            var center = RunningFlowCenter(m_RunningFlowPhase, RunningFlowHalfWidth);
            var lower = center - RunningFlowHalfWidth;
            var upper = center + RunningFlowHalfWidth;
            var firstPass = s_RunningFlowClipScratchA ??= new List<Vector2>(64);
            var band = s_RunningFlowClipScratchB ??= new List<Vector2>(64);
            ClipRunningFlowRegion(perimeter, bounds, lower, true, firstPass);
            ClipRunningFlowRegion(firstPass, bounds, upper, false, band);
            if (band.Count < 3) return;

            DrawRunningFlowRegion(context, band, bounds, center);
        }

        static void ClipRunningFlowRegion(IReadOnlyList<Vector2> input, Rect bounds,
            float boundary, bool keepAbove, List<Vector2> output)
        {
            output.Clear();
            if (input.Count == 0) return;

            var previous = input[input.Count - 1];
            var previousValue = GradientPosition(previous, bounds);
            var previousInside = keepAbove ? previousValue >= boundary : previousValue <= boundary;
            for (var i = 0; i < input.Count; i++)
            {
                var current = input[i];
                var currentValue = GradientPosition(current, bounds);
                var currentInside = keepAbove ? currentValue >= boundary : currentValue <= boundary;
                if (currentInside != previousInside)
                {
                    var denominator = currentValue - previousValue;
                    var amount = Mathf.Abs(denominator) > 0.00001f
                        ? Mathf.Clamp01((boundary - previousValue) / denominator)
                        : 0f;
                    AppendDistinct(output, Vector2.Lerp(previous, current, amount));
                }
                if (currentInside) AppendDistinct(output, current);
                previous = current;
                previousValue = currentValue;
                previousInside = currentInside;
            }
            RemoveDuplicateClosure(output);
        }

        void DrawRunningFlowRegion(MeshGenerationContext context, IReadOnlyList<Vector2> region,
            Rect bounds, float bandCenter)
        {
            var center = Vector2.zero;
            for (var i = 0; i < region.Count; i++) center += region[i];
            center /= region.Count;

            var mesh = context.Allocate(region.Count + 1, region.Count * 3);
            mesh.SetNextVertex(RunningFlowVertex(center, bounds, bandCenter));
            for (var i = 0; i < region.Count; i++)
                mesh.SetNextVertex(RunningFlowVertex(region[i], bounds, bandCenter));

            for (var i = 0; i < region.Count; i++)
            {
                mesh.SetNextIndex(0);
                mesh.SetNextIndex((ushort)(i + 1));
                mesh.SetNextIndex((ushort)(((i + 1) % region.Count) + 1));
            }
        }

        Vertex RunningFlowVertex(Vector2 point, Rect bounds, float bandCenter)
        {
            var color = m_RunningFlowColor;
            color.a = RunningFlowOpacityAt(GradientPosition(point, bounds), bandCenter,
                RunningFlowHalfWidth, color.a);
            return new Vertex
            {
                position = new Vector3(point.x, point.y, Vertex.nearZ),
                tint = color,
                uv = Vector2.zero
            };
        }
    }
}
