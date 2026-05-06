# Build Checklist

Attribute-driven pre-build validation for Unity.

Build Checklist lets you mark important `MonoBehaviour` fields, define rules for them in an editor window, and automatically check those rules before a build. In Unity editor builds, the tool asks which panel you want to build with, then validates that panel before the build continues.

## Requirements

- Unity 2022.3.62f2 or newer

## Install

In Unity, open **Window > Package Manager**.

Then choose one of these options:

- **+ > Add package from git URL** and paste **`https://github.com/PrinceGamedev/Build-Check-List.git`**.
- **+ > Add package from disk** and select this package folder from a local checkout.

## Quick Start

### 1. Mark fields

Add `[BuildCheckField]` to serialized fields on a `MonoBehaviour`.

```csharp
using Gameyogi.BuildChecklist;
using UnityEngine;

public class GameConfig : MonoBehaviour
{
    [BuildCheckField(Category = "Performance")]
    public int targetFps = 60;

    [BuildCheckField(Category = "Release Safety",
                     Description = "Cheats must be off for shipping builds.")]
    public bool enableCheats = false;

    [BuildCheckField(Category = "Audio")]
    public AudioClip themeMusic;
}
```

The attribute only makes the field available to the checklist. The rule itself is created in the editor window.

### 2. Create rules

Open **Window > Build Checklist**.

The **Rules** tab shows all fields marked with `[BuildCheckField]`, grouped by category.

For each field:

1. Enable the rule.
2. Choose an operator, such as `Equals`, `Min`, `Max`, `Range`, `NotNull`, `NotEmpty`, or `OneOf`.
3. Set the expected value.
4. Choose the severity: `Info`, `Warning`, or `Error`.
5. Optionally set a custom failure message.
6. Optionally restrict the rule to one prefab or one saved scene object.

Rules are saved in `Assets/BuildChecklist/Rules.asset`.

### 3. Use panels

Build Checklist has three panels:

- `Development`
- `Debug`
- `Release`

Each panel stores its own rule settings. For example, the same field can have different expected values for development builds and release builds.

You can change the active panel from:

- The **Panel** dropdown in **Window > Build Checklist**.
- **Project Settings > Build Checklist**.
- The build popup shown when starting a build from the Unity editor.

### 4. Run checks manually

Click **Run Now** in the Build Checklist window.

The tool checks the active panel against:

- Prefab assets in the project.
- Currently loaded scenes.
- Enabled scenes from **Build Settings**.

Results appear in the **Results** tab. Click a result to ping the object. Double-click a result to open the related script.

### 5. Build with a selected panel

When you press **Build** or **Build And Run** from Unity's Build Settings window, Build Checklist shows a popup:

**Which panel do you want to use for this build?**

Choose:

- **Development**
- **Debug**
- **Release**

After you choose a panel, the normal flow continues:

1. The selected panel is saved as the active panel.
2. The tool evaluates that panel's enabled rules.
3. If warning or error issues are found, the Results UI opens and the current build is cancelled.
4. Fix or skip the issues in the checklist window.
5. Start the build again.

In batch mode, such as CI builds, no popup is shown. The build uses the currently active panel.

## Auto-Fix

Some failed rules can be fixed automatically from the **Results** tab.

Auto-fix is supported for:

- `Equals`
- `Min`
- `Max`
- `Range`
- `OneOf`

Auto-fix is not supported for:

- `NotNull`
- `NotEmpty`
- `NotEquals`

Those operators need a human decision because the package cannot safely guess the correct value.

## Operators

| Operator | Valid for | Meaning |
| --- | --- | --- |
| `Equals` | int, float, bool, string, enum, Object | Value must equal the expected value. |
| `NotEquals` | int, float, bool, string, enum, Object | Value must not equal the expected value. |
| `Min` | int, float, enum | Value must be greater than or equal to expected. |
| `Max` | int, float, enum | Value must be less than or equal to expected. |
| `Range` | int, float, enum | Value must be between the lower and upper values. |
| `NotNull` | Object references | Reference must be assigned. |
| `NotEmpty` | string, arrays, lists | Value must contain something. |
| `OneOf` | int, float, string, enum | Value must match one allowed value. |

## Severity

- `Info`: logged only.
- `Warning`: shown in build review during interactive builds.
- `Error`: blocks the build until fixed or skipped.

## Scene And Prefab Targets

By default, a rule checks every matching component found in prefabs and scenes.

You can narrow a rule to:

- One prefab, using the prefab filter.
- One saved scene GameObject, using **Use selected** in the rule row.

Scene object filters are stored as the scene path plus the GameObject hierarchy path, so scene object references are not serialized into project assets.

## Missing Fields

Rules are keyed by the field's declaring type and field name.

If a field is renamed or deleted, its rule appears as missing in the Rules tab. You can remove the old rule and create a new one for the renamed field.

## Sample

The package includes a **Basic Usage** sample. Import it from Package Manager to get a sample `MonoBehaviour` with fields already marked using `[BuildCheckField]`.

## Version

Current version: `1.6.0`

See `CHANGELOG.md` for release notes.
