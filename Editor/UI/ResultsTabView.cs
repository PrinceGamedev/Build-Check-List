using System;
using System.Collections.Generic;
using System.Linq;
using Gameyogi.BuildChecklist.Editor.Discovery;
using Gameyogi.BuildChecklist.Editor.Evaluation;
using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gameyogi.BuildChecklist.Editor.UI
{
    /// <summary>
    /// Results tab. Shows a filterable table of every <see cref="EvaluationResult"/>
    /// produced by the most recent run. Rows ping the offending asset on click,
    /// and open the declaring script on double-click.
    /// </summary>
    internal sealed class ResultsTabView : VisualElement
    {
        private enum StatusFilter { All, Failing, Passing, Skipped }
        private enum SeverityFilter { All, Info, Warning, Error }

        private List<EvaluationResult> _all = new List<EvaluationResult>();
        private List<EvaluationResult> _filtered = new List<EvaluationResult>();

        private Label _summary;
        private ListView _list;
        private VisualElement _detail;
        private Label _detailMessage;
        private Button _pingBtn;
        private Button _openScriptBtn;
        private Button _fixSelectedBtn;
        private Button _skipSelectedBtn;
        private Button _continueBuildBtn;
        private Button _cancelBuildBtn;
        private HashSet<string> _skipped = BuildChecklistSkipState.Keys;
        private HashSet<string> _fixed = new HashSet<string>();
        private Action _continueBuild;
        private Action _cancelBuild;
        private bool _buildReviewMode;
        private EvaluationResult _selected;

        private StatusFilter _statusFilter = StatusFilter.All;
        private SeverityFilter _severityFilter = SeverityFilter.All;
        private string _search = string.Empty;

        public ResultsTabView()
        {
            style.flexGrow = 1;
            BuildLayout();
            ApplyFilter();
        }

        // ── Public API (called by window) ──────────────────────────────────────

        public void SetResults(List<EvaluationResult> results)
        {
            _all = results ?? new List<EvaluationResult>();
            _selected = null;
            _fixed.Clear();
            ApplyFilter();
        }

        public void SetSkippedKeys(HashSet<string> skipped)
        {
            _skipped = skipped ?? new HashSet<string>();
            ApplyFilter();
        }

        public void SetBuildReviewMode(bool enabled, Action continueBuild, Action cancelBuild)
        {
            _buildReviewMode = enabled;
            _continueBuild = continueBuild;
            _cancelBuild = cancelBuild;
            UpdateDetail();
            UpdateBuildReviewButtons();
        }

        // ── Layout ─────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            // Filter toolbar
            var filterRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4,
                    flexWrap = Wrap.Wrap
                }
            };

            filterRow.Add(new Label("Status") { style = { marginRight = 4 } });
            var statusField = new EnumField(_statusFilter) { style = { minWidth = 90, marginRight = 8 } };
            statusField.RegisterValueChangedCallback(evt =>
            {
                _statusFilter = (StatusFilter)evt.newValue;
                ApplyFilter();
            });
            filterRow.Add(statusField);

            filterRow.Add(new Label("Severity") { style = { marginRight = 4 } });
            var sevField = new EnumField(_severityFilter) { style = { minWidth = 90, marginRight = 8 } };
            sevField.RegisterValueChangedCallback(evt =>
            {
                _severityFilter = (SeverityFilter)evt.newValue;
                ApplyFilter();
            });
            filterRow.Add(sevField);

            var search = new ToolbarSearchField { style = { flexGrow = 1, minWidth = 140 } };
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue?.Trim() ?? string.Empty;
                ApplyFilter();
            });
            filterRow.Add(search);

            Add(filterRow);

            _summary = new Label(string.Empty)
            {
                style = { marginBottom = 4, unityFontStyleAndWeight = FontStyle.Bold }
            };
            Add(_summary);

            Add(BuildHeaderRow());

            _list = new ListView
            {
                fixedItemHeight = 22,
                style = { flexGrow = 1 },
                makeItem = MakeRow,
                bindItem = BindRow,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            _list.itemsSource = _filtered;
#if UNITY_2022_2_OR_NEWER
            _list.selectionChanged += OnSelectionChanged;
            _list.itemsChosen += OnItemsChosen;          // fires on double-click / Enter
#else
            _list.onSelectionChange += OnSelectionChanged;
            _list.onItemsChosen += OnItemsChosen;
#endif
            Add(_list);

            BuildDetailPanel();
        }

        private VisualElement BuildHeaderRow()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingLeft = 4, paddingRight = 4,
                    paddingTop = 2, paddingBottom = 2,
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(new Color(0, 0, 0, 0.2f)),
                    backgroundColor = new StyleColor(new Color(0, 0, 0, 0.06f))
                }
            };
            row.Add(HeaderCell("", 26));
            row.Add(HeaderCell("Field", 220));
            row.Add(HeaderCell("Rule", 170));
            row.Add(HeaderCell("Current", 100));
            row.Add(HeaderCell("Asset", 0, flex: true));
            return row;
        }

        private static Label HeaderCell(string text, float width, bool flex = false)
        {
            var l = new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 4, paddingRight = 4
                }
            };
            if (flex) l.style.flexGrow = 1; else l.style.width = width;
            return l;
        }

        private void BuildDetailPanel()
        {
            _detail = new VisualElement
            {
                style =
                {
                    marginTop = 4,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4,
                    borderTopWidth = 1,
                    borderTopColor = new StyleColor(new Color(0, 0, 0, 0.2f))
                }
            };

            _detailMessage = new Label("Select a result row.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 4 }
            };
            _detail.Add(_detailMessage);

            var btnRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row }
            };
            _pingBtn = new Button(PingSelectedAsset) { text = "Ping Asset" };
            _pingBtn.SetEnabled(false);
            btnRow.Add(_pingBtn);

            _openScriptBtn = new Button(OpenSelectedScript) { text = "Open Script" };
            _openScriptBtn.SetEnabled(false);
            btnRow.Add(_openScriptBtn);

            _fixSelectedBtn = new Button(FixSelected) { text = "Fix Selected" };
            _fixSelectedBtn.SetEnabled(false);
            btnRow.Add(_fixSelectedBtn);

            _skipSelectedBtn = new Button(ToggleSkipSelected) { text = "Skip Selected" };
            _skipSelectedBtn.SetEnabled(false);
            btnRow.Add(_skipSelectedBtn);

            btnRow.Add(new Button(FixAllVisible) { text = "Fix All Visible" });

            _detail.Add(btnRow);

            var buildReviewRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginTop = 4 }
            };
            buildReviewRow.Add(new Label("Build review")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 8 }
            });
            _cancelBuildBtn = new Button(() => _cancelBuild?.Invoke()) { text = "Close" };
            buildReviewRow.Add(_cancelBuildBtn);
            _continueBuildBtn = new Button(() => _continueBuild?.Invoke()) { text = "Recheck" };
            buildReviewRow.Add(_continueBuildBtn);
            _detail.Add(buildReviewRow);

            Add(_detail);
        }

        // ── Row construction ──────────────────────────────────────────────────

        private VisualElement MakeRow()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 4, paddingRight = 4
                }
            };
            row.Add(new Label { name = "status", style = { width = 26 } });
            row.Add(new Label { name = "field", style = { width = 220 } });
            row.Add(new Label { name = "rule", style = { width = 170 } });
            row.Add(new Label { name = "current", style = { width = 100 } });
            row.Add(new Label { name = "asset", style = { flexGrow = 1 } });
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _filtered.Count) return;
            var r = _filtered[index];

            var status = element.Q<Label>("status");
            var field = element.Q<Label>("field");
            var rule = element.Q<Label>("rule");
            var current = element.Q<Label>("current");
            var asset = element.Q<Label>("asset");

            status.text = StatusGlyph(r);
            status.style.color = StatusColor(r);
            field.text = r.DisplayName ?? string.Empty;
            rule.text = r.OperatorDescription ?? string.Empty;
            current.text = r.CurrentValueText ?? string.Empty;
            asset.text = string.IsNullOrEmpty(r.AssetPath) ? "—" : System.IO.Path.GetFileName(r.AssetPath);
            asset.tooltip = r.AssetPath ?? string.Empty;
        }

        private static string StatusGlyph(EvaluationResult r)
        {
            switch (r.Kind)
            {
                case ResultKind.Pass: return "✓";
                case ResultKind.Fail: return "✗";
                case ResultKind.MissingField: return "?";
                case ResultKind.NoInstances: return "∅";
                case ResultKind.BrokenRule: return "!";
                default: return "·";
            }
        }

        private static StyleColor StatusColor(EvaluationResult r)
        {
            if (r.Passed) return new StyleColor(new Color(0.40f, 0.75f, 0.40f));
            if (r.Severity == CheckSeverity.Error) return new StyleColor(new Color(0.85f, 0.35f, 0.35f));
            if (r.Severity == CheckSeverity.Warning) return new StyleColor(new Color(0.95f, 0.70f, 0.30f));
            return new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        }

        // ── Selection / actions ───────────────────────────────────────────────

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            _selected = selection?.OfType<EvaluationResult>().FirstOrDefault();
            UpdateDetail();
        }

        private void OnItemsChosen(IEnumerable<object> chosen)
        {
            var r = chosen?.OfType<EvaluationResult>().FirstOrDefault();
            if (r == null) return;

            // Double-click: ping if there's an asset; if there isn't, open the script.
            if (r.Asset != null) PingAsset(r.Asset);
            else OpenScriptFor(r);
        }

        private void UpdateDetail()
        {
            if (_selected == null)
            {
                _detailMessage.text = "Select a result row.";
            _pingBtn.SetEnabled(false);
            _openScriptBtn.SetEnabled(false);
            _fixSelectedBtn.SetEnabled(false);
            _skipSelectedBtn.SetEnabled(false);
            return;
            }

            string status = StatusGlyph(_selected) + "  " + _selected.Kind + " (" + _selected.Severity + ")";
            string body = string.IsNullOrEmpty(_selected.Message)
                ? $"{_selected.DisplayName} {_selected.OperatorDescription} — current: {_selected.CurrentValueText}"
                : _selected.Message;

            _detailMessage.text = status + "\n" + body;
            _pingBtn.SetEnabled(_selected.Asset != null);
            _openScriptBtn.SetEnabled(true);
            _fixSelectedBtn.SetEnabled(RuleAutoFixer.CanFix(_selected));
            _skipSelectedBtn.SetEnabled(!_selected.Passed);
            _skipSelectedBtn.text = IsSkipped(_selected) ? "Unskip Selected" : "Skip Selected";
            UpdateBuildReviewButtons();
        }

        private void PingSelectedAsset()
        {
            if (_selected?.Asset != null) PingAsset(_selected.Asset);
        }

        private void OpenSelectedScript()
        {
            if (_selected != null) OpenScriptFor(_selected);
        }

        private void FixSelected()
        {
            if (_selected == null) return;

            bool fixedOne = RuleAutoFixer.Fix(_selected);
            if (fixedOne)
                _fixed.Add(SkipKey(_selected));
            EditorUtility.DisplayDialog(
                "Build Checklist",
                fixedOne
                    ? (_buildReviewMode ? "Fixed selected issue. Validation will run again." : "Fixed selected issue. Click Run Now to re-check.")
                    : "This issue cannot be auto-fixed.",
                "OK");
            if (fixedOne && _buildReviewMode)
                _continueBuild?.Invoke();
            else
                ApplyFilter();
        }

        private void ToggleSkipSelected()
        {
            if (_selected == null || _selected.Passed) return;

            BuildChecklistSkipState.Toggle(_selected);

            ApplyFilter();
        }

        private void FixAllVisible()
        {
            int fixedCount = RuleAutoFixer.FixAll(_filtered);
            if (fixedCount > 0)
            {
                foreach (var r in _filtered)
                    if (RuleAutoFixer.CanFix(r))
                        _fixed.Add(SkipKey(r));
            }
            EditorUtility.DisplayDialog(
                "Build Checklist",
                fixedCount > 0
                    ? $"Fixed {fixedCount} issue(s). Click Run Now to re-check."
                    : "No visible issues could be auto-fixed.",
                "OK");
            if (fixedCount > 0 && _buildReviewMode)
                _continueBuild?.Invoke();
            else
                ApplyFilter();
        }

        private static void PingAsset(UnityEngine.Object asset)
        {
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private static void OpenScriptFor(EvaluationResult result)
        {
            // Find the C# script that declares this result's field.
            var field = AttributedFieldDiscovery.FindById(result.FieldId);
            if (field == null)
            {
                if (result.Asset != null)
                    AssetDatabase.OpenAsset(result.Asset);
                return;
            }

            var monoScript = FindMonoScriptFor(field.DeclaringType);
            if (monoScript != null)
                AssetDatabase.OpenAsset(monoScript);
            else if (result.Asset != null)
                AssetDatabase.OpenAsset(result.Asset);
        }

        private static MonoScript FindMonoScriptFor(Type type)
        {
            if (type == null) return null;

            // Scan MonoScript assets for one whose class matches.
            var guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == type)
                    return ms;
            }
            return null;
        }

        // ── Filtering ─────────────────────────────────────────────────────────

        private void ApplyFilter()
        {
            _filtered.Clear();
            foreach (var r in _all)
            {
                if (!StatusMatches(r)) continue;
                if (!SeverityMatches(r)) continue;
                if (!SearchMatches(r)) continue;
                _filtered.Add(r);
            }

            _list.itemsSource = _filtered;
            _list.Rebuild();

            UpdateSummary();
            UpdateDetail();
        }

        private bool StatusMatches(EvaluationResult r)
        {
            switch (_statusFilter)
            {
                case StatusFilter.All: return true;
                case StatusFilter.Passing: return r.Kind == ResultKind.Pass;
                case StatusFilter.Failing: return r.Kind == ResultKind.Fail || r.Kind == ResultKind.BrokenRule;
                case StatusFilter.Skipped: return r.Kind == ResultKind.MissingField || r.Kind == ResultKind.NoInstances;
                default: return true;
            }
        }

        private bool SeverityMatches(EvaluationResult r)
        {
            switch (_severityFilter)
            {
                case SeverityFilter.All: return true;
                case SeverityFilter.Info: return r.Severity == CheckSeverity.Info;
                case SeverityFilter.Warning: return r.Severity == CheckSeverity.Warning;
                case SeverityFilter.Error: return r.Severity == CheckSeverity.Error;
                default: return true;
            }
        }

        private bool SearchMatches(EvaluationResult r)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            return Contains(r.DisplayName) || Contains(r.OperatorDescription)
                || Contains(r.CurrentValueText) || Contains(r.AssetPath) || Contains(r.Message);
        }

        private bool Contains(string s)
            => !string.IsNullOrEmpty(s)
               && s.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        private bool IsSkipped(EvaluationResult result)
            => result != null && _skipped.Contains(SkipKey(result));

        public static string SkipKey(EvaluationResult result)
            => BuildChecklistSkipState.SkipKey(result);

        private void UpdateBuildReviewButtons()
        {
            if (_continueBuildBtn == null || _cancelBuildBtn == null) return;
            _continueBuildBtn.style.display = _buildReviewMode ? DisplayStyle.Flex : DisplayStyle.None;
            _cancelBuildBtn.style.display = _buildReviewMode ? DisplayStyle.Flex : DisplayStyle.None;
            _continueBuildBtn.SetEnabled(true);
        }

        private void UpdateSummary()
        {
            if (_all.Count == 0) { _summary.text = "No results yet — click \"Run Now ▶\" in the toolbar."; return; }

            int pass = 0, warn = 0, err = 0;
            foreach (var r in _all)
            {
                if (r.Passed) pass++;
                else if (r.Severity == CheckSeverity.Error) err++;
                else if (r.Severity == CheckSeverity.Warning) warn++;
            }
            _summary.text = $"{_all.Count} result(s)  —  {pass} pass · {warn} warn · {err} error" +
                            (_filtered.Count != _all.Count ? $"   (showing {_filtered.Count})" : string.Empty);
        }
    }
}
