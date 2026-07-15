using System;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public sealed class NodeGraphInstallSetupWindow : EditorWindow
    {
        NodeGraphInstallSetupDescriptor m_Descriptor;
        ScriptableObject m_Draft;
        SerializedObject m_SerializedDraft;
        Action<NodeGraphInstallSetupWindow> m_Completed;
        Action<NodeGraphInstallSetupWindow> m_Deferred;
        Vector2 m_Scroll;
        string m_Error;
        bool m_Resolved;

        public static NodeGraphInstallSetupWindow Open(
            NodeGraphInstallSetupDescriptor descriptor,
            Action<NodeGraphInstallSetupWindow> completed,
            Action<NodeGraphInstallSetupWindow> deferred)
        {
            var window = CreateInstance<NodeGraphInstallSetupWindow>();
            window.Initialize(descriptor, completed, deferred);
            window.titleContent = new GUIContent("安装路径 / Install Paths");
            window.minSize = new Vector2(560f, 380f);
            window.ShowUtility();
            return window;
        }

        void Initialize(
            NodeGraphInstallSetupDescriptor descriptor,
            Action<NodeGraphInstallSetupWindow> completed,
            Action<NodeGraphInstallSetupWindow> deferred)
        {
            m_Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            m_Completed = completed;
            m_Deferred = deferred;
            m_Draft = descriptor.CreateDraft();
            m_SerializedDraft = new SerializedObject(m_Draft);
        }

        void OnGUI()
        {
            if (m_Descriptor == null || m_SerializedDraft == null)
            {
                EditorGUILayout.HelpBox("安装配置不可用。 / Install configuration is unavailable.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField($"{m_Descriptor.DisplayName} 路径设置 / Path Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "请先确认此模块生成资产的位置。只有点击“保存并生成”后才会写入工程。\n" +
                "Review where this module will generate project assets. Nothing is written until you choose Save & Generate.",
                MessageType.Info);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            m_SerializedDraft.Update();
            var property = m_SerializedDraft.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script") continue;
                EditorGUILayout.PropertyField(property, true);
            }
            m_SerializedDraft.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(m_Error))
                EditorGUILayout.HelpBox(m_Error, MessageType.Error);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("稍后 / Later", GUILayout.Height(28f)))
                    Defer();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("保存并生成 / Save & Generate", GUILayout.Width(220f), GUILayout.Height(28f)))
                    SaveAndGenerate();
            }
            EditorGUILayout.Space(8f);
        }

        void SaveAndGenerate()
        {
            m_SerializedDraft.ApplyModifiedProperties();
            if (!m_Descriptor.TrySaveAndGenerate(m_Draft, out m_Error))
            {
                Repaint();
                return;
            }

            m_Resolved = true;
            m_Completed?.Invoke(this);
            Close();
        }

        void Defer()
        {
            m_Resolved = true;
            m_Deferred?.Invoke(this);
            Close();
        }

        void OnDisable()
        {
            if (!m_Resolved) m_Deferred?.Invoke(this);
            if (m_Draft != null && !AssetDatabase.Contains(m_Draft)) DestroyImmediate(m_Draft);
            m_Draft = null;
            m_SerializedDraft = null;
        }
    }
}
