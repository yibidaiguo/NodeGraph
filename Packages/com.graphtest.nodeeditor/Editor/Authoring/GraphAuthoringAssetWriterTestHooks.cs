using System;

namespace NodeEditor.EditorUI
{
    // 仅供 Editor 测试在确定的事务检查点抛错；生产 API 不暴露故障注入能力。
    internal static class GraphAuthoringAssetWriterTestHooks
    {
        internal const string AfterGraphMutation = "after-graph-mutation";
        internal const string AfterBlackboardMutation = "after-blackboard-mutation";

        static string s_FailurePoint;

        internal static IDisposable FailAt(string failurePoint) => new FailureScope(failurePoint);

        internal static void Checkpoint(string checkpoint)
        {
            if (string.Equals(s_FailurePoint, checkpoint, StringComparison.Ordinal))
                throw new InvalidOperationException("Injected graph authoring transaction failure at " + checkpoint + ".");
        }

        internal static void Reset() => s_FailurePoint = null;

        sealed class FailureScope : IDisposable
        {
            readonly string m_Previous;
            bool m_Disposed;

            public FailureScope(string failurePoint)
            {
                m_Previous = s_FailurePoint;
                s_FailurePoint = failurePoint;
            }

            public void Dispose()
            {
                if (m_Disposed) return;
                s_FailurePoint = m_Previous;
                m_Disposed = true;
            }
        }
    }
}
