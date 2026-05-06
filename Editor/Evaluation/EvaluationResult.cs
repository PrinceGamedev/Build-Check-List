using Gameyogi.BuildChecklist.Editor.Rules;
using UnityObject = UnityEngine.Object;

namespace Gameyogi.BuildChecklist.Editor.Evaluation
{
    /// <summary>Why an evaluation produced its status.</summary>
    public enum ResultKind
    {
        /// <summary>Rule was evaluated and the value satisfies it.</summary>
        Pass = 0,
        /// <summary>Rule was evaluated and the value violates it.</summary>
        Fail = 1,
        /// <summary>Rule references a field that no longer exists.</summary>
        MissingField = 2,
        /// <summary>No asset of the declaring type exists in the project.</summary>
        NoInstances = 3,
        /// <summary>Rule is malformed (operator/type mismatch, missing expected value, …).</summary>
        BrokenRule = 4
    }

    /// <summary>
    /// Outcome of evaluating one <see cref="RuleEntry"/> against one asset (or
    /// against "the absence of an asset" in the <see cref="ResultKind.NoInstances"/>
    /// case). One <see cref="RuleEntry"/> may produce multiple results when its
    /// declaring type has multiple asset instances.
    /// </summary>
    public sealed class EvaluationResult
    {
        public ResultKind Kind { get; }
        public CheckSeverity Severity { get; }

        public string FieldId { get; }
        public string DisplayName { get; }       // e.g. "GameConfig.targetFps"
        public string OperatorDescription { get; } // e.g. "== 60", "in [0.1 .. 1.0]"
        public string CurrentValueText { get; }    // e.g. "75", "<missing>"

        public UnityObject Asset { get; }
        public string AssetPath { get; }
        public string TargetAssetPath { get; }
        public string TargetObjectPath { get; }
        public RuleSettings RuleSettings { get; }

        public string Message { get; }

        public bool Passed => Kind == ResultKind.Pass;

        /// <summary>
        /// True iff this result should abort the build:
        /// non-pass result with Error severity. NoInstances and MissingField
        /// inherit the rule's severity so users can decide if they're fatal.
        /// </summary>
        public bool BlocksBuild => !Passed && Severity == CheckSeverity.Error;

        public EvaluationResult(
            ResultKind kind,
            CheckSeverity severity,
            string fieldId,
            string displayName,
            string operatorDescription,
            string currentValueText,
            UnityObject asset,
            string assetPath,
            string message,
            string targetAssetPath = null,
            string targetObjectPath = null,
            RuleSettings ruleSettings = null)
        {
            Kind = kind;
            Severity = severity;
            FieldId = fieldId;
            DisplayName = displayName;
            OperatorDescription = operatorDescription;
            CurrentValueText = currentValueText;
            Asset = asset;
            AssetPath = assetPath;
            Message = message;
            TargetAssetPath = targetAssetPath;
            TargetObjectPath = targetObjectPath;
            RuleSettings = ruleSettings;
        }
    }
}
