// NodeGraphUninstall.cs — 卸载前的清理：先把残留面摆给人看，确认了才删。
//
// 顺序是「先清理、后移包」，不能反过来：包一移走，它的 Editor 代码就没了，
// 那时候没有任何东西认识这批生成资产——遗留就此永久化。
//
// 删除本身不可逆，所以：被外部引用的条目默认不删、单独一栏列出；引用扫描没跑完时
// 整个「无人引用」的结论作废，按未知处理（要删得由人显式勾选）。
//
// 仅 Editor/ 程序集。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public static class NodeGraphUninstall
    {
        /// <summary>删掉残留。includeReferenced = true 时连被引用的资产也删（由人显式勾选后才该为 true）。</summary>
        public static void Apply(NodeGraphResidue residue, bool includeReferenced)
        {
            if (residue == null) return;

            var targets = (includeReferenced ? residue.Assets : residue.Removable)
                .Select(item => item.Path)
                .ToArray();

            var failures = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in targets)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(path) == null) continue;
                    if (!AssetDatabase.DeleteAsset(path)) failures.Add(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            // 目录已按「深的在前」排好；只删真空了的，留着东西的目录一律不碰。
            foreach (var folder in residue.Folders.Select(item => item.Path))
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                if (!IsEmptyFolder(folder)) continue;
                if (!AssetDatabase.DeleteAsset(folder)) failures.Add(folder);
            }

            // 节点定义被删之后，共享注册表里对应的槽位会变成 Missing。留着它们就是「删是删了、
            // 却在别处留下一排空洞」——把空槽摘掉，注册表才真回到没装这个模块之前的样子。
            PruneRegistry();

            foreach (var key in residue.EditorPrefKeys) EditorPrefs.DeleteKey(key);
            SessionState.EraseString("NodeEditor.Breakpoints");

            foreach (var file in residue.ProjectSettingsFiles)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch (Exception exception)
                {
                    failures.Add($"{file}（{exception.Message}）");
                }
            }
            if (residue.ProjectSettingsFiles.Count > 0) NodeGraphInstallRoot.Invalidate();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failures.Count > 0)
                Debug.LogError($"NodeGraph: 这些残留没能删掉，请手工处理 / could not be removed:\n- " +
                               string.Join("\n- ", failures));
            else
                Debug.Log($"NodeGraph: 已清理「{residue.DisplayName}」的工程残留。 / Cleaned up generated project files for '{residue.DisplayName}'.");
        }

        // 注册表自己也可能刚被删掉（卸框架时），那就没什么可摘的。
        static void PruneRegistry()
        {
            var registry = AssetDatabase.FindAssets("t:NodeRegistry")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(ProjectAssetPaths.IsProjectAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<NodeRegistry>)
                .FirstOrDefault(asset => asset != null);
            if (registry == null) return;

            int removed = registry.universal.RemoveAll(definition => definition == null) +
                          registry.projectDomain.RemoveAll(definition => definition == null);
            if (removed == 0) return;

            registry.InvalidateSchemaCache();
            EditorUtility.SetDirty(registry);
            Debug.Log($"NodeGraph: 从共享注册表摘掉 {removed} 个空槽位。 / Removed {removed} empty slots from the shared NodeRegistry.");
        }

        static bool IsEmptyFolder(string folder) =>
            !Directory.EnumerateFileSystemEntries(folder)
                .Any(entry => !entry.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));

        /// <summary>找一个模块的安装配置描述符；没注册就返回 null（该模块没有生成物要清）。</summary>
        public static NodeGraphInstallSetupDescriptor FindDescriptor(string moduleId) =>
            NodeGraphInstallSetupCoordinator.RegisteredDescriptors
                .FirstOrDefault(descriptor => string.Equals(descriptor.ModuleId, moduleId, StringComparison.Ordinal));
    }

    /// <summary>卸载确认窗：把要删的东西逐条摆出来，人点了才删。</summary>
    public sealed class NodeGraphUninstallWindow : EditorWindow
    {
        NodeGraphResidue m_Residue;
        Action m_Confirmed;
        Vector2 m_Scroll;
        bool m_IncludeReferenced;
        bool m_Resolved;

        /// <summary>
        /// 开确认窗。残留为空时不开窗，直接回调 confirmed——没东西要删就不该拿一个空对话框拦人。
        /// 返回 true 表示已经把决定权交出去了（开了窗或直接回调）。
        /// </summary>
        public static bool Open(NodeGraphResidue residue, Action confirmed)
        {
            if (residue == null || residue.IsEmpty)
            {
                confirmed?.Invoke();
                return true;
            }

            var window = CreateInstance<NodeGraphUninstallWindow>();
            window.m_Residue = residue;
            window.m_Confirmed = confirmed;
            window.titleContent = new GUIContent("卸载清理 / Uninstall Cleanup");
            window.minSize = new Vector2(620f, 420f);
            window.ShowUtility();
            return true;
        }

        void OnGUI()
        {
            if (m_Residue == null) { Close(); return; }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField($"{m_Residue.DisplayName} — 卸载清理 / Uninstall Cleanup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "移除这个包之前，下面这些由它生成的工程文件会被删掉。删除不可逆，请先确认。\n" +
                "These generated project files are removed before the package itself. Deletion cannot be undone.",
                MessageType.Warning);

            if (!m_Residue.ReferenceScanCompleted)
            {
                EditorGUILayout.HelpBox(
                    "引用检查被中断，「无人引用」这个结论没验过——下面的分栏不可信，请自行确认后再删。\n" +
                    "The reference check was cancelled; the 'unreferenced' grouping below is unverified.",
                    MessageType.Error);
            }

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            DrawSection("将被删除 / Will be deleted", m_Residue.Removable.Select(item => item.Path));

            var blocked = m_Residue.Blocked.ToArray();
            if (blocked.Length > 0)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("被工程其它资产引用 / Referenced elsewhere", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "这些资产还被工程里别的东西引用着，默认保留——删了会在别处留下丢失引用。\n" +
                    "These are still referenced by other project assets and are kept by default.",
                    MessageType.Info);
                foreach (var item in blocked)
                {
                    EditorGUILayout.LabelField("• " + item.Path);
                    using (new EditorGUI.IndentLevelScope())
                        foreach (var referrer in item.ExternalReferences.Take(5))
                            EditorGUILayout.LabelField("← " + referrer, EditorStyles.miniLabel);
                }
                m_IncludeReferenced = EditorGUILayout.ToggleLeft(
                    "连同被引用的资产一起删除 / Delete referenced assets too", m_IncludeReferenced);
            }

            DrawSection("将被删除的空目录 / Empty folders", m_Residue.Folders.Select(item => item.Path));
            DrawSection("编辑器偏好 / Editor preferences", m_Residue.EditorPrefKeys);
            DrawSection("工程配置 / Project settings", m_Residue.ProjectSettingsFiles);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("取消 / Cancel", GUILayout.Height(28f))) Close();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("保留文件，只移除包 / Keep Files", GUILayout.Width(220f), GUILayout.Height(28f)))
                {
                    m_Resolved = true;
                    var confirmed = m_Confirmed;
                    Close();
                    confirmed?.Invoke();
                }
                if (GUILayout.Button("清理并移除 / Clean & Remove", GUILayout.Width(200f), GUILayout.Height(28f)))
                {
                    m_Resolved = true;
                    var residue = m_Residue;
                    bool includeReferenced = m_IncludeReferenced;
                    var confirmed = m_Confirmed;
                    Close();
                    NodeGraphUninstall.Apply(residue, includeReferenced);
                    confirmed?.Invoke();
                }
            }
            EditorGUILayout.Space(8f);
        }

        static void DrawSection(string title, IEnumerable<string> lines)
        {
            var items = lines?.ToArray() ?? Array.Empty<string>();
            if (items.Length == 0) return;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"{title}（{items.Length}）", EditorStyles.boldLabel);
            foreach (var line in items) EditorGUILayout.LabelField("• " + line);
        }

        void OnDisable()
        {
            // 直接关窗 = 取消：什么都不删，也不移包。
            if (!m_Resolved) m_Confirmed = null;
        }
    }
}
