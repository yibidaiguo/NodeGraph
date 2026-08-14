// NodeEditorWindow.cs — 第 5 层（连线图编辑器），模板层。
// 画布优先的编辑器外壳（顶栏 + 满窗画布 + 贴角浮层），Unity 6，Editor/ 程序集。
//
// 布局取向（0.1.x 重构）：**画布铺满窗口，chrome 按需出现**。
// 旧外壳是固定三栏（图列表 | 画布 | 检视），实测两个对话就要为左栏付出 260px 恒定宽度，
// 其中 80% 是空白；右栏 320px 常驻，未选中节点时只写着一句"选中一个节点即可在此编辑"。
// 现在：
//   · 图列表 → 顶栏那颗胶囊点开的切换器弹层（PickerPill + GraphListPane），左栏整条删除；
//   · 变量 / 检视 → 画布上的贴角浮层（OverlayPanel），可拖可折可关，检视只在选中节点时出现；
//   · 缩放 / 全览 / 整理 / 缩略图 / 加节点 → 画布左下的坞（CanvasDock），手不用离开工作区；
//   · 长尾开关（深色、语言、整理、全览）→ 顶栏「⋯」溢出菜单，不再和主命令平铺同权重。
// 顶栏因此只剩三区：左「在哪张图 / 怎么走」，中留白，右「看什么 / 有没有问题」。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset 等）

namespace NodeEditor.EditorUI
{
    public class NodeEditorWindow : EditorWindow
    {
        GraphCanvas m_Canvas;
        Vector2 m_LastPanelMouse;   // 面板空间内的光标位置，供空格键添加节点对话框使用
        GraphDebugger m_Debugger;
        IRuntimeGraph m_AttachedRuntime;
        int m_RuntimeRegistryVersion = -1;
        List<NodeGraphAsset> m_RuntimeGraphCandidates;
        string m_RuntimeGraphCandidatesModule;
        InspectorPane m_Inspector;
        LayeredVariablePane m_Variables;   // 分层变量面板：按当前图显示 全局/模块/组 三档
        PickerPill m_Picker;               // 顶栏「当前在编辑哪张图」的胶囊（点开=切换器弹层）
        PanelToggleBar m_Panels;           // 顶栏右侧「看什么」开关组
        OverlayPanel m_VariablesOverlay;   // 画布左上：变量
        OverlayPanel m_InspectorOverlay;   // 画布右上：检视（跟随选中）
        CanvasDock m_Dock;                 // 画布左下：缩放 / 全览 / 整理 / 缩略图 / 加节点
        StatusChip m_Status;               // 顶栏最右：校验状态，可点着巡回问题节点
        Breadcrumb m_Breadcrumb;
        NavigationHistory m_Nav = new();
        Button m_BackButton;
        Button m_ForwardButton;
        NodeView m_SelectedNode;           // 当前选中的节点（决定检视浮层是否露面）
        int m_ProblemCursor;               // 状态 chip 巡回到第几个问题节点
        bool m_InspectorEnabled = true;    // 用户是否启用检视浮层（是否真的露面还要看有没有选中节点）
        const string PanelVariables = "variables";
        const string PanelInspector = "inspector";
        const string MiniMapPref = "NodeEditor.MiniMap";
        const string InspectorEnabledPref = "NodeEditor.InspectorEnabled";

        [SerializeField] NodeGraphAsset m_Asset;   // [SerializeField] 使已打开的 graph 能在 domain reload（进入播放模式）后保留
        // 模块模式：从某个领域入口（如 Tools/NodeGraph/Dialogue）打开时非空 —— 切换器只列该模块的图。
        // 从 NodeGraph Manager 的 Open Node Editor 打开则为空（自由模式：列出全部模块的图）。[SerializeField]
        // 使其扛过 domain reload，避免重载后悄悄回到自由模式。框架只认这个字符串、不认任何领域语义；
        // "锁哪个模块 / 叫什么名"由领域入口决定（机制/策略分层，见 OpenModule）。
        [SerializeField] string m_ModuleFilter;
        NodeRegistry m_Registry;
        BlackboardSet m_Blackboard;            // 当前图的有效黑板（全局⊕模块⊕组）：检视面板「键」下拉 + 调试器校验都读它

