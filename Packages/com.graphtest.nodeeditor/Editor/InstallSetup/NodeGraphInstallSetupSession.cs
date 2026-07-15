namespace NodeEditor.EditorUI
{
    public sealed class NodeGraphInstallSetupSession
    {
        readonly NodeGraphInstallSetupQueue m_Queue;
        bool m_Deferred;

        public NodeGraphInstallSetupSession(NodeGraphInstallSetupQueue queue)
        {
            m_Queue = queue;
        }

        public NodeGraphInstallSetupDescriptor Active { get; private set; }

        public bool TryBegin()
        {
            if (m_Deferred || Active != null || m_Queue == null) return false;
            Active = m_Queue.NextPending();
            return Active != null;
        }

        public void CompleteActive()
        {
            Active = null;
        }

        public void Defer()
        {
            Active = null;
            m_Deferred = true;
        }
    }
}
