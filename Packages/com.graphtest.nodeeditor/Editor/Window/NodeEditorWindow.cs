// NodeEditorWindow.cs — 第 5 层（连线图编辑器），模板层。
// 四面板 EditorWindow（画布 + 检视器 + 变量面板 + 工具栏），
// 布局借鉴自 Behavior Designer。Unity 6。Editor/ 程序集。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
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
        LayeredVariablePane m_Variables;   // 分层变量面板：按当前图显示 全局/模块/组 三档（取代单档 VariablePane）
        GraphListPane m_GraphList;     // 左侧"图/对话组"可滚动列表
        ObjectField m_GraphField;      // 工具栏的对象选择框（与左侧列表双向同步）
        Breadcrumb m_Breadcrumb;
        NavigationHistory m_Nav = new();
        ToolbarButton m_BackButton;
        ToolbarButton m_ForwardButton;

        [SerializeField] NodeGraphAsset m_Asset;   // [SerializeField] 使已打开的 graph 能在 domain reload（进入播放模式）后保留
        // 模块模式：从某个领域入口（如 Tools/NodeGraph/Dialogue）打开时非空 —— 左侧图列表只列该模块的图、
        // 隐藏工具栏对象选择框（避免跨模块乱切）。领域内仍可在本模块的多张图间切换（列表 + 导航/面包屑保留）。
        // 从 NodeGraph Manager 的 Open Node Editor 打开则为空（自由模式：列出全部模块、保留对象框）。[SerializeField] 使其扛过
        // domain reload，避免重载后悄悄回到自由模式。框架只认这个字符串、不认任何领域语义；"锁哪个模块 / 叫
        // 什么名"由领域入口决定（机制/策略分层，见 OpenModule）。
        [SerializeField] string m_ModuleFilter;
        NodeRegistry m_Registry;
        BlackboardSet m_Blackboard;            // 当前图的有效黑板（全局⊕模块⊕组）：检视面板「键」下拉 + 调试器校验都读它

        public static void Open()
        {
            var w = GetWindow<NodeEditorWindow>();
            w.UpdateWindowTitle();
            w.minSize = new Vector2(900, 500);
        // 从 NodeGraph Manager 的 Open Node Editor 入口打开 = 自由模式：若此前被某个领域入口锁到某模块，这里清掉过滤并按自由布局重建。
            if (!string.IsNullOrEmpty(w.m_ModuleFilter)) { w.m_ModuleFilter = null; w.RebuildAndReload(); }
        }

        // 领域入口用的"模块模式打开"机制（框架层，领域无关）。领域层（如对话）在自己的 Editor 程序集里调它，
        // 把窗口锁到自己的模块上并给一个本地化标题 —— 左侧只列该模块的图、隐藏工具栏对象框，但本模块内仍可
        // 多图切换。"锁哪个模块 / 初始打开哪张图 / 叫什么名"是领域策略，本方法只负责"按模块过滤布局重建并加载"。
        // 框架只提供按模块打开的机制，具体模块、标题与初始资产由领域层传入。
        public static void OpenModule(string module, string title = null, NodeGraphAsset initial = null)
        {
            if (string.IsNullOrEmpty(module)) return;
            var w = GetWindow<NodeEditorWindow>();
            w.titleContent = new GUIContent(string.IsNullOrEmpty(title) ? ModuleEditorTitle(module) : title);
            w.minSize = new Vector2(900, 500);
            w.m_ModuleFilter = module;
            w.m_Asset = RuntimeGraphLocator.FindActiveGraph(module, initial);
            w.RebuildAndReload();  // 按模块过滤布局重建（图列表只列本模块、无对象选择框）
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
            StopRuntimePoll();
            DetachRuntimeBinding();   // 若窗口在仍处于播放模式时被关闭，则解除对 EditorApplication.update 的挂接
        }

        void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode) StartRuntimePoll();
            else if (s == PlayModeStateChange.ExitingPlayMode) { StopRuntimePoll(); DetachRuntimeBinding(); }
        }

        // runner 可能晚于 EnteredPlayMode 注册，窗口也会在运行中切图/关闭重开。
        // 因此 play 期间保持一个有界 update 轮询：按当前资产匹配，变化时解绑/重绑；退出 play/关窗才拆除。
        bool m_Polling;
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
                if (reportedGraph != null && reportedGraph != m_Asset)
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

                if (reportedGraph == null)
                {
                    var activeGraph = RuntimeGraphLocator.FindActiveGraph(m_RuntimeGraphCandidates, m_Asset);
                    if (activeGraph != null && activeGraph != m_Asset)
                    {
                        LoadGraph(activeGraph);
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
            // 这会将这个普通的非序列化字段置空；在工具栏接入用到它的回调之前重新初始化。
            m_Nav ??= new NavigationHistory();
            var root = rootVisualElement;

            root.Add(BuildToolbar());

            m_Breadcrumb = new Breadcrumb(OnCrumbClicked);
            root.Add(m_Breadcrumb);   // 面包屑服务多图导航；自由 / 模块模式都可在多图间切换，故两种模式都挂它

            // outer：[ inner ] | inspector（右）
            var outer = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Horizontal);
            // inner：leftColumn（左） | canvas。leftColumn 内再上下竖切：图列表 / 变量。
            var inner = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);

            m_Variables = new LayeredVariablePane();
            m_Canvas = new GraphCanvas();
            m_Debugger = new GraphDebugger(m_Canvas);
            m_Inspector = new InspectorPane();

            // 左列上下竖切 —— 图/对话组列表（可切换） + 变量面板。模块模式下列表只列该模块的图（传入过滤），
            // 自由模式列出全部模块；两种模式都能在（本模块的）多张图间切换。
            var leftColumn = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Vertical);
            m_GraphList = new GraphListPane(m_ModuleFilter);
            // 在列表里点选一个图/对话组 → 入栈导航历史并加载（与工具栏选择框走同一条路径）。
            m_GraphList.OnSelected = a => { if (a != m_Asset) { m_Nav.Push(a); LoadGraph(a); } };
            // 列表里删除了一张图 → 若删的正是当前打开的图，换载替补（同模块的下一张；replacement 为 null=已无图则清空画布）。
            // 删别的图不影响当前画布。判据：DeleteAsset 会销毁内存对象，被删的正是当前图时 m_Asset 经 Unity 重载的 ==
            // 比较即为 null；删的是别的图则 m_Asset 仍存活 → 不动画布。换载走 LoadGraph（不入导航历史，属"被动替补"非主动跳转）。
            m_GraphList.OnDeleted = replacement => { if (m_Asset == null) LoadGraph(replacement); };
            leftColumn.Add(m_GraphList);
            leftColumn.Add(m_Variables);
            inner.Add(leftColumn);

            m_Canvas.OnNodeSelected = node => m_Inspector.Show(node, m_Registry, m_Blackboard, m_Asset);
            m_Canvas.OnGraphChanged += () => m_Debugger.RevalidateAndPaint();   // 每次编辑都重新校验（RevalidateAndPaint 会处理 asset 为 null 的情况）
            // 右键空白画布 → 在光标处打开"添加节点"搜索框（与空格键同一入口）。
            // 画布只知道面板坐标，窗口的屏幕原点（position.position）在窗口手里，故由窗口注入面板→屏幕换算。
            m_Canvas.OnRequestAddNode = screenPos => AddNodeSearchWindow.Open(screenPos, this, m_Canvas);
            m_Canvas.PanelToScreen = panelPos => position.position + panelPos;
            // Inspector 的可搜索下拉同样要把字段（面板坐标）换成屏幕坐标来弹 SearchWindow。
            m_Inspector.PanelToScreen = panelPos => position.position + panelPos;

            inner.Add(m_Canvas);
            outer.Add(inner);
            outer.Add(m_Inspector);
            root.Add(outer);

            // 在面板空间内跟踪光标——KeyDownEvent 不携带鼠标位置，
            // 因此空格键处理器改为读取最近一次的指针位置。
            m_Canvas.RegisterCallback<PointerMoveEvent>(e => m_LastPanelMouse = e.position);

            // 空格键在光标处打开添加节点的搜索对话框
            m_Canvas.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Space)
                {
                    // 面板空间鼠标位置 + 窗口的屏幕原点 = 弹窗的屏幕位置。
                    var screenPos = position.position + m_LastPanelMouse;
                    AddNodeSearchWindow.Open(screenPos, this, m_Canvas);
                    e.StopPropagation();
                }
            });

            // 在 domain reload 之后 m_Asset 会保留（[SerializeField]），但 m_Registry/m_Blackboard 不会——
            // 走 LoadGraph 重新解析它们，而非 ReloadCanvas（后者会使用为 null 的 locator）。
            if (m_Asset != null) LoadGraph(m_Asset);

            // 如果窗口是在已处于播放模式时被（重新）构建的——例如在进入播放的 domain reload 期间，
            // EnteredPlayMode 时机点先于本次 CreateGUI 触发——则补做运行时挂接。
            if (Application.isPlaying) StartRuntimePoll();
        }

        public void LoadGraph(NodeGraphAsset asset) => LoadGraph(asset, null);

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
                if (runtime == null) PollForRuntime();
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
            // 把"当前打开的图"同步到工具栏选择框与左侧列表高亮（不触发各自回调，避免重入加载）。
            m_GraphField?.SetValueWithoutNotify(m_Asset);
            m_GraphList?.SetCurrent(m_Asset);
        }

        VisualElement BuildToolbar()
        {
            var bar = new Toolbar();
            bar.AddToClassList(EditorUi.ToolbarClass);

            // 导航组（后退/前进）：两种模式都保留——模块模式也能在本模块的多张图间切换并回溯历史。
            var back = new ToolbarButton(() => { if (m_Nav.CanBack) LoadGraph(m_Nav.Back()); });
            ApplyIconButton(back, "d_tab_prev", Localizer.UI("ui.back", "Back"), "<");
            var fwd  = new ToolbarButton(() => { if (m_Nav.CanForward) LoadGraph(m_Nav.Forward()); });
            ApplyIconButton(fwd, "d_tab_next", Localizer.UI("ui.forward", "Forward"), ">");
            m_BackButton = back;
            m_ForwardButton = fwd;
            UpdateNavigationButtons();
            bar.Add(back); bar.Add(fwd);
            bar.Add(MakeSep());

            m_GraphField = null;   // 模块模式不建对象框；显式清掉上一次自由布局缓存的引用，避免 ReloadCanvas 戳到已脱树的旧控件
            // 工具栏对象框（可切到项目里任意图，含别的模块）只在自由模式出现；模块模式靠左侧本模块列表切换，不给跨模块乱切的入口。
            if (string.IsNullOrEmpty(m_ModuleFilter))
            {
                // Asset 组
                m_GraphField = new ObjectField { objectType = typeof(NodeGraphAsset), value = m_Asset };
                m_GraphField.AddToClassList("toolbar-graphfield");
                m_GraphField.RegisterValueChangedCallback(e =>
                {
                    if (e.newValue is NodeGraphAsset a) { m_Nav.Push(a); LoadGraph(a); }
                });
                bar.Add(m_GraphField);

                bar.Add(MakeSep());
            }

            // Tools 组
            var find = new ToolbarButton(() => FindDialog.Open(m_Canvas));
            ApplyIconButton(find, "Search Icon", Localizer.UI("ui.find", "Find"), Localizer.UI("ui.find", "Find"));
            bar.Add(find);

            // 数据按钮：打开通用数据编辑窗口，绑定当前图、过滤到当前模块（+ 项目级数据）。
            // 模块来源是领域无关的：模块模式用已锁定的 m_ModuleFilter，自由模式用当前图的 module
            // 标签（皆为空则传 null = 总数据中心）。框架 shell 不得出现任何领域字符串字面量（B5/§2）。
            var data = new ToolbarButton(() => DataEditorWindow.Open(
                string.IsNullOrEmpty(m_ModuleFilter) ? m_Asset?.module : m_ModuleFilter, m_Asset));
            ApplyIconButton(data, "d_SceneViewTools", Localizer.UI("ui.dataWindow", "Data"), Localizer.UI("ui.dataWindow", "Data"));
            bar.Add(data);

            // 缩略图开关：默认关（缩略图默认隐藏）。回调在点击时才读 m_Canvas（此时 CreateGUI 已建好它，
            // 工具栏是在 m_Canvas 之前构建的，故不能在构建期引用），勾选即显、取消即隐。
            var miniMapToggle = new ToolbarToggle { text = Localizer.UI("ui.minimap", "MiniMap"), tooltip = Localizer.UI("ui.minimapTip", "Toggle minimap") };
            EditorUi.ApplyToolbarToggle(miniMapToggle);
            miniMapToggle.SetValueWithoutNotify(false);
            miniMapToggle.RegisterValueChangedCallback(e => m_Canvas?.SetMiniMapVisible(e.newValue));
            bar.Add(miniMapToggle);

            // 所有窗口复用同一主题设置与同步控件。
            bar.Add(EditorUi.CreateThemeToggle());

            bar.Add(MakeSep());

            // 语言下拉：切换编辑器显示语言，写回 EditorLocalizationConfig 并重建窗口以即时本地化全部 UI。
            var cfg = EditorLocalizationLocator.Config();
            var langNames = new System.Collections.Generic.List<string> { "English", "中文" };
            int curIdx = (cfg != null && cfg.language == Language.Chinese) ? 1 : 0;
            // 有限固定值 → 走共享 EnumDropdownField（原生枚举下拉，规范 §1）。
            var langPopup = new EnumDropdownField(null, langNames, langNames[curIdx], v =>
            {
                var c = EditorLocalizationLocator.Config();
                if (c == null) return;
                var language = v == "中文" ? Language.Chinese : Language.English;
                if (c.language == language) return;
                Undo.RegisterCompleteObjectUndo(c, "Change Editor Language");
                c.language = language;
                EditorUtility.SetDirty(c); AssetDatabase.SaveAssets();
                EditorLocalizationLocator.Invalidate();
                RebuildAndReload();
            }, tooltip: Localizer.UI("ui.language", "Language"));
            langPopup.AddToClassList("toolbar-graphfield");
            bar.Add(langPopup);

            return bar;
        }

        void UpdateNavigationButtons()
        {
            m_BackButton?.SetEnabled(m_Nav.CanBack);
            m_ForwardButton?.SetEnabled(m_Nav.CanForward);
        }

        // 重建整个窗口 UI（按当前语言重新本地化全部界面），并还原已打开的图。用于语言切换。
        void RebuildAndReload()
        {
            rootVisualElement.Clear();
            CreateGUI();
        }

        // 将工具栏按钮样式化为内置图标按钮；若图标无法解析，则回退到原始文本字形
        // （纯视觉回退，不改变行为）。
        static void ApplyIconButton(ToolbarButton btn, string iconName, string tooltip, string fallbackText)
        {
            btn.tooltip = tooltip;
            var content = EditorGUIUtility.IconContent(iconName);
            if (content != null && content.image != null)
            {
                EditorUi.ApplyToolbarIconButton(btn);
                var img = new Image { image = content.image, scaleMode = ScaleMode.ScaleToFit };
                img.AddToClassList(EditorUi.ToolbarIconClass);
                btn.Add(img);
            }
            else
            {
                EditorUi.ApplyToolbarTextButton(btn);
                btn.text = fallbackText;
            }
        }

        static VisualElement MakeSep()
        {
            var sep = new VisualElement();
            sep.AddToClassList("toolbar-sep");
            return sep;
        }

        void OnCrumbClicked(int depth)
        {
            var target = m_Nav.ClimbTo(depth);
            if (target != null) LoadGraph(target);
        }
    }
}
