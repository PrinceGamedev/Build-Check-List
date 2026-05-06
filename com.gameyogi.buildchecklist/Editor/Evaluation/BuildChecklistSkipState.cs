using System.Collections.Generic;

namespace Gameyogi.BuildChecklist.Editor.Evaluation
{
    public static class BuildChecklistSkipState
    {
        private static readonly HashSet<string> Skipped = new HashSet<string>();

        public static HashSet<string> Keys => Skipped;

        public static bool Contains(EvaluationResult result)
            => result != null && Skipped.Contains(SkipKey(result));

        public static void Toggle(EvaluationResult result)
        {
            if (result == null) return;
            var key = SkipKey(result);
            if (Skipped.Contains(key)) Skipped.Remove(key);
            else Skipped.Add(key);
        }

        public static string SkipKey(EvaluationResult result)
        {
            if (result == null) return string.Empty;
            return $"{result.FieldId}|{result.TargetAssetPath}|{result.TargetObjectPath}|{result.CurrentValueText}|{result.OperatorDescription}";
        }
    }
}
