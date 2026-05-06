# Basic Usage Sample

A minimal example showing how to use the Build Checklist tool.

## What's included

- `SampleGameConfig.cs` — a `MonoBehaviour` with five fields tagged with `[BuildCheckField]` across three categories (Performance, Release Safety, Audio).

## Try it out

1. **Import this sample** from the Package Manager.
2. **Attach the component:** create a new GameObject in a scene (or a prefab) and add the *Sample Game Config* component (Component → Build Checklist → Samples → Sample Game Config). For prefab validation to work, save it as a prefab in your project.
3. **Open the window:** **Window → Build Checklist**.
4. The five sample fields should appear, grouped by category. Toggle any of them on to define a rule.
   - Suggested first rule: enable `targetFps` → operator `Equals` → expected `60` → severity `Error`.
   - Suggested second rule: enable `enableCheats` → operator `Equals` → expected `false`.
5. Edit the values on the GameObject / prefab to make the rules pass or fail.
6. Click **Run Now ▶** in the toolbar to evaluate.
7. Switch to the **Results** tab to see pass / fail status, click rows to ping the GameObject, double-click to jump to the script.

## Triggering at build time

Once at least one rule is in `Error` severity and failing, run **File → Build And Run** — the build will be aborted at preprocess time and the failing rule will be logged to the console.

> **Note:** v1 scans every prefab in the project plus every currently-loaded scene. Closed scenes in Build Settings are not auto-opened — keep them open if you want them validated.
