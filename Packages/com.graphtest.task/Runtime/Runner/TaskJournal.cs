using System.Collections.Generic;

namespace TaskEditor
{
    public class TaskJournal
    {
        readonly HashSet<string> m_Completed = new();
        readonly HashSet<string> m_Failed = new();

        public IEnumerable<string> CompletedTaskIds => m_Completed;
        public IEnumerable<string> FailedTaskIds => m_Failed;

        public bool IsCompleted(string taskId) => !string.IsNullOrEmpty(taskId) && m_Completed.Contains(taskId);
        public bool IsFailed(string taskId) => !string.IsNullOrEmpty(taskId) && m_Failed.Contains(taskId);

        public void MarkCompleted(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            m_Failed.Remove(taskId);
            m_Completed.Add(taskId);
        }

        public void MarkFailed(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            m_Completed.Remove(taskId);
            m_Failed.Add(taskId);
        }

        public void Load(IEnumerable<string> completedTaskIds, IEnumerable<string> failedTaskIds)
        {
            m_Completed.Clear();
            m_Failed.Clear();
            if (completedTaskIds != null)
                foreach (var taskId in completedTaskIds)
                    if (!string.IsNullOrEmpty(taskId)) m_Completed.Add(taskId);
            if (failedTaskIds != null)
                foreach (var taskId in failedTaskIds)
                    if (!string.IsNullOrEmpty(taskId)) m_Failed.Add(taskId);
        }
    }
}
