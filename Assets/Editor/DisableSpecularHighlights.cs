using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class DisableSpecularHighlights
{
    [MenuItem("Tools/Disable Specular Highlights On All Materials")]
    static void DisableSpecular()
    {
        HashSet<Material> materials = new HashSet<Material>();

        // Collect standalone .mat assets
        foreach (string guid in AssetDatabase.FindAssets("t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Material mat)
                    materials.Add(mat);
            }
        }

        // Collect materials embedded inside FBX / model files
        foreach (string guid in AssetDatabase.FindAssets("t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Material mat)
                    materials.Add(mat);
            }
        }

        int count = 0;
        foreach (Material mat in materials)
        {
            if (mat.HasProperty("_SpecularHighlights"))
            {
                mat.SetFloat("_SpecularHighlights", 0f);
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Done! Specular highlights disabled on {count} material(s).");
    }
}
