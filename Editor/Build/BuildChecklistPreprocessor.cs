using System.Linq;
using Gameyogi.BuildChecklist.Editor.Evaluation;
using Gameyogi.BuildChecklist.Editor.Rules;
using Gameyogi.BuildChecklist.Editor.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Gameyogi.BuildChecklist.Editor.Build
{
    /// <summary>
    /// Runs every enabled rule in the project's <see cref="BuildChecklistRules"/>
    /// asset before each build. Logs all results to the Unity console; throws
    /// <see cref="BuildFailedException"/> when any Error-severity rule fails,
    /// which aborts the build before player code is compiled.
    /// </summary>
    internal sealed class BuildChecklistPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Use FindExisting (not LoadOrCreate) so a build never silently
            // creates a rules asset when the user hasn't set one up yet.
            var rules = BuildChecklistRules.FindExisting();
            if (rules == null || rules.entries == null || rules.entries.Count == 0)
            {
                UnityEngine.Debug.Log("[Build Checklist] No rules asset / no rules — skipping.");
                return;
            }

            UnityEngine.Debug.Log($"[Build Checklist] Running {rules.activePanel} panel.");

            var results = RuleEvaluator.EvaluateAll(rules, EvaluationOptions.BuildPreprocess);
            ResultLogger.LogAll(results);

            var issues = results
                .Where(r => !r.Passed && (r.Severity == CheckSeverity.Warning || r.Severity == CheckSeverity.Error))
                .Where(r => !BuildChecklistSkipState.Contains(r))
                .ToList();

            if (issues.Count > 0 && !UnityEngine.Application.isBatchMode)
            {
                BuildChecklistWindow.ShowBuildReview(rules, results, BuildChecklistSkipState.Keys);
                throw new BuildFailedException(
                    $"Build Checklist: {issues.Count} issue(s) need review. "
                    + "Fix or skip them in Window -> Build Checklist, then start the build again.");
            }

            int blocking = results.Count(r => r.BlocksBuild);
            if (blocking > 0)
            {
                throw new BuildFailedException(
                    $"Build Checklist: {blocking} rule(s) failed with Error severity. " +
                    "Open Window → Build Checklist for details.");
            }
        }
    }
}
