using System;
using System.Collections.Generic;
using System.Linq;
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
            IEnumerable<NodeGraphModuleAction> actions)
        {
            Id = id;
            DisplayName = displayName;
            Order = order;
            m_Actions = (actions ?? Array.Empty<NodeGraphModuleAction>()).ToArray();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public IReadOnlyList<NodeGraphModuleAction> Actions => m_Actions;

        internal NodeGraphModuleDescriptor WithActions(IEnumerable<NodeGraphModuleAction> actions) =>
            new NodeGraphModuleDescriptor(Id, DisplayName, Order, actions);
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

        public bool TryGet(string moduleId, out NodeGraphModuleDescriptor descriptor) =>
            m_Modules.TryGetValue(moduleId ?? string.Empty, out descriptor);

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
