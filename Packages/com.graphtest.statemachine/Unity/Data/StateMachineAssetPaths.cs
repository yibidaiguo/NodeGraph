// StateMachineAssetPaths.cs —— 状态机领域的路径配置 SO（一类一文件，硬规则 A1；准则#14/C16：
// 写死路径集中进可定位的配置 SO，生成器/启动器读它、不各持 const）。Runtime 程序集。

using UnityEngine;

namespace StateMachine
{
    public class StateMachineAssetPaths : ScriptableObject
    {
        // 状态机领域持有的生成资产落点。Locator 只播种默认值；用户可在检视面板里改这些路径。
        public string nodeDefinitionsDir = "Assets/StateMachineContent/Nodes/Definitions";
        public string machineGroupsDir = "Assets/StateMachineContent/Machines";
        public string blackboardLayersDir = "Assets/StateMachineContent/Blackboards";
    }
}
