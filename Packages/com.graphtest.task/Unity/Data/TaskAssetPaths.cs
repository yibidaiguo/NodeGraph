using UnityEngine;

namespace TaskEditor
{
    public class TaskAssetPaths : ScriptableObject
    {
        public string nodeDefinitionsDir = "Assets/NodeGraph/Task/Nodes/Definitions";
        public string taskGraphsDir = "Assets/NodeGraph/Task/Tasks";
        public string stepGraphsDir = "Assets/NodeGraph/Task/Steps";
        public string blackboardLayersDir = "Assets/NodeGraph/Task/Blackboards";
    }
}
