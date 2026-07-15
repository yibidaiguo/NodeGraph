using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    [InitializeOnLoad]
    public static class NodeGraphInstallSetupCoordinator
    {
        const string DeferredSessionKey = "NodeGraph.InstallSetup.Deferred";

        static readonly NodeGraphInstallSetupQueue s_Queue = new NodeGraphInstallSetupQueue();
        static readonly NodeGraphInstallSetupSession s_Session = new NodeGraphInstallSetupSession(s_Queue);
        static bool s_Scheduled;
        static NodeGraphInstallSetupWindow s_Window;

        static NodeGraphInstallSetupCoordinator()
        {
            Schedule();
        }

        public static IReadOnlyList<NodeGraphInstallSetupDescriptor> RegisteredDescriptors => s_Queue.Descriptors;

        public static bool Register(NodeGraphInstallSetupDescriptor descriptor)
        {
            if (!s_Queue.TryRegister(descriptor, out var error))
            {
                Debug.LogError(error);
                return false;
            }
            Schedule();
            return true;
        }

        static void Schedule()
        {
            if (s_Scheduled || Application.isBatchMode) return;
            s_Scheduled = true;
            EditorApplication.delayCall += TryOpenNext;
        }

        static void TryOpenNext()
        {
            s_Scheduled = false;
            if (Application.isBatchMode || SessionState.GetBool(DeferredSessionKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Schedule();
                return;
            }
            if (s_Window != null || !s_Session.TryBegin()) return;
            s_Window = NodeGraphInstallSetupWindow.Open(s_Session.Active, Complete, Defer);
        }

        static void Complete(NodeGraphInstallSetupWindow window)
        {
            if (window != s_Window) return;
            s_Window = null;
            s_Session.CompleteActive();
            Schedule();
        }

        static void Defer(NodeGraphInstallSetupWindow window)
        {
            if (window != s_Window) return;
            s_Window = null;
            s_Session.Defer();
            SessionState.SetBool(DeferredSessionKey, true);
        }
    }
}
