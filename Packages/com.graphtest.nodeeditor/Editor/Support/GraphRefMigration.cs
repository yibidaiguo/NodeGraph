// GraphRefMigration.cs —— 0.0.x 的 objectOverrides 子图引用 → 0.1.0 的 graphRefs。NodeEditor.Editor 程序集。
//
// 为什么要读 YAML 文本而不是走 Unity 反序列化：0.1.0 的 NodeInstance 已经没有 objectOverrides 字段了，
// 反序列化根本读不到它。但 .asset 文件里那段 YAML 还在（Unity 只是忽略未知字段，不会主动改写文件），
// 所以在**保存这张图之前**还来得及把它捞出来。这也是迁移必须"先跑、再保存"的原因。
//
// 幂等：已经有 graphRefs 的参数不会被覆盖；跑第二遍是空操作。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public static class GraphRefMigration
    {
        // 匹配一段 objectOverrides 条目：
        //     - paramName: stepGraph
        //       value: {fileID: 11400000, guid: 5f4b..., type: 2}
        static readonly Regex EntryRx = new Regex(
            @"-\s*paramName:\s*(?<param>[^\r\n]+?)\s*[\r\n]+\s*value:\s*\{[^}]*?guid:\s*(?<guid>[0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        // 定位每个节点实例块里的 objectOverrides 段（到下一个同级键为止）。
        static readonly Regex BlockRx = new Regex(
            @"instanceId:\s*(?<id>[^\r\n]+?)\s*[\r\n](?<body>[\s\S]*?)(?=\n\s*-\s*instanceId:|\z)",
            RegexOptions.Compiled);

        [MenuItem("NodeEditor/Migrate/Upgrade Graph References (0.1.0)")]
        public static void Run()
        {
            // 第一步：给每张图播种稳定 graphId（缺失时取 asset GUID），并建 guid -> graphId 表，
            // 因为旧数据记的是被引用图的 **asset GUID**。
            var graphs = AssetDatabase.FindAssets("t:NodeGraphAsset")
                .Select(g => (guid: g, path: AssetDatabase.GUIDToAssetPath(g)))
                .Select(t => (t.guid, t.path, asset: AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(t.path)))
                .Where(t => t.asset != null)
                .ToList();

            var idByGuid = new Dictionary<string, string>();
            foreach (var (guid, _, asset) in graphs)
            {
                if (string.IsNullOrEmpty(asset.graphId)) { asset.graphId = guid; EditorUtility.SetDirty(asset); }
                idByGuid[guid] = asset.graphId;
            }

            // 第二步：逐图从磁盘 YAML 里捞旧引用，写成 graphRefs。
            int migrated = 0, skipped = 0;
            foreach (var (_, path, asset) in graphs)
            {
                if (!File.Exists(path)) continue;
                var text = File.ReadAllText(path);
                if (!text.Contains("objectOverrides")) continue;

                foreach (Match block in BlockRx.Matches(text))
                {
                    var instanceId = block.Groups["id"].Value.Trim();
                    var body = block.Groups["body"].Value;
                    var oo = body.IndexOf("objectOverrides", System.StringComparison.Ordinal);
                    if (oo < 0) continue;
                    var segment = body.Substring(oo);

                    var inst = asset.instances.FirstOrDefault(i => i.instanceId == instanceId);
                    if (inst == null) continue;

                    foreach (Match e in EntryRx.Matches(segment))
                    {
                        var param = e.Groups["param"].Value.Trim();
                        var targetGuid = e.Groups["guid"].Value;
                        if (inst.graphRefs.Any(r => r.paramName == param)) continue;      // 幂等
                        if (!idByGuid.TryGetValue(targetGuid, out var graphId))
                        {
                            Debug.LogWarning($"[Migrate] {path}: 节点 {instanceId} 的参数 '{param}' 指向的图" +
                                             $"（guid {targetGuid}）不是 NodeGraphAsset 或已不存在，跳过。");
                            skipped++;
                            continue;
                        }
                        inst.graphRefs.Add(new GraphRef { paramName = param, graphId = graphId });
                        EditorUtility.SetDirty(asset);
                        migrated++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            GraphRefs.Invalidate();
            Debug.Log($"[Migrate] 子图引用迁移完成：转换 {migrated} 处，跳过 {skipped} 处，" +
                      $"播种 graphId 的图 {graphs.Count} 张。");
        }

        // 把某个 Player 用到的子图收集到它的 subGraphs 数组里（运行时构建需要显式引用）。
        // 这里只做框架能做的部分：解析选中对象上所有 NodeGraphAsset 字段所引出的子图闭包。
        [MenuItem("NodeEditor/Collect Sub Graphs")]
        public static void CollectSubGraphs()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogWarning("[Collect] 请先在场景里选中带 Player 组件的对象。"); return; }

            foreach (var comp in go.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var rootProp = so.FindProperty("graph");
                var listProp = so.FindProperty("subGraphs");
                if (rootProp == null || listProp == null) continue;

                var root = rootProp.objectReferenceValue as NodeGraphAsset;
                if (root == null) continue;

                var closure = new List<NodeGraphAsset>();
                Collect(root, closure, new HashSet<string>());

                listProp.arraySize = closure.Count;
                for (int i = 0; i < closure.Count; i++)
                    listProp.GetArrayElementAtIndex(i).objectReferenceValue = closure[i];
                so.ApplyModifiedProperties();
                Debug.Log($"[Collect] {comp.GetType().Name}: 收集到 {closure.Count} 张子图。");
            }
        }

        static void Collect(NodeGraphAsset g, List<NodeGraphAsset> into, HashSet<string> seen)
        {
            if (g == null || !seen.Add(GraphRefs.EnsureGraphId(g))) return;
            into.Add(g);
            foreach (var inst in g.instances)
                foreach (var r in inst.graphRefs)
                    Collect(GraphRefs.ByGraphId(r.graphId), into, seen);
        }
    }
}
