using NodeEditor.EditorUI;
using UnityEditor;

namespace Dialogue.EditorUI
{
    public static class DialogueDatabaseLocator
    {
        public static DialogueDatabase Resolve(out string reason)
        {
            var paths = DialogueAssetPathsLocator.FindOrCreate();
            var guids = AssetDatabase.FindAssets("t:DialogueDatabase");
            return ResolveCandidates(paths, guids, out reason);
        }

        static DialogueDatabase ResolveCandidates(DialogueAssetPaths paths, string[] candidateGuids, out string reason)
        {
            reason = null;
            if (paths == null)
            {
                reason = Localizer.UI("ui.assetPathsUnavailable", "Dialogue Asset Paths configuration is unavailable.");
                return null;
            }
            if (paths.authoringDatabase != null) return paths.authoringDatabase;

            if (candidateGuids == null || candidateGuids.Length == 0)
            {
                reason = Localizer.UI("ui.noDatabaseCreate", "No Dialogue database exists. Create one with Create > Dialogue > Database.");
                return null;
            }
            if (candidateGuids.Length == 1)
                return AssetDatabase.LoadAssetAtPath<DialogueDatabase>(AssetDatabase.GUIDToAssetPath(candidateGuids[0]));

            reason = Localizer.UI("ui.multipleDatabasesSelect", "Multiple Dialogue databases exist. Select authoringDatabase in DialogueAssetPaths.");
            return null;
        }
    }
}
