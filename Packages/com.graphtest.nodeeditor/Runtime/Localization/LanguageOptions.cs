using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NodeEditor
{
    [Serializable]
    public class LanguageOption
    {
        public Language language = Language.English;
        public string code = "en";
        public string displayName = "English";
    }

    [CreateAssetMenu(menuName = "NodeEditor/Language Options")]
    public class LanguageOptions : ScriptableObject
    {
        [SerializeField] List<LanguageOption> languages = DefaultLanguages();

        public IReadOnlyList<LanguageOption> Languages => Entries().ToList();

        public IEnumerable<string> Codes()
        {
            var codes = Entries()
                .Select(e => e.code?.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();
            return codes.Count > 0 ? codes : DefaultLanguages().Select(e => e.code);
        }

        public string DisplayName(string code)
        {
            var match = Entries().FirstOrDefault(e => e.code == code);
            if (match == null) return code ?? string.Empty;
            return string.IsNullOrWhiteSpace(match.displayName) ? match.code : $"{match.displayName} ({match.code})";
        }

        static List<LanguageOption> DefaultLanguages() => new()
        {
            new LanguageOption { language = Language.English, code = "en", displayName = "English" },
            new LanguageOption { language = Language.Chinese, code = "zh", displayName = "Chinese" },
        };

        IEnumerable<LanguageOption> Entries()
        {
            if (languages == null || languages.Count == 0)
                return DefaultLanguages();
            return languages.Where(e => e != null);
        }

        void OnValidate()
        {
            if (languages == null || languages.Count == 0)
                languages = DefaultLanguages();

            foreach (var entry in languages)
                if (entry != null)
                    entry.code = entry.code?.Trim();
        }
    }
}
