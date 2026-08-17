using System.Collections.Generic;

namespace NodeEditor.EditorUI
{
    public sealed class GraphAuthoringReadResult
    {
        internal GraphAuthoringReadResult(GraphAuthoringDocument document, IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        { Document = document; Diagnostics = diagnostics; }
        public GraphAuthoringDocument Document { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Document != null && Diagnostics.Count == 0;
    }

    public sealed class GraphAuthoringWriteResult
    {
        internal GraphAuthoringWriteResult(GraphAuthoringDocument document, IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        { Document = document; Diagnostics = diagnostics; }
        public GraphAuthoringDocument Document { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Document != null && Diagnostics.Count == 0;
    }

    public sealed class GraphAuthoringValidationResult
    {
        internal GraphAuthoringValidationResult(IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        { Diagnostics = diagnostics; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Diagnostics.Count == 0;
    }

    public sealed class GraphAuthoringCatalogResult
    {
        internal GraphAuthoringCatalogResult(GraphAuthoringCatalog catalog, IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        { Catalog = catalog; Diagnostics = diagnostics; }
        public GraphAuthoringCatalog Catalog { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Catalog != null && Diagnostics.Count == 0;
    }

    public sealed class GraphAuthoringListResult
    {
        internal GraphAuthoringListResult(IReadOnlyList<GraphAuthoringGraphInfo> graphs, IReadOnlyList<GraphAuthoringDiagnostic> diagnostics)
        { Graphs = graphs; Diagnostics = diagnostics; }
        public IReadOnlyList<GraphAuthoringGraphInfo> Graphs { get; }
        public IReadOnlyList<GraphAuthoringDiagnostic> Diagnostics { get; }
        public bool Succeeded => Graphs != null && Diagnostics.Count == 0;
    }

    public sealed class GraphAuthoringGraphInfo
    {
        internal GraphAuthoringGraphInfo(string assetPath, string assetGuid, NodeGraphAsset graph)
        {
            AssetPath = assetPath;
            AssetGuid = assetGuid;
            GraphId = string.IsNullOrEmpty(graph.graphId) ? assetGuid : graph.graphId;
            Module = graph.module;
            Group = graph.group;
            GraphType = graph.graphType;
        }

        public string AssetPath { get; }
        public string AssetGuid { get; }
        public string GraphId { get; }
        public string Module { get; }
        public string Group { get; }
        public GraphType GraphType { get; }
    }
}
