using System.Collections.Generic;
using System.Text;
using Gameyogi.BuildChecklist.Editor.Evaluation;
using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEngine;

namespace Gameyogi.BuildChecklist.Editor.Build
{
    /// <summary>
    /// Formats and prints <see cref="EvaluationResult"/>s to the Unity console.
    /// One log entry per result; severity drives Debug.Log / LogWarning / LogError
    /// so failures show up red in CI logs.
    /// </summary>
    public static class ResultLogger
    {
        public static void LogAll(IReadOnlyList<EvaluationResult> results)
        {
            if (results == null || results.Count == 0) return;

            int passCount = 0, warnCount = 0, errorCount = 0;
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (r.Passed) { passCount++; continue; }
                if (r.Severity == CheckSeverity.Error) errorCount++;
                else if (r.Severity == CheckSeverity.Warning) warnCount++;
            }

            Debug.Log($"[Build Checklist] {results.Count} result(s) — " +
                      $"{passCount} pass, {warnCount} warn, {errorCount} error.");

            for (int i = 0; i < results.Count; i++)
                LogOne(results[i]);
        }

        private static void LogOne(EvaluationResult r)
        {
            var sb = new StringBuilder(128);
            sb.Append("[Build Checklist] ").Append(StatusIcon(r)).Append(' ');
            sb.Append(r.DisplayName);
            if (!string.IsNullOrEmpty(r.OperatorDescription))
                sb.Append(" (").Append(r.OperatorDescription).Append(')');
            if (!string.IsNullOrEmpty(r.AssetPath))
                sb.Append(" — ").Append(r.AssetPath);
            if (!string.IsNullOrEmpty(r.Message))
                sb.Append(" — ").Append(r.Message);

            string line = sb.ToString();

            if (r.Passed)
            {
                Debug.Log(line, r.Asset);
            }
            else if (r.Severity == CheckSeverity.Error)
            {
                Debug.LogError(line, r.Asset);
            }
            else if (r.Severity == CheckSeverity.Warning)
            {
                Debug.LogWarning(line, r.Asset);
            }
            else
            {
                Debug.Log(line, r.Asset);
            }
        }

        private static string StatusIcon(EvaluationResult r)
        {
            switch (r.Kind)
            {
                case ResultKind.Pass: return "PASS";
                case ResultKind.Fail: return "FAIL";
                case ResultKind.MissingField: return "MISSING";
                case ResultKind.NoInstances: return "NO-ASSET";
                case ResultKind.BrokenRule: return "BROKEN";
                default: return "?";
            }
        }
    }
}
