using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NodeEditor;

namespace NodeEditor.EditorUI
{
    public sealed class GraphCreateRecipe
    {
        public string id;
        public string module;
        public string labelKey;
        public string labelFallback;
        public string defaultFileName;
        public Func<string> graphRoot;
        public Func<string> blackboardFolder;
        public Func<NodeGraphAsset, bool> initialize;

        internal bool legacyCompatibility;
    }

    public static class GraphCreationRegistry
    {
        static readonly List<GraphCreateRecipe> s_Recipes = new();
        static readonly HashSet<string> s_ExplicitModules = new();
        static readonly HashSet<string> s_ConflictedIds = new();

        public static void Register(GraphCreateRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.id) ||
                string.IsNullOrEmpty(recipe.module) || recipe.initialize == null) return;

            if (!recipe.legacyCompatibility)
            {
                s_ExplicitModules.Add(recipe.module);
                s_Recipes.RemoveAll(item => item.legacyCompatibility && item.module == recipe.module);
            }

            if (s_ConflictedIds.Contains(recipe.id))
            {
                Debug.LogError($"NodeEditor: graph creation recipe '{recipe.id}' remains disabled until it is unregistered.");
                return;
            }

            int at = s_Recipes.FindIndex(item => item.id == recipe.id);
            if (at >= 0)
            {
                Debug.LogError($"NodeEditor: graph creation recipe '{recipe.id}' is registered more than once; disabling that id until it is unregistered.");
                s_Recipes.RemoveAt(at);
                s_ConflictedIds.Add(recipe.id);
                return;
            }
            s_Recipes.Add(recipe);
        }

        public static void Unregister(string id)
        {
            s_Recipes.RemoveAll(item => item.id == id);
            s_ConflictedIds.Remove(id);
        }

        public static IReadOnlyList<GraphCreateRecipe> ForModule(string module) =>
            s_Recipes.Where(item => item.module == module).ToList();

        internal static bool HasExplicitModuleOwnership(string module) =>
            s_ExplicitModules.Contains(module);
    }
}
