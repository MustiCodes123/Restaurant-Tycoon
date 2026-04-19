using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class RemoveMissingScripts : EditorWindow
{
    private int missingCount = 0;
    private int removedCount = 0;
    private bool includeScenes = true;
    private bool includePrefabs = true;
    private Vector2 scrollPosition;
    private List<string> log = new List<string>();

    [MenuItem("Tools/Remove Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow<RemoveMissingScripts>("Remove Missing Scripts");
    }

    void OnGUI()
    {
        GUILayout.Label("Remove All Missing Scripts", EditorStyles.boldLabel);
        GUILayout.Space(10);

        includeScenes = EditorGUILayout.Toggle("Clean Current Scene", includeScenes);
        includePrefabs = EditorGUILayout.Toggle("Clean All Prefabs", includePrefabs);
        
        GUILayout.Space(10);

        if (GUILayout.Button("Find Missing Scripts", GUILayout.Height(30)))
        {
            FindMissingScripts();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("Remove All Missing Scripts", GUILayout.Height(40)))
        {
            RemoveAllMissingScripts();
        }

        GUILayout.Space(10);
        GUILayout.Label($"Missing Scripts Found: {missingCount}", EditorStyles.helpBox);
        GUILayout.Label($"Missing Scripts Removed: {removedCount}", EditorStyles.helpBox);

        GUILayout.Space(10);
        GUILayout.Label("Log:", EditorStyles.boldLabel);
        
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        foreach (var entry in log)
        {
            GUILayout.Label(entry);
        }
        GUILayout.EndScrollView();
    }

    void FindMissingScripts()
    {
        log.Clear();
        missingCount = 0;
        
        log.Add("=== Searching for Missing Scripts ===");

        if (includeScenes)
        {
            missingCount += FindInScene();
        }

        if (includePrefabs)
        {
            missingCount += FindInPrefabs();
        }

        log.Add($"Total missing scripts found: {missingCount}");
        Repaint();
    }

    void RemoveAllMissingScripts()
    {
        log.Clear();
        removedCount = 0;
        
        log.Add("=== Removing Missing Scripts ===");

        if (includeScenes)
        {
            removedCount += CleanScene();
        }

        if (includePrefabs)
        {
            removedCount += CleanPrefabs();
        }

        log.Add($"Total missing scripts removed: {removedCount}");
        log.Add("Done! Don't forget to save your scene/project.");
        
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Complete", 
            $"Removed {removedCount} missing scripts.\nAssets saved.", "OK");
        
        Repaint();
    }

    int FindInScene()
    {
        int count = 0;
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        foreach (GameObject go in allObjects)
        {
            int missingInObject = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingInObject > 0)
            {
                count += missingInObject;
                log.Add($"Found {missingInObject} missing script(s) in: {go.name}");
            }
        }
        
        log.Add($"Scene: {count} missing scripts found");
        return count;
    }

    int CleanScene()
    {
        int count = 0;
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        foreach (GameObject go in allObjects)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                count += removed;
                log.Add($"Removed {removed} missing script(s) from: {go.name}");
            }
        }
        
        log.Add($"Scene: {count} missing scripts removed");
        return count;
    }

    int FindInPrefabs()
    {
        int count = 0;
        string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab");
        
        int progress = 0;
        foreach (string guid in allPrefabs)
        {
            progress++;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            EditorUtility.DisplayProgressBar("Searching Prefabs", 
                $"Checking {progress}/{allPrefabs.Length}: {path}", 
                (float)progress / allPrefabs.Length);
            
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                int missingInPrefab = CountMissingScriptsInPrefab(prefab);
                if (missingInPrefab > 0)
                {
                    count += missingInPrefab;
                    log.Add($"Found {missingInPrefab} missing script(s) in prefab: {path}");
                }
            }
        }
        
        EditorUtility.ClearProgressBar();
        log.Add($"Prefabs: {count} missing scripts found");
        return count;
    }

    int CleanPrefabs()
    {
        int count = 0;
        string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab");
        
        int progress = 0;
        foreach (string guid in allPrefabs)
        {
            progress++;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            EditorUtility.DisplayProgressBar("Cleaning Prefabs", 
                $"Processing {progress}/{allPrefabs.Length}: {path}", 
                (float)progress / allPrefabs.Length);
            
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                int removed = RemoveMissingScriptsFromPrefab(prefab, path);
                if (removed > 0)
                {
                    count += removed;
                }
            }
        }
        
        EditorUtility.ClearProgressBar();
        log.Add($"Prefabs: {count} missing scripts removed");
        return count;
    }

    int CountMissingScriptsInPrefab(GameObject prefab)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab);
        
        foreach (Transform child in prefab.transform)
        {
            count += CountMissingScriptsInPrefab(child.gameObject);
        }
        
        return count;
    }

    int RemoveMissingScriptsFromPrefab(GameObject prefab, string path)
    {
        int count = 0;
        
        // Get all GameObjects in the prefab hierarchy
        GameObject[] allObjects = prefab.GetComponentsInChildren<Transform>(true)
            .Select(t => t.gameObject).ToArray();
        
        foreach (GameObject go in allObjects)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                count += removed;
            }
        }
        
        if (count > 0)
        {
            EditorUtility.SetDirty(prefab);
            log.Add($"Removed {count} missing script(s) from prefab: {path}");
        }
        
        return count;
    }
}