        public static void Open()
        {
            var w = GetWindow<NodeEditorWindow>();
            w.UpdateWindowTitle();
            w.minSize = new Vector2(720, 460);
            // 从 NodeGraph Manager 的 Open Node Editor 入口打开 = 自由模式：若此前被某个领域入口锁到某模块，这里清掉过滤并按自由布局重建。
            if (!string.IsNullOrEmpty(w.m_ModuleFilter)) { w.m_ModuleFilter = null; w.RebuildAndReload(); }
        }

        // 领域入口用的"模块模式打开"机制（框架层，领域无关）。领域层（如对话）在自己的 Editor 程序集里调它，
        // 把窗口锁到自己的模块上并给一个本地化标题 —— 切换器只列该模块的图，但本模块内仍可多图切换。
        // "锁哪个模块 / 初始打开哪张图 / 叫什么名"是领域策略，本方法只负责"按模块过滤布局重建并加载"。
        public static void OpenModule(string module, string title = null, NodeGraphAsset initial = null)
        {
            if (string.IsNullOrEmpty(module)) return;
            var w = GetWindow<NodeEditorWindow>();
            w.titleContent = new GUIContent(string.IsNullOrEmpty(title) ? ModuleEditorTitle(module) : title);
            w.minSize = new Vector2(720, 460);
            w.m_ModuleFilter = module;
            w.m_Asset = RuntimeGraphLocator.FindActiveGraph(module, initial);
            w.RebuildAndReload();  // 按模块过滤布局重建
        }

        // 在 Project 中双击某个 NodeGraphAsset 即可在此打开它。
        [UnityEditor.Callbacks.OnOpenAsset]
        public static bool OnOpen(int instanceId, int line)
        {
            var obj = EditorUtility.EntityIdToObject(instanceId);
            if (obj is NodeGraphAsset asset)
            {
                Open();
                GetWindow<NodeEditorWindow>().LoadGraph(asset);
                return true;
            }
            return false;
        }

