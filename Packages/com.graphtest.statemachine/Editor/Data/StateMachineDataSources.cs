// StateMachineDataSources.cs — 状态机领域的数据源注册（StateMachine.Editor 程序集，[InitializeOnLoad]）。
// 「框架留缝、领域填领域数据」：框架自注册黑板 / 本地化 / 图概览（见 FrameworkDataSources），
// 本类把状态机领域自己的数据接进同一个通用数据窗口（照 DialogueDataSources 逐行）：
//   · 状态机模块变量（领域档）—— 复用框架分层面板（缺该档则给「新建」按钮）；
//   · 状态机节点定义（领域档，只读）—— 列出生成的本域节点定义，供排查；它们由
//     Tools/NodeGraph/Manager 中的 State Machine / Setup Assets 生成，不在窗口里改。
// 仅 Editor/ 程序集 —— 本文件无运行时依赖。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using NodeEditor;
using NodeEditor.EditorUI;
// Unity 6 的 UI Toolkit 也有个 DataSourceContext（数据绑定用）；显式别名指向本框架的那个，消除歧义（API 参考 §5）。
using DataSourceContext = NodeEditor.EditorUI.DataSourceContext;

namespace StateMachine.EditorUI
{
    [InitializeOnLoad]
    public static class StateMachineDataSources
    {
        const string Domain = "statemachine";

        static StateMachineDataSources()
        {
            // 模块变量（领域级）：状态机模块（module=="statemachine"）那一档黑板。复用框架的分层面板。
            DataSourceRegistry.Register("statemachine.blackboard", ctx =>
            {
                var asset = BlackboardLocator.FindLayer(Domain, "");
                return new DelegateListDataSource("statemachine.blackboard", Localizer.UI("ui.sm.moduleVariables", "State Machine Module Variables"),
                    DataScope.Domain, Domain,
                    _ => VariablePane.Items(asset),
                    (c, item) => VariablePane.BuildVariableDetail(c.Registry, asset, c.Graph, item),
                    c => FrameworkDataSources.BuildLayerPane(c, Domain, ""));
            });

            // 状态机节点定义（领域级，只读）：注册表 projectDomain 档里属于本领域(StateMachineNodeDefinition)的节点。
            // 全局/通用节点归框架源「全局节点定义」(universal 档)，本源只列状态机自有节点；
            // 通用查看器逻辑（零领域语义）复用框架 FrameworkDataSources，本类不自持。
            DataSourceRegistry.Register("statemachine.defs", ctx =>
            {
                var registry = NodeRegistryLocator.Find();
                IEnumerable<NodeDefinition> Mine() => registry == null
                    ? Enumerable.Empty<NodeDefinition>()
                    : registry.projectDomain.Where(d => d is StateMachineNodeDefinition);
                return new DelegateListDataSource("statemachine.defs", Localizer.UI("ui.sm.nodeDefs", "State Machine Node Definitions (read-only)"),
                    DataScope.Domain, Domain,
                    _ => FrameworkDataSources.NodeDefItems(Mine()),
                    FrameworkDataSources.BuildNodeDefDetail,
                    _ => FrameworkDataSources.BuildNodeDefsView(Mine()));
            });
        }
    }
}
