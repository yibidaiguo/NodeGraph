// GraphCanvasView.cs —— 第 5 层（连线图编辑器），模板级。
// GraphView 画布适配器：业务层不直接依赖 GraphView，统一经这里的封装类型交互。
// 所有功能模块都只与这些封装类型交互，绝不直接与 GraphView 打交道。
// 消费第 4 层数据类型：NodeGraphAsset、NodeInstance、NodeDefinition、Connection、PortDef、TypeRef、NodeRegistry。
// Unity 6（2023+）。放置于某个 Editor/ 程序集下。

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;   // 仅限于此适配器文件使用
using UnityEngine;
using UnityEngine.UIElements;
using NodeEditor;          // 第 4 层数据/运行时类型（NodeDefinition、NodeGraphAsset、……）

namespace NodeEditor.EditorUI
{
    // ---- 画布 -------------------------------------------------------------
    public class GraphCanvas : GraphView
    {
        public System.Action<NodeView> OnNodeSelected;
        public System.Action OnGraphChanged;   // 在任何结构性编辑（连接/移除/添加）后触发，以便 5d 重新校验
        // 右键空白画布请求"添加节点"：入参为屏幕坐标，由窗口接到 AddNodeSearchWindow.Open（与空格键同一入口）。
        public System.Action<Vector2> OnRequestAddNode;
        // 面板坐标→屏幕坐标换算：画布只知道面板坐标，窗口的屏幕原点在窗口手里，故由窗口注入。
        public System.Func<Vector2, Vector2> PanelToScreen;
        public NodeGraphAsset Asset { get; private set; }
        public NodeRegistry Registry { get; private set; }

        readonly Dictionary<string, NodeView> m_Views = new();   // instanceId -> view 视图
        Label m_Banner;     // 画布内承载"图级校验问题"的横幅（替代刷屏 console）
        MiniMap m_MiniMap;  // 缩略图：默认隐藏、可拖动，由工具栏的 MiniMap 开关控制显隐

