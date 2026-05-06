using System;
using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEditor;

namespace Gameyogi.BuildChecklist.Editor.Evaluation
{
    /// <summary>
    /// Pure comparison logic. Takes a <see cref="SerializedProperty"/> (the
    /// current value on an asset) and a <see cref="RuleSettings"/>, returns
    /// pass/fail plus a human-readable description of the current value.
    /// No Unity-asset I/O happens here.
    /// </summary>
    public static class OperatorEvaluator
    {
        public readonly struct OperatorOutcome
        {
            public readonly bool Passed;
            public readonly string CurrentValueText;
            public readonly string FailureReason;     // null when passed
            public readonly bool RuleIsBroken;        // op/type mismatch, missing data

            public OperatorOutcome(bool passed, string currentValueText,
                                   string failureReason, bool ruleIsBroken = false)
            {
                Passed = passed;
                CurrentValueText = currentValueText;
                FailureReason = failureReason;
                RuleIsBroken = ruleIsBroken;
            }

            public static OperatorOutcome Pass(string currentValueText)
                => new OperatorOutcome(true, currentValueText, null);

            public static OperatorOutcome Fail(string currentValueText, string reason)
                => new OperatorOutcome(false, currentValueText, reason);

            public static OperatorOutcome Broken(string currentValueText, string reason)
                => new OperatorOutcome(false, currentValueText, reason, ruleIsBroken: true);
        }

        public static OperatorOutcome Evaluate(SerializedProperty prop, RuleSettings rule)
        {
            if (prop == null)
                return OperatorOutcome.Broken("<no property>", "Could not resolve serialized property.");
            if (rule == null)
                return OperatorOutcome.Broken("<no rule>", "Rule is null.");

            string current = DescribeCurrent(prop);

            switch (rule.op)
            {
                case ComparisonOp.Equals: return EvalEquals(prop, rule, current, expected: true);
                case ComparisonOp.NotEquals: return EvalEquals(prop, rule, current, expected: false);
                case ComparisonOp.Min: return EvalMinMax(prop, rule, current, isMin: true);
                case ComparisonOp.Max: return EvalMinMax(prop, rule, current, isMin: false);
                case ComparisonOp.Range: return EvalRange(prop, rule, current);
                case ComparisonOp.NotNull: return EvalNotNull(prop, current);
                case ComparisonOp.NotEmpty: return EvalNotEmpty(prop, current);
                case ComparisonOp.OneOf: return EvalOneOf(prop, rule, current);
                default:
                    return OperatorOutcome.Broken(current, $"Unknown operator {rule.op}.");
            }
        }

        public static string DescribeOperator(RuleSettings rule)
        {
            if (rule == null) return "<no rule>";
            switch (rule.op)
            {
                case ComparisonOp.Equals: return $"== {rule.expected.Describe()}";
                case ComparisonOp.NotEquals: return $"!= {rule.expected.Describe()}";
                case ComparisonOp.Min: return $">= {rule.expected.Describe()}";
                case ComparisonOp.Max: return $"<= {rule.expected.Describe()}";
                case ComparisonOp.Range: return $"in [{rule.expected.Describe()} .. {rule.expectedMax.Describe()}]";
                case ComparisonOp.NotNull: return "is assigned";
                case ComparisonOp.NotEmpty: return "is not empty";
                case ComparisonOp.OneOf: return "in {" + string.Join(", ", DescribeOneOf(rule)) + "}";
                default: return rule.op.ToString();
            }
        }

        // ── Operator implementations ──────────────────────────────────────────

        private static OperatorOutcome EvalEquals(SerializedProperty prop, RuleSettings rule, string current, bool expected)
        {
            if (!TryReadAsBoxed(prop, out var actual, out var err))
                return OperatorOutcome.Broken(current, err);

            var exp = rule.expected?.GetBoxed();
            bool eq = ValuesEqual(actual, exp);
            bool passed = eq == expected;
            return passed
                ? OperatorOutcome.Pass(current)
                : OperatorOutcome.Fail(current,
                    expected
                        ? $"expected {rule.expected.Describe()} but was {current}"
                        : $"expected anything other than {rule.expected.Describe()}");
        }

        private static OperatorOutcome EvalMinMax(SerializedProperty prop, RuleSettings rule, string current, bool isMin)
        {
            if (!TryReadAsDouble(prop, out var d, out var err))
                return OperatorOutcome.Broken(current, err);
            if (!TryReadExpectedAsDouble(rule.expected, out var threshold, out var err2))
                return OperatorOutcome.Broken(current, err2);

            bool ok = isMin ? d >= threshold : d <= threshold;
            return ok
                ? OperatorOutcome.Pass(current)
                : OperatorOutcome.Fail(current,
                    isMin ? $"expected >= {threshold} but was {d}"
                          : $"expected <= {threshold} but was {d}");
        }

