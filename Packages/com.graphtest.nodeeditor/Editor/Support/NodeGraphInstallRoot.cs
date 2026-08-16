// NodeGraphInstallRoot.cs — 节点图在工程里落地的唯一顶级目录。
//
// 过去安装根是写死的：设置资产固定去 "Assets/NodeEditorSettings"，每个模块的内容固定去
// "Assets/<模块>Content"。装齐四个包，Assets 根目录平白多出五个文件夹，且工程无从干预。
// 现在只有一个可配置的安装根，设置与各模块内容都挂在它下面：
//
//     Assets/NodeGraph/            ← 安装根（可改）
//       Settings/                  ← 各模块的 *AssetPaths 配置资产
//       NodeEditor/ Dialogue/ ...  ← 各模块生成的内容
//
// 安装根存在 ProjectSettings/NodeGraphInstall.json：它是工程级配置、随仓库提交、
// 不占 Assets、不产生 .meta，也就不会成为「卸载后的遗留物」——卸载清理会把它一并带走。
//
// 老工程不动：配置文件不存在、但 Assets/NodeEditorSettings 还在时，走 legacy 布局，
// 路径与升级前逐字一致。否则「升级即换根」会让 FindOrCreate 在旧资产之外再造一份，
// 直接撞上「multiple project-owned assets found」的关门错误。
//
// 仅 Editor/ 程序集。

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NodeEditor.EditorUI
{
    public static class NodeGraphInstallRoot
    {
        /// <summary>新工程的默认安装根。</summary>
        public const string DefaultRoot = "Assets/NodeGraph";

        /// <summary>升级前的设置目录；它还在就说明这是个 legacy 布局的老工程。</summary>
        public const string LegacySettingsRoot = "Assets/NodeEditorSettings";

        const string ConfigFile = "ProjectSettings/NodeGraphInstall.json";

        [Serializable]
        sealed class Config
        {
            public string installRoot;
        }

        static string s_Root;
        static bool s_Legacy;
        static bool s_Resolved;

        /// <summary>安装根，例 "Assets/NodeGraph"。legacy 工程返回 "Assets"（各路径按老规则拼）。</summary>
        public static string Root
        {
            get
            {
                Resolve();
                return s_Root;
            }
        }

        /// <summary>true = 这个工程还在用升级前的分散布局，路径不会被改动。</summary>
        public static bool IsLegacyLayout
        {
            get
            {
                Resolve();
                return s_Legacy;
            }
        }

        /// <summary>设置资产（各模块的 *AssetPaths）所在目录。</summary>
        public static string SettingsRoot =>
            IsLegacyLayout ? LegacySettingsRoot : $"{Root}/Settings";

        /// <summary>某个模块生成内容的根目录。</summary>
        public static string ContentRoot(string moduleName) =>
            IsLegacyLayout ? $"Assets/{moduleName}Content" : $"{Root}/{moduleName}";

        /// <summary>工程配置文件的磁盘路径；卸载清理要删它。</summary>
        public static string ConfigFilePath =>
            Path.Combine(ProjectDirectory, ConfigFile).Replace('\\', '/');

        /// <summary>配置文件是否已经落盘（= 这个工程明确选过安装根）。</summary>
        public static bool IsConfigured => File.Exists(ConfigFilePath);

        /// <summary>
        /// 改安装根。返回错误串，null 表示成功。只写配置文件，不搬运已生成的资产——
        /// 换根之后已有资产会变成「配置指向别处」，由各 Locator 的 FindConfigured 报出来。
        /// </summary>
        public static string TrySet(string root)
        {
            string normalized = ProjectAssetPaths.NormalizeAssetPath(root);
            string error = Validate(normalized);
            if (error != null) return error;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath) ?? ".");
                File.WriteAllText(ConfigFilePath, JsonUtility.ToJson(new Config { installRoot = normalized }, true));
            }
            catch (Exception exception)
            {
                return $"写入 {ConfigFile} 失败：{exception.Message} / Could not write {ConfigFile}: {exception.Message}";
            }

            Invalidate();
            return null;
        }

        /// <summary>校验一个候选安装根；返回错误串，null 表示可用。</summary>
        public static string Validate(string root)
        {
            string normalized = ProjectAssetPaths.NormalizeAssetPath(root);
            if (!ProjectAssetPaths.IsProjectAssetPath(normalized))
                return $"安装根必须是 Assets/ 下的工程路径：{normalized}\n" +
                       $"The install root must be a project path under Assets/: {normalized}";
            if (normalized == "Assets")
                return "安装根不能就是 Assets/ 本身——那正是「全堆在根目录」。\n" +
                       "The install root cannot be Assets/ itself; that is the flat layout this replaces.";
            return null;
        }

        /// <summary>两个路径规范化之后是不是同一个。</summary>
        public static bool NormalizedEquals(string left, string right) =>
            string.Equals(
                ProjectAssetPaths.NormalizeAssetPath(left),
                ProjectAssetPaths.NormalizeAssetPath(right),
                StringComparison.Ordinal);

        /// <summary>
        /// 把当前生效的安装根钉进配置文件（默认根被默认接受时也要留痕）。
        /// legacy 布局不钉——那种工程没有单一根可写。返回错误串，null 表示成功或无需动作。
        /// </summary>
        public static string PinCurrent()
        {
            if (IsConfigured || IsLegacyLayout) return null;
            return TrySet(Root);
        }

        /// <summary>丢掉缓存，下次读时重新解析（配置文件被外部改动或删除后调用）。</summary>
        public static void Invalidate()
        {
            s_Resolved = false;
            s_Root = null;
        }

        static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;

            string configured = ReadConfiguredRoot();
            if (configured != null)
            {
                s_Root = configured;
                s_Legacy = false;
                return;
            }

            // 没配置过：老工程认 legacy 布局，新工程吃默认根。
            // 判据是「老位置真有设置资产」，不是「老目录还在」——空掉的 Assets/NodeEditorSettings/
            // 到处都是（删过资产、或者根本没生成成功），拿它当判据会把干净工程也永久钉在分散布局上。
            if (HasLegacySettingsAssets())
            {
                s_Root = "Assets";
                s_Legacy = true;
                return;
            }

            s_Root = DefaultRoot;
            s_Legacy = false;
        }

        static bool HasLegacySettingsAssets() =>
            AssetDatabase.IsValidFolder(LegacySettingsRoot) &&
            AssetDatabase.FindAssets(string.Empty, new[] { LegacySettingsRoot }).Length > 0;

        static string ReadConfiguredRoot()
        {
            string path = ConfigFilePath;
            if (!File.Exists(path)) return null;

            string raw;
            try { raw = File.ReadAllText(path); }
            catch (Exception exception)
            {
                Debug.LogError($"NodeGraph: 读不了 {ConfigFile}，本次回落到默认安装根。 / Could not read {ConfigFile}: {exception.Message}");
                return null;
            }

            Config config = null;
            try { config = JsonUtility.FromJson<Config>(raw); }
            catch (Exception exception)
            {
                Debug.LogError($"NodeGraph: {ConfigFile} 不是合法 JSON，本次回落到默认安装根。 / {ConfigFile} is not valid JSON: {exception.Message}");
                return null;
            }

            string normalized = ProjectAssetPaths.NormalizeAssetPath(config?.installRoot);
            string error = Validate(normalized);
            if (error == null) return normalized;

            Debug.LogError($"NodeGraph: {ConfigFile} 里的安装根不可用，本次回落到默认安装根。\n{error}");
            return null;
        }

        static string ProjectDirectory =>
            Path.GetDirectoryName(Application.dataPath.Replace('/', Path.DirectorySeparatorChar)) ?? ".";
    }
}
