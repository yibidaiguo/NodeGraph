using UnityEngine;

namespace NodeEditor
{
    public class NodeEditorAssetPaths : ScriptableObject
    {
        public string nodeDefinitionsRootDir = "Assets/NodeGraph/NodeEditor/Nodes/Definitions";
        public string registryPath = "Assets/NodeGraph/NodeEditor/Nodes/NodeRegistry.asset";
        public string globalBlackboardPath = "Assets/NodeGraph/NodeEditor/Blackboards/GlobalBlackboard.asset";
        public string localizationTablePath = "Assets/NodeGraph/NodeEditor/Localization/LocalizationTable.asset";
        public string editorLocalizationConfigPath = "Assets/NodeGraph/NodeEditor/Config/EditorLocalizationConfig.asset";
        public string runtimeLocalizationConfigPath = "Assets/NodeGraph/NodeEditor/Config/RuntimeLocalizationConfig.asset";
        public string languageOptionsPath = "Assets/NodeGraph/NodeEditor/Config/LanguageOptions.asset";
    }
}
