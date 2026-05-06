using Gameyogi.BuildChecklist.Editor.Discovery;
using Gameyogi.BuildChecklist.Editor.Evaluation;
using Gameyogi.BuildChecklist.Editor.Rules;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gameyogi.BuildChecklist.Editor.UI
{
    /// <summary>
    /// Single window that hosts both the Rules tab (define rules per field)
    /// and the Results tab (live evaluation status).
    /// </summary>
    public sealed class BuildChecklistWindow : EditorWindow
    {
        private enum Tab { Rules, Results }

        private Tab _activeTab = Tab.Rules;
        private BuildChecklistRules _rules;
        private RulesTabView _rulesView;
        private ResultsTabView _resultsView;
        private VisualElement _bodyContainer;
        private ToolbarToggle _rulesToggle;
        private ToolbarToggle _resultsToggle;
        private EnumField _panelField;

        [MenuItem("Window/Build Checklist", priority = 2200)]
        public static void Open()
        {
            var window = GetWindow<BuildChecklistWindow>();
            window.titleContent = new GUIContent("Build Checklist");
            window.minSize = new Vector2(560, 320);
            window.Show();
        }

        public static void OpenWithResults(BuildChecklistRules rules, List<EvaluationResult> results)
        {
            var window = GetWindow<BuildChecklistWindow>();
            window.titleContent = new GUIContent("Build Checklist");
            window.minSize = new Vector2(560, 320);
            window.Show();

            window._rules = rules ?? BuildChecklistRules.LoadOrCreate();
            window.BuildLayout();
            window._resultsView.SetResults(results);
            window._resultsView.SetBuildReviewMode(false, null, null);
            window.ShowTab(Tab.Results);
            window.Focus();
        }

        public static BuildReviewAction ShowBuildReview(
            BuildChecklistRules rules,
            List<EvaluationResult> results,
            HashSet<string> skippedKeys)
        {
            var window = GetWindow<BuildChecklistWindow>();
            window.titleContent = new GUIContent("Build Checklist");
            window.minSize = new Vector2(760, 420);
            window.Show();
            window._rules = rules ?? BuildChecklistRules.LoadOrCreate();
            window.BuildLayout();
            window._resultsView.SetSkippedKeys(skippedKeys);
            window._resultsView.SetResults(results);
            window._resultsView.SetBuildReviewMode(
                true,
                () =>
                {
                    window.RunNow();
                },
                () =>
                {
                    window.Close();
                });
            window.ShowTab(Tab.Results);
            window.Focus();
            return BuildReviewAction.CancelBuild;
        }

        private void OnEnable()
        {
            _rules = BuildChecklistRules.LoadOrCreate();

            BuildLayout();

            AttributedFieldDiscovery.Refreshed += OnDiscoveryRefreshed;
        }

        private void OnDisable()
        {
            AttributedFieldDiscovery.Refreshed -= OnDiscoveryRefreshed;
        }

        private void OnDiscoveryRefreshed()
        {
            // Window may have been closed between scheduling and firing.
            if (rootVisualElement == null) return;
            _rulesView?.Rebuild();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            var root = rootVisualElement;
            root.Clear();

            root.Add(BuildToolbar());

            _bodyContainer = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4
                }
            };
            root.Add(_bodyContainer);

            _rulesView = new RulesTabView(_rules, _rules.activePanel);
            _resultsView = new ResultsTabView();

            ShowTab(_activeTab);
        }

        private Toolbar BuildToolbar()
        {
            var bar = new Toolbar();

            _rulesToggle = new ToolbarToggle { text = "Rules", value = _activeTab == Tab.Rules };
            _rulesToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowTab(Tab.Rules); else _rulesToggle.SetValueWithoutNotify(true);
            });
            bar.Add(_rulesToggle);

            _resultsToggle = new ToolbarToggle { text = "Results", value = _activeTab == Tab.Results };
            _resultsToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowTab(Tab.Results); else _resultsToggle.SetValueWithoutNotify(true);
            });
            bar.Add(_resultsToggle);

            bar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            bar.Add(new Label("Panel")
            {
                style =
                {
                    marginLeft = 6,
                    marginRight = 4,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            });

            _panelField = new EnumField(_rules.activePanel)
            {
                style = { minWidth = 110 }
            };
            _panelField.tooltip = "Select the active build-check panel. Rules are reused directly across panels.";
            _panelField.RegisterValueChangedCallback(evt =>
            {
                _rules.activePanel = (BuildCheckPanel)evt.newValue;
                EditorUtility.SetDirty(_rules);
                AssetDatabase.SaveAssetIfDirty(_rules);
                _rulesView = new RulesTabView(_rules, _rules.activePanel);
                if (_activeTab == Tab.Rules)
                    ShowTab(Tab.Rules);
            });
            bar.Add(_panelField);

            var refreshBtn = new ToolbarButton(() =>
            {
                AttributedFieldDiscovery.Refresh();
                _rulesView.Rebuild();
            })
            { text = "Refresh discovery" };
            bar.Add(refreshBtn);

            var runBtn = new ToolbarButton(RunNow) { text = "Run Now ▶" };
            bar.Add(runBtn);

            return bar;
        }

        private void ShowTab(Tab tab)
        {
            _activeTab = tab;
            _rulesToggle.SetValueWithoutNotify(tab == Tab.Rules);
            _resultsToggle.SetValueWithoutNotify(tab == Tab.Results);

            _bodyContainer.Clear();
            _bodyContainer.Add(tab == Tab.Rules ? (VisualElement)_rulesView : _resultsView);

            if (tab == Tab.Rules) _rulesView.Rebuild();
        }

        private void RunNow()
        {
            Debug.Log($"[Build Checklist] Running {_rules.activePanel} panel.");
            var results = RuleEvaluator.EvaluateAll(_rules, EvaluationOptions.BuildPreprocess);
            _resultsView.SetResults(results);
            ShowTab(Tab.Results);
        }
    }
}
