using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Gameyogi.BuildChecklist.Editor.Discovery
{
    /// <summary>
    /// Scans all loaded assemblies for fields tagged with
    /// <see cref="BuildCheckFieldAttribute"/> using Unity's <see cref="TypeCache"/>.
    /// Results are cached and refreshed automatically after every assembly reload.
    /// </summary>
    [InitializeOnLoad]
    public static class AttributedFieldDiscovery
    {
        private static IReadOnlyList<DiscoveredField> _cached;
        private static Dictionary<string, DiscoveredField> _byId;

        /// <summary>Raised after a (re)scan completes.</summary>
        public static event Action Refreshed;

        static AttributedFieldDiscovery()
        {
            // Initial scan on editor load / domain reload.
            Refresh();
            AssemblyReloadEvents.afterAssemblyReload += Refresh;
        }

        /// <summary>All discovered fields, ordered by Category then DisplayName.</summary>
        public static IReadOnlyList<DiscoveredField> GetAll()
        {
            if (_cached == null) Refresh();
            return _cached;
        }

        /// <summary>Look up a discovered field by its stable id. Returns null if missing.</summary>
        public static DiscoveredField FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null) Refresh();
            return _byId.TryGetValue(id, out var f) ? f : null;
        }

        /// <summary>Force a full re-scan. Called automatically after assembly reload.</summary>
        public static void Refresh()
        {
            var fields = TypeCache.GetFieldsWithAttribute<BuildCheckFieldAttribute>();
            var list = new List<DiscoveredField>(fields.Count);

            foreach (var field in fields)
            {
                // Defensive: TypeCache should only return fields, but guard anyway.
                if (field == null || field.DeclaringType == null) continue;

                var attr = field.GetCustomAttribute<BuildCheckFieldAttribute>(inherit: true);
                if (attr == null) continue;

                list.Add(new DiscoveredField(field, attr));
            }

            list.Sort((a, b) =>
            {
                int byCat = string.Compare(a.Category, b.Category, StringComparison.Ordinal);
                return byCat != 0
                    ? byCat
                    : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });

            _cached = list;
            _byId = list.ToDictionary(f => f.Id, f => f);

            Refreshed?.Invoke();
        }
    }
}
