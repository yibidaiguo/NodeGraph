// DialogueGraphScaffold.cs — 对话领域层：新建对话图时的"播种"策略。
// 框架（GraphListPane）按 module 显示 GraphCreationRegistry 中的显式创建配方；配方声明目录、默认文件名和
// 落盘前初始化器。本类在 [InitializeOnLoad] 把对话配方注册到 module="dialogue"：每张新图固定带一个
// "进入"节点(Start)与一个"退出"节点(End)，二者钉住(pinned)不可删除。框架提供机制（pinned + 按模块注册），
// 这里只定策略（对话用 Start/End）。仅 Editor/ 程序集。
//
// 左侧面板「新建」按钮按 module 查表调用本初始化器。外部或脚本创建且已标 module="dialogue" 的空图，
// 则由下方 AssetPostprocessor 兜底；Project 右键创建的 module 为空的裸图不会被播种。

using UnityEditor;
using UnityEngine;
using NodeEditor;
using NodeEditor.EditorUI;   // GraphCreationRegistry + NodeDefinitionLocator

namespace Dialogue.EditorUI
{
    [InitializeOnLoad]
    public static class DialogueGraphScaffold
    {
        public const string Module = "dialogue";

        static DialogueGraphScaffold()
        {
            GraphCreationRegistry.Register(new GraphCreateRecipe
            {
                id = "dialogue.graph",
                module = Module,
                labelKey = "ui.newDialogueGraph",
                labelFallback = "New Dialogue",
                defaultFileName = "NewDialogue",
                graphRoot = () => DialogueAssetPathsLocator.FindOrCreate()?.dialogueGroupsDir,
                blackboardFolder = () => DialogueAssetPathsLocator.FindOrCreate()?.blackboardLayersDir,
                initialize = Seed
            });
        }

        // 给一张新的（裸）对话图播种：控制流类型 + 钉住的 Start(入口)/End(退出)。
        // 不在两者间连线——中间的剧情由设计师自己编排。返回是否真的加了节点
        //（定义资产缺失时为 false，调用方据此不要标脏/存盘，避免空图反复重导入触发死循环）。
        public static bool Seed(NodeGraphAsset g)
        {
            if (g == null) return false;
            var start = MakePinned(typeof(StartNode), new Vector2(0, 0));
            var end = MakePinned(typeof(EndNode), new Vector2(320, 40));
            if (start == null || end == null) return false;   // 定义资产还没生成（未跑 Setup）→ 不动

            g.graphType = GraphType.ControlFlow;
            g.module = "dialogue";                             // 归入左侧"对话"分组 / 对话编辑器的模块过滤
            g.instances.Add(start);
            g.entryInstanceIds.Add(start.instanceId);          // Start 是控制流入口
            g.instances.Add(end);
            return true;
        }

        // 仅当图为空时播种（幂等）——供"任意路径新建"的兜底 postprocessor 使用，避免给已有内容的图重复加节点。
        public static bool SeedIfEmpty(NodeGraphAsset g)
        {
            if (g == null || g.instances.Count != 0 || g.entryInstanceIds.Count != 0) return false;
            return Seed(g);
        }

        // 解析节点定义资产（其 Id 即 RebuildFromCode 后的稳定 id），建一个钉住的实例。
        // 定义资产不存在（还没跑过 Setup Assets）时返回 null，由调用方决定如何处理。
        static NodeInstance MakePinned(System.Type defType, Vector2 pos)
        {
            var def = NodeDefinitionLocator.ForType(defType);
            if (def == null) return null;
            return new NodeInstance { definitionId = def.Id, position = pos, pinned = true };
        }
    }

    // 兜底：已标 module="dialogue" 的"空"对话图（脚本创建、外部导入……），在导入回调里当即补上进入/退出节点——
    // 使"固定带 Start/End"不依赖具体从哪个入口创建、也不依赖编辑器 tick 时机。多模块下**只认 module=="dialogue"**：
    // 右键 Create ▸ NodeEditor/Graph 产生的裸图（module 为空）保持裸图，不被误种成对话图（别的模块的空图同理免疫）；
    // 「新建」按钮走 GraphListPane.CreateGraph，已在落盘前播种，此处见非空即跳过、不重复。仅对真正空的对话图生效
    //（已播种/已有内容的跳过）；定义资产缺失时 Seed 返回 false、不标脏、不存盘，不会触发重导入死循环。落盘延后到导入结束后做。
    class DialogueGraphAutoSeed : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var paths = System.Array.FindAll(imported,
                path => path.EndsWith(".asset"));
            if (paths.Length == 0) return;

            // Asset import callbacks are not a safe place to register editor Undo. Defer the
            // persistent edit, then snapshot each existing graph immediately before seeding it.
            EditorApplication.delayCall += () =>
            {
                bool any = false;
                foreach (var path in paths)
                {
                    var g = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
                    if (g == null || g.module != "dialogue" ||
                        g.instances.Count != 0 || g.entryInstanceIds.Count != 0) continue;
                    if (NodeDefinitionLocator.ForType(typeof(StartNode)) == null ||
                        NodeDefinitionLocator.ForType(typeof(EndNode)) == null) continue;

                    Undo.RegisterCompleteObjectUndo(g, "Seed Dialogue Graph");
                    if (DialogueGraphScaffold.SeedIfEmpty(g))
                    {
                        EditorUtility.SetDirty(g);
                        any = true;
                    }
                }
                if (any) AssetDatabase.SaveAssets();
            };
        }
    }
}