        private static OperatorOutcome EvalRange(SerializedProperty prop, RuleSettings rule, string current)
        {
            if (!TryReadAsDouble(prop, out var d, out var err))
                return OperatorOutcome.Broken(current, err);
            if (!TryReadExpectedAsDouble(rule.expected, out var lo, out var errLo))
                return OperatorOutcome.Broken(current, errLo);
            if (!TryReadExpectedAsDouble(rule.expectedMax, out var hi, out var errHi))
                return OperatorOutcome.Broken(current, errHi);

            if (lo > hi)
                return OperatorOutcome.Broken(current, $"Range lower bound {lo} > upper bound {hi}.");

            bool ok = d >= lo && d <= hi;
            return ok
                ? OperatorOutcome.Pass(current)
                : OperatorOutcome.Fail(current, $"expected in [{lo} .. {hi}] but was {d}");
        }

        private static OperatorOutcome EvalNotNull(SerializedProperty prop, string current)
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                return OperatorOutcome.Broken(current, "NotNull only valid on Object reference fields.");
            return prop.objectReferenceValue != null
                ? OperatorOutcome.Pass(current)
                : OperatorOutcome.Fail(current, "expected an assigned reference, was null");
        }

        private static OperatorOutcome EvalNotEmpty(SerializedProperty prop, string current)
        {
            if (prop.propertyType == SerializedPropertyType.String)
            {
                return !string.IsNullOrEmpty(prop.stringValue)
                    ? OperatorOutcome.Pass(current)
                    : OperatorOutcome.Fail(current, "expected non-empty string");
            }
            if (prop.isArray)
            {
                return prop.arraySize > 0
                    ? OperatorOutcome.Pass(current)
                    : OperatorOutcome.Fail(current, "expected non-empty collection");
            }
            return OperatorOutcome.Broken(current, "NotEmpty only valid on string or collection fields.");
        }

        private static OperatorOutcome EvalOneOf(SerializedProperty prop, RuleSettings rule, string current)
        {
            if (rule.oneOfValues == null || rule.oneOfValues.Count == 0)
                return OperatorOutcome.Broken(current, "OneOf rule has no allowed values.");
            if (!TryReadAsBoxed(prop, out var actual, out var err))
                return OperatorOutcome.Broken(current, err);

            for (int i = 0; i < rule.oneOfValues.Count; i++)
            {
                var candidate = rule.oneOfValues[i]?.GetBoxed();
                if (ValuesEqual(actual, candidate))
                    return OperatorOutcome.Pass(current);
            }
            return OperatorOutcome.Fail(current,
                $"expected one of {{{string.Join(", ", DescribeOneOf(rule))}}} but was {current}");
        }

        // ── Reading helpers ───────────────────────────────────────────────────

        private static bool TryReadAsBoxed(SerializedProperty prop, out object value, out string err)
        {
            err = null;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: value = prop.intValue; return true;
                case SerializedPropertyType.Float: value = prop.floatValue; return true;
                case SerializedPropertyType.Boolean: value = prop.boolValue; return true;
                case SerializedPropertyType.String: value = prop.stringValue ?? ""; return true;
                case SerializedPropertyType.Enum: value = prop.intValue; return true;
                case SerializedPropertyType.ObjectReference: value = prop.objectReferenceValue; return true;
                default:
                    value = null;
                    err = $"Unsupported field type: {prop.propertyType}.";
                    return false;
            }
        }

        private static bool TryReadAsDouble(SerializedProperty prop, out double value, out string err)
        {
            err = null;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: value = prop.intValue; return true;
                case SerializedPropertyType.Float: value = prop.floatValue; return true;
                case SerializedPropertyType.Enum: value = prop.intValue; return true;
                default:
                    value = 0;
                    err = $"Numeric operator not valid on field type {prop.propertyType}.";
                    return false;
            }
        }

        private static bool TryReadExpectedAsDouble(SerializedValue expected, out double value, out string err)
        {
            err = null;
            if (expected == null) { value = 0; err = "Expected value is missing."; return false; }
            switch (expected.kind)
            {
                case ValueKind.Int: value = expected.intValue; return true;
                case ValueKind.Float: value = expected.floatValue; return true;
                case ValueKind.Enum: value = expected.intValue; return true;
                default:
                    value = 0;
                    err = $"Numeric operator requires int/float/enum expected value (got {expected.kind}).";
                    return false;
            }
        }

        // ── Equality ──────────────────────────────────────────────────────────

        private static bool ValuesEqual(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            // Numeric coercion (int vs float on either side)
            if (IsNumeric(a) && IsNumeric(b))
                return Convert.ToDouble(a) == Convert.ToDouble(b);

            return a.Equals(b);
        }

        private static bool IsNumeric(object o) => o is int || o is float || o is double || o is long || o is short;

        private static string DescribeCurrent(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("R");
                case SerializedPropertyType.Boolean: return prop.boolValue ? "true" : "false";
                case SerializedPropertyType.String: return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.intValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue ? prop.objectReferenceValue.name : "<null>";
                default: return $"<{prop.propertyType}>";
            }
        }

        private static string[] DescribeOneOf(RuleSettings rule)
        {
            var arr = new string[rule.oneOfValues.Count];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = rule.oneOfValues[i]?.Describe() ?? "<unset>";
            return arr;
        }
    }
}
