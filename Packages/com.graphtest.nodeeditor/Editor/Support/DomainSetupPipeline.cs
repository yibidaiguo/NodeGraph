// DomainSetupPipeline.cs — 领域 Setup 生成器的「通用管线」（机制），领域只供策略（TDef 基类 + 目录 + 名称）。
// 过去 Dialogue/Task/StateMachine 三个 Setup 各自复制 SetupDefinitions/SetupRegistry 且已经漂移
//（坏资产守卫只在 StateMachine 版、路径 Trim 只在 Task 版）——现收敛为本管线，安全守卫齐备且单点维护：
//   · 一类型一 .asset（缺则建、永远 RebuildFromCode 刷新——Define() 变更后端口/参数跟着更新）；
//   · 类型不符 / 文件在但载入为 null（坏资产如 m_Script fileID 0）→ 报告并跳过，绝不覆盖来源不明的文件（硬规则 A1 失败关闭）；
//   · registry 只补本域：保留 projectDomain 里所有非本域定义（别域各自的生成器掌管它们自己的），绝不动 universal（节点分层规则）；
//   · 所有写入带 Undo + SetDirty（硬规则 A2）；目录经 ProjectAssetPaths.EnsureFolder（带项目路径守卫）。
// 仅 Editor 程序集。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public static class DomainSetupPipeline
    {
        // 发现 TDef 全部具体子类 → 每类型一个 <defsDir>/<TypeName>.asset。owner 用于 Undo 标签与诊断（如 "Dialogue"）。
        public static (List<NodeDefinition> defs, int created) SetupDefinitions<TDef>(string owner, string defsDir)
            where TDef : NodeDefinition
        {
            var defs = new List<NodeDefinition>();
            int created = 0;
            defsDir = ProjectAssetPaths.NormalizeAssetPath(defsDir);
            ProjectAssetPaths.EnsureFolder(defsDir);

            var types = TypeCache.GetTypesDerivedFrom<TDef>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name);

            foreach (var type in types)
            {
                var path = $"{defsDir}/{type.Name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<NodeDefinition>(path);
                bool assetCreated = false;
                if (asset != null && asset.GetType() != type)
                {
                    // 防御性处理：此路径上存在类型不符的陈旧 asset（正常使用下不会发生，路径以类型名为键）——跳过而非悄悄误报。
                    Debug.LogWarning($"{owner}Setup: {path} exists but is not a {type.Name}; skipping.");
                    continue;
                }
                if (asset == null && File.Exists(path))
                {
                    // 文件在但载入为 null = 损坏资产（如 m_Script fileID 0）。不要覆盖来源不明的文件（A1 失败关闭）。
                    Debug.LogWarning($"{owner}Setup: {path} exists but failed to load as {type.Name}; skipping.");
                    continue;
                }
                if (asset == null)
                {
                    asset = (NodeDefinition)ScriptableObject.CreateInstance(type);
                    AssetDatabase.CreateAsset(asset, path);
                    Undo.RegisterCreatedObjectUndo(asset, $"Create {owner} Node Definition");
                    assetCreated = true;
                    created++;
                }

                // 始终重建 —— 若 Define() 自 asset 创建以来发生变化，则刷新 ports/params。
                if (!assetCreated)
                    Undo.RegisterCompleteObjectUndo(asset, $"Rebuild {owner} Node Definition");
                asset.RebuildFromCode();
                EditorUtility.SetDirty(asset);
                defs.Add(asset);
            }

            return (defs, created);
        }

        // registry 合并：projectDomain 中本域（TDef）条目完全由调用方 defs 接管；非本域条目原样保留；universal 不动。
        public static bool MergeIntoRegistry<TDef>(string owner, string registryPath, List<NodeDefinition> defs)
            where TDef : NodeDefinition
        {
            registryPath = ProjectAssetPaths.NormalizeAssetPath(registryPath);
            ProjectAssetPaths.EnsureFolder(Path.GetDirectoryName(registryPath)?.Replace('\\', '/'));

            var registry = AssetDatabase.LoadAssetAtPath<NodeRegistry>(registryPath);
            bool created = false;
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<NodeRegistry>();
                AssetDatabase.CreateAsset(registry, registryPath);
                Undo.RegisterCreatedObjectUndo(registry, "Create Node Registry");
                created = true;
            }

            var preserved = registry.projectDomain
                .Where(d => d != null && !(d is TDef))
                .ToList();
            if (!created)
                Undo.RegisterCompleteObjectUndo(registry, $"Register {owner} Node Definitions");
            registry.projectDomain.Clear();
            registry.projectDomain.AddRange(preserved);
            registry.projectDomain.AddRange(defs);
            EditorUtility.SetDirty(registry);
            return created;
        }
    }
}
