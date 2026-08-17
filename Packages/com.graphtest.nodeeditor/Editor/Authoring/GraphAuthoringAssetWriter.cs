using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    internal static class GraphAuthoringAssetWriter
    {
        internal sealed class Plan
        {
            public string GraphPath;
            public NodeGraphAsset ExistingGraph;
            public GraphData Draft;
            public UnitAuthoringCatalog Units;
            public IReadOnlyList<GraphAuthoringBlackboardOwner> BlackboardDrafts;
            public Dictionary<string, BlackboardAsset> ExistingBlackboards = new(StringComparer.Ordinal);
            public Dictionary<string, string> OriginalHashes = new(StringComparer.Ordinal);
            public Dictionary<string, string> DesiredBlackboardHashes = new(StringComparer.Ordinal);
            public GraphData OriginalGraph;
            public Dictionary<string, IBlackboardDecl> OriginalBlackboards = new(StringComparer.Ordinal);
            public bool ExistingGraphMutationStarted;
            public HashSet<string> ExistingBlackboardMutationsStarted = new(StringComparer.Ordinal);
        }

        sealed class CreatedAsset
        {
            public CreatedAsset(string path, string guid) { Path = path; Guid = guid; }
            public string Path { get; }
            public string Guid { get; }
        }

        sealed class CreatedFolder
        {
            public CreatedFolder(string path, string guid) { Path = path; Guid = guid; }
            public string Path { get; }
            public string Guid { get; }
        }

        public static GraphAuthoringValidationResult Validate(string assetPath, GraphAuthoringDocument document)
        {
            Prepare(assetPath, document, out _, out var diagnostics);
            return new GraphAuthoringValidationResult(Array.AsReadOnly(diagnostics.ToArray()));
        }

        public static GraphAuthoringWriteResult Write(string assetPath, GraphAuthoringDocument document)
        {
            if (!Prepare(assetPath, document, out var plan, out var diagnostics))
                return Failure(diagnostics);

            int undoGroup = -1;
            var createdAssets = new Dictionary<string, CreatedAsset>(StringComparer.Ordinal);
            var createdFolders = new List<CreatedFolder>();
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply Graph Authoring Document");

                TrackAndCreateFolders(plan.GraphPath, createdFolders);
                foreach (var owner in plan.BlackboardDrafts)
                    TrackAndCreateFolders(owner.OwnerPath, createdFolders);

                NodeGraphAsset graph;
                if (plan.ExistingGraph != null)
                {
                    graph = plan.ExistingGraph;
                    Undo.RegisterCompleteObjectUndo(graph, "Write Node Graph");
                    plan.ExistingGraphMutationStarted = true;
                }
                else
                {
                    graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
                    graph.name = Path.GetFileNameWithoutExtension(plan.GraphPath);
                    AssetDatabase.CreateAsset(graph, plan.GraphPath);
                    string guid = AssetDatabase.AssetPathToGUID(plan.GraphPath);
                    createdAssets.Add(plan.GraphPath, new CreatedAsset(plan.GraphPath, guid));
                    if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("新图资产未获得 GUID。");
                    Undo.RegisterCreatedObjectUndo(graph, "Create Node Graph");
                    plan.Draft.graphId = guid;
                }
                graph.FromData(plan.Draft);
                EditorUtility.SetDirty(graph);
                GraphAuthoringAssetWriterTestHooks.Checkpoint(
                    GraphAuthoringAssetWriterTestHooks.AfterGraphMutation);

                foreach (var owner in plan.BlackboardDrafts)
                {
                    if (plan.ExistingBlackboards.TryGetValue(owner.OwnerPath, out var existing))
                    {
                        if (plan.OriginalHashes.TryGetValue(owner.OwnerPath, out var originalHash) &&
                            plan.DesiredBlackboardHashes.TryGetValue(owner.OwnerPath, out var desiredHash) &&
                            string.Equals(originalHash, desiredHash, StringComparison.Ordinal))
                            continue;
                        Undo.RegisterCompleteObjectUndo(existing, "Write Blackboard");
                        plan.ExistingBlackboardMutationsStarted.Add(owner.OwnerPath);
                        existing.FromData(owner.Data);
                        EditorUtility.SetDirty(existing);
                        GraphAuthoringAssetWriterTestHooks.Checkpoint(
                            GraphAuthoringAssetWriterTestHooks.AfterBlackboardMutation);
                    }
                    else
                    {
                        var blackboard = ScriptableObject.CreateInstance<BlackboardAsset>();
                        blackboard.name = Path.GetFileNameWithoutExtension(owner.OwnerPath);
                        blackboard.FromData(owner.Data);
                        AssetDatabase.CreateAsset(blackboard, owner.OwnerPath);
                        string guid = AssetDatabase.AssetPathToGUID(owner.OwnerPath);
                        createdAssets.Add(owner.OwnerPath, new CreatedAsset(owner.OwnerPath, guid));
                        if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("新黑板资产未获得 GUID。");
                        Undo.RegisterCreatedObjectUndo(blackboard, "Create Blackboard");
                        EditorUtility.SetDirty(blackboard);
                        GraphAuthoringAssetWriterTestHooks.Checkpoint(
                            GraphAuthoringAssetWriterTestHooks.AfterBlackboardMutation);
                    }
                }

                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                var committed = GraphAuthoringAssetReader.Read(plan.GraphPath);
                if (!committed.Succeeded)
                    throw new InvalidOperationException("提交后无法重新读取完整创作快照：" +
                        string.Join("; ", committed.Diagnostics.Select(item => item.code + ": " + item.message)));
                return new GraphAuthoringWriteResult(committed.Document, committed.Diagnostics);
            }
            catch (Exception ex)
            {
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.write.failed", "$transaction", ex.Message);
                Rollback(plan, undoGroup, createdAssets, createdFolders, diagnostics);
                return Failure(diagnostics);
            }
        }

        static bool Prepare(
            string assetPath,
            GraphAuthoringDocument document,
            out Plan plan,
            out List<GraphAuthoringDiagnostic> diagnostics)
        {
            plan = null;
            diagnostics = new List<GraphAuthoringDiagnostic>();
            string path = GraphAuthoringAssetEnvironment.NormalizeExplicitPath(assetPath, "$assetPath", diagnostics);
            if (document == null)
            {
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.document.missing", "$", "创作文档不能为空。");
                return false;
            }
            if (path == null) return false;

            var existingGraph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
            var occupied = AssetDatabase.LoadMainAssetAtPath(path);
            if (occupied != null && existingGraph == null)
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.path.occupied", "$assetPath", "目标路径被非 NodeGraphAsset 资产占用。");
            if (existingGraph == null && !GraphAuthoringAssetEnvironment.IsUnderRegisteredRoot(document.module, path))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph.root.invalid", "$assetPath",
                    "新图只能创建在其已注册模块声明的 graph root 下。");

            var units = GraphAuthoringAssetEnvironment.Units(document.module, diagnostics);
            var imported = units == null ? null : GraphAuthoringCodec.Import(document, units);
            if (imported != null) diagnostics.AddRange(imported.Diagnostics);
            var blackboards = GraphAuthoringBlackboardCodec.Import(document);
            diagnostics.AddRange(blackboards.Diagnostics);
            ValidateOwnerPaths(path, blackboards.Owners, diagnostics);
            var registry = GraphAuthoringAssetEnvironment.Registry(diagnostics);

            GraphAuthoringCatalog catalog = null;
            if (blackboards.Succeeded)
                catalog = GraphAuthoringAssetEnvironment.Catalog(
                    document.module, registry, units, blackboards.Owners.Select(owner => owner.Data), diagnostics);
            if (catalog != null)
                diagnostics.AddRange(GraphAuthoringSemanticValidator.Validate(document, catalog));

            var effective = GraphAuthoringAssetEnvironment.EffectiveBlackboards(document.module, document.group, diagnostics);
            ValidateBlackboardClosure(document, effective, diagnostics);
            ValidateIdentity(existingGraph, path, document, units, diagnostics);
            ValidateGraphReferences(document, existingGraph, path, diagnostics);

            var candidate = new Plan
            {
                GraphPath = path,
                ExistingGraph = existingGraph,
                Draft = imported?.Data,
                Units = units,
                BlackboardDrafts = blackboards.Succeeded ? blackboards.Owners : Array.Empty<GraphAuthoringBlackboardOwner>()
            };
            foreach (var layer in document.blackboards ?? new List<GraphAuthoringBlackboardLayer>())
                if (layer != null && !string.IsNullOrEmpty(layer.ownerPath))
                    candidate.DesiredBlackboardHashes[layer.ownerPath] = GraphAuthoringSemanticHash.Blackboard(layer);
            ValidateRevisions(candidate, document, diagnostics);
            ValidateTransient(candidate, registry, diagnostics);

            if (diagnostics.Count != 0) return false;
            plan = candidate;
            return true;
        }

        static void ValidateOwnerPaths(
            string graphPath,
            IReadOnlyList<GraphAuthoringBlackboardOwner> blackboards,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (blackboards == null) return;
            for (int i = 0; i < blackboards.Count; i++)
            {
                var owner = blackboards[i];
                if (owner != null && string.Equals(owner.OwnerPath, graphPath, StringComparison.Ordinal))
                    GraphAuthoringAssetEnvironment.Add(
                        diagnostics,
                        "asset.owner-path.collision",
                        $"$.blackboards[{i}].ownerPath",
                        $"图和黑板 owner 不得共享资产路径 '{graphPath}'。");
            }
        }

        static void ValidateBlackboardClosure(
            GraphAuthoringDocument document,
            IReadOnlyList<BlackboardAsset> effective,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var listed = new HashSet<string>(
                (document.blackboards ?? new List<GraphAuthoringBlackboardLayer>())
                    .Where(layer => layer != null)
                    .Select(layer => layer.ownerPath),
                StringComparer.Ordinal);
            foreach (var asset in effective)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (!listed.Contains(path))
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.blackboard.closure.missing", "$.blackboards",
                        $"完整快照缺少当前有效黑板 owner '{path}'。");
            }
        }

        static void ValidateIdentity(
            NodeGraphAsset existing,
            string path,
            GraphAuthoringDocument document,
            UnitAuthoringCatalog units,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (existing == null)
            {
                if (!string.IsNullOrEmpty(document.graphId))
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph-id.new-not-empty", "$.graphId",
                        "新图的 graphId 必须为空；提交时将采用新资产 meta GUID。");
                return;
            }
            if (units == null) return;
            string guid = AssetDatabase.AssetPathToGUID(path);
            string graphId = string.IsNullOrEmpty(existing.graphId) ? guid : existing.graphId;
            if (!string.Equals(document.module, existing.module, StringComparison.Ordinal) ||
                !string.Equals(document.group, existing.group, StringComparison.Ordinal))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph-scope.changed", "$.module",
                    "现有图的 module/group 作用域不可由创作事务静默迁移。");
            if (!string.Equals(document.graphId, graphId, StringComparison.Ordinal))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph-id.changed", "$.graphId", "现有图的稳定 graphId 不可改写。");

            var view = existing.ToData();
            var source = new GraphData
            {
                graphId = graphId, module = view.module, group = view.group,
                graphType = view.graphType, orientation = view.orientation,
                instances = view.instances, entryInstanceIds = view.entryInstanceIds
            };
            var exported = GraphAuthoringCodec.Export(source, units);
            if (!exported.Succeeded) { diagnostics.AddRange(exported.Diagnostics); return; }
            var oldByKey = exported.Document.nodes.ToDictionary(node => node.authoringKey, node => node.instanceId, StringComparer.Ordinal);
            foreach (var node in document.nodes ?? new List<GraphAuthoringNode>())
            {
                if (node == null) continue;
                if (oldByKey.TryGetValue(node.authoringKey ?? string.Empty, out var oldId) && oldId != node.instanceId)
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.node-identity.key-rebound", "$.nodes", "既有 authoringKey 不可绑定到不同 instanceId。");
            }
        }

        static void ValidateGraphReferences(
            GraphAuthoringDocument document,
            NodeGraphAsset existing,
            string path,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            var byId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:NodeGraphAsset"))
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(candidatePath);
                if (graph == null) continue;
                string id = string.IsNullOrEmpty(graph.graphId) ? guid : graph.graphId;
                if (byId.TryGetValue(id, out var other) && other != candidatePath)
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph-id.duplicate", "$.graphRefs",
                        $"graphId '{id}' 同时属于 '{other}' 与 '{candidatePath}'。");
                else byId[id] = candidatePath;
            }
            foreach (var node in document.nodes ?? new List<GraphAuthoringNode>())
                foreach (var reference in node?.graphRefs ?? new List<GraphAuthoringGraphRef>())
                    if (reference != null && !string.IsNullOrEmpty(reference.graphId) && !byId.ContainsKey(reference.graphId))
                        GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph-ref.unresolved", "$.nodes.graphRefs",
                            $"graphId '{reference.graphId}' 无法解析到唯一现有图资产。");
        }

        static void ValidateRevisions(Plan plan, GraphAuthoringDocument document, List<GraphAuthoringDiagnostic> diagnostics)
        {
            var owners = document.revisionVector?.owners;
            var expectedPaths = new HashSet<string>(StringComparer.Ordinal) { plan.GraphPath };
            foreach (var owner in plan.BlackboardDrafts) expectedPaths.Add(owner.OwnerPath);
            if (owners == null)
            {
                foreach (string expected in expectedPaths.OrderBy(value => value, StringComparer.Ordinal))
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-missing", expected,
                        $"revision vector 缺少 owner；expectedPath='{expected}'。");
                return;
            }
            var byPath = new Dictionary<string, GraphAuthoringRevisionOwner>(StringComparer.Ordinal);
            for (int i = 0; i < owners.Count; i++)
            {
                var owner = owners[i];
                if (owner == null)
                {
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-missing", $"$.revisionVector.owners[{i}]",
                        "revision owner 不能为空。");
                    continue;
                }
                if (string.IsNullOrEmpty(owner.ownerPath))
                {
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-path-invalid", $"$.revisionVector.owners[{i}].ownerPath",
                        "revision ownerPath 不能为空。");
                    continue;
                }
                if (!byPath.TryAdd(owner.ownerPath, owner))
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-duplicate", "$.revisionVector.owners", "revision ownerPath 重复。");
            }
            foreach (string missing in expectedPaths.Where(item => !byPath.ContainsKey(item)))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-missing", missing,
                    $"revision vector 缺少 owner；expectedPath='{missing}'。");
            foreach (string extra in byPath.Keys.Where(item => !expectedPaths.Contains(item)))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-path-changed", extra,
                    $"revision ownerPath 不属于当前完整快照；actualPath='{extra}'。");

            ValidateGraphRevision(plan, byPath.TryGetValue(plan.GraphPath, out var graphOwner) ? graphOwner : null, diagnostics);
            foreach (var owner in plan.BlackboardDrafts)
                ValidateBlackboardRevision(plan, owner, byPath.TryGetValue(owner.OwnerPath, out var revision) ? revision : null, diagnostics);
        }

        static void ValidateGraphRevision(Plan plan, GraphAuthoringRevisionOwner revision, List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (revision == null) return;
            if (plan.ExistingGraph == null)
            {
                ValidateMustNotExist(revision, plan.GraphPath, diagnostics);
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(plan.GraphPath);
            var exported = ExportLiveGraph(plan.ExistingGraph, plan.GraphPath, plan.Units, diagnostics);
            if (exported == null) return;
            var imported = GraphAuthoringCodec.Import(exported, plan.Units);
            diagnostics.AddRange(imported.Diagnostics);
            if (!imported.Succeeded) return;
            plan.OriginalGraph = imported.Data;
            string hash = GraphAuthoringSemanticHash.Graph(exported);
            plan.OriginalHashes[plan.GraphPath] = hash;
            CompareExistingRevision(revision, plan.GraphPath, guid, hash, "graph", diagnostics);
        }

        static void ValidateBlackboardRevision(
            Plan plan,
            GraphAuthoringBlackboardOwner owner,
            GraphAuthoringRevisionOwner revision,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (revision == null) return;
            var asset = AssetDatabase.LoadAssetAtPath<BlackboardAsset>(owner.OwnerPath);
            var occupied = AssetDatabase.LoadMainAssetAtPath(owner.OwnerPath);
            if (asset == null)
            {
                if (owner.Data.Module.Length == 0 && owner.Data.Group.Length == 0)
                {
                    string configured = ProjectAssetPaths.NormalizeAssetPath(
                        NodeEditorAssetPathsLocator.Find()?.globalBlackboardPath);
                    if (!string.Equals(configured, owner.OwnerPath, StringComparison.Ordinal))
                        GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.blackboard.global-path.changed", owner.OwnerPath,
                            $"新全局黑板必须使用配置路径；expectedPath='{configured}', actualPath='{owner.OwnerPath}'。");
                }
                if (occupied != null)
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-unexpected-exists", owner.OwnerPath,
                        $"owner 应不存在但路径已被占用；actualType='{occupied.GetType().FullName}'。");
                else ValidateMustNotExist(revision, owner.OwnerPath, diagnostics);
                return;
            }
            plan.ExistingBlackboards[owner.OwnerPath] = asset;
            if (!string.Equals(asset.Module, owner.Data.Module, StringComparison.Ordinal) ||
                !string.Equals(asset.Group, owner.Data.Group, StringComparison.Ordinal))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.blackboard.scope.changed", owner.OwnerPath,
                    "既有黑板 owner 的 module/group 身份不可通过图快照改写。");
            string guid = AssetDatabase.AssetPathToGUID(owner.OwnerPath);
            var exported = GraphAuthoringBlackboardCodec.Export(
                new[] { new GraphAuthoringBlackboardOwner(owner.OwnerPath, asset) });
            diagnostics.AddRange(exported.Diagnostics);
            if (!exported.Succeeded) return;
            var imported = GraphAuthoringBlackboardCodec.Import(exported.Layers);
            diagnostics.AddRange(imported.Diagnostics);
            if (!imported.Succeeded) return;
            plan.OriginalBlackboards[owner.OwnerPath] = imported.Owners[0].Data;
            string hash = GraphAuthoringSemanticHash.Blackboard(exported.Layers[0]);
            plan.OriginalHashes[owner.OwnerPath] = hash;
            CompareExistingRevision(revision, owner.OwnerPath, guid, hash, "blackboard", diagnostics);
        }

        static void ValidateMustNotExist(GraphAuthoringRevisionOwner revision, string path, List<GraphAuthoringDiagnostic> diagnostics)
        {
            var actual = AssetDatabase.LoadMainAssetAtPath(path);
            if (actual != null)
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-unexpected-exists", path,
                    $"owner 应不存在但实际存在；actualType='{actual.GetType().FullName}'。");
            if (revision.expectedState != GraphAuthoringExpectedState.MustNotExist)
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-missing", path,
                    $"owner 实际不存在但 revision 期望 Exists；expectedId='{revision.ownerId}', expectedHash='{revision.contentHash}'。");
            if (!string.IsNullOrEmpty(revision.ownerId))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-id-mismatch", path,
                    $"MustNotExist ownerId 必须为空；expected='', actual='{revision.ownerId}'。");
            if (!string.IsNullOrEmpty(revision.contentHash))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.content-changed", path,
                    $"MustNotExist contentHash 必须为空；expected='', actual='{revision.contentHash}'。");
        }

        static void CompareExistingRevision(
            GraphAuthoringRevisionOwner revision,
            string path,
            string actualId,
            string actualHash,
            string kind,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (revision.expectedState != GraphAuthoringExpectedState.Exists)
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-unexpected-exists", path,
                    $"owner 已存在但 revision 期望 MustNotExist；kind='{kind}', actualId='{actualId}', actualHash='{actualHash}'。");
            if (!string.Equals(revision.ownerId, actualId, StringComparison.Ordinal))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.owner-id-mismatch", path,
                    $"owner identity 已变化；kind='{kind}', expectedId='{revision.ownerId}', actualId='{actualId}'。");
            if (!string.Equals(revision.contentHash, actualHash, StringComparison.Ordinal))
                GraphAuthoringAssetEnvironment.Add(diagnostics, "revision.content-changed", path,
                    $"owner 内容已变化；kind='{kind}', expectedHash='{revision.contentHash}', actualHash='{actualHash}'。");
        }

        static void ValidateTransient(Plan plan, NodeRegistry registry, List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (plan.Draft == null || registry == null) return;
            var draft = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                draft.FromData(plan.Draft);
                var blackboard = new BlackboardSet(plan.BlackboardDrafts.Select(owner => owner.Data));
                foreach (var issue in GraphValidator.ValidateAll(draft, registry, blackboard)
                             .Where(issue => issue.severity == ValidationIssue.Sev.Error))
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.graph-validation.failed", issue.target, issue.message);
            }
            finally { UnityEngine.Object.DestroyImmediate(draft); }
        }

        static string LiveGraphHash(NodeGraphAsset graph, string path, UnitAuthoringCatalog units, List<GraphAuthoringDiagnostic> diagnostics)
        {
            var exported = ExportLiveGraph(graph, path, units, diagnostics);
            return exported == null ? null : GraphAuthoringSemanticHash.Graph(exported);
        }

        static GraphAuthoringDocument ExportLiveGraph(
            NodeGraphAsset graph,
            string path,
            UnitAuthoringCatalog units,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            var source = graph.ToData();
            var view = new GraphData
            {
                graphId = string.IsNullOrEmpty(graph.graphId) ? guid : graph.graphId,
                module = source.module, group = source.group, graphType = source.graphType, orientation = source.orientation,
                instances = source.instances, entryInstanceIds = source.entryInstanceIds
            };
            var exported = GraphAuthoringCodec.Export(view, units);
            if (!exported.Succeeded) { diagnostics.AddRange(exported.Diagnostics); return null; }
            return exported.Document;
        }

        static string LiveBlackboardHash(BlackboardAsset asset, string path, List<GraphAuthoringDiagnostic> diagnostics)
        {
            var exported = GraphAuthoringBlackboardCodec.Export(new[] { new GraphAuthoringBlackboardOwner(path, asset) });
            if (!exported.Succeeded) { diagnostics.AddRange(exported.Diagnostics); return null; }
            return GraphAuthoringSemanticHash.Blackboard(exported.Layers[0]);
        }

        static void Rollback(
            Plan plan,
            int undoGroup,
            IReadOnlyDictionary<string, CreatedAsset> createdAssets,
            IReadOnlyList<CreatedFolder> createdFolders,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            RollbackStage(
                () => DeleteCreatedAssets(createdAssets, diagnostics),
                "asset.rollback.asset-cleanup-failed",
                diagnostics);
            RollbackStage(
                () => { if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup); },
                "asset.rollback.undo-failed",
                diagnostics);
            RollbackStage(
                () => DeleteCreatedAssets(createdAssets, diagnostics),
                "asset.rollback.asset-cleanup-failed",
                diagnostics);
            RollbackStage(
                () => RestoreOriginalOwners(plan, diagnostics),
                "asset.rollback.restore-failed",
                diagnostics);
            RollbackStage(
                () => DeleteCreatedFolders(createdFolders, diagnostics),
                "asset.rollback.folder-cleanup-failed",
                diagnostics);
            RollbackStage(
                AssetDatabase.SaveAssets,
                "asset.rollback.save-failed",
                diagnostics);
            RollbackStage(
                () => AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport),
                "asset.rollback.refresh-failed",
                diagnostics);
            RollbackStage(
                () => DeleteCreatedAssets(createdAssets, diagnostics),
                "asset.rollback.asset-cleanup-failed",
                diagnostics);
            RollbackStage(
                () => DeleteCreatedFolders(createdFolders, diagnostics),
                "asset.rollback.folder-cleanup-failed",
                diagnostics);
            RollbackStage(
                () => AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport),
                "asset.rollback.refresh-failed",
                diagnostics);
            RollbackStage(
                () => VerifyRestored(plan, createdAssets.Values, createdFolders, diagnostics),
                "asset.rollback.verify-failed",
                diagnostics);
        }

        static void RollbackStage(Action action, string code, List<GraphAuthoringDiagnostic> diagnostics)
        {
            try { action(); }
            catch (Exception ex)
            {
                GraphAuthoringAssetEnvironment.Add(diagnostics, code, "$transaction", ex.Message);
            }
        }

        static void DeleteCreatedAssets(
            IReadOnlyDictionary<string, CreatedAsset> createdAssets,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            foreach (var item in createdAssets.Values)
            {
                try
                {
                    if (string.IsNullOrEmpty(item.Guid)) continue;
                    string currentGuid = AssetDatabase.AssetPathToGUID(item.Path);
                    if (string.Equals(currentGuid, item.Guid, StringComparison.Ordinal))
                        AssetDatabase.DeleteAsset(item.Path);
                }
                catch (Exception ex)
                {
                    GraphAuthoringAssetEnvironment.Add(
                        diagnostics, "asset.rollback.asset-cleanup-failed", item.Path, ex.Message);
                }
            }
        }

        static void DeleteCreatedFolders(
            IReadOnlyList<CreatedFolder> createdFolders,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            for (int i = createdFolders.Count - 1; i >= 0; i--)
            {
                var item = createdFolders[i];
                try
                {
                    if (string.IsNullOrEmpty(item.Guid) || !AssetDatabase.IsValidFolder(item.Path)) continue;
                    string currentGuid = AssetDatabase.AssetPathToGUID(item.Path);
                    if (!string.Equals(currentGuid, item.Guid, StringComparison.Ordinal)) continue;
                    if (AssetDatabase.FindAssets(string.Empty, new[] { item.Path }).Length == 0)
                        AssetDatabase.DeleteAsset(item.Path);
                }
                catch (Exception ex)
                {
                    GraphAuthoringAssetEnvironment.Add(
                        diagnostics, "asset.rollback.folder-cleanup-failed", item.Path, ex.Message);
                }
            }
        }

        static void RestoreOriginalOwners(Plan plan, List<GraphAuthoringDiagnostic> diagnostics)
        {
            if (plan.ExistingGraphMutationStarted && plan.ExistingGraph != null && plan.OriginalGraph != null)
            {
                try
                {
                    plan.ExistingGraph.FromData(plan.OriginalGraph);
                    EditorUtility.SetDirty(plan.ExistingGraph);
                }
                catch (Exception ex)
                {
                    GraphAuthoringAssetEnvironment.Add(
                        diagnostics, "asset.rollback.restore-failed", plan.GraphPath, ex.Message);
                }
            }
            foreach (var pair in plan.OriginalBlackboards)
            {
                if (!plan.ExistingBlackboardMutationsStarted.Contains(pair.Key)) continue;
                try
                {
                    if (plan.ExistingBlackboards.TryGetValue(pair.Key, out var asset) && asset != null)
                    {
                        asset.FromData(pair.Value);
                        EditorUtility.SetDirty(asset);
                    }
                }
                catch (Exception ex)
                {
                    GraphAuthoringAssetEnvironment.Add(
                        diagnostics, "asset.rollback.restore-failed", pair.Key, ex.Message);
                }
            }
        }

        static void VerifyRestored(
            Plan plan,
            IEnumerable<CreatedAsset> createdAssets,
            IReadOnlyList<CreatedFolder> createdFolders,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            foreach (var item in createdAssets)
                VerifyCreatedGuidAbsent(item.Path, item.Guid, "owner", diagnostics);
            foreach (var item in createdFolders)
                VerifyCreatedGuidAbsent(item.Path, item.Guid, "目录", diagnostics);
            foreach (var pair in plan.OriginalHashes)
            {
                try
                {
                    string actual;
                    if (pair.Key == plan.GraphPath)
                    {
                        var graph = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(pair.Key);
                        actual = graph == null ? null : LiveGraphHash(graph, pair.Key, plan.Units, diagnostics);
                    }
                    else
                    {
                        var blackboard = AssetDatabase.LoadAssetAtPath<BlackboardAsset>(pair.Key);
                        actual = blackboard == null ? null : LiveBlackboardHash(blackboard, pair.Key, diagnostics);
                    }
                    if (!string.Equals(actual, pair.Value, StringComparison.Ordinal))
                        GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.rollback.verification-failed", pair.Key,
                            "回滚后 owner 语义哈希未恢复，请停止后续写入并人工检查。");
                }
                catch (Exception ex)
                {
                    GraphAuthoringAssetEnvironment.Add(
                        diagnostics, "asset.rollback.verification-failed", pair.Key, ex.Message);
                }
            }
        }

        static void VerifyCreatedGuidAbsent(
            string path,
            string guid,
            string kind,
            List<GraphAuthoringDiagnostic> diagnostics)
        {
            try
            {
                if (string.IsNullOrEmpty(guid)) return;
                string mappedPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(mappedPath)) return;
                string fullPath = Path.GetFullPath(mappedPath);
                bool stillExists = AssetDatabase.LoadMainAssetAtPath(mappedPath) != null ||
                                   AssetDatabase.IsValidFolder(mappedPath) ||
                                   File.Exists(fullPath) || Directory.Exists(fullPath) ||
                                   File.Exists(fullPath + ".meta");
                if (stillExists)
                    GraphAuthoringAssetEnvironment.Add(diagnostics, "asset.rollback.verification-failed", path,
                        $"回滚后新{kind} GUID '{guid}' 仍解析到实时路径 '{mappedPath}'，请停止后续写入并人工检查。");
            }
            catch (Exception ex)
            {
                GraphAuthoringAssetEnvironment.Add(
                    diagnostics, "asset.rollback.verification-failed", path, ex.Message);
            }
        }

        static void TrackAndCreateFolders(string assetPath, List<CreatedFolder> createdFolders)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || folder == "Assets") return;
            var missing = new Stack<string>();
            string cursor = folder;
            while (cursor != "Assets" && !AssetDatabase.IsValidFolder(cursor))
            {
                missing.Push(cursor);
                cursor = Path.GetDirectoryName(cursor)?.Replace('\\', '/');
            }
            while (missing.Count != 0)
            {
                string path = missing.Pop();
                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                string name = Path.GetFileName(path);
                string guid = AssetDatabase.CreateFolder(parent, name);
                createdFolders.Add(new CreatedFolder(path, guid));
                if (string.IsNullOrEmpty(guid))
                    throw new InvalidOperationException($"新目录 '{path}' 未获得 GUID。");
            }
        }

        static GraphAuthoringWriteResult Failure(List<GraphAuthoringDiagnostic> diagnostics) =>
            new(null, Array.AsReadOnly(diagnostics.ToArray()));
    }
}
