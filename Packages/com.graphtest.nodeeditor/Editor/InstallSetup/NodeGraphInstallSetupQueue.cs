using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeEditor.EditorUI
{
    public sealed class NodeGraphInstallSetupQueue
    {
        readonly Dictionary<string, NodeGraphInstallSetupDescriptor> m_Descriptors =
            new Dictionary<string, NodeGraphInstallSetupDescriptor>(StringComparer.Ordinal);

        public IReadOnlyList<NodeGraphInstallSetupDescriptor> Descriptors => m_Descriptors.Values
            .OrderBy(descriptor => descriptor.Order)
            .ThenBy(descriptor => descriptor.ModuleId, StringComparer.Ordinal)
            .ToArray();

        public bool TryRegister(NodeGraphInstallSetupDescriptor descriptor, out string error)
        {
            if (descriptor == null)
            {
                error = "NodeGraph install setup descriptor is null.";
                return false;
            }
            if (!descriptor.IsValid(out error)) return false;
            if (m_Descriptors.ContainsKey(descriptor.ModuleId))
            {
                error = $"NodeGraph install setup '{descriptor.ModuleId}' is already registered.";
                return false;
            }

            m_Descriptors.Add(descriptor.ModuleId, descriptor);
            error = null;
            return true;
        }

        public NodeGraphInstallSetupDescriptor NextPending()
        {
            return Descriptors.FirstOrDefault(descriptor => !descriptor.IsConfigured);
        }
    }
}
