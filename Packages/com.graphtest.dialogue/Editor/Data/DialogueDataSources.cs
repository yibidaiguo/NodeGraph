// DialogueDataSources.cs — 对话领域的数据源注册（Dialogue.Editor 程序集，[InitializeOnLoad]）。
// 「框架留缝、领域填领域数据」：框架自注册黑板 / 本地化 / 图概览（见 FrameworkDataSources），
// 本类把对话领域自己的数据接进同一个通用数据窗口：
//   · 对话数据库（领域 "dialogue"）—— 直接用 InspectorElement 复用现成的 DialogueDatabaseEditor，零重写；
//   · 节点定义 / 注册表（领域 "dialogue"，只读）—— 列出生成的节点定义，供排查；它们由
//     Tools/NodeGraph/Manager 中的 Dialogue / Setup Assets 生成，不在窗口里改。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NodeEditor;
using NodeEditor.EditorUI;
// Unity 6 的 UI Toolkit 也有个 DataSourceContext（数据绑定用）；本文件同时 using 了两个命名空间，
// 显式别名指向本框架的那个，消除歧义。
using DataSourceContext = NodeEditor.EditorUI.DataSourceContext;

namespace Dialogue.EditorUI
{
    [InitializeOnLoad]
    public static class DialogueDataSources
    {
        const string Domain = "dialogue";

        static DialogueDataSources()
        {
            // 模块变量（领域级）：对话模块（module=="dialogue"）那一档黑板。复用框架的分层面板（缺该档则给「新建」按钮）。
            DataSourceRegistry.Register("dialogue.blackboard", ctx =>
            {
                var asset = BlackboardLocator.FindLayer(Domain, "");
                return new DelegateListDataSource("dialogue.blackboard", Localizer.UI("ui.moduleVariables", "Module Variables"),
                    DataScope.Domain, Domain,
                    _ => VariablePane.Items(asset),
                    (c, item) => VariablePane.BuildVariableDetail(c.Registry, asset, c.Graph, item),
                    c => FrameworkDataSources.BuildLayerPane(c, Domain, ""));
            });

            // 对话数据库（领域级）：InspectorElement 把 DialogueDatabaseEditor 的 GUI 整段嵌进窗口右侧。
            DataSourceRegistry.Register("dialogue.database", ctx =>
            {
                var db = DialogueDatabaseLocator.Resolve(out var reason);
                return new DelegateListDataSource("dialogue.database", Localizer.UI("ui.dialogueData", "Dialogue Database"),
                    DataScope.Domain, Domain,
                    _ => DialogueDatabaseEditor.Items(db),
                    (_, item) => DialogueDatabaseEditor.BuildEntryDetail(db, item),
                    _ => BuildDatabasePane(db, reason));
            });

            // 对话节点定义（领域级，只读）：注册表 projectDomain 档里属于本领域(DialogueNodeDefinition)的节点。
            // 节点按层分别展示：全局/通用节点归框架源「全局节点定义」(项目档=universal)，本源只列对话自有节点；
            // 通用查看器逻辑（零领域语义）复用框架 FrameworkDataSources，本类不再自持。
            DataSourceRegistry.Register("dialogue.defs", ctx =>
            {
                var registry = NodeRegistryLocator.Find();
                IEnumerable<NodeDefinition> Mine() => registry == null
                    ? Enumerable.Empty<NodeDefinition>()
                    : registry.projectDomain.Where(d => d is DialogueNodeDefinition);
                return new DelegateListDataSource("dialogue.defs", Localizer.UI("ui.dialogueNodeDefs", "Dialogue Node Definitions (read-only)"),
                    DataScope.Domain, Domain,
                    _ => FrameworkDataSources.NodeDefItems(Mine()),
                    FrameworkDataSources.BuildNodeDefDetail,
                    _ => FrameworkDataSources.BuildNodeDefsView(Mine()));
            });

            // 对话单元（领域级，只读目录）：本领域程序集的可组合单元（如「触发事件」）。复用框架单元目录查看器；
            // 全局通用单元归框架源「全局单元」(项目档)，本源只列对话自有单元。只读浏览，实际编辑在节点的 Unit 槽里做。
            DataSourceRegistry.Register("dialogue.units", ctx =>
                new DelegateListDataSource("dialogue.units", Localizer.UI("ui.dialogueUnits", "Dialogue Units (read-only)"),
                    DataScope.Domain, Domain,
                    _ => FrameworkDataSources.UnitCatalogItems(true),
                    FrameworkDataSources.BuildUnitDetail,
                    _ => FrameworkDataSources.BuildUnitCatalogView(true)));
        }

        static VisualElement BuildDatabasePane(DialogueDatabase db, string reason)
        {
            return BuildDatabasePane(db, reason, DialogueAssetPathsLocator.FindOrCreate());
        }

        static VisualElement BuildDatabasePane(DialogueDatabase db, string reason, DialogueAssetPaths paths)
        {
            if (db != null) return new InspectorElement(db);

            var root = new VisualElement();
            root.Add(EditorUi.EmptyState(reason));
            if (paths == null) return root;
            var field = new ObjectField
            {
                objectType = typeof(DialogueDatabase),
                allowSceneObjects = false
            };
            field.SetValueWithoutNotify(paths.authoringDatabase);
            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RegisterCompleteObjectUndo(paths, "Select Dialogue Database");
                paths.authoringDatabase = evt.newValue as DialogueDatabase;
                EditorUtility.SetDirty(paths);
            });
            root.Add(EditorUi.FormRow(Localizer.UI("ui.authoringDatabase", "Authoring Database"), field));
            return root;
        }

        // 节点定义只读查看器（通用、零领域语义：仅读 NodeDefinition 的角色/端口/参数）已上移至框架
        // FrameworkDataSources（NodeDefItems / BuildNodeDefDetail / BuildNodeDefsView）。本类只复用、不再自持，
        // 从而「全局节点」与「对话节点」各按所在档（universal / projectDomain）分层展示，互不耦合。
    }
}
