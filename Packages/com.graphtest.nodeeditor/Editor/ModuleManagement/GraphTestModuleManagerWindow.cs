using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSample = UnityEditor.PackageManager.UI.Sample;

namespace NodeEditor.EditorUI
{
    public sealed class GraphTestModulePlan
    {
        internal GraphTestModulePlan(bool succeeded, IEnumerable<string> packageIds, string error)
        {
            Succeeded = succeeded;
            PackageIds = (packageIds ?? Array.Empty<string>()).ToArray();
            Error = error;
        }

        public bool Succeeded { get; }
        public IReadOnlyList<string> PackageIds { get; }
        public string Error { get; }

        internal static GraphTestModulePlan Success(IEnumerable<string> packageIds) =>
            new GraphTestModulePlan(true, packageIds, null);

        internal static GraphTestModulePlan Failure(string error) =>
            new GraphTestModulePlan(false, Array.Empty<string>(), error);
    }

    public static class GraphTestModuleManager
    {
        static readonly Regex DependenciesPattern = new Regex(
            "\\\"dependencies\\\"\\s*:\\s*\\{(?<body>(?:[^\\\"{}]|\\\"(?:\\\\.|[^\\\"\\\\])*\\\")*)\\}",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        static readonly Regex DependencyIdPattern = new Regex(
            "\\\"(?<id>(?:\\\\.|[^\\\"\\\\])+)\\\"\\s*:",
            RegexOptions.CultureInvariant);

        public static HashSet<string> ReadInstalledPackageIds(string manifestText)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(manifestText)) return result;

            var dependencies = DependenciesPattern.Match(manifestText);
            if (!dependencies.Success) return result;
            foreach (Match match in DependencyIdPattern.Matches(dependencies.Groups["body"].Value))
                result.Add(Regex.Unescape(match.Groups["id"].Value));
            return result;
        }

