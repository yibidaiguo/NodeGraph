// GraphListPane.cs — 第 5 层（连线图编辑器），模板层。
// 左侧"图列表"面板：项目里全部 NodeGraphAsset，**按模块分组**展示——每个模块一张可开合（CollapsibleCard）、
// 可滚动的分组（如"对话""任务"……日后新增领域自然多一组）。顶部搜索框跨组过滤，单击一行即加载该图。
// 可选「模块过滤」：领域入口（如对话编辑器）把面板钉到自己的模块上，只列本模块的图（仍是分组+可开合的样式）。
// 自身只负责"列出 + 选中后回调"，真正的加载由 NodeEditorWindow 经 OnSelected 完成。Editor/ 程序集。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;         // ScriptableObject.CreateInstance（新建图）
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据类型（NodeGraphAsset）

namespace NodeEditor.EditorUI
{
    public class GraphListPane : VisualElement
    {
        // 用户在列表里点选了某个图 → 请求窗口加载它（窗口负责 Push 导航历史 + LoadGraph）。
        public Action<NodeGraphAsset> OnSelected;

        // 删除当前图后回调（窗口据此换载同模块的替补图或清空画布；replacement 为 null=该模块已无图）。
        // 删除发生在面板内（弹确认 + 删资产 + 刷新列表），但"删后画布加载什么"是窗口的导航职责，故回调出去。
        public Action<NodeGraphAsset> OnDeleted;

        public static void RegisterModuleInitializer(string module, Action<NodeGraphAsset> init)
        {
            if (string.IsNullOrEmpty(module) || init == null) return;
            var recipe = LegacyRecipe(module);
            if (recipe == null) return;
            recipe.initialize = graph => { init(graph); return true; };
        }

        public static void RegisterModuleAssetFolders(string module, Func<string> graphRoot, Func<string> blackboardFolder)
        {
            if (string.IsNullOrEmpty(module) || (graphRoot == null && blackboardFolder == null)) return;
            var recipe = LegacyRecipe(module);
            if (recipe == null) return;
            if (graphRoot != null) recipe.graphRoot = graphRoot;
            if (blackboardFolder != null) recipe.blackboardFolder = blackboardFolder;
        }

        static GraphCreateRecipe LegacyRecipe(string module)
        {
            if (GraphCreationRegistry.HasExplicitModuleOwnership(module)) return null;
            var recipes = GraphCreationRegistry.ForModule(module);
            var recipe = recipes.FirstOrDefault(item => item.legacyCompatibility);
            if (recipe != null) return recipe;

            recipe = new GraphCreateRecipe
            {
                id = "__legacy." + module,
                module = module,
                labelKey = "ui.newGraph",
                labelFallback = "New",
                defaultFileName = "NewGraph",
                initialize = _ => true,
                legacyCompatibility = true
            };
            GraphCreationRegistry.Register(recipe);
            return recipe;
        }

        readonly ToolbarSearchField m_Search;
        readonly ScrollView m_Scroll;        // 全部分组竖排其中，整体可滚动
        readonly Label m_Empty;
        readonly VisualElement m_Actions;
        readonly Button m_Delete;            // 删除当前选中图（无选中时置灰）
        readonly List<NodeGraphAsset> m_All = new();          // 项目里全部图（未过滤）
        readonly Dictionary<NodeGraphAsset, VisualElement> m_Rows = new();  // 图→行视图（切换高亮用，避免整列表重建）
        readonly HashSet<string> m_Collapsed = new();         // 被收起的模块 key（跨 Reload 保持开合）
        NodeGraphAsset m_Current;                             // 当前打开的图（用于高亮）

        // 模块过滤：null/空 = 自由模式（列出全部模块的图）；非空 = 只列该模块的图（领域入口锁定用）。
        string m_ModuleFilter;

