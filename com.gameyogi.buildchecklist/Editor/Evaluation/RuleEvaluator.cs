using System.Collections.Generic;
using Gameyogi.BuildChecklist.Editor.Discovery;
using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameyogi.BuildChecklist.Editor.Evaluation
{
    /// <summary>
    /// Finds every Component instance of each rule's declaring type across
    /// prefab assets and currently-loaded scenes, then evaluates the active
    /// panel's settings against each instance.
    /// </summary>
    public static class RuleEvaluator
    {
        public static List<EvaluationResult> EvaluateAll(BuildChecklistRules rules)
            => EvaluateAll(rules, EvaluationOptions.Default);

        public static List<EvaluationResult> EvaluateAll(BuildChecklistRules rules, EvaluationOptions options)
        {
            var results = new List<EvaluationResult>();
            if (rules == null || rules.entries == null) return results;
            if (options == null) options = EvaluationOptions.Default;

            var setup = options.includeEnabledBuildSettingsScenes
                ? EditorSceneManager.GetSceneManagerSetup()
                : null;

            try
            {
                if (options.includeEnabledBuildSettingsScenes)
                    OpenEnabledBuildSettingsScenes();

                foreach (var rule in rules.entries)
                {
                    if (rule == null) continue;

                    var settings = rule.GetSettings(rules.activePanel);
                    if (settings == null || !settings.enabled) continue;

                    EvaluateOne(rule, settings, options, results);
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            return results;
        }

        private static void EvaluateOne(RuleEntry rule, RuleSettings settings, EvaluationOptions options, List<EvaluationResult> results)
        {
            var field = AttributedFieldDiscovery.FindById(rule.fieldId);

            if (field == null)
            {
                results.Add(new EvaluationResult(
                    kind: ResultKind.MissingField,
                    severity: settings.severity,
                    fieldId: rule.fieldId,
                    displayName: rule.fieldId ?? "<unknown>",
                    operatorDescription: OperatorEvaluator.DescribeOperator(settings),
                    currentValueText: "<missing>",
                    asset: null,
                    assetPath: null,
                    message: settings.customMessage
                             ?? $"Field '{rule.fieldId}' no longer exists. Remove or update the rule."));
                return;
            }

            var instances = FindComponentInstances(field, settings, options);

            if (instances.Count == 0)
            {
                results.Add(new EvaluationResult(
                    kind: ResultKind.NoInstances,
                    severity: settings.severity,
                    fieldId: rule.fieldId,
                    displayName: field.DisplayName,
                    operatorDescription: OperatorEvaluator.DescribeOperator(settings),
                    currentValueText: "<no instance>",
                    asset: null,
                    assetPath: null,
                    message: settings.customMessage
                             ?? $"No GameObject with a {field.DeclaringType.Name} component found "
                                + NoInstancesScope(settings)));
                return;
            }

            foreach (var component in instances)
                EvaluateAgainstComponent(rule, settings, field, component, results);
        }

        private static void EvaluateAgainstComponent(
            RuleEntry rule,
            RuleSettings settings,
            DiscoveredField field,
            Component component,
            List<EvaluationResult> results)
        {
            string container = DescribeContainer(component);
            string opDesc = OperatorEvaluator.DescribeOperator(settings);
            string targetAssetPath = GetTargetAssetPath(component);
            string targetObjectPath = GetTransformPath(component.transform);

            using (var so = new SerializedObject(component))
            {
                var prop = so.FindProperty(field.Field.Name);
                if (prop == null)
                {
                    results.Add(new EvaluationResult(
                        kind: ResultKind.BrokenRule,
                        severity: settings.severity,
                        fieldId: rule.fieldId,
                        displayName: field.DisplayName,
                        operatorDescription: opDesc,
                        currentValueText: "<not serialized>",
                        asset: component,
                        assetPath: container,
                        message: settings.customMessage
                                 ?? $"Field {field.DisplayName} is not serialized - cannot evaluate.",
                        targetAssetPath: targetAssetPath,
                        targetObjectPath: targetObjectPath,
                        ruleSettings: settings));
                    return;
                }

                var outcome = OperatorEvaluator.Evaluate(prop, settings);

                ResultKind kind = outcome.RuleIsBroken
                    ? ResultKind.BrokenRule
                    : (outcome.Passed ? ResultKind.Pass : ResultKind.Fail);

                string message = outcome.Passed
                    ? null
                    : (settings.customMessage
                       ?? $"{field.DisplayName} on {container}: {outcome.FailureReason}");

                results.Add(new EvaluationResult(
                    kind: kind,
                    severity: settings.severity,
                    fieldId: rule.fieldId,
                    displayName: field.DisplayName,
                    operatorDescription: opDesc,
                    currentValueText: outcome.CurrentValueText,
                    asset: component,
                    assetPath: container,
                    message: message,
                    targetAssetPath: targetAssetPath,
                    targetObjectPath: targetObjectPath,
                    ruleSettings: settings));
            }
        }

        private static List<Component> FindComponentInstances(DiscoveredField field, RuleSettings settings, EvaluationOptions options)
        {
            var list = new List<Component>();
            var type = field.DeclaringType;

            if (!string.IsNullOrEmpty(settings.assetGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(settings.assetGuid);
                if (string.IsNullOrEmpty(path)) return list;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) return list;

                CollectComponents(prefab, type, list);
                return list;
            }

            if (HasSceneObjectFilter(settings))
            {
                CollectSceneObjectFilterComponent(type, settings, options, list);
                return list;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            if (prefabGuids != null)
            {
                foreach (var guid in prefabGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    CollectComponents(prefab, type, list);
                }
            }

            if (options.includeOpenScenes || options.includeEnabledBuildSettingsScenes)
                CollectOpenSceneComponents(type, list);

            return list;
        }

        private static void CollectOpenSceneComponents(System.Type type, List<Component> dest)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                CollectSceneComponents(scene, type, dest);
            }
        }

        private static void OpenEnabledBuildSettingsScenes()
        {
            var scenePaths = GetEnabledBuildScenePaths();
            for (int i = 0; i < scenePaths.Count; i++)
            {
                var path = scenePaths[i];
                if (string.IsNullOrEmpty(path)) continue;

                var alreadyLoaded = FindLoadedSceneByPath(path);
                if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded)
                    continue;

                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
        }

        private static void CollectSceneObjectFilterComponent(System.Type type, RuleSettings settings, EvaluationOptions options, List<Component> dest)
        {
            var loadedScene = FindLoadedSceneByPath(settings.scenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                CollectSceneObjectFilterComponentFromScene(loadedScene, type, settings.sceneObjectPath, dest);
                return;
            }

            if (options.includeEnabledBuildSettingsScenes)
                Debug.LogWarning($"[Build Checklist] Scene object filter scene is not loaded or not in enabled Build Settings scenes: {settings.scenePath}");
        }

        private static void CollectSceneObjectFilterComponentFromScene(Scene scene, System.Type type, string objectPath, List<Component> dest)
        {
            var go = FindGameObjectByPath(scene, objectPath);
            if (go == null) return;

            var component = go.GetComponent(type);
            if (component != null)
                dest.Add(component);
        }

        private static GameObject FindGameObjectByPath(Scene scene, string objectPath)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(objectPath)) return null;

            var parts = objectPath.Split('/');
            if (parts.Length == 0) return null;

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

        private static List<string> GetEnabledBuildScenePaths()
        {
            var paths = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null) return paths;

            for (int i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                    paths.Add(scene.path);
            }
            return paths;
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

        private static void CollectSceneComponents(Scene scene, System.Type type, List<Component> dest)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
                CollectComponents(root, type, dest);
        }

        private static void CollectComponents(GameObject root, System.Type type, List<Component> dest)
        {
            if (root == null) return;
            var comps = root.GetComponentsInChildren(type, includeInactive: true);
            for (int i = 0; i < comps.Length; i++)
                if (comps[i] != null) dest.Add(comps[i]);
        }

        private static string DescribeContainer(Component c)
        {
            if (c == null) return "<null>";
            var go = c.gameObject;
            string transformPath = GetTransformPath(c.transform);

            if (PrefabUtility.IsPartOfPrefabAsset(go))
            {
                var prefabRoot = go.transform.root.gameObject;
                var prefabPath = AssetDatabase.GetAssetPath(prefabRoot);
                if (string.IsNullOrEmpty(prefabPath))
                    prefabPath = AssetDatabase.GetAssetPath(go);
                return $"{prefabPath} -> {transformPath}";
            }

            if (go.scene.IsValid())
                return $"[{go.scene.name}] {transformPath}";

            return transformPath;
        }

        private static string GetTargetAssetPath(Component c)
        {
            if (c == null) return null;
            var go = c.gameObject;

            if (PrefabUtility.IsPartOfPrefabAsset(go))
            {
                var prefabRoot = go.transform.root.gameObject;
                var prefabPath = AssetDatabase.GetAssetPath(prefabRoot);
                return string.IsNullOrEmpty(prefabPath) ? AssetDatabase.GetAssetPath(go) : prefabPath;
            }

            return go.scene.IsValid() ? go.scene.path : null;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return string.Empty;
            if (t.parent == null) return t.name;
            return GetTransformPath(t.parent) + "/" + t.name;
        }

        private static bool HasSceneObjectFilter(RuleSettings settings)
            => !string.IsNullOrEmpty(settings.scenePath) && !string.IsNullOrEmpty(settings.sceneObjectPath);

        private static string NoInstancesScope(RuleSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.assetGuid))
                return "in the configured prefab.";
            if (HasSceneObjectFilter(settings))
                return $"at scene object '{settings.sceneObjectPath}' in '{settings.scenePath}'.";
            return "in any prefab or open scene.";
        }
    }
}
