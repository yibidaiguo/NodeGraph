using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public sealed class NodeGraphModuleAction
    {
        readonly Action m_Execute;
        readonly Func<bool> m_IsEnabled;
        readonly string m_NameKey;

        // displayName = 英文回退（铁律#5）；本地化 key 默认按 id 约定 ui.moduleAction.<id>，
        // 同 id 语义不同的注册方（如框架的 open）经 nameKey 显式覆盖。
        public NodeGraphModuleAction(
            string id,
            string displayName,
            Action execute,
            Func<bool> isEnabled = null,
            string nameKey = null)
        {
            Id = id;
            FallbackName = displayName;
            m_NameKey = nameKey;
            m_Execute = execute;
            m_IsEnabled = isEnabled;
        }

        public string Id { get; }

        // 注册期只存 key + 英文回退，渲染时经 Localizer 解析——切换编辑器语言后重开/刷新 Manager 即生效
        //（若在注册期解析，文案会被冻结成 domain-reload 时的语言）。
        public string DisplayName => Localizer.UI(m_NameKey ?? $"ui.moduleAction.{Id}", FallbackName);

        // 未本地化的英文回退名（校验/日志用，避免在 InitializeOnLoad 校验期触发 Localizer 资产解析）。
        public string FallbackName { get; }

        public bool IsEnabled
        {
            get
            {
                try { return m_IsEnabled?.Invoke() ?? true; }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return false;
                }
            }
        }

        public bool TryExecute(out string error)
        {
            error = null;
            if (!IsEnabled)
            {
                // 会经 DisplayDialog 呈现给用户——走 Localizer（C11）。
                error = string.Format(
                    Localizer.UI("ui.moduleManager.actionUnavailable", "NodeGraph action '{0}' is unavailable."),
                    DisplayName);
                return false;
            }

            try
            {
                m_Execute();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = exception.Message;
                return false;
            }
        }

        internal bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                error = "NodeGraph module actions require a non-empty id.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(FallbackName))
            {
                error = $"NodeGraph module action '{Id}' requires a display name.";
                return false;
            }
            if (m_Execute == null)
            {
                error = $"NodeGraph module action '{Id}' requires a callback.";
                return false;
            }
            error = null;
            return true;
        }
    }

    public sealed class NodeGraphModuleDescriptor
    {
        readonly NodeGraphModuleAction[] m_Actions;

        public NodeGraphModuleDescriptor(
            string id,
            string displayName,
            int order,
            IEnumerable<NodeGraphModuleAction> actions,
            GraphOrientation defaultOrientation = GraphOrientation.Vertical,
            string moduleKey = null,
            string retiredSamplePackage = null,
            string retiredSamplePath = null)
        {
            Id = id;
            DisplayName = displayName;
            Order = order;
            DefaultOrientation = defaultOrientation;
            // 未显式声明时从包 id 的最后一段推出短键（com.graphtest.dialogue -> dialogue）。
            // 这是**兜底**，不是查找机制：注册表按 ModuleKey 建索引，不再靠 EndsWith 扫描去猜。
            ModuleKey = string.IsNullOrEmpty(moduleKey)
                ? (id ?? string.Empty).Substring((id ?? string.Empty).LastIndexOf('.') + 1)
                : moduleKey;
            RetiredSamplePackage = retiredSamplePackage;
            RetiredSamplePath = retiredSamplePath;
            m_Actions = (actions ?? Array.Empty<NodeGraphModuleAction>()).ToArray();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public GraphOrientation DefaultOrientation { get; }
        public IReadOnlyList<NodeGraphModuleAction> Actions => m_Actions;

        // 图上记录的短模块键（NodeGraphAsset.module，如 "dialogue"）。与 Id（包名）是两个东西：
        // 图里存的是短键，描述符按包名注册，过去靠 m.Id.EndsWith("." + module) 把两者对起来——
        // 那是在猜，而且一个叫 com.other.dialogue 的包会误命中。现在由模块自己声明。
        public string ModuleKey { get; }

        // 已退役的独立样例包（0.0.4 时代每个领域一个 *.samples 包）。由模块自己声明，
        // 框架不再硬编码这张表——新模块要么不声明（没有历史包袱），要么声明自己的。
        // 为 null 表示该模块没有退役样例包需要清理。
        public string RetiredSamplePackage { get; }
        public string RetiredSamplePath { get; }

        internal NodeGraphModuleDescriptor WithActions(IEnumerable<NodeGraphModuleAction> actions) =>
            new NodeGraphModuleDescriptor(Id, DisplayName, Order, actions, DefaultOrientation,
                                          ModuleKey, RetiredSamplePackage, RetiredSamplePath);
    }

    public sealed class NodeGraphModuleRegistry
    {
        readonly Dictionary<string, NodeGraphModuleDescriptor> m_Modules =
            new Dictionary<string, NodeGraphModuleDescriptor>(StringComparer.Ordinal);

        public IReadOnlyList<NodeGraphModuleDescriptor> Modules => m_Modules.Values
            .OrderBy(module => module.Order)
            .ThenBy(module => module.DisplayName, StringComparer.Ordinal)
            .ThenBy(module => module.Id, StringComparer.Ordinal)
            .ToArray();

        public bool TryRegister(NodeGraphModuleDescriptor descriptor, out string error)
        {
            if (!Validate(descriptor, out error)) return false;
            if (m_Modules.ContainsKey(descriptor.Id))
            {
                error = $"NodeGraph module '{descriptor.Id}' is already registered.";
                return false;
            }

            m_Modules.Add(descriptor.Id, descriptor);
            return true;
        }

        public bool TryAddActions(
            string moduleId,
            IEnumerable<NodeGraphModuleAction> actions,
            out string error)
        {
            if (!m_Modules.TryGetValue(moduleId ?? string.Empty, out var descriptor))
            {
                error = $"NodeGraph module '{moduleId}' is not registered.";
                return false;
            }

            var combined = descriptor.Actions.Concat(actions ?? Array.Empty<NodeGraphModuleAction>()).ToArray();
            var replacement = descriptor.WithActions(combined);
            if (!Validate(replacement, out error)) return false;
            m_Modules[moduleId] = replacement;
            return true;
        }

        // 同时接受包 id（com.graphtest.dialogue）和图上记录的短模块键（dialogue）。
        // 短键走 ModuleKey 索引精确命中——过去调用方拿不到就自己按 Id 后缀扫一遍，
        // 那既是重复实现又会把 com.other.dialogue 这类同后缀包误判成同一模块。
        public bool TryGet(string moduleId, out NodeGraphModuleDescriptor descriptor)
        {
            var key = moduleId ?? string.Empty;
            if (m_Modules.TryGetValue(key, out descriptor)) return true;
            foreach (var m in m_Modules.Values)
            {
                if (string.Equals(m.ModuleKey, key, StringComparison.Ordinal))
                {
                    descriptor = m;
                    return true;
                }
            }
            descriptor = null;
            return false;
        }

        static bool Validate(NodeGraphModuleDescriptor descriptor, out string error)
        {
            if (descriptor == null)
            {
                error = "NodeGraph module descriptor is null.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(descriptor.Id))
            {
                error = "NodeGraph modules require a non-empty id.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            {
                error = $"NodeGraph module '{descriptor.Id}' requires a display name.";
                return false;
            }

            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in descriptor.Actions)
            {
                if (action == null)
                {
                    error = $"NodeGraph module '{descriptor.Id}' contains a null action.";
                    return false;
                }
                if (!action.IsValid(out error)) return false;
                if (!actionIds.Add(action.Id))
                {
                    error = $"NodeGraph module '{descriptor.Id}' contains duplicate action '{action.Id}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }

    public static class NodeGraphModules
    {
        public static NodeGraphModuleRegistry Registry { get; } = new NodeGraphModuleRegistry();
    }
}
