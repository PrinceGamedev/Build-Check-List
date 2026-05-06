using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gameyogi.BuildChecklist.Editor.Rules
{
    /// <summary>
    /// Project-level rule store. Edited via the Build Checklist editor window,
    /// read by the build preprocessor. There is one shared asset per project,
    /// auto-created at <see cref="DefaultAssetPath"/> on first use.
    /// </summary>
    public sealed class BuildChecklistRules : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/BuildChecklist/Rules.asset";

        public BuildCheckPanel activePanel = BuildCheckPanel.Development;

        public List<RuleEntry> entries = new List<RuleEntry>();

        // ── Lookup ────────────────────────────────────────────────────────────

        public RuleEntry FindByFieldId(string fieldId)
        {
            if (string.IsNullOrEmpty(fieldId)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].fieldId == fieldId)
                    return entries[i];
            return null;
        }

        public RuleEntry GetOrCreate(string fieldId)
        {
            var existing = FindByFieldId(fieldId);
            if (existing != null) return existing;

            var created = new RuleEntry { fieldId = fieldId };
            entries.Add(created);
            EditorUtility.SetDirty(this);
            return created;
        }

        public bool Remove(RuleEntry entry)
        {
            var removed = entries.Remove(entry);
            if (removed) EditorUtility.SetDirty(this);
            return removed;
        }

        // ── Load / Create ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the existing rules asset, or creates one at
        /// <see cref="DefaultAssetPath"/> if none exists in the project.
        /// </summary>
        public static BuildChecklistRules LoadOrCreate()
        {
            var existing = FindExisting();
            if (existing != null) return existing;

            var asset = CreateInstance<BuildChecklistRules>();
            EnsureDirectory(DefaultAssetPath);
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        /// <summary>
        /// Finds the first existing rules asset anywhere in the project; null
        /// if none. Lets users move the asset out of the default folder.
        /// </summary>
        public static BuildChecklistRules FindExisting()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(BuildChecklistRules)}");
            if (guids == null || guids.Length == 0) return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<BuildChecklistRules>(path);
        }

        private static void EnsureDirectory(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;

            var parts = dir.Replace('\\', '/').Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
