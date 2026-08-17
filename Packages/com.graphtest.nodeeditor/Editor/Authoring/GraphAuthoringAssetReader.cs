using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace NodeEditor.EditorUI
{
    internal static class GraphAuthoringAssetReader
    {
        public static GraphAuthoringReadResult Read(string assetPath)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            string path = GraphAuthoringAssetEnvironment.NormalizeExplicitPath(assetPath, "$assetPath", diagnostics);
            if (path == null) return Failure(diagnostics);

            var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
            if (graph == null)
            {
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph.missing", "$assetPath",
                    $"路径 '{path}' 没有 NodeGraphAsset。");
                return Failure(diagnostics);
            }

            var units = GraphAuthoringAssetEnvironment.Units(graph.module, diagnostics);
            var blackboards = GraphAuthoringAssetEnvironment.EffectiveBlackboards(graph.module, graph.group, diagnostics);
            if (units == null || diagnostics.Count != 0) return Failure(diagnostics);

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.guid.missing", "$assetPath", "图资产没有有效 GUID。");
                return Failure(diagnostics);
            }

            string effectiveGraphId = string.IsNullOrEmpty(graph.graphId) ? guid : graph.graphId;
            var source = graph.ToData();
            var readView = new GraphData
            {
                graphId = effectiveGraphId,
                module = source.module,
                group = source.group,
                graphType = source.graphType,
                orientation = source.orientation,
                instances = source.instances,
                entryInstanceIds = source.entryInstanceIds
            };
            var exported = GraphAuthoringCodec.Export(readView, units);
            diagnostics.AddRange(exported.Diagnostics);
            if (!exported.Succeeded) return Failure(diagnostics);

            return AttachOwners(
                exported.Document,
                path,
                guid,
                GraphAuthoringExpectedState.Exists,
                blackboards,
                diagnostics);
        }

        public static GraphAuthoringReadResult CreateDraft(
            string assetPath,
            string module,
            string group,
            GraphType graphType)
        {
            var diagnostics = new List<GraphAuthoringDiagnostic>();
            string path = GraphAuthoringAssetEnvironment.NormalizeExplicitPath(assetPath, "$assetPath", diagnostics);
            if (path == null) return Failure(diagnostics);
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.draft.target-exists", "$assetPath",
                    $"路径 '{path}' 已存在资产；请使用 Read 编辑现有图。");
            if (!GraphAuthoringAssetEnvironment.IsUnderRegisteredRoot(module, path))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph.root.invalid", "$assetPath",
                    "新图草稿只能位于其已注册模块声明的 graph root 下。");

            var units = GraphAuthoringAssetEnvironment.Units(module, diagnostics);
            var blackboards = GraphAuthoringAssetEnvironment.EffectiveBlackboards(module, group, diagnostics);
            if (units == null || diagnostics.Count != 0) return Failure(diagnostics);

            var exported = GraphAuthoringCodec.Export(new GraphData
            {
                graphId = string.Empty,
                module = module,
                group = group,
                graphType = graphType,
                orientation = GraphOrientation.Inherit
            }, units);
            diagnostics.AddRange(exported.Diagnostics);
            if (!exported.Succeeded) return Failure(diagnostics);

            return AttachOwners(
                exported.Document,
                path,
                string.Empty,
                GraphAuthoringExpectedState.MustNotExist,
                blackboards,
                diagnostics);
        }

        static GraphAuthoringReadResult AttachOwners(
            GraphAuthoringDocument document,
            string graphPath,
            string graphGuid,
            GraphAuthoringExpectedState graphState,
            IReadOnlyList<BlackboardAsset> blackboards,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var owners = blackboards.Select(asset => new GraphAuthoringBlackboardOwner(
                AssetDatabase.GetAssetPath(asset), asset));
            var blackboardExport = GraphAuthoringBlackboardCodec.Export(owners);
            diagnostics.AddRange(blackboardExport.Diagnostics);
            if (!blackboardExport.Succeeded) return Failure(diagnostics);

            document.blackboards = blackboardExport.Layers.ToList();
            document.revisionVector = new GraphAuthoringRevisionVector();
            document.revisionVector.owners.Add(new GraphAuthoringRevisionOwner
            {
                ownerId = graphGuid,
                ownerPath = graphPath,
                contentHash = graphState == GraphAuthoringExpectedState.Exists
                    ? GraphAuthoringSemanticHash.Graph(document)
                    : string.Empty,
                expectedState = graphState
            });
            foreach (var layer in document.blackboards)
            {
                string ownerGuid = AssetDatabase.AssetPathToGUID(layer.ownerPath);
                if (string.IsNullOrEmpty(ownerGuid))
                {
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.guid.missing", layer.ownerPath,
                        "黑板资产没有有效 GUID。");
                    return Failure(diagnostics);
                }
                document.revisionVector.owners.Add(new GraphAuthoringRevisionOwner
                {
                    ownerId = ownerGuid,
                    ownerPath = layer.ownerPath,
                    contentHash = GraphAuthoringSemanticHash.Blackboard(layer),
                    expectedState = GraphAuthoringExpectedState.Exists
                });
            }
            document.revisionVector.owners = document.revisionVector.owners
                .OrderBy(owner => owner.ownerId, StringComparer.Ordinal)
                .ThenBy(owner => owner.ownerPath, StringComparer.Ordinal)
                .ToList();
            return new GraphAuthoringReadResult(document, Array.AsReadOnly(diagnostics.ToArray()));
        }

        static GraphAuthoringReadResult Failure(List<GraphAuthoringDiagnostic> diagnostics) =>
            new(null, Array.AsReadOnly(diagnostics.ToArray()));
    }
}
