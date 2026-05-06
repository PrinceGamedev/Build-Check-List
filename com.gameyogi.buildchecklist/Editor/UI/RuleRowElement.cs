using System;
using System.Collections.Generic;
using Gameyogi.BuildChecklist.Editor.Discovery;
using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gameyogi.BuildChecklist.Editor.UI
{
    internal sealed class RuleRowElement : VisualElement
    {
        private readonly DiscoveredField _field;
        private readonly BuildChecklistRules _rules;
        private readonly BuildCheckPanel _panel;
        private RuleEntry _entry;
        private RuleSettings _settings;

        private Toggle _enableToggle;
        private VisualElement _details;
        private VisualElement _expectedHolder;
        private VisualElement _expectedMaxHolder;
        private VisualElement _oneOfHolder;
        private EnumField _opField;
        private EnumField _severityField;
        private TextField _messageField;
        private ObjectField _specificAssetField;
        private Label _sceneObjectLabel;

        public RuleRowElement(DiscoveredField field, BuildChecklistRules rules, BuildCheckPanel panel)
        {
            _field = field;
            _rules = rules;
            _panel = panel;
            _entry = rules.FindByFieldId(field.Id);
            _settings = _entry?.GetSettings(panel);

            style.flexDirection = FlexDirection.Column;
            style.borderTopWidth = 1;
            style.borderTopColor = new StyleColor(new Color(0, 0, 0, 0.15f));
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 4;
            style.paddingBottom = 4;

            BuildHeader();
            BuildDetails();
            UpdateDetailsVisibility();
        }

        private void BuildHeader()
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
            };

            _enableToggle = new Toggle { value = _settings != null && _settings.enabled };
            _enableToggle.RegisterValueChangedCallback(evt =>
            {
                EnsureEntryAndSettings();
                _settings.enabled = evt.newValue;
                if (evt.newValue && _settings.expected.kind == ValueKind.None)
                    ApplyDefaultExpectedForType();
                PersistAndRebuildDetails();
                UpdateDetailsVisibility();
            });
            row.Add(_enableToggle);

            row.Add(new Label(_field.DisplayName)
            {
                style = { flexGrow = 1, marginLeft = 6, unityFontStyleAndWeight = FontStyle.Bold }
            });

            row.Add(new Label($"({TypeLabel(_field.FieldType)})")
            {
                style = { color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)) }
            });

            if (!string.IsNullOrEmpty(_field.Attribute.Description))
                row.tooltip = _field.Attribute.Description;

            Add(row);
        }

        private static string TypeLabel(Type t)
        {
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t.IsEnum) return $"enum {t.Name}";
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return t.Name;
            return t.Name;
        }

        private void BuildDetails()
        {
            _details = new VisualElement { style = { marginTop = 4, marginLeft = 22 } };
            Add(_details);

            var panelLine = new Label($"Panel: {_panel}")
            {
                style =
                {
                    color = new StyleColor(new Color(0.55f, 0.55f, 0.55f)),
                    marginBottom = 3,
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            };
            _details.Add(panelLine);

            var opRow = NewLabelledRow("Operator");
            _opField = new EnumField(GetCurrentOp());
            _opField.RegisterValueChangedCallback(evt =>
            {
                EnsureEntryAndSettings();
                var newOp = (ComparisonOp)evt.newValue;
                if (!OperatorAllowedForField(_field.FieldType, newOp))
                {
                    _opField.SetValueWithoutNotify(_settings.op);
                    return;
                }
                _settings.op = newOp;
                PersistAndRebuildDetails();
            });
            opRow.Add(_opField);
            _details.Add(opRow);

            var expectedRow = NewLabelledRow("Expected");
            _expectedHolder = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };
            expectedRow.Add(_expectedHolder);
            _details.Add(expectedRow);

            var maxRow = NewLabelledRow("Up to");
            _expectedMaxHolder = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };
            maxRow.Add(_expectedMaxHolder);
            _details.Add(maxRow);

            var oneOfRow = NewLabelledRow("Allowed");
            _oneOfHolder = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };
            oneOfRow.Add(_oneOfHolder);
            _details.Add(oneOfRow);

            var sevRow = NewLabelledRow("Severity");
            _severityField = new EnumField(GetCurrentSeverity());
            _severityField.RegisterValueChangedCallback(evt =>
            {
                EnsureEntryAndSettings();
                _settings.severity = (CheckSeverity)evt.newValue;
                Persist();
            });
            sevRow.Add(_severityField);
            _details.Add(sevRow);

            var assetRow = NewLabelledRow("Prefab filter");
            _specificAssetField = new ObjectField
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            _specificAssetField.tooltip = "Optional. Leave empty to check every prefab and open scene, or choose one prefab to limit this rule.";
            _specificAssetField.RegisterValueChangedCallback(evt =>
            {
                EnsureEntryAndSettings();
                var obj = evt.newValue as GameObject;
                if (obj != null && !PrefabUtility.IsPartOfPrefabAsset(obj))
                {
                    Debug.LogWarning("[Build Checklist] Prefab filter only accepts prefab assets.");
                    _specificAssetField.SetValueWithoutNotify(null);
                    _settings.assetGuid = string.Empty;
                    Persist();
                    return;
                }
                _settings.assetGuid = obj == null
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
                if (obj != null)
                {
                    _settings.scenePath = string.Empty;
                    _settings.sceneObjectPath = string.Empty;
                    UpdateSceneObjectLabel();
                }
                Persist();
            });
            assetRow.Add(_specificAssetField);
            _details.Add(assetRow);

            _details.Add(new Label("(optional; empty = all prefabs and open scenes)")
            {
                style =
                {
                    color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)),
                    marginLeft = 92,
                    marginBottom = 4,
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            });

            var sceneRow = NewLabelledRow("Scene object");
            var useSelectedBtn = new Button(UseSelectedSceneObject) { text = "Use selected" };
            useSelectedBtn.tooltip = "Stores the selected scene GameObject as scene path + hierarchy path.";
            sceneRow.Add(useSelectedBtn);
            sceneRow.Add(new Button(ClearSceneObjectFilter) { text = "Clear" });
            _details.Add(sceneRow);

            _sceneObjectLabel = new Label
            {
                style =
                {
                    color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)),
                    marginLeft = 92,
                    marginBottom = 4,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            _details.Add(_sceneObjectLabel);

            var msgRow = NewLabelledRow("Message");
            _messageField = new TextField { multiline = false, style = { flexGrow = 1 } };
            _messageField.RegisterCallback<BlurEvent>(_ =>
            {
                EnsureEntryAndSettings();
                _settings.customMessage = _messageField.value ?? string.Empty;
                Persist();
            });
            msgRow.Add(_messageField);
            _details.Add(msgRow);

            RebuildDynamicSections();
        }

        private void RebuildDynamicSections()
        {
            _expectedHolder.Clear();
            _expectedMaxHolder.Clear();
            _oneOfHolder.Clear();

            if (_settings == null)
            {
                _opField.SetValueWithoutNotify(GetCurrentOp());
                _severityField.SetValueWithoutNotify(GetCurrentSeverity());
                _messageField?.SetValueWithoutNotify(string.Empty);
                _specificAssetField?.SetValueWithoutNotify(null);
                SetRowVisible(_expectedHolder, false);
                SetRowVisible(_expectedMaxHolder, false);
                SetRowVisible(_oneOfHolder, false);
                return;
            }

            _opField.SetValueWithoutNotify(_settings.op);
            _severityField.SetValueWithoutNotify(_settings.severity);
            _messageField.SetValueWithoutNotify(_settings.customMessage ?? string.Empty);
            UpdateSceneObjectLabel();

            UnityEngine.Object asset = null;
            if (!string.IsNullOrEmpty(_settings.assetGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(_settings.assetGuid);
                if (!string.IsNullOrEmpty(path))
                    asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            _specificAssetField.SetValueWithoutNotify(asset);

            bool needsExpected =
                _settings.op == ComparisonOp.Equals ||
                _settings.op == ComparisonOp.NotEquals ||
                _settings.op == ComparisonOp.Min ||
                _settings.op == ComparisonOp.Max ||
                _settings.op == ComparisonOp.Range;

            SetRowVisible(_expectedHolder, needsExpected);
            SetRowVisible(_expectedMaxHolder, _settings.op == ComparisonOp.Range);
            SetRowVisible(_oneOfHolder, _settings.op == ComparisonOp.OneOf);

            if (needsExpected)
                _expectedHolder.Add(BuildValueEditor(_settings.expected, () => Persist()));

            if (_settings.op == ComparisonOp.Range)
                _expectedMaxHolder.Add(BuildValueEditor(_settings.expectedMax, () => Persist()));

            if (_settings.op == ComparisonOp.OneOf)
                BuildOneOfList();
        }

        private void BuildOneOfList()
        {
            if (_settings.oneOfValues == null)
                _settings.oneOfValues = new List<SerializedValue>();

            for (int i = 0; i < _settings.oneOfValues.Count; i++)
            {
                int captured = i;
                var item = _settings.oneOfValues[i];
                if (item == null)
                {
                    item = new SerializedValue();
                    _settings.oneOfValues[i] = item;
                }

                var itemRow = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 2 }
                };
                itemRow.Add(BuildValueEditor(item, () => Persist()));
                itemRow.Add(new Button(() =>
                {
                    _settings.oneOfValues.RemoveAt(captured);
                    Persist();
                    RebuildDynamicSections();
                })
                { text = "-" });
                _oneOfHolder.Add(itemRow);
            }

            _oneOfHolder.Add(new Button(() =>
            {
                _settings.oneOfValues.Add(MakeDefaultValueForFieldType());
                Persist();
                RebuildDynamicSections();
            })
            { text = "+ Add allowed value" });
        }

        private VisualElement BuildValueEditor(SerializedValue value, Action onChanged)
        {
            EnsureKindMatchesField(value);

            switch (value.kind)
            {
                case ValueKind.Int:
                    {
                        var f = new IntegerField { value = value.intValue, style = { flexGrow = 1 } };
                        f.RegisterValueChangedCallback(evt => { value.intValue = evt.newValue; onChanged(); });
                        return f;
                    }
                case ValueKind.Float:
                    {
                        var f = new FloatField { value = value.floatValue, style = { flexGrow = 1 } };
                        f.RegisterValueChangedCallback(evt => { value.floatValue = evt.newValue; onChanged(); });
                        return f;
                    }
                case ValueKind.Bool:
                    {
                        var f = new Toggle { value = value.boolValue, style = { flexGrow = 1 } };
                        f.RegisterValueChangedCallback(evt => { value.boolValue = evt.newValue; onChanged(); });
                        return f;
                    }
                case ValueKind.String:
                    {
                        var f = new TextField { value = value.stringValue ?? string.Empty, style = { flexGrow = 1 } };
                        f.RegisterCallback<BlurEvent>(_ => { value.stringValue = f.value; onChanged(); });
                        return f;
                    }
                case ValueKind.Enum:
                    {
                        var enumType = value.ResolveEnumType() ?? _field.FieldType;
                        Enum current;
                        try
                        {
                            current = (Enum)Enum.ToObject(enumType, value.intValue);
                        }
                        catch
                        {
                            current = (Enum)Enum.GetValues(enumType).GetValue(0);
                        }
                        var f = new EnumField(current) { style = { flexGrow = 1 } };
                        f.RegisterValueChangedCallback(evt =>
                        {
                            value.intValue = Convert.ToInt32(evt.newValue);
                            value.enumTypeName = evt.newValue.GetType().AssemblyQualifiedName;
                            onChanged();
                        });
                        if (string.IsNullOrEmpty(value.enumTypeName))
                            value.enumTypeName = enumType.AssemblyQualifiedName;
                        return f;
                    }
                case ValueKind.Object:
                    {
                        var f = new ObjectField
                        {
                            objectType = typeof(UnityEngine.Object).IsAssignableFrom(_field.FieldType) ? _field.FieldType : typeof(UnityEngine.Object),
                            value = value.objectValue,
                            allowSceneObjects = false,
                            style = { flexGrow = 1 }
                        };
                        f.RegisterValueChangedCallback(evt => { value.objectValue = evt.newValue; onChanged(); });
                        return f;
                    }
                default:
                    return new Label("<unsupported field type>");
            }
        }

        private VisualElement NewLabelledRow(string label)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2
                }
            };
            row.Add(new Label(label) { style = { width = 90 } });
            return row;
        }

        private void SetRowVisible(VisualElement holder, bool visible)
        {
            var row = holder.parent;
            if (row != null) row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateDetailsVisibility()
        {
            bool show = _settings != null && _settings.enabled;
            _details.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private ComparisonOp GetCurrentOp()
            => _settings != null ? _settings.op : DefaultOpForField(_field.FieldType);

        private CheckSeverity GetCurrentSeverity()
            => _settings != null ? _settings.severity : CheckSeverity.Error;

        private void EnsureEntryAndSettings()
        {
            if (_entry == null)
                _entry = _rules.GetOrCreate(_field.Id);
            if (_settings == null)
                _settings = _entry.GetSettings(_panel);
        }

        private void UseSelectedSceneObject()
        {
            EnsureEntryAndSettings();

            var selected = Selection.activeGameObject;
            if (selected == null || !selected.scene.IsValid() || string.IsNullOrEmpty(selected.scene.path))
            {
                Debug.LogWarning("[Build Checklist] Select a GameObject from a saved scene first.");
                return;
            }

            if (!typeof(Component).IsAssignableFrom(_field.DeclaringType)
                || selected.GetComponent(_field.DeclaringType) == null)
            {
                Debug.LogWarning($"[Build Checklist] Selected GameObject does not have a {_field.DeclaringType.Name} component.");
                return;
            }

            _settings.scenePath = selected.scene.path;
            _settings.sceneObjectPath = GetTransformPath(selected.transform);
            _settings.assetGuid = string.Empty;
            _specificAssetField?.SetValueWithoutNotify(null);
            Persist();
            UpdateSceneObjectLabel();
        }

        private void ClearSceneObjectFilter()
        {
            EnsureEntryAndSettings();
            _settings.scenePath = string.Empty;
            _settings.sceneObjectPath = string.Empty;
            Persist();
            UpdateSceneObjectLabel();
        }

        private void UpdateSceneObjectLabel()
        {
            if (_sceneObjectLabel == null) return;
            if (_settings == null || string.IsNullOrEmpty(_settings.scenePath) || string.IsNullOrEmpty(_settings.sceneObjectPath))
            {
                _sceneObjectLabel.text = "(optional; empty = all matching scene objects)";
                return;
            }

            _sceneObjectLabel.text = $"{_settings.scenePath} -> {_settings.sceneObjectPath}";
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return string.Empty;
            if (t.parent == null) return t.name;
            return GetTransformPath(t.parent) + "/" + t.name;
        }

        private void Persist()
        {
            if (_rules == null) return;
            EditorUtility.SetDirty(_rules);
            AssetDatabase.SaveAssetIfDirty(_rules);
        }

        private void PersistAndRebuildDetails()
        {
            Persist();
            RebuildDynamicSections();
        }

        private void ApplyDefaultExpectedForType()
        {
            _settings.expected = MakeDefaultValueForFieldType();
            _settings.expectedMax = MakeDefaultValueForFieldType();
            _settings.oneOfValues = new List<SerializedValue>();
            _settings.op = DefaultOpForField(_field.FieldType);
        }

        private SerializedValue MakeDefaultValueForFieldType()
        {
            var t = _field.FieldType;
            if (t == typeof(int)) return SerializedValue.OfInt(0);
            if (t == typeof(float)) return SerializedValue.OfFloat(0f);
            if (t == typeof(bool)) return SerializedValue.OfBool(false);
            if (t == typeof(string)) return SerializedValue.OfString(string.Empty);
            if (t.IsEnum)
            {
                var first = (Enum)Enum.GetValues(t).GetValue(0);
                return SerializedValue.OfEnum(first);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                return SerializedValue.OfObject(null);
            return new SerializedValue();
        }

        private void EnsureKindMatchesField(SerializedValue v)
        {
            var t = _field.FieldType;
            ValueKind expected =
                t == typeof(int) ? ValueKind.Int :
                t == typeof(float) ? ValueKind.Float :
                t == typeof(bool) ? ValueKind.Bool :
                t == typeof(string) ? ValueKind.String :
                t.IsEnum ? ValueKind.Enum :
                typeof(UnityEngine.Object).IsAssignableFrom(t) ? ValueKind.Object :
                ValueKind.None;

            if (v.kind != expected)
            {
                v.kind = expected;
                if (expected == ValueKind.Enum && string.IsNullOrEmpty(v.enumTypeName))
                    v.enumTypeName = t.AssemblyQualifiedName;
            }
        }

        private static ComparisonOp DefaultOpForField(Type t)
        {
            if (t == typeof(bool)) return ComparisonOp.Equals;
            if (t == typeof(string)) return ComparisonOp.Equals;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return ComparisonOp.NotNull;
            return ComparisonOp.Equals;
        }

        private static bool OperatorAllowedForField(Type t, ComparisonOp op)
        {
            if (t == typeof(int) || t == typeof(float))
            {
                return op == ComparisonOp.Equals || op == ComparisonOp.NotEquals
                    || op == ComparisonOp.Min || op == ComparisonOp.Max
                    || op == ComparisonOp.Range || op == ComparisonOp.OneOf;
            }
            if (t == typeof(bool))
                return op == ComparisonOp.Equals || op == ComparisonOp.NotEquals;
            if (t == typeof(string))
                return op == ComparisonOp.Equals || op == ComparisonOp.NotEquals
                    || op == ComparisonOp.NotEmpty || op == ComparisonOp.OneOf;
            if (t.IsEnum)
                return op == ComparisonOp.Equals || op == ComparisonOp.NotEquals || op == ComparisonOp.OneOf;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                return op == ComparisonOp.NotNull || op == ComparisonOp.Equals || op == ComparisonOp.NotEquals;
            return false;
        }
    }
}
