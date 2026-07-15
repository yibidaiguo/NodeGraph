using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    [Serializable]
    public sealed class GraphTestModuleCatalogEntry
    {
        public string id;
        public string path;
        public string moduleType;
        public string[] requiredPackageIds = Array.Empty<string>();
        public string sampleFor;

        public string Id => id;
        public string Path => path;
        public string ModuleType => moduleType;
        public IReadOnlyList<string> RequiredPackageIds => requiredPackageIds ?? Array.Empty<string>();
        public string SampleFor => sampleFor;
    }

    [Serializable]
    public sealed class GraphTestModuleCatalog
    {
        public GraphTestModuleCatalogEntry[] packages = Array.Empty<GraphTestModuleCatalogEntry>();

        Dictionary<string, GraphTestModuleCatalogEntry> m_ById;

        public IReadOnlyList<GraphTestModuleCatalogEntry> Packages => packages ?? Array.Empty<GraphTestModuleCatalogEntry>();

        public static GraphTestModuleCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("The NodeGraph module catalog is empty.");

            GraphTestModuleCatalog catalog;
            try
            {
                catalog = JsonUtility.FromJson<GraphTestModuleCatalog>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("The NodeGraph module catalog is not valid JSON.", exception);
            }

            if (catalog == null || catalog.packages == null)
                throw new FormatException("The NodeGraph module catalog must contain a packages array.");

            catalog.Validate();
            return catalog;
        }

        public bool TryGet(string packageId, out GraphTestModuleCatalogEntry entry)
        {
            EnsureIndex();
            return m_ById.TryGetValue(packageId ?? string.Empty, out entry);
        }

        public GraphTestModuleCatalogEntry Get(string packageId)
        {
            if (!TryGet(packageId, out var entry))
            throw new KeyNotFoundException($"Unknown NodeGraph package '{packageId}'.");
            return entry;
        }

        void Validate()
        {
            m_ById = new Dictionary<string, GraphTestModuleCatalogEntry>(StringComparer.Ordinal);
            foreach (var entry in packages)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    throw new FormatException("Every NodeGraph catalog package must have an id.");
                if (!m_ById.TryAdd(entry.id, entry))
                    throw new FormatException($"Duplicate NodeGraph package id '{entry.id}'.");
                if (!GraphTestPackageSource.IsSafePackagePath(entry.path))
                    throw new FormatException($"NodeGraph package '{entry.id}' has unsafe path '{entry.path}'.");
                if (entry.moduleType != "framework" && entry.moduleType != "domain" && entry.moduleType != "sample")
                    throw new FormatException($"NodeGraph package '{entry.id}' has unknown module type '{entry.moduleType}'.");

                entry.requiredPackageIds ??= Array.Empty<string>();
                if (entry.requiredPackageIds.Any(string.IsNullOrWhiteSpace) ||
                    entry.requiredPackageIds.Distinct(StringComparer.Ordinal).Count() != entry.requiredPackageIds.Length)
                    throw new FormatException($"NodeGraph package '{entry.id}' has invalid required package ids.");
            }

            foreach (var entry in packages)
            {
                foreach (var dependencyId in entry.requiredPackageIds)
                {
                    if (!m_ById.ContainsKey(dependencyId))
                        throw new FormatException($"NodeGraph package '{entry.id}' requires unknown package '{dependencyId}'.");
                    if (dependencyId == entry.id)
                        throw new FormatException($"NodeGraph package '{entry.id}' cannot require itself.");
                }

                if (entry.moduleType == "sample")
                {
                    if (string.IsNullOrWhiteSpace(entry.sampleFor) || !m_ById.TryGetValue(entry.sampleFor, out var product))
                    throw new FormatException($"NodeGraph sample package '{entry.id}' has unknown sampleFor '{entry.sampleFor}'.");
                    if (product.moduleType != "domain" || !entry.requiredPackageIds.Contains(entry.sampleFor))
                    throw new FormatException($"NodeGraph sample package '{entry.id}' must require its domain '{entry.sampleFor}'.");
                }
                else if (!string.IsNullOrEmpty(entry.sampleFor))
                {
                    throw new FormatException($"Only NodeGraph sample packages may declare sampleFor ('{entry.id}').");
                }
            }

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in packages)
                ValidateAcyclic(entry, visiting, visited);
        }

        void ValidateAcyclic(
            GraphTestModuleCatalogEntry entry,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visited.Contains(entry.id)) return;
            if (!visiting.Add(entry.id))
                throw new FormatException($"NodeGraph package dependency cycle contains '{entry.id}'.");

            foreach (var dependencyId in entry.requiredPackageIds)
                ValidateAcyclic(m_ById[dependencyId], visiting, visited);

            visiting.Remove(entry.id);
            visited.Add(entry.id);
        }

        void EnsureIndex()
        {
            if (m_ById == null) Validate();
        }
    }
}
