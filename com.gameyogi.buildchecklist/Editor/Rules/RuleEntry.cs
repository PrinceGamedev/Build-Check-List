using System;
using System.Collections.Generic;

namespace Gameyogi.BuildChecklist.Editor.Rules
{
    [Serializable]
    public sealed class RuleSettings
    {
        public bool enabled = true;
        public ComparisonOp op = ComparisonOp.Equals;
        public CheckSeverity severity = CheckSeverity.Error;
        public string assetGuid;
        public string scenePath;
        public string sceneObjectPath;
        public string customMessage;
        public SerializedValue expected = new SerializedValue();
        public SerializedValue expectedMax = new SerializedValue();
        public List<SerializedValue> oneOfValues = new List<SerializedValue>();

        public static RuleSettings FromLegacy(RuleEntry entry)
        {
            var settings = new RuleSettings();
            if (entry == null) return settings;

            settings.enabled = entry.enabled;
            settings.op = entry.op;
            settings.severity = entry.severity;
            settings.assetGuid = entry.assetGuid;
            settings.scenePath = string.Empty;
            settings.sceneObjectPath = string.Empty;
            settings.customMessage = entry.customMessage;
            settings.expected = CloneValue(entry.expected);
            settings.expectedMax = CloneValue(entry.expectedMax);
            settings.oneOfValues = CloneValues(entry.oneOfValues);
            return settings;
        }

        private static SerializedValue CloneValue(SerializedValue value)
        {
            if (value == null) return new SerializedValue();
            return new SerializedValue
            {
                kind = value.kind,
                intValue = value.intValue,
                floatValue = value.floatValue,
                boolValue = value.boolValue,
                stringValue = value.stringValue,
                enumTypeName = value.enumTypeName,
                objectValue = value.objectValue
            };
        }

        private static List<SerializedValue> CloneValues(List<SerializedValue> values)
        {
            var clone = new List<SerializedValue>();
            if (values == null) return clone;
            for (int i = 0; i < values.Count; i++)
                clone.Add(CloneValue(values[i]));
            return clone;
        }
    }

    [Serializable]
    public sealed class PanelRuleSettings
    {
        public BuildCheckPanel panel;
        public RuleSettings settings = new RuleSettings();
    }

    /// <summary>
    /// One row in the rules asset. References a discovered field by its stable id
    /// and carries the operator + expected value(s) + severity for that rule.
    /// </summary>
    [Serializable]
    public sealed class RuleEntry
    {
        /// <summary>Stable field id — see <c>DiscoveredField.Id</c>.</summary>
        public string fieldId;

        public bool enabled = true;
        public ComparisonOp op = ComparisonOp.Equals;
        public CheckSeverity severity = CheckSeverity.Error;

        /// <summary>
        /// Optional asset GUID. When empty, the rule applies to every asset
        /// instance of the field's declaring type. When set, the rule applies
        /// only to that single asset.
        /// </summary>
        public string assetGuid;

        /// <summary>
        /// Optional override message displayed in the results tab. When empty
        /// the engine generates a default message.
        /// </summary>
        public string customMessage;

        /// <summary>
        /// Primary expected value. Used by Equals / NotEquals / Min / Max and
        /// as the lower bound for Range. Unused for NotNull / NotEmpty.
        /// </summary>
        public SerializedValue expected = new SerializedValue();

        /// <summary>Range upper bound. Unused for non-Range operators.</summary>
        public SerializedValue expectedMax = new SerializedValue();

        /// <summary>Allowed values for OneOf. Unused for other operators.</summary>
        public List<SerializedValue> oneOfValues = new List<SerializedValue>();

        public List<PanelRuleSettings> panelSettings = new List<PanelRuleSettings>();

        public RuleSettings GetSettings(BuildCheckPanel panel)
        {
            if (panelSettings == null)
                panelSettings = new List<PanelRuleSettings>();

            for (int i = 0; i < panelSettings.Count; i++)
                if (panelSettings[i] != null && panelSettings[i].panel == panel)
                    return panelSettings[i].settings ?? (panelSettings[i].settings = RuleSettings.FromLegacy(this));

            var created = new PanelRuleSettings
            {
                panel = panel,
                settings = RuleSettings.FromLegacy(this)
            };
            panelSettings.Add(created);
            return created.settings;
        }
    }
}