        public static GraphTestModulePlan PlanInstall(
            GraphTestModuleCatalog catalog,
            IEnumerable<string> installedPackageIds,
            string requestedPackageId)
        {
            // 计划错误会作为按钮 tooltip / 状态呈现给用户——走 Localizer（C11）；包 id 保持插值以便定位。
            if (catalog == null) return GraphTestModulePlan.Failure(
                Localizer.UI("ui.moduleManager.errNoCatalog", "The NodeGraph module catalog is unavailable."));
            if (!catalog.TryGet(requestedPackageId, out _))
                return GraphTestModulePlan.Failure(string.Format(
                    Localizer.UI("ui.moduleManager.errUnknownPackage", "Unknown NodeGraph package '{0}'."), requestedPackageId));

            var installed = new HashSet<string>(installedPackageIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var ordered = new List<string>();
            var planned = new HashSet<string>(StringComparer.Ordinal);
            AddWithDependencies(catalog, requestedPackageId, installed, planned, ordered);
            return GraphTestModulePlan.Success(ordered);
        }

        public static GraphTestModulePlan PlanRemove(
            GraphTestModuleCatalog catalog,
            IEnumerable<string> installedPackageIds,
            string requestedPackageId)
        {
            if (catalog == null) return GraphTestModulePlan.Failure(
                Localizer.UI("ui.moduleManager.errNoCatalog", "The NodeGraph module catalog is unavailable."));
            if (!catalog.TryGet(requestedPackageId, out _))
                return GraphTestModulePlan.Failure(string.Format(
                    Localizer.UI("ui.moduleManager.errUnknownPackage", "Unknown NodeGraph package '{0}'."), requestedPackageId));

            var installed = new HashSet<string>(installedPackageIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (!installed.Contains(requestedPackageId)) return GraphTestModulePlan.Success(Array.Empty<string>());

            var dependents = catalog.Packages
                .Where(entry => installed.Contains(entry.Id) && entry.Id != requestedPackageId)
                .Where(entry => Requires(catalog, entry.Id, requestedPackageId, new HashSet<string>(StringComparer.Ordinal)))
                .Select(entry => entry.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (dependents.Length > 0)
            {
                return GraphTestModulePlan.Failure(string.Format(
                    Localizer.UI("ui.moduleManager.errDependents", "Cannot remove '{0}' because installed package(s) require it: {1}."),
                    requestedPackageId, string.Join(", ", dependents)));
            }

            return GraphTestModulePlan.Success(new[] { requestedPackageId });
        }

        static void AddWithDependencies(
            GraphTestModuleCatalog catalog,
            string packageId,
            HashSet<string> installed,
            HashSet<string> planned,
            List<string> ordered)
        {
            if (planned.Contains(packageId)) return;
            var entry = catalog.Get(packageId);
            foreach (var dependencyId in entry.RequiredPackageIds)
                AddWithDependencies(catalog, dependencyId, installed, planned, ordered);
            if (!installed.Contains(packageId) && planned.Add(packageId)) ordered.Add(packageId);
        }

        static bool Requires(
            GraphTestModuleCatalog catalog,
            string packageId,
            string requiredPackageId,
            HashSet<string> visited)
        {
            if (!visited.Add(packageId)) return false;
            var entry = catalog.Get(packageId);
            foreach (var dependencyId in entry.RequiredPackageIds)
            {
                if (dependencyId == requiredPackageId || Requires(catalog, dependencyId, requiredPackageId, visited))
                    return true;
            }
            return false;
        }
    }

    public sealed class GraphTestModuleManagerWindow : EditorWindow
    {
        const string FrameworkId = "com.graphtest.nodeeditor";
        static readonly Dictionary<string, string> LegacySamplePackages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["com.graphtest.dialogue"] = "com.graphtest.dialogue.samples",
            ["com.graphtest.task"] = "com.graphtest.task.samples",
            ["com.graphtest.statemachine"] = "com.graphtest.statemachine.samples"
        };
        static readonly Dictionary<string, string> LegacySamplePaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["com.graphtest.dialogue"] = "Assets/Samples/NodeGraph Dialogue Samples/0.0.4/Dialogue Basics",
            ["com.graphtest.task"] = "Assets/Samples/NodeGraph Task Samples/0.0.4/Task Basics",
            ["com.graphtest.statemachine"] = "Assets/Samples/NodeGraph State Machine Samples/0.0.4/State Machine Basics"
        };

        readonly Dictionary<string, string> m_TransientStates = new Dictionary<string, string>(StringComparer.Ordinal);
        GraphTestModuleCatalog m_Catalog;
        GraphTestPackageSource m_Source;
        HashSet<string> m_Installed = new HashSet<string>(StringComparer.Ordinal);
        string m_ContextError;

        [MenuItem("Tools/NodeGraph/Manager", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<GraphTestModuleManagerWindow>();
            window.titleContent = new GUIContent(Localizer.UI("ui.moduleManager", "NodeGraph Manager"));
            window.minSize = new Vector2(520, 360);
        }

        public void CreateGUI()
        {
            EditorUi.ConfigureWindow(rootVisualElement);
            Reload();
        }

        void Reload()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            TryLoadContext(out m_Catalog, out m_Source, out m_Installed, out m_ContextError);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label(Localizer.UI("ui.moduleManager", "NodeGraph Manager"));
            title.AddToClassList("ne-manager-title");   // 字号/字重走 USS（铁律#3：C# 内联只准纯布局）
            title.style.flexGrow = 1;
            header.Add(title);
            header.Add(EditorUi.CreateThemeToggle());
            rootVisualElement.Add(header);

            if (!string.IsNullOrEmpty(m_ContextError))
            {
                var error = new HelpBox(m_ContextError, HelpBoxMessageType.Warning);
                error.style.marginBottom = 8;
                rootVisualElement.Add(error);
            }

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.style.flexGrow = 1;
            rootVisualElement.Add(content);

            if (m_Catalog == null) return;
            AddLegacyPackageNotices(content);
            AddHeading(content, Localizer.UI("ui.moduleManager.framework", "Framework"));
            foreach (var entry in m_Catalog.Packages.Where(entry => entry.ModuleType == "framework"))
                content.Add(BuildModuleCard(entry));

            AddHeading(content, Localizer.UI("ui.moduleManager.products", "Modules"));
            foreach (var entry in m_Catalog.Packages.Where(entry => entry.ModuleType == "domain"))
                content.Add(BuildModuleCard(entry));
        }

        void AddHeading(VisualElement parent, string heading)
        {
            var label = new Label(heading);
            label.AddToClassList("ne-manager-heading");   // 字重走 USS；margin 属布局留内联
            label.style.marginTop = 8;
            label.style.marginBottom = 3;
            label.style.flexShrink = 0;
            parent.Add(label);
        }

        VisualElement BuildModuleCard(GraphTestModuleCatalogEntry entry)
        {
            var card = new VisualElement();
            card.AddToClassList("entry-card");
            card.style.flexShrink = 0;
            card.Add(BuildPackageRow(entry));

            if (m_Installed.Contains(entry.Id))
            {
                if (NodeGraphModules.Registry.TryGet(entry.Id, out var descriptor))
                    card.Add(BuildActionRow(descriptor));
                else
                    card.Add(new HelpBox(Localizer.UI("ui.moduleManager.noActions",
                        "The installed module has not registered its NodeGraph actions."), HelpBoxMessageType.Warning));
            }

            if (m_Installed.Contains(entry.Id) && entry.ModuleType == "domain")
                card.Add(BuildDomainSampleRows(entry));
            return card;
        }

        VisualElement BuildActionRow(NodeGraphModuleDescriptor descriptor)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginLeft = 8;
            row.style.marginBottom = 4;
            row.style.flexShrink = 0;
            foreach (var action in descriptor.Actions)
            {
                var button = new Button(() => ExecuteAction(action)) { text = action.DisplayName };
                EditorUi.ApplyToolbarTextButton(button);
                button.style.marginRight = 4;
                button.SetEnabled(action.IsEnabled);
                row.Add(button);
            }
            return row;
        }

