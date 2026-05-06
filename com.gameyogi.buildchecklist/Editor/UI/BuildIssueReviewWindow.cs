using System.Collections.Generic;
using System.Linq;
using Gameyogi.BuildChecklist.Editor.Evaluation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameyogi.BuildChecklist.Editor.UI
{
    internal sealed class BuildIssueReviewWindow : EditorWindow
    {
        public enum ReviewAction
        {
            CancelBuild,
            ContinueBuild,
            Recheck
        }

        private sealed class IssueState
        {
            public EvaluationResult result;
            public bool skipped;
            public bool fixedNow;
        }

        private readonly List<IssueState> _issues = new List<IssueState>();
        private Vector2 _scroll;
        private ReviewAction _action = ReviewAction.CancelBuild;

        public static ReviewAction ShowModal(List<EvaluationResult> issues, out List<EvaluationResult> skipped)
        {
            var window = CreateInstance<BuildIssueReviewWindow>();
            window.titleContent = new GUIContent("Build Checklist Review");
            window.minSize = new Vector2(760, 360);
            window._issues.AddRange((issues ?? new List<EvaluationResult>()).Select(r => new IssueState { result = r }));
            window.ShowModalUtility();

            skipped = window._issues
                .Where(i => i.skipped && i.result != null)
                .Select(i => i.result)
                .ToList();

            var action = window._action;
            DestroyImmediate(window);
            return action;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build Checklist found warning/error issues", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Handle each issue separately. Fix writes the expected value when possible. Skip allows this issue for the current build only.", MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var issue in _issues)
                DrawIssue(issue);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel Build", GUILayout.Width(120)))
                {
                    _action = ReviewAction.CancelBuild;
                    Close();
                }

                EditorGUI.BeginDisabledGroup(!AllHandled());
                if (GUILayout.Button("Continue Build", GUILayout.Width(140)))
                {
                    _action = AnyFixed() ? ReviewAction.Recheck : ReviewAction.ContinueBuild;
                    Close();
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawIssue(IssueState issue)
        {
            var r = issue.result;
            if (r == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{r.Severity} - {r.DisplayName}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(issue.fixedNow ? "Fixed" : issue.skipped ? "Skipped" : "Needs action", GUILayout.Width(90));
                }

                EditorGUILayout.LabelField("Rule", r.OperatorDescription ?? string.Empty);
                EditorGUILayout.LabelField("Current", r.CurrentValueText ?? string.Empty);
                EditorGUILayout.LabelField("Object", string.IsNullOrEmpty(r.AssetPath) ? "<none>" : r.AssetPath);

                if (!string.IsNullOrEmpty(r.Message))
                    EditorGUILayout.HelpBox(r.Message, MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(!RuleAutoFixer.CanFix(r) || issue.fixedNow);
                    if (GUILayout.Button("Fix", GUILayout.Width(80)))
                    {
                        issue.fixedNow = RuleAutoFixer.Fix(r);
                        if (!issue.fixedNow)
                            EditorUtility.DisplayDialog("Build Checklist", "This issue could not be auto-fixed.", "OK");
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(issue.fixedNow);
                    if (GUILayout.Button(issue.skipped ? "Unskip" : "Skip", GUILayout.Width(80)))
                        issue.skipped = !issue.skipped;
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("Ping", GUILayout.Width(80)))
                        PingResult(r);
                }
            }
        }

        private bool AllHandled()
            => _issues.All(i => i.fixedNow || i.skipped);

        private bool AnyFixed()
            => _issues.Any(i => i.fixedNow);

        private static void PingResult(EvaluationResult result)
        {
            if (result == null) return;

            if (result.Asset != null)
            {
                Selection.activeObject = result.Asset;
                EditorGUIUtility.PingObject(result.Asset);
                return;
            }

            if (string.IsNullOrEmpty(result.TargetAssetPath)
                || !result.TargetAssetPath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                return;

            var scene = FindLoadedSceneByPath(result.TargetAssetPath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(result.TargetAssetPath, OpenSceneMode.Additive);

            var go = FindGameObjectByPath(scene, result.TargetObjectPath);
            if (go == null) return;

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private static Scene FindLoadedSceneByPath(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == path) return scene;
            }
            return default;
        }

        private static GameObject FindGameObjectByPath(Scene scene, string objectPath)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(objectPath)) return null;

            var parts = objectPath.Split('/');
            var roots = scene.GetRootGameObjects();
            GameObject current = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == parts[0])
                {
                    current = roots[i];
                    break;
                }
            }

            for (int i = 1; current != null && i < parts.Length; i++)
            {
                var child = current.transform.Find(parts[i]);
                current = child != null ? child.gameObject : null;
            }

            return current;
        }
    }
}