        void OnEnable() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }
        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Popover.CloseAll();   // 弹层挂在本窗口根上，窗口没了它的静态"当前打开"引用也得清掉
            StopRuntimePoll();
            DetachRuntimeBinding();   // 若窗口在仍处于播放模式时被关闭，则解除对 EditorApplication.update 的挂接
        }

        void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                m_RuntimeGraphPinned = false;
                StartRuntimePoll();
            }
            else if (s == PlayModeStateChange.ExitingPlayMode)
            {
                m_RuntimeGraphPinned = false;
                StopRuntimePoll();
                DetachRuntimeBinding();
            }
        }

        // runner 可能晚于 EnteredPlayMode 注册，窗口也会在运行中切图/关闭重开。
        // 因此 play 期间保持一个有界 update 轮询：按当前资产匹配，变化时解绑/重绑；退出 play/关窗才拆除。
        bool m_Polling;
        bool m_RuntimeGraphPinned;
        void StartRuntimePoll() { if (!m_Polling) { m_Polling = true; EditorApplication.update += PollForRuntime; } }
        void StopRuntimePoll()  { if (m_Polling)  { m_Polling = false; EditorApplication.update -= PollForRuntime; } }
        void PollForRuntime() => PollForRuntime(Application.isPlaying);
        void PollForRuntime(bool isPlaying)
        {
            if (!isPlaying) { StopRuntimePoll(); return; }
            if (m_Debugger == null) return;                 // 等待 CreateGUI 在 reload 后重建它

            NodeGraphAsset attachedGraph = null;
            NodeGraphAsset reportedGraph = null;
            if (!string.IsNullOrEmpty(m_ModuleFilter))
            {
                attachedGraph = RuntimeGraphLocator.FindReportedActiveGraph(m_AttachedRuntime, m_ModuleFilter);
                reportedGraph = attachedGraph ?? RuntimeGraphLocator.FindReportedActiveGraph(m_ModuleFilter, m_Asset);
                if (!m_RuntimeGraphPinned && reportedGraph != null && reportedGraph != m_Asset)
                {
                    LoadGraph(reportedGraph, attachedGraph != null ? m_AttachedRuntime : null);
                    return;
                }
            }

            var registryVersion = RuntimeGraphRegistry.Version;
            var registryChanged = registryVersion != m_RuntimeRegistryVersion;
            if (registryChanged)
                m_RuntimeRegistryVersion = registryVersion;

            if (!string.IsNullOrEmpty(m_ModuleFilter))
            {
                if (registryChanged || m_RuntimeGraphCandidates == null ||
                    !string.Equals(m_RuntimeGraphCandidatesModule, m_ModuleFilter))
                {
                    m_RuntimeGraphCandidates = RuntimeGraphLocator.FindModuleGraphs(m_ModuleFilter, m_Asset);
                    m_RuntimeGraphCandidatesModule = m_ModuleFilter;
                }

                if (!m_RuntimeGraphPinned && reportedGraph == null)
                {
                    var activeGraph = RuntimeGraphLocator.FindActiveGraph(m_RuntimeGraphCandidates, m_Asset);
                    if (activeGraph != null && activeGraph != m_Asset)
                    {
                        LoadGraph(activeGraph, null);
                        return;
                    }
                }
            }

            var runtime = attachedGraph == m_Asset && m_AttachedRuntime != null
                ? m_AttachedRuntime
                : RuntimeGraphLocator.Find(m_Asset);
            if (ReferenceEquals(runtime, m_AttachedRuntime)) return;

            DetachRuntimeBinding();
            if (runtime == null) return;
            AttachRuntimeBinding(runtime);
        }

        void AttachRuntimeBinding(IRuntimeGraph runtime)
        {
            if (runtime == null || m_Debugger == null) return;
            m_Debugger.AttachRuntime(runtime);
            m_AttachedRuntime = runtime;
        }

        void DetachRuntimeBinding()
        {
            m_Debugger?.DetachRuntime();
            m_AttachedRuntime = null;
        }

        public void CreateGUI()
        {
            EditorUi.ConfigureWindow(rootVisualElement);
            StopRuntimePoll();
            DetachRuntimeBinding();
            m_RuntimeRegistryVersion = -1;
            m_RuntimeGraphCandidates = null;
            m_RuntimeGraphCandidatesModule = null;
            // 字段初始化器不会在 domain-reload 反序列化路径（例如进入播放模式）上执行，
            // 这会将这个普通的非序列化字段置空；在顶栏接入用到它的回调之前重新初始化。
            m_Nav ??= new NavigationHistory();
            m_SelectedNode = null;
            var root = rootVisualElement;

            // 「最近访问过的图」这条要嵌进顶栏同一行、紧挨后退/前进，所以必须先于顶栏创建。
            m_Breadcrumb = new Breadcrumb(OnCrumbClicked);

            m_Variables = new LayeredVariablePane();
            m_Canvas = new GraphCanvas();
            // 模块模式把锁定的模块交给画布：图还没建出来时（空壳）它是节点准入的唯一作用域，
            // 否则"添加节点"会列出全部模块的节点。m_ModuleFilter 变化一律走 RebuildAndReload → CreateGUI，
            // 所以在这里赋一次即可。
            m_Canvas.ModuleScope = m_ModuleFilter;
            m_Debugger = new GraphDebugger(m_Canvas);
            m_Inspector = new InspectorPane();

            root.Add(BuildAppBar());
            m_Canvas.style.flexGrow = 1;
            root.Add(m_Canvas);
            BuildOverlays();

            // 选中即出检视、取消即收 —— 未选中时不为一句"请选中节点"占着画布的一角。
            m_Canvas.OnNodeSelected = node =>
            {
                m_SelectedNode = node;
                m_Inspector.Show(node, m_Registry, m_Blackboard, m_Asset);
                SyncInspectorVisibility();
            };
            m_Canvas.OnNodeDeselected = node =>
            {
                m_Inspector.ClearIfShowing(node);
                if (m_SelectedNode == node) m_SelectedNode = null;
                SyncInspectorVisibility();
            };
            m_Canvas.OnSelectionCleared = () =>
            {
                m_Inspector.Clear();
                m_SelectedNode = null;
                SyncInspectorVisibility();
            };
            m_Canvas.OnGraphChanged += () => m_Debugger.RevalidateAndPaint();   // 每次编辑都重新校验（RevalidateAndPaint 会处理 asset 为 null 的情况）
            m_Debugger.OnValidated = RefreshStatus;   // 校验一跑完就刷新状态 chip
            // 右键空白画布 → 在光标处打开"添加节点"搜索框（与空格键、画布坞的「＋」同一入口）。
            // 画布只知道面板坐标，窗口的屏幕原点（position.position）在窗口手里，故由窗口注入面板→屏幕换算。
            m_Canvas.OnRequestAddNode = screenPos => AddNodeSearchWindow.Open(screenPos, this, m_Canvas);
            m_Canvas.PanelToScreen = panelPos => position.position + panelPos;
            m_Canvas.OnZoomChanged = scale => m_Dock?.SetZoom(scale);
            // Inspector 的可搜索下拉同样要把字段（面板坐标）换成屏幕坐标来弹 SearchWindow。
            m_Inspector.PanelToScreen = panelPos => position.position + panelPos;

            // 在面板空间内跟踪光标——KeyDownEvent 不携带鼠标位置，
            // 因此空格键处理器改为读取最近一次的指针位置。
            m_Canvas.RegisterCallback<PointerMoveEvent>(e => m_LastPanelMouse = e.position);

            // 空格键在光标处打开添加节点的搜索对话框
            m_Canvas.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Space)
                {
                    AddNodeAtCursor();
                    e.StopPropagation();
                }
            });

            // 在 domain reload 之后 m_Asset 会保留（[SerializeField]），但 m_Registry/m_Blackboard 不会——
            // 走 LoadGraph 重新解析它们，而非 ReloadCanvas（后者会使用为 null 的 locator）。
            if (m_Asset != null) LoadGraph(m_Asset, null);
            else RefreshStatus();

            // 如果窗口是在已处于播放模式时被（重新）构建的——例如在进入播放的 domain reload 期间，
            // EnteredPlayMode 时机点先于本次 CreateGUI 触发——则补做运行时挂接。
            if (Application.isPlaying) StartRuntimePoll();
        }

        public void LoadGraph(NodeGraphAsset asset) => LoadGraphFromUserSelection(asset);

        void LoadGraphFromUserSelection(NodeGraphAsset asset)
        {
            if (Application.isPlaying) m_RuntimeGraphPinned = true;
            LoadGraph(asset, null);
        }

        void LoadGraph(NodeGraphAsset asset, IRuntimeGraph runtime)
        {
            DetachRuntimeBinding();
            m_Asset = asset;
            m_Nav.Push(asset);
            if (SyncModuleShellToGraph(asset)) return;
            UpdateWindowTitle();
            m_Registry = NodeRegistryLocator.Find();              // 项目的 registry
            m_Blackboard = BlackboardLocator.ResolveFor(asset);   // 本图有效黑板（全局⊕模块⊕组，供下拉/校验）
            if (m_Canvas != null) ReloadCanvas();
            if (runtime != null) AttachRuntimeBinding(runtime);
            if (Application.isPlaying)
            {
                StartRuntimePoll();
            }
        }

        bool SyncModuleShellToGraph(NodeGraphAsset asset)
        {
            if (asset == null || string.IsNullOrEmpty(m_ModuleFilter)) return false;
            var graphModule = string.IsNullOrEmpty(asset.module) ? null : asset.module;
            if (m_ModuleFilter == graphModule) return false;

            m_ModuleFilter = graphModule;
            UpdateWindowTitle();
            RebuildAndReload();
            return true;
        }

        void UpdateWindowTitle()
        {
            var module = !string.IsNullOrEmpty(m_Asset?.module)
                ? m_Asset.module
                : m_ModuleFilter;
            titleContent = new GUIContent(ModuleEditorTitle(module));
        }

        static string ModuleEditorTitle(string module)
        {
            if (string.IsNullOrEmpty(module)) return Localizer.UI("ui.nodeEditor", "Node Editor");
            var fallback = char.ToUpperInvariant(module[0]) + module.Substring(1) + " Editor";
            return Localizer.UI($"ui.{module}Editor", fallback);
        }

        void ReloadCanvas()
        {
            m_Variables.Bind(m_Registry, m_Asset);   // 分层变量面板按当前图重建三档
            m_Canvas.Load(m_Asset, m_Registry);
            m_Debugger.SetBlackboard(m_Blackboard);   // 以便运行 blackboard-key 校验（合并视图）（v3 issue I）
            m_Debugger.IndexViews(m_Canvas.nodes.ToList().ConvertAll(n => (NodeView)n));
            m_Debugger.RevalidateAndPaint();
            m_Breadcrumb.SetPath(m_Nav.PathTitles());
            UpdateNavigationButtons();
            // 换图 = 旧选中作废：收起检视，别把上一张图的节点参数留在屏幕上。
            m_SelectedNode = null;
            SyncInspectorVisibility();
            m_ProblemCursor = 0;
            SyncPickerLabel();
        }

        // ---- 顶栏 ------------------------------------------------------------

        VisualElement BuildAppBar()
        {
            var bar = new AppBar();

            // 左区：怎么走 + 在哪张图。
            m_BackButton = AppBar.NavButton("‹", Localizer.UI("ui.back", "Back"),
                () => { if (m_Nav.CanBack) LoadGraph(m_Nav.Back()); });
            m_ForwardButton = AppBar.NavButton("›", Localizer.UI("ui.forward", "Forward"),
                () => { if (m_Nav.CanForward) LoadGraph(m_Nav.Forward()); });
            bar.Add(m_BackButton);
            bar.Add(m_ForwardButton);
            UpdateNavigationButtons();

            m_Picker = new PickerPill(OpenGraphPicker) { tooltip = Localizer.UI("ui.graphPickerTip", "Switch graph") };
            bar.Add(m_Picker);
            SyncPickerLabel();

            // 「最近访问过的图」紧跟胶囊，同一行读作导航区。
            if (m_Breadcrumb != null) bar.Add(m_Breadcrumb);

            bar.AddSpacer();

            // 右区：看什么。浮层开关与"去别处看"的命令同权重，摆在同一段里。
            m_Panels = new PanelToggleBar();
            m_Panels.Add(PanelVariables, Localizer.UI("ui.variables", "Variables"),
                Localizer.UI("ui.variablesTip", "Show or hide the variables overlay"),
                false, on => SetVariablesVisible(on));
            m_Panels.Add(PanelInspector, Localizer.UI("ui.inspector", "Inspector"),
                Localizer.UI("ui.inspectorPaneTip", "Inspector overlay — appears while a node is selected"),
                false, on => SetInspectorEnabled(on));
            m_Panels.Add("data", Localizer.UI("ui.dataWindow", "Data"),
                Localizer.UI("ui.dataWindowTip", "Open the data window for this module"),
                false, _ => DataEditorWindow.Open(
                    string.IsNullOrEmpty(m_ModuleFilter) ? m_Asset?.module : m_ModuleFilter, m_Asset));
            m_Panels.Add("find", Localizer.UI("ui.find", "Find"),
                Localizer.UI("ui.findTip", "Find a node in this graph"),
                false, _ => FindDialog.Open(m_Canvas));
            bar.Add(m_Panels);

            // 三个句点而不是 U+22EF：编辑器字体没有那个码位，真机上是个空方框。
            var more = AppBar.CommandButton("···", null, Localizer.UI("ui.more", "More"), null);
            more.clicked += () => OpenOverflowMenu(more);
            bar.Add(more);

            m_Status = new StatusChip(JumpToNextProblem);
            bar.Add(m_Status);
            RefreshStatus();

            return bar;
        }

        // 切换器弹层：搜索 + 本模块的图 + 新建/定位/删除。整条左栏的职责都收在这里。
        Popover OpenGraphPicker(VisualElement anchor)
        {
            return Popover.Open(anchor, 292f, popover =>
            {
                var list = new GraphListPane(m_ModuleFilter);
                // 在列表里点选一个图/对话组 → 入栈导航历史并加载。
                list.OnSelected = a => { if (a != m_Asset) { m_Nav.Push(a); LoadGraphFromUserSelection(a); } };
                // 列表里删除了一张图 → 若删的正是当前打开的图，换载替补（同模块的下一张；replacement 为 null=已无图则清空画布）。
                // 删别的图不影响当前画布。判据：DeleteAsset 会销毁内存对象，被删的正是当前图时 m_Asset 经 Unity 重载的 ==
                // 比较即为 null；删的是别的图则 m_Asset 仍存活 → 不动画布。换载走 LoadGraph（不入导航历史，属"被动替补"非主动跳转）。
                list.OnDeleted = replacement => { if (m_Asset == null) LoadGraph(replacement); };
                list.OnRequestClose = Popover.CloseAll;
                list.SetCurrent(m_Asset);
                popover.Add(list);
            });
        }

        // 「⋯」溢出菜单：一年动一次的全局设置（主题 / 语言）和低频画布命令，从主栏挪进来。
        void OpenOverflowMenu(VisualElement anchor)
        {
            if (Popover.IsOpenFor(anchor)) { Popover.CloseAll(); return; }
            Popover.Open(anchor, 200f, menu =>
            {
                menu.Add(Popover.MenuRow(Localizer.UI("ui.darkTheme", "Dark"), EditorUi.DarkTheme,
                    () => { EditorUi.DarkTheme = !EditorUi.DarkTheme; Popover.CloseAll(); }));
                menu.Add(Popover.MenuRow(Localizer.UI("ui.minimap", "MiniMap"), MiniMapOn,
                    () => { SetMiniMapVisible(!MiniMapOn); Popover.CloseAll(); }));
                menu.Add(Popover.Separator());
                menu.Add(Popover.MenuRow(Localizer.UI("ui.tidy", "Tidy"), false,
                    () => { Popover.CloseAll(); m_Canvas?.TidyLayout(); }));
                menu.Add(Popover.MenuRow(Localizer.UI("ui.frameAll", "Frame all"), false,
                    () => { Popover.CloseAll(); m_Canvas?.FrameAll(); }));
                menu.Add(Popover.Separator());
                menu.Add(Popover.Section(Localizer.UI("ui.language", "Language")));
                menu.Add(Popover.MenuRow("English", Localizer.Lang == Language.English,
                    () => { Popover.CloseAll(); SetLanguage(Language.English); }));
                menu.Add(Popover.MenuRow("中文", Localizer.Lang == Language.Chinese,
                    () => { Popover.CloseAll(); SetLanguage(Language.Chinese); }));
            });
        }

        void SetLanguage(Language language)
        {
            var config = EditorLocalizationLocator.Config();
            if (config == null || config.language == language) return;
            Undo.RegisterCompleteObjectUndo(config, "Change Editor Language");
            config.language = language;
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            EditorLocalizationLocator.Invalidate();
            RebuildAndReload();   // 整窗重建，让全部文案即时换语言
        }

        // ---- 浮层 ------------------------------------------------------------

        void BuildOverlays()
        {
            m_VariablesOverlay = new OverlayPanel("variables", Localizer.UI("ui.variables", "Variables"),
                OverlayPanel.Corner.TopLeft, 244f);
            m_VariablesOverlay.Body.Add(m_Variables);
            m_VariablesOverlay.OnCloseRequested = () => SetVariablesVisible(false);
            m_Canvas.AddOverlay(m_VariablesOverlay);

            m_InspectorOverlay = new OverlayPanel("inspector", Localizer.UI("ui.inspector", "Inspector"),
                OverlayPanel.Corner.TopRight, 288f);
            m_InspectorOverlay.Body.Add(m_Inspector);
            m_InspectorOverlay.OnCloseRequested = () => SetInspectorEnabled(false);
            m_Canvas.AddOverlay(m_InspectorOverlay);

            m_Dock = new CanvasDock(
                () => m_Canvas?.FrameAll(),
                () => m_Canvas?.TidyLayout(),
                SetMiniMapVisible,
                AddNodeAtCursor);
            m_Canvas.AddOverlay(m_Dock);

            // 还原上次会话的显隐。检视浮层的"开"只是启用意图（真正露面还要有选中节点），
            // 因此它的状态存在窗口自己的 pref 里 —— 存进浮层的话，每次取消选中都会把意图冲成 false。
            m_Panels.SetOn(PanelVariables, m_VariablesOverlay.RestoreVisible(true));
            m_InspectorEnabled = EditorPrefs.GetBool(InspectorEnabledPref, true);
            m_Panels.SetOn(PanelInspector, m_InspectorEnabled);
            SyncInspectorVisibility();

            var miniMap = EditorPrefs.GetBool(MiniMapPref, false);
            m_Canvas.SetMiniMapVisible(miniMap);
            m_Dock.SetMiniMapOn(miniMap);
            m_Dock.SetZoom(m_Canvas.ZoomScale);
        }

        void SetVariablesVisible(bool visible)
        {
            if (m_VariablesOverlay != null) m_VariablesOverlay.Visible = visible;
            m_Panels?.SetOn(PanelVariables, visible);
        }

        void SetInspectorEnabled(bool enabled)
        {
            m_InspectorEnabled = enabled;
            EditorPrefs.SetBool(InspectorEnabledPref, enabled);
            m_Panels?.SetOn(PanelInspector, enabled);
            SyncInspectorVisibility();
        }

        // 检视浮层 = 启用 且 有选中节点。这条是本次重构的核心：旧外壳为一句"选中一个节点即可在此编辑"
        // 常驻 320px；现在没选中就整块不在，画布拿回整幅宽度。
        void SyncInspectorVisibility()
        {
            m_InspectorOverlay?.SetDisplayed(m_InspectorEnabled && m_SelectedNode != null);
        }

        bool MiniMapOn => m_Canvas != null && m_Canvas.MiniMapVisible;

        void SetMiniMapVisible(bool visible)
        {
            m_Canvas?.SetMiniMapVisible(visible);
            m_Dock?.SetMiniMapOn(visible);
            EditorPrefs.SetBool(MiniMapPref, visible);
        }

        // 空格 / 右键 / 画布坞的「＋」共用同一个添加节点入口（面板坐标 + 窗口屏幕原点 = 弹窗屏幕位置）。
        void AddNodeAtCursor()
        {
            if (m_Canvas == null) return;
            AddNodeSearchWindow.Open(position.position + m_LastPanelMouse, this, m_Canvas);
        }

        // ---- 状态 ------------------------------------------------------------

        // 状态 chip：本图有几个错误/警告。没有问题时说「无问题」，不留空。
        void RefreshStatus()
        {
            if (m_Status == null) return;
            int errors = m_Debugger != null ? m_Debugger.ErrorCount : 0;
            int warnings = m_Debugger != null ? m_Debugger.WarnCount : 0;
            m_Status.SetCounts(errors, warnings, m_Asset != null);
            if (errors + warnings == 0) m_ProblemCursor = 0;
        }

        // 点状态 chip：逐个跳到出问题的节点（选中 + 框住）。只报数字不指路等于让人自己去找红边。
        void JumpToNextProblem()
        {
            var problems = m_Debugger?.ProblemInstanceIds;
            if (problems == null || problems.Count == 0 || m_Canvas == null) return;
            for (int i = 0; i < problems.Count; i++)
            {
                var index = (m_ProblemCursor + i) % problems.Count;
                if (!m_Canvas.FocusInstance(problems[index])) continue;
                m_ProblemCursor = (index + 1) % problems.Count;
                return;
            }
        }

        void SyncPickerLabel()
        {
            if (m_Picker == null) return;
            m_Picker.Text = m_Asset != null ? m_Asset.name : Localizer.UI("ui.noGraphOpen", "No graph");
        }

        void UpdateNavigationButtons()
        {
            m_BackButton?.SetEnabled(m_Nav.CanBack);
            m_ForwardButton?.SetEnabled(m_Nav.CanForward);
        }

        // 重建整个窗口 UI（按当前语言重新本地化全部界面），并还原已打开的图。用于语言切换。
        void RebuildAndReload()
        {
            Popover.CloseAll();
            rootVisualElement.Clear();
            CreateGUI();
        }

        void OnCrumbClicked(int depth)
        {
            var target = m_Nav.ClimbTo(depth);
            if (target != null) LoadGraph(target);
        }
    }
}
