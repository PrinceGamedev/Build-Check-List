using Gameyogi.BuildChecklist;
using UnityEngine;

namespace Gameyogi.BuildChecklist.Samples
{
    /// <summary>
    /// Sample MonoBehaviour demonstrating how to mark fields as participants
    /// in the Build Checklist. After importing this sample, attach this
    /// component to a prefab or a scene GameObject, open
    /// Window → Build Checklist, and define rules for the marked fields.
    /// </summary>
    [AddComponentMenu("Build Checklist/Samples/Sample Game Config")]
    public sealed class SampleGameConfig : MonoBehaviour
    {
        [BuildCheckField(Category = "Performance",
                         Description = "Target frame rate the player runs at.")]
        public int targetFps = 60;

        [BuildCheckField(Category = "Performance")]
        public float musicVolume = 1.0f;

        [BuildCheckField(Category = "Release Safety",
                         Description = "Cheat console must be disabled in shipping builds.")]
        public bool enableCheats = false;

        [BuildCheckField(Category = "Release Safety")]
        public string buildLabel = "release";

        [BuildCheckField(Category = "Audio",
                         Description = "Theme clip must be assigned for the menu to play music.")]
        public AudioClip themeMusic;
    }
}
