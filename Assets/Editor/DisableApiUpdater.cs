using UnityEditor;

// Ensures the Unity API Updater is disabled to prevent crashes on corrupted assemblies.
[InitializeOnLoad]
public static class DisableApiUpdater
{
    static DisableApiUpdater()
    {
        // Known editor prefs that gate the API Updater.
        EditorPrefs.SetBool("UnityAPIUpdater.Disable", true);
        EditorPrefs.SetBool("UnityUseAPIUpdater", false);
        EditorPrefs.SetBool("ApiUpdater/ShowDialog", false);
    }
}
