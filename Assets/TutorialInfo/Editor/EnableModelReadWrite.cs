#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automatically ensures that all 3D mesh assets in Assets/Models have Read/Write enabled
/// so that procedural slicing, meniscus caps, and boolean fracture culling can read vertices at runtime.
/// </summary>
[InitializeOnLoad]
public static class EnableModelReadWrite
{
    static EnableModelReadWrite()
    {
        EnableAllModels();
    }

    [MenuItem("Tools/Enable Read-Write On All Models")]
    public static void EnableAllModels()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" });
        int updated = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                updated++;
            }
        }
        if (updated > 0)
        {
            Debug.Log($"[EnableModelReadWrite] Successfully enabled Read/Write on {updated} model assets.");
        }
    }
}
#endif
