namespace NodeEditor.EditorUI
{
    // 唯一公共门面。所有人工/AI 创作最终都读写 NodeGraphAsset 与 BlackboardAsset 本身。
    public static class GraphAuthoringAssetAccess
    {
        public static GraphAuthoringReadResult Read(string assetPath) => GraphAuthoringAssetReader.Read(assetPath);
        public static GraphAuthoringReadResult CreateDraft(
            string assetPath,
            string module,
            string group,
            GraphType graphType) =>
            GraphAuthoringAssetReader.CreateDraft(assetPath, module, group, graphType);
        public static GraphAuthoringWriteResult Write(string assetPath, GraphAuthoringDocument document) =>
            GraphAuthoringAssetWriter.Write(assetPath, document);
        public static GraphAuthoringValidationResult Validate(string assetPath, GraphAuthoringDocument document) =>
            GraphAuthoringAssetWriter.Validate(assetPath, document);
        public static GraphAuthoringCatalogResult Describe(string module) => GraphAuthoringAssetQuery.Describe(module);
        public static GraphAuthoringListResult List(string module) => GraphAuthoringAssetQuery.List(module);
    }
}
