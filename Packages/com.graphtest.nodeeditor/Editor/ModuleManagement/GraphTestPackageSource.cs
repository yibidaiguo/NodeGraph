using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public sealed class GraphTestPackageSource
    {
        const string FrameworkPath = "Packages/com.graphtest.nodeeditor";

        readonly List<string> m_QueryParts;
        readonly int m_PathPartIndex;
        readonly bool m_PathUsesLeadingSlash;

        GraphTestPackageSource(
            string repositoryUrl,
            string revision,
            string packagePath,
            List<string> queryParts,
            int pathPartIndex,
            bool pathUsesLeadingSlash)
        {
            RepositoryUrl = repositoryUrl;
            Revision = revision;
            PackagePath = packagePath;
            m_QueryParts = queryParts;
            m_PathPartIndex = pathPartIndex;
            m_PathUsesLeadingSlash = pathUsesLeadingSlash;
        }

        public string RepositoryUrl { get; }
        public string Revision { get; }
        public string PackagePath { get; }

        public static bool TryResolve(
            string frameworkPackageId,
            string frameworkManifestJson,
            out GraphTestPackageSource source,
            out string error)
        {
            source = null;
            error = null;

            int separator = frameworkPackageId?.IndexOf('@') ?? -1;
            string reference = separator >= 0 ? frameworkPackageId.Substring(separator + 1) : string.Empty;
            if (TryParseGitReference(reference, out source, out _)) return true;

            if (!IsLocalFolderReference(reference))
            {
                error = "The installed NodeGraph framework source is not a Git URL or a local development folder.";
                return false;
            }

            RepositoryManifest manifest = null;
            try
            {
                manifest = JsonUtility.FromJson<PackageManifest>(frameworkManifestJson ?? string.Empty)?.repository;
            }
            catch (Exception)
            {
                // The structured error below is more useful than JsonUtility's parse exception.
            }

            if (manifest != null && string.Equals(manifest.type, "git", StringComparison.OrdinalIgnoreCase) &&
                TryParseGitReference(AppendRevision(manifest.url, manifest.revision), out source, out _))
            {
                source = new GraphTestPackageSource(
                    source.RepositoryUrl,
                    source.Revision,
                    FrameworkPath,
                    source.m_QueryParts,
                    source.m_PathPartIndex,
                    source.m_PathUsesLeadingSlash);
                return true;
            }

                error = "The installed NodeGraph framework is not backed by a Git package and its package.json has no usable git repository metadata.";
            return false;
        }

        public bool TryBuildModuleUrl(string packagePath, out string url, out string error)
        {
            url = null;
            error = null;
            if (!IsSafePackagePath(packagePath))
            {
                error = $"NodeGraph catalog path '{packagePath}' is unsafe. Package paths must remain below Packages/.";
                return false;
            }

            var query = new List<string>(m_QueryParts);
            string pathPart = "path=" + ((m_PathPartIndex < 0 || m_PathUsesLeadingSlash) ? "/" : string.Empty) + packagePath;
            if (m_PathPartIndex >= 0) query[m_PathPartIndex] = pathPart;
            else query.Add(pathPart);

            url = RepositoryUrl;
            if (query.Count > 0) url += "?" + string.Join("&", query);
            if (!string.IsNullOrEmpty(Revision)) url += "#" + Revision;
            return true;
        }

        internal static bool IsSafePackagePath(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || packagePath != packagePath.Trim()) return false;
            if (!packagePath.StartsWith("Packages/", StringComparison.Ordinal) || packagePath.Length <= "Packages/".Length) return false;
            if (packagePath.IndexOfAny(new[] { '\\', '?', '&', '#', '%', ':' }) >= 0) return false;

            var parts = packagePath.Split('/');
            return parts.Length >= 2 && parts.All(part =>
                !string.IsNullOrEmpty(part) && part != "." && part != "..");
        }

        static bool TryParseGitReference(
            string reference,
            out GraphTestPackageSource source,
            out string error)
        {
            source = null;
            error = null;
            if (string.IsNullOrWhiteSpace(reference) || !LooksLikeGitUrl(reference))
            {
                error = "The package source is not a Git URL.";
                return false;
            }

            string withoutRevision = reference;
            string revision = null;
            int fragment = reference.LastIndexOf('#');
            if (fragment >= 0)
            {
                revision = reference.Substring(fragment + 1);
                withoutRevision = reference.Substring(0, fragment);
                if (string.IsNullOrEmpty(revision))
                {
                    error = "The Git package revision is empty.";
                    return false;
                }
            }

            string repositoryUrl = withoutRevision;
            var queryParts = new List<string>();
            int pathPartIndex = -1;
            bool pathUsesLeadingSlash = false;
            string packagePath = null;
            int queryStart = withoutRevision.IndexOf('?');
            if (queryStart >= 0)
            {
                repositoryUrl = withoutRevision.Substring(0, queryStart);
                string query = withoutRevision.Substring(queryStart + 1);
                if (!string.IsNullOrEmpty(query)) queryParts.AddRange(query.Split('&'));
                for (int i = 0; i < queryParts.Count; i++)
                {
                    if (!queryParts[i].StartsWith("path=", StringComparison.Ordinal)) continue;
                    if (pathPartIndex >= 0)
                    {
                        error = "The Git package URL contains more than one path query parameter.";
                        return false;
                    }
                    pathPartIndex = i;
                    string rawPath = queryParts[i].Substring("path=".Length);
                    pathUsesLeadingSlash = rawPath.StartsWith("/", StringComparison.Ordinal);
                    packagePath = rawPath.TrimStart('/');
                }
            }

            if (string.IsNullOrEmpty(repositoryUrl) || !LooksLikeGitUrl(repositoryUrl))
            {
                error = "The package source does not contain a valid Git repository URL.";
                return false;
            }

            source = new GraphTestPackageSource(
                repositoryUrl, revision, packagePath, queryParts, pathPartIndex, pathUsesLeadingSlash);
            return true;
        }

        static bool LooksLikeGitUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            int query = value.IndexOf('?');
            int fragment = value.IndexOf('#');
            int end = new[] { query, fragment }.Where(index => index >= 0).DefaultIfEmpty(value.Length).Min();
            string repository = value.Substring(0, end);
            bool explicitlyGit = repository.StartsWith("git+https://", StringComparison.OrdinalIgnoreCase) ||
                                 repository.StartsWith("git+http://", StringComparison.OrdinalIgnoreCase) ||
                                 repository.StartsWith("git+ssh://", StringComparison.OrdinalIgnoreCase) ||
                                 repository.StartsWith("git+file://", StringComparison.OrdinalIgnoreCase);
            return repository.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
                   repository.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ||
                   repository.StartsWith("git://", StringComparison.OrdinalIgnoreCase) ||
                   explicitlyGit ||
                   (repository.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                    repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) ||
                   ((repository.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                     repository.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) &&
                    repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase));
        }

        static bool IsLocalFolderReference(string reference) =>
            !string.IsNullOrWhiteSpace(reference) &&
            reference.StartsWith("file:", StringComparison.OrdinalIgnoreCase) &&
            !reference.StartsWith("file://", StringComparison.OrdinalIgnoreCase);

        static string AppendRevision(string repositoryUrl, string revision) =>
            string.IsNullOrWhiteSpace(revision) ? repositoryUrl : repositoryUrl + "#" + revision;

        [Serializable]
        sealed class PackageManifest
        {
            public RepositoryManifest repository;
        }

        [Serializable]
        sealed class RepositoryManifest
        {
            public string type;
            public string url;
            public string revision;
        }
    }
}
