# Build Checklist

Attribute-driven pre-build validation for Unity.

Tag any serialized field with `[BuildCheckField]`, open **Window → Build Checklist**, define a rule (operator + expected value + severity), select the active panel, and the rule is evaluated automatically before every build. Builds are aborted when an `Error`-severity rule fails.

## Requirements

- Unity **2022.3.62f2** or newer

## Install

In Unity, open **Window → Package Manager** → **+ ▾** → **Add package from git URL** and paste your repository URL, or use **Add package from disk** to point at a local checkout.

## Usage

### 1. Tag fields you care about

Add `[BuildCheckField]` to any serialized field on a `MonoBehaviour`:

```csharp
using Gameyogi.BuildChecklist;
using UnityEngine;

public class GameConfig : MonoBehaviour
{
    [BuildCheckField(Category = "Performance")]
    public int targetFps;

    [BuildCheckField(Category = "Release Safety",
                     Description = "Cheats must be off for shipping builds.")]
    public bool enableCheats;

    [BuildCheckField]
    public AudioClip themeMusic;
}
```

The attribute only declares that the field is checkable. The actual rule is defined in the window.

The tool finds every `Component` of the declaring type by scanning **all prefab assets in the project** and **currently-loaded scenes**. During build validation, it also temporarily opens enabled scenes from **Build Settings** so closed build scenes are checked too.

### 2. Define rules in the window

Open **Window → Build Checklist**.

Use the **Panel** dropdown in the toolbar to select the active checking panel: `Development`, `Debug`, or `Release`. Each panel remembers its own enabled state, operator, expected value, severity, prefab filter, and message for every rule.

For example, one `int` field can be `60` in Development, `30` in Debug, and `120` in Release. Set it once while each panel is selected; switching panels reloads that panel's saved value automatically.

You can also select the active build panel from **Project Settings → Build Checklist**.

The **Rules** tab lists every tagged field, grouped by `Category`. For each field:

1. Toggle the checkbox to enable the rule.
2. Pick an **Operator** — the dropdown only shows operators valid for the field's type.
3. Set the **Expected** value (and **Up to** for `Range`, or the **Allowed** list for `OneOf`).
4. Pick a **Severity**.
5. Optionally restrict the rule with **Prefab filter** — empty = scans every prefab and currently-loaded scene; set = only that prefab is checked.
6. Optionally restrict the rule with **Scene object** by selecting a saved scene GameObject in the Hierarchy and clicking **Use selected**. The tool stores the scene path and hierarchy path as strings.
7. Optionally provide a **Message** to override the default failure text.

Edits are saved immediately to `Assets/BuildChecklist/Rules.asset`.

### 3. Run on demand or on build

- **Run Now ▶** in the toolbar evaluates every enabled rule for the active panel against prefabs, currently-loaded scenes, and enabled scenes from **Build Settings**, then switches to the **Results** tab.
- Triggering a build (File → Build And Run, or batch-mode CI) runs validation automatically against prefabs, currently-loaded scenes, and enabled scenes from **Build Settings**. If any rule with `Error` severity fails, the build is aborted before player code is compiled and the failure is logged to the console.
- If warning/error issues are found during an interactive build, Unity opens the Build Checklist results UI and cancels the build. Fix or skip issues there, click **Recheck**, then start the build again once the checklist is clean.
- The Results tab also includes **Fix Selected** and **Fix All Visible** buttons for auto-fixable issues.

## Operators

| Operator | Valid for                              | Notes |
|----------|----------------------------------------|-------|
| Equals   | int, float, bool, string, enum, Object | reference equality for Object |
| NotEquals| int, float, bool, string, enum, Object | reference inequality for Object |
| Min      | int, float                             | `value >= expected` |
| Max      | int, float                             | `value <= expected` |
| Range    | int, float                             | `expected <= value <= expectedMax` |
| NotNull  | UnityEngine.Object                     | reference must be assigned |
| NotEmpty | string, arrays / lists                 | length > 0 |
| OneOf    | int, float, string, enum               | value matches at least one allowed entry |

## Severity

- **Info / Warning** — logged to the console; build proceeds.
- **Error** — logged as `Debug.LogError` and the build is aborted via `BuildFailedException`.

## What happens when a tagged field is renamed?

The rule is keyed by `{Type.FullName}::{FieldName}`. If you rename or delete the field, the rule does not silently disappear — it appears in a **Missing fields** section at the bottom of the Rules tab so you can clean it up or re-create it.

## Sample

A "Basic Usage" sample is shipped with the package — import it via the Package Manager to get a sample `MonoBehaviour` with several tagged fields ready to play with.

## Status

`1.5.0`. See `CHANGELOG.md` for release notes.