        public GraphListPane(string moduleFilter = null)
        {
            m_ModuleFilter = string.IsNullOrEmpty(moduleFilter) ? null : moduleFilter;
            AddToClassList("graphlist-root");

            var header = new Label(ModuleUI("ui.graphs", "Graphs"));
            header.AddToClassList("inspector-header");
            Add(header);

            m_Search = new ToolbarSearchField { tooltip = Localizer.UI("ui.searchHint", "Type to filter…") };
            m_Search.AddToClassList("graphlist-search");
            m_Search.RegisterValueChangedCallback(_ => Rebuild());
            Add(m_Search);

            m_Scroll = new ScrollView(ScrollViewMode.Vertical);
            m_Scroll.AddToClassList("graphlist-scroll");
            m_Scroll.style.flexGrow = 1;
            Add(m_Scroll);

            // 空状态提示（过滤后无图时显示）。
            m_Empty = new Label(ModuleUI("ui.noGraphs", "No graphs in project"));
            m_Empty.AddToClassList("graphlist-empty");
            m_Empty.style.display = DisplayStyle.None;
            Add(m_Empty);

            // 底部操作行：新建（以当前选中图为种类、造同模块的同类图）+ 删除（删当前选中图）。
            // 不放"刷新"：项目资产变化已自动 Reload（见下方 projectChanged 订阅）。
            m_Actions = new VisualElement();
            m_Actions.AddToClassList("graphlist-actions");

            m_Delete = new Button(DeleteCurrent) { text = Localizer.UI("ui.deleteGraph", "Delete") };
            m_Delete.AddToClassList("add-button");
            RebuildActions();
            Add(m_Actions);

            // 项目资产变化（新建/删除/重命名图）时自动刷新；面板脱离时取消订阅，避免泄漏。
            RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.projectChanged += Reload);
            RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= Reload);

