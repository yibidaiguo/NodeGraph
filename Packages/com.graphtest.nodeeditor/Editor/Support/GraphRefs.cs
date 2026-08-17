// GraphRefs.cs —— 编辑期把 NodeInstance.graphRefs 的 graphId 解析回 NodeGraphAsset。NodeEditor.Editor 程序集。
//
// 运行期由 IGraphSource（Unity 侧是 NodeGraphSource，需显式挂接）负责；编辑期不同——
// 这里有 AssetDatabase，可以按 graphId 全库搜，所以创作时不需要预先登记子图，
// 体验与 0.0.x 的直连对象引用一致（右键选图即可，不必先注册）。
//
// 迁移：0.0.x 里取子图引用的写法 —— ParamResolver 的 ResolveObject 加 as NodeGraphAsset ——
// 在编辑器侧一律换成 GraphRefs.Resolve(inst, "subGraph")。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace NodeEditor.EditorUI
{
    public static class GraphRefs
    {
        // graphId -> asset 的编辑期缓存。校验/视图会在一次重绘里对同一批子图反复解析，
        // 每次都 FindAssets 会很慢。资产变更由 AssetPostprocessor 清空（见下）。
        static Dictionary<string, NodeGraphAsset> s_ById;

        public static NodeGraphAsset Resolve(NodeInstance inst, string paramName)
        {
            var id = ParamResolver.ResolveGraphRef(inst, paramName);
            return string.IsNullOrEmpty(id) ? null : ByGraphId(id);
        }

        public static NodeGraphAsset ByGraphId(string graphId)
        {
            if (string.IsNullOrEmpty(graphId)) return null;
            BuildIndex();
            return s_ById.TryGetValue(graphId, out var g) && g != null ? g : null;
        }

        // 把某个图参数指向某张图（Inspector 的图选择器写入）。传 null 清除该引用。
        public static void Set(NodeInstance inst, string paramName, NodeGraphAsset graph)
        {
            if (inst == null || string.IsNullOrEmpty(paramName)) return;
            var existing = inst.graphRefs.FirstOrDefault(r => r.paramName == paramName);
            if (graph == null)
            {
                if (existing != null) inst.graphRefs.Remove(existing);
                return;
            }
            var id = EnsureGraphId(graph);
            if (existing != null) existing.graphId = id;
            else inst.graphRefs.Add(new GraphRef { paramName = paramName, graphId = id });
        }

        // 保证一张图有稳定 graphId：为空则播种为该 asset 的 GUID（跨机器/跨会话稳定），并落盘。
        public static string EnsureGraphId(NodeGraphAsset graph)
        {
            if (graph == null) return null;
            if (!string.IsNullOrEmpty(graph.graphId)) return graph.graphId;
            var path = AssetDatabase.GetAssetPath(graph);
            var guid = string.IsNullOrEmpty(path) ? System.Guid.NewGuid().ToString("N") : AssetDatabase.AssetPathToGUID(path);
            graph.graphId = guid;
            EditorUtility.SetDirty(graph);
            Invalidate();
            return guid;
        }

        public static void Invalidate() => s_ById = null;

        static void BuildIndex()
        {
            if (s_ById != null) return;
            s_ById = new Dictionary<string, NodeGraphAsset>();
            var ambiguous = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:NodeGraphAsset"))
            {
                var g = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (g == null) continue;
                // 查找必须只读。旧图以 asset GUID 作为有效 fallback；真正持久化只由
                // EnsureGraphId 或创作事务显式执行，Validate/目录查询绝不顺带 SetDirty。
                string effectiveId = string.IsNullOrEmpty(g.graphId) ? guid : g.graphId;
                if (ambiguous.Contains(effectiveId)) continue;
                if (s_ById.ContainsKey(effectiveId))
                {
                    s_ById.Remove(effectiveId);
                    ambiguous.Add(effectiveId);
                }
                else s_ById.Add(effectiveId, g);
            }
        }

        // 图资产增删改后让缓存失效，避免解析到已删除的图或漏掉新建的图。
        class Watcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (imported.Length > 0 || deleted.Length > 0 || moved.Length > 0) Invalidate();
            }
        }
    }
}
