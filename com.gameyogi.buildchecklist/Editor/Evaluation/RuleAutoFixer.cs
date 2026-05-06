using System;
using System.Collections.Generic;
using Gameyogi.BuildChecklist.Editor.Discovery;
using Gameyogi.BuildChecklist.Editor.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameyogi.BuildChecklist.Editor.Evaluation
{
    public static class RuleAutoFixer
    {
        public static bool CanFix(EvaluationResult result)
        {
            if (result == null || result.Kind != ResultKind.Fail || result.RuleSettings == null) return false;

            switch (result.RuleSettings.op)
            {
                case ComparisonOp.Equals:
                case ComparisonOp.Min:
                case ComparisonOp.Max:
                case ComparisonOp.Range:
                    return result.RuleSettings.expected != null && result.RuleSettings.expected.kind != ValueKind.None;
                case ComparisonOp.OneOf:
                    return result.RuleSettings.oneOfValues != null
                           && result.RuleSettings.oneOfValues.Count > 0
                           && result.RuleSettings.oneOfValues[0] != null
                           && result.RuleSettings.oneOfValues[0].kind != ValueKind.None;
                default:
                    return false;
            }
        }

        public static int FixAll(IEnumerable<EvaluationResult> results)
        {
            int fixedCount = 0;
            if (results == null) return fixedCount;

            foreach (var result in results)
                if (Fix(result))
                    fixedCount++;

            AssetDatabase.SaveAssets();
            return fixedCount;
        }

        public static bool Fix(EvaluationResult result)
        {
            if (!CanFix(result)) return false;

            var field = AttributedFieldDiscovery.FindById(result.FieldId);
            if (field == null) return false;

            var value = GetFixValue(result.RuleSettings);
            if (value == null) return false;

            if (!string.IsNullOrEmpty(result.TargetAssetPath)
                && result.TargetAssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return FixSceneObject(result, field, value);
            }

            return FixLoadedOrPrefabObject(result, field, value);
        }

        private static SerializedValue GetFixValue(RuleSettings settings)
        {
            if (settings.op == ComparisonOp.OneOf)
                return settings.oneOfValues[0];
            return settings.expected;
        }

        private static bool FixLoadedOrPrefabObject(EvaluationResult result, DiscoveredField field, SerializedValue value)
        {
            var component = result.Asset as Component;
            if (component == null && !string.IsNullOrEmpty(result.TargetAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.TargetAssetPath);
                var target = FindGameObjectByPath(prefab, result.TargetObjectPath);
                component = target != null ? target.GetComponent(field.DeclaringType) : null;
            }

            if (component == null) return false;
            return WriteValue(component, field.Field.Name, value, saveScene: true);
        }

        private static bool FixSceneObject(EvaluationResult result, DiscoveredField field, SerializedValue value)
        {
            var scene = FindLoadedSceneByPath(result.TargetAssetPath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(result.TargetAssetPath, OpenSceneMode.Additive);

            var go = FindGameObjectByPath(scene, result.TargetObjectPath);
            var component = go != null ? go.GetComponent(field.DeclaringType) : null;
            if (component == null) return false;

            if (!WriteValue(component, field.Field.Name, value, saveScene: false))
                return false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            return true;
        }

        private static bool WriteValue(Component component, string fieldName, SerializedValue value, bool saveScene)
        {
            using (var so = new SerializedObject(component))
            {
                var prop = so.FindProperty(fieldName);
                if (prop == null) return false;

                if (!WriteProperty(prop, value)) return false;

                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(component);
            if (saveScene && component.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                EditorSceneManager.SaveScene(component.gameObject.scene);
            }
            return true;
        }

        private static bool WriteProperty(SerializedProperty prop, SerializedValue value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (value.kind != ValueKind.Int && value.kind != ValueKind.Enum) return false;
                    prop.intValue = value.intValue;
                    return true;
                case SerializedPropertyType.Float:
                    if (value.kind == ValueKind.Float) prop.floatValue = value.floatValue;
                    else if (value.kind == ValueKind.Int) prop.floatValue = value.intValue;
                    else return false;
                    return true;
                case SerializedPropertyType.Boolean:
                    if (value.kind != ValueKind.Bool) return false;
                    prop.boolValue = value.boolValue;
                    return true;
                case SerializedPropertyType.String:
                    if (value.kind != ValueKind.String) return false;
                    prop.stringValue = value.stringValue ?? string.Empty;
                    return true;
                case SerializedPropertyType.Enum:
                    if (value.kind != ValueKind.Enum && value.kind != ValueKind.Int) return false;
                    prop.intValue = value.intValue;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    if (value.kind != ValueKind.Object) return false;
                    prop.objectReferenceValue = value.objectValue;
                    return true;
                default:
                    return false;
            }
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

        private static GameObject FindGameObjectByPath(GameObject root, string objectPath)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(objectPath)) return root;

            var parts = objectPath.Split('/');
            if (parts.Length == 0 || parts[0] != root.name) return null;

            var current = root.transform;
            for (int i = 1; current != null && i < parts.Length; i++)
                current = current.Find(parts[i]);

            return current != null ? current.gameObject : null;
        }
    }
}
