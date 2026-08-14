// GraphAutoLayout.cs — 第 5 层（连线图编辑器），画布组织。
// 「整理」：把当前图按连接方向排成分层网格（沿流向分层，层内按父节点顺序排）。
// 手摆节点很快就会歪；旧外壳没有任何整理入口，图一乱只能一个个拖回去。
// 只改 NodeInstance.position（记 Undo + 标脏），不碰结构。Editor/ 程序集。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NodeEditor;          // 第 4 层数据类型（NodeGraphAsset、NodeInstance、Connection）

namespace NodeEditor.EditorUI
{
    public static class GraphAutoLayout
    {
        // 层间距 / 层内间距。取值来自真实节点尺寸的经验值：节点宽 ~168、两行 cue 的高 ~110，
        // 留出的空档要能塞下连线的贝塞尔弯，又不至于一屏放不下三层。
        const float LayerGap = 110f;
        const float SiblingGap = 34f;
        const float FallbackWidth = 180f;
        const float FallbackHeight = 96f;

        // 对整张图做一次分层布局。sizeOf 给出每个实例的实测尺寸（画布可传真实 NodeView 布局；
        // 为 null 时退回常量），这样宽节点不会互相压住。
        public static void Apply(NodeGraphAsset asset, GraphOrientation orientation,
                                 System.Func<NodeInstance, Vector2> sizeOf = null)
        {
            if (asset == null || asset.instances == null || asset.instances.Count == 0) return;

            var layers = ResolveLayers(asset);
            Undo.RegisterCompleteObjectUndo(asset, "Auto Layout");

            var byLayer = asset.instances
                .Where(i => i != null && layers.ContainsKey(i.instanceId))
                .GroupBy(i => layers[i.instanceId])
                .OrderBy(g => g.Key)
                .ToList();

            bool horizontal = orientation != GraphOrientation.Vertical;
            float cursor = 0f;   // 沿流向推进的位置（横图=x，竖图=y）

            foreach (var layer in byLayer)
            {
                var members = layer.ToList();
                var sizes = members.ToDictionary(m => m, m => Size(m, sizeOf));

                // 这一层在流向上占多厚（取本层最厚的那个），以及横切方向的总长度（用来居中）。
                float thickness = members.Count == 0 ? 0f
                    : members.Max(m => horizontal ? sizes[m].x : sizes[m].y);
                float span = members.Sum(m => horizontal ? sizes[m].y : sizes[m].x)
                             + SiblingGap * Mathf.Max(0, members.Count - 1);

                float cross = -span * 0.5f;   // 每层围绕主轴居中，图整体读起来是一条对称的干道
                foreach (var member in members)
                {
                    var size = sizes[member];
                    member.position = horizontal
                        ? new Vector2(cursor, cross).ToVec2()
                        : new Vector2(cross, cursor).ToVec2();
                    cross += (horizontal ? size.y : size.x) + SiblingGap;
                }
                cursor += thickness + LayerGap;
            }

            EditorUtility.SetDirty(asset);
        }

        static Vector2 Size(NodeInstance instance, System.Func<NodeInstance, Vector2> sizeOf)
        {
            var measured = sizeOf?.Invoke(instance) ?? Vector2.zero;
            return new Vector2(
                measured.x > 1f ? measured.x : FallbackWidth,
                measured.y > 1f ? measured.y : FallbackHeight);
        }

        // 分层：入口（显式 entry，没有则入度为 0 的节点）为第 0 层，其余取「所有前驱层号的最大值 + 1」
        // —— 用最长路径而非最短，父节点才不会排到子节点右边。环里的节点拓扑排不出来，
        // 兜底按已定层的前驱推一层，再排不出就归 0，保证每个实例都有位置、不会被丢在原地。
        public static Dictionary<string, int> ResolveLayers(NodeGraphAsset asset)
        {
            var instances = asset.instances.Where(i => i != null).ToList();
            var ids = new HashSet<string>(instances.Select(i => i.instanceId));
            var outgoing = new Dictionary<string, List<string>>();
            var indegree = instances.ToDictionary(i => i.instanceId, _ => 0);

            foreach (var instance in instances)
            {
                var targets = new List<string>();
                foreach (var connection in instance.connections ?? new List<Connection>())
                {
                    if (connection == null || !ids.Contains(connection.toInstanceId)) continue;
                    if (connection.toInstanceId == instance.instanceId) continue;   // 自环不参与分层
                    targets.Add(connection.toInstanceId);
                    indegree[connection.toInstanceId]++;
                }
                outgoing[instance.instanceId] = targets;
            }

            var layers = new Dictionary<string, int>();
            var queue = new Queue<string>();
            var explicitEntries = (asset.entryInstanceIds ?? new List<string>()).Where(ids.Contains).ToList();
            var roots = explicitEntries.Count > 0
                ? explicitEntries
                : instances.Where(i => indegree[i.instanceId] == 0).Select(i => i.instanceId).ToList();
            // 一个根都找不到（整图是一个环）：拿第一个实例当根，总比全部堆在原点强。
            if (roots.Count == 0 && instances.Count > 0) roots.Add(instances[0].instanceId);

            foreach (var root in roots)
            {
                if (layers.ContainsKey(root)) continue;
                layers[root] = 0;
                queue.Enqueue(root);
            }

            // 有界松弛：每个节点最多被重排 instances.Count 次，环不会把这里转成死循环。
            int budget = instances.Count * Mathf.Max(1, instances.Count);
            while (queue.Count > 0 && budget-- > 0)
            {
                var id = queue.Dequeue();
                foreach (var next in outgoing[id])
                {
                    int candidate = layers[id] + 1;
                    if (layers.TryGetValue(next, out var existing) && existing >= candidate) continue;
                    layers[next] = candidate;
                    queue.Enqueue(next);
                }
            }

            // 没被走到的（孤立子图 / 环内）：按已定层的前驱补一层，仍无前驱则归 0。
            foreach (var instance in instances)
            {
                if (layers.ContainsKey(instance.instanceId)) continue;
                int best = -1;
                foreach (var other in instances)
                {
                    if (!layers.TryGetValue(other.instanceId, out var otherLayer)) continue;
                    if (outgoing[other.instanceId].Contains(instance.instanceId)) best = Mathf.Max(best, otherLayer);
                }
                layers[instance.instanceId] = best + 1;
            }
            return layers;
        }
    }
}