            Reload();
        }

        // 重新扫描项目里全部 NodeGraphAsset，再按模块分组重建 UI。保留当前选中高亮、搜索词与各组开合状态。
        public void Reload()
        {
            m_All.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(NodeGraphAsset)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var a = AssetDatabase.LoadAssetAtPath<NodeGraphAsset>(path);
                if (a != null) m_All.Add(a);
            }
            m_All.Sort((x, y) => string.Compare(x.name, y.name, StringComparison.OrdinalIgnoreCase));
            Rebuild();
        }

        // 按模块分组重建分组卡片。每组一张 CollapsibleCard（头部=模块名+计数，内容=该组的行）。
        void Rebuild()
        {
            m_Scroll.Clear();
            m_Rows.Clear();

            var q = m_Search?.value?.Trim();
            // 过滤：模块（锁定时）+ 搜索词。
            var shown = m_All.Where(a => a != null
                && (m_ModuleFilter == null || ModuleKey(a) == m_ModuleFilter)
                && (string.IsNullOrEmpty(q) || a.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));

            // 按模块分组；组键排序时空模块（"其他"）永远垫底，其余按 key 字典序。
            var groups = shown.GroupBy(ModuleKey)
                .OrderBy(g => string.IsNullOrEmpty(g.Key) ? 1 : 0)
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            int total = 0;
            foreach (var g in groups)
            {
                total += g.Count();
                m_Scroll.Add(BuildGroup(g.Key, g.ToList()));
            }

            m_Empty.style.display = total == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            SyncSelection();
        }

        // 单个模块分组：CollapsibleCard，头部放模块名+计数，内容放各行。开合状态写回 m_Collapsed 以跨重建保持。
        VisualElement BuildGroup(string moduleKey, List<NodeGraphAsset> assets)
        {
            bool expanded = !m_Collapsed.Contains(moduleKey);
            var card = new CollapsibleCard(expanded);
            card.AddToClassList("graphlist-group");

            var title = new Label($"{ModuleName(moduleKey)}  ({assets.Count})");
            title.AddToClassList("graphlist-group-title");
            card.HeaderMid.Add(title);

            string key = moduleKey ?? "";
            card.OnExpandedChanged = open =>
            {
                if (open) m_Collapsed.Remove(key);
                else m_Collapsed.Add(key);
            };

            foreach (var a in assets)
            {
                var row = MakeRow(a);
                card.Content.Add(row);
                m_Rows[a] = row;
            }
            return card;
        }

        // 一行 = 一张图。单击加载（OnSelected）；双击在 Project 里定位（PingObject）。
        VisualElement MakeRow(NodeGraphAsset asset)
        {
            var row = new VisualElement();
            row.AddToClassList("graphlist-row");
            var label = new Label(asset.name);
            label.AddToClassList("graphlist-row-label");
            row.Add(label);
            // 同名图很常见——把资产路径放进 tooltip，数量很多时便于区分/定位。
            row.tooltip = AssetDatabase.GetAssetPath(asset);

            row.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.clickCount >= 2) { EditorGUIUtility.PingObject(asset); return; }   // 双击=定位
                if (asset != m_Current) OnSelected?.Invoke(asset);                        // 单击=加载
            });
            return row;
        }

        // 由窗口在 LoadGraph 时调用：高亮当前打开的图（不触发 OnSelected，避免重入加载）。
        public void SetCurrent(NodeGraphAsset asset)
        {
            m_Current = asset;
            RebuildActions();
            SyncSelection();
        }

        public static string ResolveCreateModule(string moduleFilter, NodeGraphAsset current) =>
            string.IsNullOrEmpty(moduleFilter)
                ? (current != null ? ModuleKey(current) : "")
                : moduleFilter;

        public static NodeGraphAsset SelectReplacementAfterDelete(
            IEnumerable<NodeGraphAsset> all, NodeGraphAsset current, string moduleFilter)
        {
            if (current == null) return null;
            var assets = (all ?? Enumerable.Empty<NodeGraphAsset>()).Where(a => a != null && a != current).ToList();
            string mod = ModuleKey(current);
            var sameModule = assets.FirstOrDefault(a => ModuleKey(a) == mod);
            return !string.IsNullOrEmpty(moduleFilter) ? sameModule : sameModule ?? assets.FirstOrDefault();
        }

        void RebuildActions()
        {
            m_Actions.Clear();
            string module = ResolveCreateModule(m_ModuleFilter, m_Current);
            var recipes = GraphCreationRegistry.ForModule(module);
            if (recipes.Count == 0)
            {
                AddCreateButton(null, "ui.newGraph", "New");
            }
            else
            {
                foreach (var recipe in recipes)
                    AddCreateButton(recipe, recipe.labelKey, recipe.labelFallback);
            }
            m_Actions.Add(m_Delete);
        }

        void AddCreateButton(GraphCreateRecipe recipe, string labelKey, string labelFallback)
        {
            var create = new Button(() => CreateGraph(recipe))
            {
                text = Localizer.UI(labelKey, labelFallback)
            };
            create.AddToClassList("add-button");
            m_Actions.Add(create);
        }

        // 切换行高亮：只改 class，不重建（性能 + 不打断滚动位置）。无选中图时删除按钮置灰。
        void SyncSelection()
        {
            foreach (var kv in m_Rows)
                kv.Value.EnableInClassList("is-selected", kv.Key == m_Current);
            if (m_Delete != null) m_Delete.SetEnabled(m_Current != null);
        }

        // 取一张图的模块 key（null/空都归一为 ""，即"其他"组）。
        static string ModuleKey(NodeGraphAsset a) => string.IsNullOrEmpty(a.module) ? "" : a.module;

        // 模块 key → 显示名：查本地化表 module.<key>；空模块用 module.none（"其他"）。表里没有则回退到 key 本身。
        static string ModuleName(string key) =>
            string.IsNullOrEmpty(key)
                ? Localizer.UI("module.none", "Other")
                : Localizer.UI("module." + key, key);

        string ModuleUI(string key, string fallback)
        {
            if (string.IsNullOrEmpty(m_ModuleFilter))
                return Localizer.UI(key, fallback);

            var value = Localizer.UI(key + "." + m_ModuleFilter, null);
            return string.IsNullOrEmpty(value) ? Localizer.UI(key, fallback) : value;
        }

        void CreateGraph(GraphCreateRecipe recipe)
        {
            string module = recipe?.module ?? ResolveCreateModule(m_ModuleFilter, m_Current);

            var configuredRoot = recipe?.graphRoot?.Invoke();
            var configuredBlackboard = recipe?.blackboardFolder?.Invoke();
            if ((recipe?.graphRoot != null && !ProjectAssetPaths.IsProjectAssetPath(configuredRoot)) ||
                (recipe?.blackboardFolder != null && !ProjectAssetPaths.IsProjectAssetPath(configuredBlackboard)))
            {
                Debug.LogError($"NodeEditor: graph recipe '{recipe?.id}' has unresolved or non-project asset paths. " +
                               "Open the module Asset Paths configuration and use folders under Assets/.");
                return;
            }

            string dir = NormalizeFolder(configuredRoot) ?? "Assets";
            if (dir == "Assets" && m_Current != null)
            {
                var cur = AssetDatabase.GetAssetPath(m_Current);
                if (!string.IsNullOrEmpty(cur))
                    dir = System.IO.Path.GetDirectoryName(cur).Replace('\\', '/');
            }
            string createLabel = Localizer.UI(recipe?.labelKey ?? "ui.newGraph", recipe?.labelFallback ?? "New");
            var path = EditorUtility.SaveFilePanelInProject(
                createLabel,
                recipe?.defaultFileName ?? "NewGraph", "asset",
                ModuleUI("ui.newGraphPrompt", "Create a new graph"),
                dir);
            if (string.IsNullOrEmpty(path)) return;   // 用户取消

            path = NormalizeGraphPath(recipe, path);

            var asset = ScriptableObject.CreateInstance<NodeGraphAsset>();
            asset.module = module;   // 盖上种类，落进对应分组
            // 每张图自带一个「组」（= 图名），从而拥有自己的「图黑板」（组档）。组档需要模块（非法组合 module 空+group 非空），
            // 故仅在 module 非空时设组；模块为空的裸图无组档，只继承全局。
            if (!string.IsNullOrEmpty(module))
                asset.group = System.IO.Path.GetFileNameWithoutExtension(path);
            if (recipe != null && !recipe.initialize(asset))
            {
                UnityEngine.Object.DestroyImmediate(asset);
                EditorUtility.DisplayDialog(
                    createLabel,
                    Localizer.UI("ui.graphSetupFailed", "Graph setup failed. Run the module's Setup Assets and try again."),
                    "OK");
                return;
            }
            ProjectAssetPaths.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            PersistCreatedGraph(asset, path);

            // 自动配齐本图的分层黑板（按标签 find-or-create，幂等）：创建模块即配「模块黑板」、创建图即配「图黑板」（组档）。
            // 分层原则（准则 #15）：模块/组黑板落在**本模块的资产区**——即新图所在的文件夹，而非框架目录。
            if (!string.IsNullOrEmpty(module))
            {
                var graphDir = NormalizeFolder(configuredBlackboard) ??
                    System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                ProjectAssetPaths.EnsureFolder(graphDir);
                if (BlackboardLocator.FindLayer(module, "") == null) BlackboardLocator.CreateLayer(module, "", graphDir);
                if (!string.IsNullOrEmpty(asset.group) && BlackboardLocator.FindLayer(module, asset.group) == null)
                    BlackboardLocator.CreateLayer(module, asset.group, graphDir);
            }
            AssetDatabase.SaveAssets();

            Reload();                   // 让新资产进入列表
            OnSelected?.Invoke(asset);  // 立即在编辑器里打开它（走窗口的加载 / 导航历史路径）
        }

        static void PersistCreatedGraph(NodeGraphAsset asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Graph");
            EditorUtility.SetDirty(asset);
        }

        static string NormalizeFolder(string folder) =>
            string.IsNullOrEmpty(folder) ? null : folder.Replace('\\', '/').TrimEnd('/');

        // 已登记 graph root 的模块采用「每图一文件夹」：用户选择 Foo.asset 时，实际落到 Foo/Foo.asset。
        // 若用户已经选在同名文件夹内，则保持该文件夹，只保证唯一资产路径。
        static string NormalizeGraphPath(GraphCreateRecipe recipe, string path)
        {
            if (recipe?.graphRoot == null) return path;
            path = path.Replace('\\', '/');
            var selectedDir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var dirName = System.IO.Path.GetFileName(selectedDir);
            var graphDir = string.Equals(dirName, name, StringComparison.OrdinalIgnoreCase)
                ? selectedDir
                : $"{selectedDir}/{name}";
            var candidate = $"{graphDir}/{name}.asset";
            return AssetDatabase.IsValidFolder(graphDir)
                ? AssetDatabase.GenerateUniqueAssetPath(candidate)
                : candidate;
        }

        // 删除当前选中的图：弹确认（删文件不可逆）→ 在删之前先选好同模块的替补图（列表里同组的下一张，
        // 没有则 null）→ 删资产 + 刷新列表 → 经 OnDeleted 让窗口换载替补图 / 清空画布（删的若是正打开那张）。
        // 无选中时按钮已置灰，不会进来。
        void DeleteCurrent()
        {
            if (m_Current == null) return;
            string path = AssetDatabase.GetAssetPath(m_Current);
            if (string.IsNullOrEmpty(path)) return;

            bool ok = EditorUtility.DisplayDialog(
                Localizer.UI("ui.deleteGraph", "Delete"),
                string.Format(Localizer.UI("ui.deleteGraphConfirm", "Delete graph \"{0}\"? You can restore it with Undo."), m_Current.name),
                Localizer.UI("ui.delete", "Delete"),
                Localizer.UI("ui.cancel", "Cancel"));
            if (!ok) return;

            // 删前先在同模块里挑替补（同组、按当前排序的下一张；没有则 null）。
            var replacement = SelectReplacementAfterDelete(m_All, m_Current, m_ModuleFilter);

            DestroyGraphWithUndo(m_Current);
            Reload();                       // 重扫列表（projectChanged 也会触发，但这里立即同步）
            OnDeleted?.Invoke(replacement); // 窗口据此换载替补 / 清空画布
        }

        static void DestroyGraphWithUndo(NodeGraphAsset graph)
        {
            Undo.DestroyObjectImmediate(graph);
        }
    }
}
