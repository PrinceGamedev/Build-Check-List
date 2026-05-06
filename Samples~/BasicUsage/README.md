# Basic Usage Sample

This sample shows the simplest way to use Build Checklist.

## Included File

- `SampleGameConfig.cs`: a `MonoBehaviour` with five fields marked using `[BuildCheckField]`.

The fields are grouped into these categories:

- Performance
- Release Safety
- Audio

## Try It

1. Import this sample from Package Manager.
2. Create a GameObject in a scene.
3. Add **Sample Game Config** from **Component > Build Checklist > Samples > Sample Game Config**.
4. Save the scene. You can also save the GameObject as a prefab if you want prefab validation.
5. Open **Window > Build Checklist**.
6. Pick a panel from the toolbar: `Development`, `Debug`, or `Release`.
7. Enable a rule for `targetFps`.
8. Set the operator to `Equals`.
9. Set the expected value to `60`.
10. Set severity to `Error`.
11. Enable a rule for `enableCheats`.
12. Set the operator to `Equals`.
13. Set the expected value to `false`.
14. Click **Run Now**.
15. Open the **Results** tab to see pass and fail results.

Try changing `targetFps` or `enableCheats` on the GameObject, then click **Run Now** again.

## Build Time Test

To test the build flow:

1. Make one `Error` rule fail.
2. Open Unity's **Build Settings** window.
3. Press **Build** or **Build And Run**.
4. Choose a panel from the Build Checklist popup.

Build Checklist will validate the selected panel. If warning or error issues are found, the Results UI opens and the current build is cancelled.

After that:

1. Fix the issue manually or use **Fix Selected** / **Fix All Visible** if available.
2. Click **Recheck** in the checklist window.
3. Start the build again.

## Notes

- The selected build panel is saved before validation starts.
- Batch mode builds do not show the popup. They use the currently active panel.
- The tool checks prefab assets, currently loaded scenes, and enabled scenes from Build Settings.