        VisualElement BuildDomainSampleRows(GraphTestModuleCatalogEntry entry)
        {
            var container = new VisualElement();
            container.style.marginLeft = 8;
            container.style.marginTop = 2;
            container.style.flexShrink = 0;
            var package = PackageInfo.GetAllRegisteredPackages().FirstOrDefault(info => info.name == entry.Id);
            if (package == null) return container;
            var samples = PackageSample.FindByPackage(package.name, package.version).ToArray();
            bool legacyImported = IsLegacySampleImported(entry.Id);
            foreach (var sample in samples)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.minHeight = 32;
                row.style.flexShrink = 0;

                var name = new Label(sample.displayName);
                name.style.flexGrow = 1;
                row.Add(name);

                string status = legacyImported
                    ? Localizer.UI("ui.moduleManager.importedLegacy", "Imported (legacy location)")
                    : sample.isImported
                        ? Localizer.UI("ui.moduleManager.imported", "Imported")
                        : Localizer.UI("ui.moduleManager.available", "Available");
                var statusLabel = new Label(status);
                statusLabel.style.width = 180;
                row.Add(statusLabel);

                var button = new Button(() => ImportSample(sample))
                {
                    text = Localizer.UI("ui.moduleManager.import", "Import")
                };
                EditorUi.ApplyToolbarTextButton(button);
                button.style.width = 86;
                button.SetEnabled(!legacyImported && !sample.isImported && !GraphTestPackageOperations.IsBusy);
                row.Add(button);
                container.Add(row);
            }
            return container;
        }

        VisualElement BuildPackageRow(GraphTestModuleCatalogEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 34;
            row.style.flexShrink = 0;

            var name = new Label(DisplayName(entry));
            name.tooltip = entry.Id;
            name.style.flexGrow = 1;
            row.Add(name);

            bool installed = m_Installed.Contains(entry.Id);
            string status = m_TransientStates.TryGetValue(entry.Id, out var transient)
                ? transient
                : installed
                    ? Localizer.UI("ui.moduleManager.installed", "Installed")
                    : m_Source == null
                        ? Localizer.UI("ui.moduleManager.unavailable", "Unavailable")
                        : Localizer.UI("ui.moduleManager.available", "Available");
            var statusLabel = new Label(status);
            statusLabel.style.width = 180;
            row.Add(statusLabel);

            var plan = installed
                ? GraphTestModuleManager.PlanRemove(m_Catalog, m_Installed, entry.Id)
                : GraphTestModuleManager.PlanInstall(m_Catalog, m_Installed, entry.Id);
            var button = new Button(() => ChangePackage(entry.Id, installed))
            {
                text = installed
                    ? Localizer.UI("ui.moduleManager.remove", "Remove")
                    : Localizer.UI("ui.moduleManager.install", "Install")
            };
            EditorUi.ApplyToolbarTextButton(button);
            button.style.width = 86;
            bool installing = status.StartsWith(Localizer.UI("ui.moduleManager.installing", "Installing"), StringComparison.Ordinal);
            bool canRemove = entry.Id != FrameworkId;
            button.SetEnabled(!GraphTestPackageOperations.IsBusy && !installing && plan.Succeeded &&
                              (installed ? canRemove : m_Source != null));
            if (installed && !canRemove) button.tooltip = Localizer.UI("ui.moduleManager.frameworkLocked",
                "The framework package manages this window and cannot remove itself.");
            if (!plan.Succeeded) button.tooltip = plan.Error;
            row.Add(button);
            return row;
        }

        void ExecuteAction(NodeGraphModuleAction action)
        {
            if (!action.TryExecute(out var error))
                EditorUtility.DisplayDialog(Localizer.UI("ui.moduleManager", "NodeGraph Manager"), error,
                    Localizer.UI("ui.ok", "OK"));
        }

        void ImportSample(PackageSample sample)
        {
            if (!sample.Import(PackageSample.ImportOptions.None))
                EditorUtility.DisplayDialog(Localizer.UI("ui.moduleManager", "NodeGraph Manager"),
                    string.Format(Localizer.UI("ui.moduleManager.importFailed", "Could not import '{0}'."), sample.displayName),
                    Localizer.UI("ui.ok", "OK"));
            Reload();
        }

        void ChangePackage(string packageId, bool remove)
        {
            Action<string, string> stateChanged = (id, state) =>
            {
                m_TransientStates[id] = state;
                Reload();
            };
            Action<bool, string> completed = (succeeded, error) =>
            {
                m_TransientStates.Clear();
                if (!succeeded) m_TransientStates[packageId] = Localizer.UI("ui.moduleManager.failed", "Failed") + ": " + error;
                Reload();
            };

            if (remove)
            {
                var plan = GraphTestModuleManager.PlanRemove(m_Catalog, m_Installed, packageId);
                GraphTestPackageOperations.Remove(plan, stateChanged, completed);
            }
            else
            {
                var plan = GraphTestModuleManager.PlanInstall(m_Catalog, m_Installed, packageId);
                GraphTestPackageOperations.Install(m_Catalog, m_Source, plan, stateChanged, completed);
            }
        }

        static string DisplayName(GraphTestModuleCatalogEntry entry)
        {
            if (entry.Id == FrameworkId) return Localizer.UI("ui.moduleManager.nodeEditor", "Node Editor Framework");
            string name = entry.Id.Substring(entry.Id.LastIndexOf('.') + 1);
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        void AddLegacyPackageNotices(VisualElement parent)
        {
            foreach (var pair in LegacySamplePackages)
            {
                if (!m_Installed.Contains(pair.Value)) continue;
                var notice = new VisualElement();
                notice.AddToClassList("entry-card");
                notice.style.flexDirection = FlexDirection.Row;
                notice.style.alignItems = Align.Center;
                notice.style.flexShrink = 0;

                var message = new Label(string.Format(Localizer.UI("ui.moduleManager.legacySamplePackage",
                    "Legacy sample package '{0}' is still installed. Imported sample assets will be kept."), pair.Value));
                message.style.flexGrow = 1;
                message.style.whiteSpace = WhiteSpace.Normal;
                notice.Add(message);

                var button = new Button(() => RemoveLegacyPackage(pair.Value))
                {
                    text = Localizer.UI("ui.moduleManager.removeLegacy", "Remove legacy package")
                };
                EditorUi.ApplyToolbarTextButton(button);
                button.SetEnabled(!GraphTestPackageOperations.IsBusy);
                notice.Add(button);
                parent.Add(notice);
            }
        }

        void RemoveLegacyPackage(string packageId)
        {
            Action<string, string> stateChanged = (id, state) =>
            {
                m_TransientStates[id] = state;
                Reload();
            };
            Action<bool, string> completed = (succeeded, error) =>
            {
                m_TransientStates.Clear();
                if (!succeeded) m_ContextError = error;
                Reload();
            };
            GraphTestPackageOperations.RemovePackage(packageId, stateChanged, completed);
        }

        static bool IsLegacySampleImported(string domainPackageId)
        {
            if (!LegacySamplePaths.TryGetValue(domainPackageId, out var relativePath)) return false;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Directory.Exists(Path.Combine(projectRoot, relativePath));
        }

        internal static bool TryLoadContext(
            out GraphTestModuleCatalog catalog,
            out GraphTestPackageSource source,
            out HashSet<string> installed,
            out string error)
        {
            catalog = null;
            source = null;
            installed = new HashSet<string>(StringComparer.Ordinal);
            error = null;
            try
            {
                const string catalogPath = "Packages/com.graphtest.nodeeditor/Editor/ModuleManagement/GraphTestModuleCatalog.json";
                var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(catalogPath);
                if (catalogAsset == null) throw new FileNotFoundException(
                    Localizer.UI("ui.moduleManager.errCatalogMissing", "GraphTestModuleCatalog.json was not found in the framework package."));
                catalog = GraphTestModuleCatalog.FromJson(catalogAsset.text);

                string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Packages/manifest.json"));
                installed = GraphTestModuleManager.ReadInstalledPackageIds(File.ReadAllText(manifestPath));

                var packageInfo = PackageInfo.FindForAssetPath("Packages/com.graphtest.nodeeditor/package.json");
                if (packageInfo == null) throw new InvalidOperationException(
                    Localizer.UI("ui.moduleManager.errResolveFramework", "Unity Package Manager could not resolve the installed NodeGraph framework package."));
                string packageManifest = File.ReadAllText(Path.Combine(packageInfo.resolvedPath, "package.json"));
                if (!GraphTestPackageSource.TryResolve(packageInfo.packageId, packageManifest, out source, out error)) return false;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }

    static class GraphTestPackageOperations
    {
        public static bool IsBusy { get; private set; }

        public static void Install(
            GraphTestModuleCatalog catalog,
            GraphTestPackageSource source,
            GraphTestModulePlan plan,
            Action<string, string> stateChanged,
            Action<bool, string> completed)
        {
            if (IsBusy) { completed(false, Localizer.UI("ui.moduleManager.errBusy", "Another NodeGraph package operation is already running.")); return; }
            if (plan == null || !plan.Succeeded) { completed(false, plan?.Error ?? Localizer.UI("ui.moduleManager.errNoPlan", "The install plan is unavailable.")); return; }
            if (source == null) { completed(false, Localizer.UI("ui.moduleManager.errNoSource", "The NodeGraph Git package source is unavailable.")); return; }

            var urls = new List<string>();
            foreach (string packageId in plan.PackageIds)
            {
                if (!source.TryBuildModuleUrl(catalog.Get(packageId).Path, out var url, out var error))
                {
                    completed(false, error);
                    return;
                }
                stateChanged(packageId, Localizer.UI("ui.moduleManager.installing", "Installing") + "...");
                urls.Add(url);
            }

            if (urls.Count == 0) { completed(true, null); return; }
            IsBusy = true;
            AddAndRemoveRequest request = Client.AddAndRemove(urls.ToArray(), Array.Empty<string>());
            EditorApplication.update += Poll;

            void Poll()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Poll;
                IsBusy = false;
                completed(request.Status == StatusCode.Success,
                    request.Status == StatusCode.Success
                        ? null
                        : request.Error?.message ?? Localizer.UI("ui.moduleManager.errInstallFailed", "Unity Package Manager install failed."));
            }
        }

        public static void Remove(
            GraphTestModulePlan plan,
            Action<string, string> stateChanged,
            Action<bool, string> completed)
        {
            if (IsBusy) { completed(false, Localizer.UI("ui.moduleManager.errBusy", "Another NodeGraph package operation is already running.")); return; }
            if (plan == null || !plan.Succeeded) { completed(false, plan?.Error ?? Localizer.UI("ui.moduleManager.errNoPlan", "The removal plan is unavailable.")); return; }
            if (plan.PackageIds.Count == 0) { completed(true, null); return; }

            RemovePackage(plan.PackageIds[0], stateChanged, completed);
        }

        public static void RemovePackage(
            string packageId,
            Action<string, string> stateChanged,
            Action<bool, string> completed)
        {
            if (IsBusy) { completed(false, Localizer.UI("ui.moduleManager.errBusy", "Another NodeGraph package operation is already running.")); return; }
            if (string.IsNullOrWhiteSpace(packageId)) { completed(false, Localizer.UI("ui.moduleManager.errNoPlan", "The removal plan is unavailable.")); return; }

            IsBusy = true;
            stateChanged(packageId, Localizer.UI("ui.moduleManager.removing", "Removing") + "...");
            RemoveRequest request = Client.Remove(packageId);
            EditorApplication.update += Poll;

            void Poll()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Poll;
                IsBusy = false;
                completed(request.Status == StatusCode.Success,
                    request.Status == StatusCode.Success ? null : request.Error?.message ?? Localizer.UI("ui.moduleManager.errRemoveFailed", "Unity Package Manager remove failed."));
            }
        }

    }

}
