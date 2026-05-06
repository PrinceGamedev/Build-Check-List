using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEditor;

namespace Gameyogi.BuildChecklist.Editor.Settings
{
    internal static class BuildChecklistSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Build Checklist", SettingsScope.Project)
            {
                label = "Build Checklist",
                guiHandler = _ =>
                {
                    var rules = BuildChecklistRules.LoadOrCreate();
                    if (rules == null)
                    {
                        EditorGUILayout.HelpBox("Could not load or create Build Checklist rules asset.", MessageType.Error);
                        return;
                    }

                    EditorGUILayout.LabelField("Build Panel", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "This panel is used by Run Now and by build preprocessing.",
                        MessageType.Info);

                    EditorGUI.BeginChangeCheck();
                    var panel = (BuildCheckPanel)EditorGUILayout.EnumPopup("Active Panel", rules.activePanel);
                    if (EditorGUI.EndChangeCheck())
                    {
                        rules.activePanel = panel;
                        EditorUtility.SetDirty(rules);
                        AssetDatabase.SaveAssetIfDirty(rules);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.ObjectField("Rules Asset", rules, typeof(BuildChecklistRules), false);
                },
                keywords = new[] { "build", "checklist", "panel", "development", "debug", "release" }
            };
        }
    }
}
