using System;
using System.Collections.Generic;
using System.Linq;
using Gameyogi.BuildChecklist.Editor.Discovery;
using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Gameyogi.BuildChecklist.Editor.UI
{
    /// <summary>
    /// Rules tab content. Shows every discovered field grouped by Category,
    /// plus any "missing field" rules that no longer match a discovered field.
    /// </summary>
    internal sealed class RulesTabView : VisualElement
    {
        private readonly BuildChecklistRules _rules;
        private readonly BuildCheckPanel _panel;
        private readonly ToolbarSearchField _search;
        private readonly ScrollView _scroll;
        private string _searchFilter = string.Empty;

        public RulesTabView(BuildChecklistRules rules, BuildCheckPanel panel)
        {
            _rules = rules;
            _panel = panel;
            style.flexGrow = 1;

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4,
                    alignItems = Align.Center
                }
            };

            _search = new ToolbarSearchField { style = { flexGrow = 1 } };
            _search.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue?.Trim() ?? string.Empty;
                Rebuild();
            });
            header.Add(_search);

            Add(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            Add(_scroll);
        }

        public void Rebuild()
        {
            _scroll.Clear();

            var discovered = AttributedFieldDiscovery.GetAll();
            if (discovered == null || discovered.Count == 0)
            {
                _scroll.Add(InfoLine("No fields tagged with [BuildCheckField] found in this project. " +
                                     "Add the attribute to a serialized field on a MonoBehaviour and click Refresh discovery."));
                AppendMissingRulesSection();
                return;
            }

            var byCategory = discovered
                .Where(MatchesFilter)
                .GroupBy(f => f.Category)
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            bool any = false;
            foreach (var group in byCategory)
            {
                any = true;
                var foldout = new Foldout { text = group.Key, value = true };
                foldout.style.marginBottom = 4;
                foreach (var field in group.OrderBy(f => f.DisplayName, StringComparer.Ordinal))
                {
                    var row = new RuleRowElement(field, _rules, _panel);
                    foldout.Add(row);
                }
                _scroll.Add(foldout);
            }

            if (!any)
                _scroll.Add(InfoLine("No fields match the current search."));

            AppendMissingRulesSection();
        }

        private bool MatchesFilter(DiscoveredField f)
        {
            if (string.IsNullOrEmpty(_searchFilter)) return true;
            return f.DisplayName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                || f.Category.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // 6.9 — surface rules whose fieldId no longer resolves so the user can clean up.
        private void AppendMissingRulesSection()
        {
            if (_rules == null || _rules.entries == null || _rules.entries.Count == 0) return;

            var missing = _rules.entries
                .Where(e => e != null
                            && !string.IsNullOrEmpty(e.fieldId)
                            && AttributedFieldDiscovery.FindById(e.fieldId) == null)
                .ToList();

            if (missing.Count == 0) return;

            var foldout = new Foldout { text = $"Missing fields ({missing.Count})", value = true };
            foldout.style.marginTop = 8;

            foreach (var entry in missing)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2,
                        marginBottom = 2
                    }
                };
                row.Add(new Label($"🚫 {entry.fieldId}") { style = { flexGrow = 1 } });

                var removeBtn = new Button(() =>
                {
                    _rules.Remove(entry);
                    AssetDatabase.SaveAssetIfDirty(_rules);
                    Rebuild();
                })
                { text = "Remove" };
                row.Add(removeBtn);

                foldout.Add(row);
            }
            _scroll.Add(foldout);
        }

        private static Label InfoLine(string text)
        {
            var l = new Label(text) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 8, marginBottom = 8 } };
            return l;
        }
    }
}
