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
        string m_RootDraft;
        string m_RootError;
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
            m_RootDraft = NodeGraphInstallRoot.Root;
            m_Draft = descriptor.CreateDraft();
            m_SerializedDraft = new SerializedObject(m_Draft);
        }

        // 安装根变了，下面那批模块路径就全是旧根拼出来的——重建草稿让它们跟着走。
        // 已经手改过的路径一并丢掉：留着才是「一半新根一半旧根」的坏结果。
        void RebuildDraft()
        {
            if (m_Draft != null && !AssetDatabase.Contains(m_Draft)) DestroyImmediate(m_Draft);
            m_Draft = m_Descriptor.CreateDraft();
            m_SerializedDraft = new SerializedObject(m_Draft);
            m_Error = null;
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

            DrawInstallRoot();

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

        // 安装根：节点图在这个工程里唯一的顶级目录，下面那批模块路径全从它拼出来。
        // 放在最前面是刻意的——先定根，再看落点，而不是装完才发现 Assets 根目录多了五个文件夹。
        void DrawInstallRoot()
        {
            EditorGUILayout.Space(4f);
            if (NodeGraphInstallRoot.IsLegacyLayout)
            {
                EditorGUILayout.HelpBox(
                    "这个工程沿用升级前的分散布局（Assets/NodeEditorSettings、Assets/<模块>Content），路径保持原样不动。" +
                    "要收进单一安装根，先把已有资产搬到新根下，再在这里填写新根。\n" +
                    "This project still uses the pre-upgrade flat layout; paths are left untouched. " +
                    "Move the existing assets under a single root first, then set that root here.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("安装根 / Install Root");
                m_RootDraft = EditorGUILayout.TextField(m_RootDraft);
                using (new EditorGUI.DisabledScope(
                    NodeGraphInstallRoot.NormalizedEquals(m_RootDraft, NodeGraphInstallRoot.Root)))
                {
                    if (GUILayout.Button("应用 / Apply", GUILayout.Width(110f)))
                        ApplyInstallRoot();
                }
            }

            if (!string.IsNullOrEmpty(m_RootError))
                EditorGUILayout.HelpBox(m_RootError, MessageType.Error);
        }

        void ApplyInstallRoot()
        {
            m_RootError = NodeGraphInstallRoot.TrySet(m_RootDraft);
            if (!string.IsNullOrEmpty(m_RootError)) return;
            m_RootDraft = NodeGraphInstallRoot.Root;
            RebuildDraft();
        }

        void SaveAndGenerate()
        {
            m_SerializedDraft.ApplyModifiedProperties();

            // 生成之前把安装根落盘：接受默认根的工程也要留下痕迹，否则卸载清理无从知道该扫哪。
            m_RootError = NodeGraphInstallRoot.PinCurrent();
            if (!string.IsNullOrEmpty(m_RootError))
            {
                Repaint();
                return;
            }

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
