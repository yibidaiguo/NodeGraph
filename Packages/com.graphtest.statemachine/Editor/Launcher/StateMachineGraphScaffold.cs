// StateMachineGraphScaffold.cs — 状态机领域层：新建状态机图时的「播种」策略。
// 框架（GraphListPane）按 module 分派初始化：造出裸 NodeGraphAsset、盖好 module 后，按该 module 查注册表
// 调对应领域的初始化器。本类在 [InitializeOnLoad] 把状态机的策略注册到 module="statemachine"：
// 每张新图固定带一个钉住(pinned)的入口节点(Entry)——状态机唯一入口，其出边指向初始状态；并把它登记进
// entryInstanceIds（控制流入口 + CheckReachability 播种源）。框架提供机制（pinned + 按模块注册），
// 这里只定策略（状态机用 Entry）。照 DialogueGraphScaffold 成例。仅 Editor/ 程序集。
// 另把本域的资产落点（图目录 / 黑板目录）注册给框架，目录读 StateMachineAssetPaths SO（准则#14）。

using UnityEditor;
using UnityEngine;
using NodeEditor;
using NodeEditor.EditorUI;   // GraphListPane（按模块注册）+ NodeDefinitionLocator

namespace StateMachine.EditorUI
{
    [InitializeOnLoad]
    public static class StateMachineGraphScaffold
    {
        public const string Module = "statemachine";

        static StateMachineGraphScaffold()
        {
            GraphListPane.RegisterModuleInitializer(Module, g => Seed(g));
            GraphListPane.RegisterModuleAssetFolders(
                Module,
                () => StateMachineAssetPathsLocator.FindOrCreate()?.machineGroupsDir,
                () => StateMachineAssetPathsLocator.FindOrCreate()?.blackboardLayersDir);
        }

        // 给一张新的（裸）状态机图播种：控制流类型 + 钉住的 Entry(唯一入口)。不预置状态——
        // 状态与转移由设计师自己编排。返回是否真的加了节点（定义资产缺失时为 false）。
        // 注：主路径 GraphListPane 的 initializer 是 Action、不消费该 bool；返回值留给将来的
        // 兜底 AssetPostprocessor（若需要照 Dialogue 的 AutoSeed 补）使用。
        public static bool Seed(NodeGraphAsset g)
        {
            if (g == null) return false;
            var def = NodeDefinitionLocator.ForType(typeof(EntryNode));
            if (def == null) return false;   // 定义资产还没生成（未在 NodeGraph Manager 中运行 Setup Assets）→ 不动

            var entry = new NodeInstance { definitionId = def.Id, position = new Vector2(0, 0), pinned = true };
            g.graphType = GraphType.ControlFlow;
            g.module = Module;                           // 归入左侧「状态机」分组 / 状态机编辑器的模块过滤
            g.instances.Add(entry);
            g.entryInstanceIds.Add(entry.instanceId);    // Entry 是控制流入口（也是可达性播种源）
            return true;
        }
    }
}
