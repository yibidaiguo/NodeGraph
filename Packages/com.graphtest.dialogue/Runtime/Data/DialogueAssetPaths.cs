using UnityEngine;

namespace Dialogue
{
    public class DialogueAssetPaths : ScriptableObject
    {
        // Dialogue-owned generated assets. The locator only seeds defaults; users may edit these paths in the SO.
        public string nodeDefinitionsDir = "Assets/DialogueContent/Nodes/Definitions";
        public string dialogueGroupsDir = "Assets/DialogueContent/Dialogues";
        public string blackboardLayersDir = "Assets/DialogueContent/Blackboards";

        [Tooltip("Database used by Dialogue editor data sources and parameter choices. " +
                 "Leave empty only when the project contains exactly one DialogueDatabase.")]
        public DialogueDatabase authoringDatabase;
    }
}
