# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.0] - 2026-05-05

### Added
- Interactive build review for warning/error issues opens the Build Checklist results UI and cancels the current build so users can fix or skip issues in the normal checklist window.
- Auto-fix support for rules with known target values: `Equals`, `Min`, `Max`, `Range`, and first value from `OneOf`.
- Results tab actions: **Fix Selected** and **Fix All Visible**.
- Auto-fix writes prefab values directly and opens/saves scenes when fixing scene objects.

### Notes
- `NotNull`, `NotEmpty`, and `NotEquals` are not auto-fixed because the package cannot infer a safe replacement value.
- Scene object fixes now keep/open the target scene, save it, and ping the fixed GameObject.

## [1.4.0] - 2026-05-05

### Added
- Added scene object filters stored as scene path + hierarchy path strings.
- Rule rows now include **Use selected** / **Clear** controls for targeting a selected scene GameObject.
- Validation can target only the saved scene object when a scene object filter is configured.
- Added **Project Settings → Build Checklist** with an active panel selector used by Run Now and build preprocessing.

### Notes
- Scene object filters avoid serializing scene object references directly, which Unity project assets cannot reliably store.

## [1.3.0] - 2026-05-05

### Added
- Build preprocessing now validates enabled scenes from Build Settings, even when those scenes are closed in the editor.
- Added evaluation options so editor Run Now can keep checking currently-loaded scenes while build validation can include Build Settings scenes.

### Notes
- Build Settings scenes are opened additively for validation and the previous editor scene setup is restored afterward.

## [1.2.0] - 2026-05-05

### Added
- Added a persisted active **Panel** selector with `Development`, `Debug`, and `Release` options.
- Each panel now stores its own rule settings per field: enabled state, operator, expected value, severity, prefab filter, and message.
- Switching panels in the editor window reloads the selected panel's saved field values.
- Build preprocessing logs the active panel before evaluating that panel's rules.

### Notes
- Existing top-level rule values are used as the starting value when a panel is opened for the first time.

## [1.1.0] - 2026-05-05

### Changed
- **BREAKING:** Validation now targets `MonoBehaviour` components on prefab assets and currently-loaded scenes, rather than `ScriptableObject` assets. `[BuildCheckField]` should now be applied to fields on `MonoBehaviour` scripts.
- "Specific asset" rule filter renamed to **Specific prefab** (`ObjectField<GameObject>`, prefab-only).
- `RuleEvaluator` rewritten to enumerate components via `AssetDatabase.FindAssets("t:Prefab")` + `GetComponentsInChildren(type, true)` and via `SceneManager.GetSceneAt(i)` for open scenes.
- Result `AssetPath` now describes the container (`PrefabPath → /Root/Child` or `[SceneName] /Root/Child`).
- Sample updated to a `MonoBehaviour`.

### Notes
- Closed scenes in Build Settings are not auto-opened during validation. Keep them open if you want them validated, or rely on prefab validation.

## [1.0.0] - 2026-05-05

### Added
- `BuildCheckFieldAttribute` for marking serialized fields as participants in the checklist (`Category`, `Description`).
- Auto-discovery of attributed fields via `TypeCache`, refreshed on every assembly reload.
- `BuildChecklistRules` ScriptableObject storing per-field rule entries (operator, expected value, severity, message, optional asset filter).
- `RuleEvaluator` engine reading current values via `SerializedObject` / `SerializedProperty` and evaluating per-operator: Equals, NotEquals, Min, Max, Range, NotNull, NotEmpty, OneOf.
- `IPreprocessBuildWithReport` hook that runs every enabled rule before each build and aborts with `BuildFailedException` when any `Error`-severity rule fails.
- Single editor window (**Window → Build Checklist**) with two tabs:
  - **Rules** — category-grouped, type-aware rule editor with search, missing-field cleanup, and asset filter.
  - **Results** — filterable table with status / severity / search filters, click-to-ping, double-click-to-open-script.
- Basic Usage sample (`Samples~/BasicUsage`) shipping a tagged ScriptableObject and a getting-started README.

### Notes
- Editor-only feature with a tiny Runtime asmdef carrying only the attribute (so user runtime scripts can reference it).
- Targets Unity 2022.3.62f2 and newer.
