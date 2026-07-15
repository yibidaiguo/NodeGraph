using UnityEngine;

namespace NodeEditor
{
    public class NodeEditorAssetPaths : ScriptableObject
    {
        public string nodeDefinitionsRootDir = "Assets/NodeEditorContent/Nodes/Definitions";
        public string registryPath = "Assets/NodeEditorContent/Nodes/NodeRegistry.asset";
        public string globalBlackboardPath = "Assets/NodeEditorContent/Blackboards/GlobalBlackboard.asset";
        public string localizationTablePath = "Assets/NodeEditorContent/Localization/LocalizationTable.asset";
        public string editorLocalizationConfigPath = "Assets/NodeEditorContent/Config/EditorLocalizationConfig.asset";
        public string runtimeLocalizationConfigPath = "Assets/NodeEditorContent/Config/RuntimeLocalizationConfig.asset";
        public string languageOptionsPath = "Assets/NodeEditorContent/Config/LanguageOptions.asset";
    }
}
