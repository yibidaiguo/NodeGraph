using System;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public sealed class NodeGraphInstallSetupDescriptor
    {
        readonly Func<bool> m_IsConfigured;
        readonly Func<ScriptableObject> m_CreateDraft;
        readonly Func<ScriptableObject, string> m_Validate;
        readonly Func<ScriptableObject, string> m_Save;
        readonly Action m_Generate;

        public NodeGraphInstallSetupDescriptor(
            string moduleId,
            string displayName,
            int order,
            Func<bool> isConfigured,
            Func<ScriptableObject> createDraft,
            Func<ScriptableObject, string> validate,
            Func<ScriptableObject, string> save,
            Action generate)
        {
            ModuleId = moduleId;
            DisplayName = displayName;
            Order = order;
            m_IsConfigured = isConfigured;
            m_CreateDraft = createDraft;
            m_Validate = validate;
            m_Save = save;
            m_Generate = generate;
        }

        public string ModuleId { get; }
        public string DisplayName { get; }
        public int Order { get; }

        public bool IsConfigured
        {
            get
            {
                try { return m_IsConfigured?.Invoke() ?? true; }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return true;
                }
            }
        }

        public ScriptableObject CreateDraft()
        {
            var draft = m_CreateDraft?.Invoke();
            if (draft == null)
                throw new InvalidOperationException($"NodeGraph install setup '{ModuleId}' did not create a configuration draft.");
            return draft;
        }

        public string Validate(ScriptableObject draft)
        {
            if (draft == null) return "The path configuration draft is missing.";
            return m_Validate?.Invoke(draft);
        }

        public bool TrySaveAndGenerate(ScriptableObject draft, out string error)
        {
            error = Validate(draft);
            if (!string.IsNullOrEmpty(error)) return false;

            try
            {
                if (!AssetDatabase.Contains(draft))
                {
                    error = m_Save?.Invoke(draft);
                    if (!string.IsNullOrEmpty(error)) return false;
                }
                m_Generate?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = exception.Message;
                return false;
            }
        }

        internal bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(ModuleId))
            {
                error = "NodeGraph install setup descriptors require a non-empty module id.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                error = $"NodeGraph install setup '{ModuleId}' requires a display name.";
                return false;
            }
            if (m_IsConfigured == null || m_CreateDraft == null || m_Validate == null || m_Save == null || m_Generate == null)
            {
                error = $"NodeGraph install setup '{ModuleId}' is missing a required callback.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
