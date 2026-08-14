using UnityEngine;

namespace TaskEditor
{
    public class TaskAssetPaths : ScriptableObject
    {
        public string nodeDefinitionsDir = "Assets/TaskContent/Nodes/Definitions";
        public string taskGraphsDir = "Assets/TaskContent/Tasks";
        public string stepGraphsDir = "Assets/TaskContent/Steps";
        public string blackboardLayersDir = "Assets/TaskContent/Blackboards";
    }
}