        public GraphCanvas()
        {
            style.flexGrow = 1;
            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // 缩略图默认隐藏（不再遮挡左上角入口节点）；anchored=false 让它可被拖动，
            // 显隐由工具栏的 MiniMap 开关切换——这样它"消除得掉"，需要时再打开。
            m_MiniMap = new MiniMap { anchored = false };
            m_MiniMap.SetPosition(new Rect(12, 320, 200, 140));
            m_MiniMap.style.display = DisplayStyle.None;
            Add(m_MiniMap);

            // 图级校验横幅 —— 固定在画布顶部的覆盖层。图范围的问题
            //（如 "no single entry"）没有具体节点可标记，因此在此处呈现，而不是在每次加载/编辑时反复
            // Debug.LogWarning（那会在正常编排过程中污染控制台）。
            m_Banner = new Label { name = "graph-banner" };
            m_Banner.AddToClassList(EditorUi.BannerClass);
            m_Banner.AddToClassList(EditorUi.BannerIssueClass);
            m_Banner.style.position = Position.Absolute;
            m_Banner.style.top = 0; m_Banner.style.left = 0; m_Banner.style.right = 0;
            m_Banner.style.display = DisplayStyle.None;
            Add(m_Banner);

            var styleSheet = Resources.Load<StyleSheet>("NodeEditorStyles");
            if (styleSheet != null) styleSheets.Add(styleSheet);
            EditorUi.BindTheme(this);

            graphViewChanged = OnGraphViewChanged;

            // Delete/Backspace 键删除选中（连线 + 非钉住节点）。GraphView 的删除"命令"只有在
            // deleteSelection 委托被赋值时才生效——不设它，按 Delete 键什么都不会发生（连线尤其明显，
            // 因为右键菜单的"删除"走的是 DeleteSelection() 方法，能删节点，让人误以为删除是通的）。
            // 这里把命令接到内置 DeleteSelection()：它会经 graphViewChanged → OnGraphViewChanged 更新数据，
            // 并自动尊重 pinned 节点被剥掉的 Deletable 能力。
            deleteSelection = (operationName, askUser) => DeleteSelection();

            // 复制/粘贴（Ctrl+C/V/D）经由这三个委托处理；GraphView 将它们绑定到标准快捷键。
            serializeGraphElements = SerializeSelection;
            canPasteSerializedData = CanPasteSerializedData;
            unserializeAndPaste = UnserializeAndPaste;

            // 撤销/重做会恢复资产的序列化数据（通过下文每次编辑前所取的 RegisterCompleteObjectUndo 快照），
            // 但其本身不会对活动的 GraphView 视觉树做任何处理 ——
            // 因此每次撤销/重做时都从资产重建视觉树，使画布反映 Unity 恢复后的状态。
            // 仅在挂载到 panel 期间订阅，这样关闭窗口时不会泄漏静态处理器。
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedoPerformed);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedoPerformed);
        }

        void OnUndoRedoPerformed()
        {
            if (Asset == null) return;
            Load(Asset, Registry);
            OnGraphChanged?.Invoke();
        }

        // 在画布内横幅中显示图级校验信息（empty/null 则隐藏它）。由 debugger
        // 在每次重新校验后调用，使这些问题无需控制台警告即可可见。
        public void SetBanner(IReadOnlyList<string> messages)
        {
            if (m_Banner == null) return;
            if (messages == null || messages.Count == 0)
            {
                m_Banner.text = string.Empty;
                m_Banner.style.display = DisplayStyle.None;
                return;
            }
            m_Banner.text = string.Join("\n", messages);
            m_Banner.style.display = DisplayStyle.Flex;
        }

        // 显示/隐藏缩略图（由工具栏的 MiniMap 开关调用）。当前是否可见见 MiniMapVisible。
        public void SetMiniMapVisible(bool show)
        {
            if (m_MiniMap != null) m_MiniMap.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
        public bool MiniMapVisible => m_MiniMap != null && m_MiniMap.style.display != DisplayStyle.None;

        public VisualElement CreateGroup(string title, IEnumerable<NodeView> nodes)
        {
            var group = new Group { title = title };
            group.AddToClassList("node-group");
            foreach (var node in nodes) group.AddElement(node);
            AddElement(group);
            return group;
        }

        public void Load(NodeGraphAsset asset, NodeRegistry registry)
        {
            Asset = asset; Registry = registry;
            // 关键：Load 是"程序化重建视图"。GraphView.DeleteElements 会触发 graphViewChanged 回调，
            // 而 OnGraphViewChanged 会把"被删的 NodeView"当成用户删节点 → 从 Asset.instances 里移除，
            // 于是每次重载（刷新/撤销/重新选图/重新打开）都把整张图清空并存盘（节点"刷新就没了"）。
            // 重建期间先摘掉回调，结束再装回，让 DeleteElements/AddElement 不会改动 Asset 数据。
            var savedHandler = graphViewChanged;
            graphViewChanged = null;
            try
            {
                DeleteElements(graphElements.ToList());
                m_Views.Clear();
                if (asset == null || registry == null) return;   // 没有 registry 就无法解析 definition

                foreach (var inst in asset.instances)
                {
                    var def = registry.Find(inst.definitionId);
                    if (def == null) continue;
                    if (!NodeAdmission.Evaluate(asset, def).allowed) continue;
                    var view = new NodeView(inst, def);
                    view.OnSelectedCallback = () => OnNodeSelected?.Invoke(view);
                    view.SetPosition(new Rect(inst.position, Vector2.zero));
                    AddElement(view);
                    m_Views[inst.instanceId] = view;
                }
                // 待所有节点存在后再创建连线
                foreach (var inst in asset.instances)
                    foreach (var c in inst.connections)
                        CreateEdgeView(inst, c);
            }
            finally { graphViewChanged = savedHandler; }
        }

        void CreateEdgeView(NodeInstance from, Connection c)
        {
            if (!m_Views.TryGetValue(from.instanceId, out var fromView)) return;
            if (!m_Views.TryGetValue(c.toInstanceId, out var toView)) return;
            var outPort = fromView.OutputPort(c.fromPort);
            var inPort = toView.InputPort(c.toPort);
            if (outPort == null || inPort == null) return;
            var edge = outPort.ConnectTo<EdgeView>(inPort);
            edge.Model = c;
            edge.AddManipulator(new EdgeManipulator());   // 允许日后拖动端点重连/改向（见 OnGraphViewChanged 的重连分支）
            AddElement(edge);
        }

        // 右键上下文菜单：空白画布上加一条"添加节点"（命中节点/端口/连线时让基类给出 复制/删除 等）。
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var ve = evt.target as VisualElement;
            bool onElement = ve != null && (ve is Node || ve is Edge
                          || ve.GetFirstAncestorOfType<Node>() != null
                          || ve.GetFirstAncestorOfType<Edge>() != null);
            if (!onElement && OnRequestAddNode != null)
            {
                evt.menu.AppendAction(Localizer.UI("ui.addNode", "Add Node"), a =>
                {
                    // a.eventInfo.mousePosition 是面板坐标；经窗口注入的 PanelToScreen 换成屏幕坐标，
                    // 再走与空格键加节点完全相同的 AddNodeSearchWindow.Open(screenPos…)。
                    var screen = PanelToScreen != null ? PanelToScreen(a.eventInfo.mousePosition) : a.eventInfo.mousePosition;
                    OnRequestAddNode(screen);
                }, DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendSeparator();
            }
            base.BuildContextualMenu(evt);   // 保留 GraphView 默认的 复制/粘贴/复制副本/删除
        }

        // 经类型检查 + 连接规则的连接（第 3 层类型匹配 + 节点种类 include/exclude 规则，实时）。
        // 被规则否决的端口不进入候选集 => 策划根本拖不上去（与 GraphValidator.CheckConnectionRules 同一规则源）。
        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter _)
        {
            var sv = start as PortView;
            if (sv == null) return new List<Port>();
            var startDef = (start.node as NodeView)?.Definition;
            if (!NodeAdmission.Evaluate(Asset, startDef).allowed)
                return new List<Port>();
            bool startIsOutput = start.direction == Direction.Output;
            return ports.ToList().Where(p =>
            {
                var pv = p as PortView;
                if (pv == null || pv.direction == start.direction || pv.node == start.node) return false;
                if (!TypeRefCompat.Compatible(sv.PortType, pv.PortType)) return false;
                // 边永远 output->input：按 start 的方向定出 from/to 两端，再问连接规则。
                var otherDef = (pv.node as NodeView)?.Definition;
                if (!NodeAdmission.Evaluate(Asset, otherDef).allowed) return false;
                var fromDef  = startIsOutput ? startDef : otherDef;
                var fromPort = startIsOutput ? sv.PortName : pv.PortName;
                var toDef    = startIsOutput ? otherDef : startDef;
                var toPort   = startIsOutput ? pv.PortName : sv.PortName;
                return ConnectionRules.Evaluate(fromDef, fromPort, toDef, toPort).allowed;
            }).ToList();
        }

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (Asset == null) return change;

            // 防御：钉住的固定节点永不删除。NodeView 已去掉其 Deletable 能力（标准删除路径不会收集它），
            // 这里再把任何混入 elementsToRemove 的 pinned 节点剔除——返回裁剪后的 change，GraphView 连视图带数据都保留。
            change.elementsToRemove?.RemoveAll(e => e is NodeView nv && nv.Instance.pinned);

            // 注意：新建/重连"连线"不经过这里。GraphView 的 EdgeConnector / EdgeManipulator 在松手于合法端口时
            // 调用的是 IEdgeConnectorListener.OnDrop（见底部 EdgeConnectorListener → OnEdgeDropped）；
            // graphViewChanged 的 edgesToCreate 对"拖拽连线"并不填充。这里只处理"移动节点"和"删除节点/连线"。
            bool anyChange = (change.movedElements != null && change.movedElements.Count > 0)
                           || (change.elementsToRemove != null && change.elementsToRemove.Count > 0);
            // 在下文修改 instances/connections 之前先做快照，使 Ctrl+Z 能恢复到编辑前的资产状态。
            // 用 RegisterCompleteObjectUndo（而非 RecordObject）：删除会改变 instances/connections 列表长度，
            // 而 Unity 基于 diff 的 RecordObject 无法可靠地撤销数组长度变化 —— 完整对象快照则可以。
            if (anyChange) Undo.RegisterCompleteObjectUndo(Asset, "Graph Edit");

            if (change.movedElements != null)
                foreach (var moved in change.movedElements.OfType<NodeView>())
                    moved.Instance.position = moved.GetPosition().position;

            if (change.elementsToRemove != null)
                foreach (var el in change.elementsToRemove)
                {
                    if (el is EdgeView ev) RemoveConnection(ev);
                    else if (el is NodeView nv) RemoveInstance(nv);
                }

            if (anyChange) EditorUtility.SetDirty(Asset);
            // 仅在结构性编辑（删除节点/连线）时重新校验。纯移动不改变校验结果，每帧拖动都重算会刷屏
            // 图级警告（如 "no entry yet"）。新建/重连连线由 OnEdgeDropped 自行触发 OnGraphChanged。
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
                OnGraphChanged?.Invoke();
            return change;
        }

        void AppendConnection(Edge edge)
        {
            var fromView = edge.output.node as NodeView;
            var toView = edge.input.node as NodeView;
            if (fromView == null || toView == null) return;
            var outPv = edge.output as PortView;
            var inPv = edge.input as PortView;
            if (outPv == null || inPv == null) return;
            var conn = new Connection
            {
                fromPort = outPv.PortName,
                toInstanceId = toView.Instance.instanceId,
                toPort = inPv.PortName
            };
            fromView.Instance.connections.Add(conn);
            if (edge is EdgeView ev) ev.Model = conn;
        }

        // 连线拖拽的落点（IEdgeConnectorListener.OnDrop 的目的地）。EdgeConnector（从端口拉新线）与
        // EdgeManipulator（拖已有边的端点重连）在松手于合法端口时都走这里——这是"建立/重连连接"的唯一入口，
        // 不经 graphViewChanged。负责：写数据 + 标脏 + 记 Undo + 触发重新校验。
        public void OnEdgeDropped(Edge edge)
        {
            if (Asset == null) return;
            var ev = edge as EdgeView;
            bool reconnect = ev != null && ev.Model != null;   // 已带 Model = 拖动已有边的端点在改向（重连）
            Undo.RegisterCompleteObjectUndo(Asset, reconnect ? "Reconnect" : "Connect");
            if (reconnect) RemoveConnectionModel(ev.Model);     // 重连：先解除旧关系（可能挂在任意源节点上）
            AppendConnection(edge);                             // 建立/重建数据关系（会刷新 ev.Model）
            // 拖拽落点不会自动调用 Port.Connect——必须在这里补上。否则数据上连了、视觉上 Unity 仍认为端口未连接，
            // 连接点 cap 不点亮（"连上了但没亮"），要等下次重载经 CreateEdgeView/ConnectTo 才点亮。先把两端登记上再判单容量
            //（这样 keep 已在 port.connections 里，EnforceSingleCapacity 能把它和待挤掉的旧边区分开）。
            EnsurePortConnected(edge.output, edge);
            EnsurePortConnected(edge.input, edge);
            // 单连线 vs 多连线在此区分：Capacity.Single 的端口（源自 arity=Exactly/Optional）落上新边后，
            // 把它上面其余的旧边挤掉，形成"替换"语义；Multi 端口（arity=Many/AtLeast/Range>1）保留全部。两端各判一次。
            EnforceSingleCapacity(edge.output, edge);
            EnforceSingleCapacity(edge.input, edge);
            if (!reconnect && ev != null)
            {
                if (ev.parent == null) AddElement(ev);          // 兜底：候选边若尚未在画布里则补加（多数版本已在）
                ev.AddManipulator(new EdgeManipulator());       // 让这条新边今后也能拖端点重连
            }
            EditorUtility.SetDirty(Asset);
            OnGraphChanged?.Invoke();
        }

        void RemoveConnection(EdgeView ev)
        {
            var fromView = ev.output?.node as NodeView;
            if (fromView != null && ev.Model != null)
                fromView.Instance.connections.Remove(ev.Model);
        }

        // 按引用从拥有它的源节点里移除一条连接。重连时旧关系可能挂在任意源节点上
        //（拖动出端会换源节点），故全表查找而非只看某个 fromView。
        void RemoveConnectionModel(Connection model)
        {
            if (model == null || Asset == null) return;
            foreach (var inst in Asset.instances)
                if (inst.connections.Remove(model)) return;
        }

        // 把一条边登记进端口的连接表（这一步才会点亮端口的连接点 cap）。幂等：已在表里就跳过，
        // 避免重连时 Unity 的 EdgeManipulator 可能已连过、又被这里重复 Add 导致连接计数翻倍。
        static void EnsurePortConnected(Port port, Edge edge)
        {
            if (port == null || edge == null) return;
            foreach (var e in port.connections) if (e == edge) return;
            port.Connect(edge);
        }

        // 单连线端口的"替换"语义：当一条新边落在某 Capacity.Single 端口上时，把该端口上其余的旧边
        // （keep 之外）连数据带视图一并挤掉，使单连线端口始终只保留一条。Multi 端口直接返回、保留全部。
        // 端口容量由 PortView.Create 依 arity 推导（Exactly/Optional→Single，Many/AtLeast/Range>1→Multi）。
        void EnforceSingleCapacity(Port port, Edge keep)
        {
            if (port == null || port.capacity != Port.Capacity.Single) return;
            foreach (var e in port.connections.Where(x => x != keep).OfType<EdgeView>().ToList())
            {
                if (e.Model != null) RemoveConnectionModel(e.Model);   // 删数据关系
                e.input?.Disconnect(e); e.output?.Disconnect(e);       // 解除两端口的视图连接
                RemoveElement(e);                                      // 从画布移除旧边（程序化移除，不触发 graphViewChanged）
            }
        }

        void RemoveInstance(NodeView nv)
        {
            Asset.instances.Remove(nv.Instance);
            // 从 entry 列表中剔除被删除的 id，使删除入口节点后不会残留悬空的
            // entryInstanceId（CheckEntry 会将其标记为缺失实例错误）。来自
            // 其他节点的入边已经由 GraphView 删除时收集相连连线的机制移除（RemoveConnection）。
            Asset.entryInstanceIds.Remove(nv.Instance.instanceId);
            m_Views.Remove(nv.Instance.instanceId);
        }

        // 由"添加节点"对话框使用，在指定位置生成一个新节点。
        public NodeView CreateNode(NodeDefinition def, Vector2 graphPos)
        {
            if (Asset == null) return null;   // 未加载任何图（例如在打开图之前通过菜单打开了窗口）—— 无处可添加
            var availability = NodeAdmission.Evaluate(Asset, def);
            if (!availability.allowed)
            {
                Debug.LogWarning(availability.reason);
                return null;
            }
            // RegisterCompleteObjectUndo：向 instances 列表添加是一次数组长度变化，
            // RecordObject 的 diff 无法可靠地撤销（参见 OnGraphViewChanged）。
            Undo.RegisterCompleteObjectUndo(Asset, "Add Node");
            var inst = new NodeInstance { definitionId = def.Id, position = graphPos };
            Asset.instances.Add(inst);
            var view = new NodeView(inst, def);
            view.OnSelectedCallback = () => OnNodeSelected?.Invoke(view);
            view.SetPosition(new Rect(graphPos, Vector2.zero));
            AddElement(view);
            m_Views[inst.instanceId] = view;
            EditorUtility.SetDirty(Asset);
            OnGraphChanged?.Invoke();   // 添加节点同样是一次编辑期变更
            return view;
        }

        // ---- 复制 / 粘贴（Ctrl+C、Ctrl+V、Ctrl+D） -------------------------
        // GraphView 的内置快捷键经由这三个委托汇聚。剪贴板的编码/解码 + id 重映射
        // 逻辑位于 ClipboardCodec（纯粹、不依赖 GraphView），因此可单元测试；这些包装方法只负责
        // 在 GraphElements / 活动视图树 与 codec 的数据类型之间转换。
        string SerializeSelection(IEnumerable<GraphElement> elements)
            => ClipboardCodec.Serialize(elements.OfType<NodeView>().Select(v => v.Instance));

        // 'new'：有意隐藏 GraphView.CanPasteSerializedData。GraphView 是通过构造函数中赋值的
        // canPasteSerializedData 委托来调用我们的粘贴检查，而非通过此方法名，因此
        // 隐藏基类成员是刻意为之（并消除 CS0108 警告）。
        new bool CanPasteSerializedData(string data) => ClipboardCodec.CanPaste(data, Registry, Asset);

        void UnserializeAndPaste(string operationName, string data)
        {
            if (Asset == null || Registry == null) return;
            var pasted = ClipboardCodec.BuildPasted(data, Registry, Asset, new Vector2(30f, 30f));
            if (pasted.Count == 0) return;   // 剪贴板为空/无效，或没有解析到 definition

            // RegisterCompleteObjectUndo（而非 RecordObject）：粘贴会扩大 instances 列表，这是一次
            // RecordObject 的 diff 无法可靠撤销的数组长度变化。在修改列表之前取得，
            // 使 Ctrl+Z 能恢复到粘贴前的资产。
            Undo.RegisterCompleteObjectUndo(Asset, operationName);

            ClearSelection();
            var newViews = new List<NodeView>();
            foreach (var inst in pasted)
            {
                Asset.instances.Add(inst);
                var view = new NodeView(inst, Registry.Find(inst.definitionId));
                view.OnSelectedCallback = () => OnNodeSelected?.Invoke(view);
                view.SetPosition(new Rect(inst.position, Vector2.zero));
                AddElement(view);
                m_Views[inst.instanceId] = view;
                newViews.Add(view);
            }
            // 待所有粘贴的节点存在后再创建连线（connections 已重映射到新的 id 上）
            foreach (var inst in pasted)
                foreach (var c in inst.connections)
                    CreateEdgeView(inst, c);

            foreach (var v in newViews) AddToSelection(v);

            EditorUtility.SetDirty(Asset);
            OnGraphChanged?.Invoke();
        }
    }

    // ---- 节点视图 ----------------------------------------------------------
    public partial class NodeView : Node
    {
        static readonly CustomStyleProperty<Color> s_ShapeFill =
            new("--ne-node-shape-fill");
        static readonly CustomStyleProperty<Color> s_ShapeOutline =
            new("--ne-node-shape-outline");
        static readonly CustomStyleProperty<float> s_ShapeOutlineWidth =
            new("--ne-node-shape-outline-width");
        static readonly CustomStyleProperty<Color> s_ShapeHighlight =
            new("--ne-node-shape-highlight");
        static readonly CustomStyleProperty<Color> s_ShapeShadow =
            new("--ne-node-shape-shadow");
        static readonly CustomStyleProperty<Color> s_ShapeGlow =
            new("--ne-node-shape-glow");
        static readonly CustomStyleProperty<Color> s_SelectionOutline =
            new("--ne-node-selection-outline");
        static readonly CustomStyleProperty<Color> s_ValidationOutline =
            new("--ne-node-validation-outline");
        [System.ThreadStatic] static List<Vector2> s_RolePolygonScratch;
        [System.ThreadStatic] static List<Vector2> s_RoundedSampleScratch;
        [System.ThreadStatic] static List<Vector2> s_GradientUpperScratch;
        [System.ThreadStatic] static List<Vector2> s_GradientLowerScratch;

        public NodeInstance Instance { get; }
        public NodeDefinition Definition { get; }
        public System.Action OnSelectedCallback;

        readonly Dictionary<string, PortView> m_In = new();
        readonly Dictionary<string, PortView> m_Out = new();
        VisualElement m_ExtraContent;     // 用于每个节点的自定义视图（调试/个性化）
        IVisualElementScheduledItem m_HoverSchedule;   // 悬停满 1 秒后弹出 tooltip 的计划任务（离开/移除时取消）
        Color m_ShapeFill;
        Color m_ShapeOutline;
        Color m_ShapeHighlight;
        Color m_ShapeShadow;
        Color m_ShapeGlow;
        Color m_SelectionOutline;
        Color m_ValidationOutline;
        readonly NodeRole m_VisualRole;
        float m_ShapeOutlineWidth = 1f;

        public NodeView(NodeInstance inst, NodeDefinition def)
        {
            Instance = inst; Definition = def;
            // 标题优先级：备注 > 自定义名 > 定义的本地化名称（统一走 NodeInspectorEdits.ResolveTitle）。
            var resolvedTitle = NodeInspectorEdits.ResolveTitle(inst, def);
            title = resolvedTitle;

            var roleName = def.Role.ToString();
            var roleKey = roleName.ToLowerInvariant();
            var iconKind = NodeIconRegistry.Resolve(def.GetType(), def.Role);
            m_VisualRole = ResolveVisualRole(iconKind, def.Role);
            var visualRoleKey = m_VisualRole.ToString().ToLowerInvariant();
            AddToClassList("node-base");
            AddToClassList($"node-role-{visualRoleKey}");
            generateVisualContent += DrawRoleSilhouette;
            RegisterCallback<CustomStyleResolvedEvent>(OnShapeStyleResolved);
            if (def.Purity == NodePurity.Domain) AddToClassList("node-purity-domain");
            titleContainer.AddToClassList("ne-node-title");
            var titleLabel = titleContainer.Q<Label>();
            if (titleLabel != null)
            {
                titleLabel.AddToClassList("ne-node-title-label");
                titleLabel.tooltip = resolvedTitle;
            }
            var icon = new NodeIconControl(iconKind)
            {
                tooltip = Localizer.UI($"ui.nodeRole.{roleKey}", roleName)
            };
            titleContainer.Insert(0, icon);
            inputContainer.AddToClassList("ne-node-ports");
            outputContainer.AddToClassList("ne-node-ports");

            // 钉住的固定节点（如对话图的进入/退出节点）不可删除：去掉 GraphView 原生的 Deletable 能力，
            // 这会灰掉右键删除、并把它排除出 Delete 键 / 框选删除 / 剪切。OnGraphViewChanged 另有一道防御过滤。
            if (inst.pinned) capabilities &= ~Capabilities.Deletable;

            BuildPorts();

            m_ExtraContent = new VisualElement { name = "extra-content" };
            mainContainer.Add(m_ExtraContent);
            NodeViewControlRegistry.AttachIfAny(this, m_ExtraContent);   // styling.md / debug-mode.md 挂钩

            RefreshExpandedState();
            RefreshPorts();

            // 悬停满 1 秒弹出本地化"功能 tooltip"（节点说明 + 各参数名/说明/当前值）；离开或被移除时取消并隐藏。
            RegisterCallback<MouseEnterEvent>(_ =>
                m_HoverSchedule = schedule.Execute(() => NodeHoverTooltip.Show(this)).StartingIn(1000));
            RegisterCallback<MouseLeaveEvent>(_ => { m_HoverSchedule?.Pause(); NodeHoverTooltip.Hide(); });
            RegisterCallback<DetachFromPanelEvent>(_ => { m_HoverSchedule?.Pause(); NodeHoverTooltip.Hide(); });
            InitializeRunningFlowLifecycle();
        }

        void BuildPorts()
        {
            foreach (var pd in Definition.InputPorts)
            {
                var p = PortView.Create(pd, Direction.Input);
                m_In[pd.name] = p;
                inputContainer.Add(p);
            }
            foreach (var pd in Definition.OutputPorts)
            {
                var p = PortView.Create(pd, Direction.Output);
                m_Out[pd.name] = p;
                outputContainer.Add(p);
            }
        }

        public PortView InputPort(string name) => m_In.TryGetValue(name, out var p) ? p : null;
        public PortView OutputPort(string name) => m_Out.TryGetValue(name, out var p) ? p : null;
        public VisualElement ExtraContent => m_ExtraContent;
        public NodeViewControl AttachedControl { get; set; }   // 每个节点的自定义视图（如果有）

        void OnShapeStyleResolved(CustomStyleResolvedEvent evt)
        {
            evt.customStyle.TryGetValue(s_ShapeFill, out m_ShapeFill);
            evt.customStyle.TryGetValue(s_ShapeOutline, out m_ShapeOutline);
            evt.customStyle.TryGetValue(s_ShapeOutlineWidth, out m_ShapeOutlineWidth);
            evt.customStyle.TryGetValue(s_ShapeHighlight, out m_ShapeHighlight);
            evt.customStyle.TryGetValue(s_ShapeShadow, out m_ShapeShadow);
            evt.customStyle.TryGetValue(s_ShapeGlow, out m_ShapeGlow);
            evt.customStyle.TryGetValue(s_SelectionOutline, out m_SelectionOutline);
            evt.customStyle.TryGetValue(s_ValidationOutline, out m_ValidationOutline);
            ResolveRunningFlowStyle(evt);
            MarkDirtyRepaint();
        }

        public override bool ContainsPoint(Vector2 localPoint)
            => ContainsRoleSilhouettePoint(m_VisualRole, RoleSilhouetteBounds(contentRect), localPoint);

        static NodeRole ResolveVisualRole(NodeIconKind iconKind, NodeRole fallback)
        {
            switch (iconKind)
            {
                case NodeIconKind.Dialogue:
                case NodeIconKind.Label:
                case NodeIconKind.Objective:
                case NodeIconKind.State:
                    return NodeRole.Provider;

                case NodeIconKind.Choice:
                case NodeIconKind.Option:
                case NodeIconKind.Condition:
                case NodeIconKind.Gate:
                case NodeIconKind.Transition:
                    return NodeRole.Condition;

                case NodeIconKind.Action:
                case NodeIconKind.Jump:
                case NodeIconKind.Task:
                case NodeIconKind.Complete:
                case NodeIconKind.Failure:
                    return NodeRole.Action;

                case NodeIconKind.Entry:
                case NodeIconKind.Terminal:
                case NodeIconKind.SubGraph:
                case NodeIconKind.WaitEvent:
                case NodeIconKind.AnyState:
                    return NodeRole.Control;

                case NodeIconKind.RoleProvider:
                    return NodeRole.Provider;
                case NodeIconKind.RoleCondition:
                    return NodeRole.Condition;
                case NodeIconKind.RoleAction:
                    return NodeRole.Action;
                case NodeIconKind.RoleControl:
                    return NodeRole.Control;
                default:
                    return fallback;
            }
        }

        public override void OnSelected()
        {
            base.OnSelected();
            MarkDirtyRepaint();
            OnSelectedCallback?.Invoke();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            MarkDirtyRepaint();
        }

        // --- 调试 / 校验挂钩（debug-mode.md、styling.md） ---
        public void SetStatusClass(string statusClass)
        {
            RemoveFromClassList("status-success"); RemoveFromClassList("status-failure");
            RemoveFromClassList("status-running");  RemoveFromClassList("status-inactive");
            if (!string.IsNullOrEmpty(statusClass)) AddToClassList(statusClass);
            SetRunningFlowEnabled(statusClass == "status-running");
            MarkDirtyRepaint();
        }
        public void MarkValidation(ValidationSeverity sev)
        {
            AddToClassList(sev == ValidationSeverity.Error ? "validation-error" : "validation-warn");
            MarkDirtyRepaint();
        }
        public void ClearValidationMarks()
        {
            RemoveFromClassList("validation-error"); RemoveFromClassList("validation-warn");
            MarkDirtyRepaint();
        }
    }

    // ---- 端口视图 ----------------------------------------------------------
    public class PortView : Port
    {
        public TypeRef PortType { get; private set; }
        public string PortName { get; private set; }

        protected PortView(Orientation o, Direction d, Capacity c, System.Type t) : base(o, d, c, t) { }

        public static PortView Create(PortDef pd, Direction dir)
        {
            // 只要 arity 允许多于一条连线（Many/AtLeast，或上界大于 1 的
            // Range/Exactly），就用 Multi 容量 —— 否则用 Single。像 2..5 的 Range 或 Exactly 3 必须
            // 允许绘制多条连线，而旧的仅 (Many|AtLeast) 判定会禁止这一点。
            bool multi = pd.arity.kind == ArityKind.Many
                      || pd.arity.kind == ArityKind.AtLeast
                      || (pd.arity.kind == ArityKind.Range && pd.arity.max > 1)
                      || (pd.arity.kind == ArityKind.Exactly && pd.arity.min > 1);
            var cap = multi ? Capacity.Multi : Capacity.Single;
            var p = new PortView(Orientation.Vertical, dir, cap, typeof(object))
            {
                PortType = pd.type,
                PortName = pd.name,
                portName = pd.name
            };
            // 单连线 vs 多连线在画布上可辨：按容量挂一个样式类，USS 据此把连接点渲染成不同形状/颜色
            //（多连线=方形+强调色，单连线=圆形+中性色），再配本地化 tooltip 说明语义。
            // 这只是"看得见"；落新边挤掉旧边的替换语义在 GraphCanvas.EnforceSingleCapacity。
            p.AddToClassList(multi ? "ne-port-multi" : "ne-port-single");
            p.tooltip = multi
                ? Localizer.UI("ui.port.multi", "Multi-connection: accepts multiple wires")
                : Localizer.UI("ui.port.single", "Single-connection: only one wire (a new one replaces the old)");
            // 连接点颜色走 Unity 的 Port.portColor：它在 C# 里直接写连接点的描边/填充色，会盖过 USS 的 border-color，
            // 所以颜色必须在这里设、不能只靠 USS。连接点的形状（多连线=方、单连线=圆）仍由上面的样式类经 USS 控制。
            // 多连线=哑光青（rgb 95,141,135，对齐 --ne-accent-cyan），单连线=中性暖灰（rgb 150,143,130）。
            p.portColor = multi ? new Color(0.373f, 0.553f, 0.529f) : new Color(0.588f, 0.561f, 0.510f);
            var connector = new EdgeConnector<EdgeView>(new EdgeConnectorListener());
            p.m_EdgeConnector = connector;          // 为继承而来的（protected）Port.m_EdgeConnector 赋值
            p.AddManipulator(connector);            // 使 port.edgeConnector 能解析 + 连线拖拽生效
            return p;
        }
    }

    // 校验严重级别，在 5a 中本地定义，使画布只依赖 4a 而非 4c。
    // 5c 在调用 NodeView.MarkValidation 时将 4c 的 ValidationIssue.Sev 映射到此处。（解耦：5a 不引用 4c。）
    public enum ValidationSeverity { Error, Warn }

    // ---- 连线视图 ----------------------------------------------------------
    public class EdgeView : Edge { public Connection Model; }

    // 最小化的 listener，使被拖动的连线创建 EdgeView 实例。
    class EdgeConnectorListener : IEdgeConnectorListener
    {
        // 松手在合法端口上：把"建立/重连连接"交给画布（graphView 即承载这条边的 GraphCanvas）。
        public void OnDrop(GraphView graphView, Edge edge) => (graphView as GraphCanvas)?.OnEdgeDropped(edge);
        public void OnDropOutsidePort(Edge edge, Vector2 position) { /* 可在此打开"添加节点"对话框 */ }
    }
}
