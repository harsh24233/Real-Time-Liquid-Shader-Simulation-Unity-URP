#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupFluidContainers
{
    [MenuItem("Tools/Setup Fluid Showcase")]
    public static void SetupShowcase()
    {
        // 1. Ensure Prefabs directory exists
        if (!Directory.Exists("Assets/Prefabs"))
        {
            Directory.CreateDirectory("Assets/Prefabs");
            AssetDatabase.Refresh();
        }

        // 2. Load Materials
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
        Material waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Water_Fluid.mat");
        Material milkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Milk_Fluid.mat");
        Material vegOilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/VegOil_Fluid.mat");
        Material honeyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Honey_Fluid.mat");
        Material ketchupMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ketchup_Fluid.mat");

        // 3. Define the 5 container setups with discrete Glass and Fluid models
        var containerConfigs = new (
            string name,
            string glassModelPath,
            string fluidModelPath,
            string fluidChildName,
            FluidDensityType fluidType,
            Material fluidMat,
            Vector3 pos
        )[]
        {
            (
                "Flask_Conical_Milk",
                "Assets/Models/Flask_Conical_Glass.obj",
                "Assets/Models/Flask_Conical_Fluid.obj",
                "Milk",
                FluidDensityType.Milk,
                milkMat,
                new Vector3(-2.4f, 1.0f, -6.0f)
            ),
            (
                "Flask_Spherical_Honey",
                "Assets/Models/Flask_Spherical_Glass.obj",
                "Assets/Models/Flask_Spherical_Fluid.obj",
                "Honey",
                FluidDensityType.Honey,
                honeyMat,
                new Vector3(-1.2f, 1.0f, -6.0f)
            ),
            (
                "Bottle_Square_VegOil",
                "Assets/Models/Bottle_Square_Glass.obj",
                "Assets/Models/Bottle_Square_Fluid.obj",
                "VegOil",
                FluidDensityType.VegetableOil,
                vegOilMat,
                new Vector3(0.0f, 1.0f, -6.0f)
            ),
            (
                "Jar_Cylinder_Ketchup",
                "Assets/Models/Jar_Cylinder_Glass.obj",
                "Assets/Models/Jar_Cylinder_Fluid.obj",
                "Ketchup",
                FluidDensityType.Ketchup,
                ketchupMat,
                new Vector3(1.2f, 1.0f, -6.0f)
            ),
            (
                "Bottle_Hexagonal_Water",
                "Assets/Models/Bottle_Hexagonal_Glass.obj",
                "Assets/Models/Bottle_Hexagonal_Fluid.obj",
                "Water",
                FluidDensityType.Water,
                waterMat,
                new Vector3(2.4f, 1.0f, -6.0f)
            )
        };

        // 4. Create or find Showcase Root in current scene
        GameObject showcaseRoot = GameObject.Find("FluidContainers_Showcase");
        if (showcaseRoot == null)
        {
            showcaseRoot = new GameObject("FluidContainers_Showcase");
        }
        else
        {
            while (showcaseRoot.transform.childCount > 0)
            {
                Object.DestroyImmediate(showcaseRoot.transform.GetChild(0).gameObject);
            }
        }

        // Add a clean tabletop shelf underneath for visual presentation
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Showcase_Shelf";
        shelf.transform.SetParent(showcaseRoot.transform);
        shelf.transform.position = new Vector3(0f, 0.86f, -6.0f);
        shelf.transform.localScale = new Vector3(6.5f, 0.04f, 0.6f);

        // 5. Instantiate, configure and create Prefabs for all 5 containers
        foreach (var cfg in containerConfigs)
        {
            GameObject glassModel = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.glassModelPath);
            GameObject fluidModel = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.fluidModelPath);

            if (glassModel == null || fluidModel == null)
            {
                Debug.LogError($"Could not load model for {cfg.name}: glass={glassModel}, fluid={fluidModel}");
                continue;
            }

            // Create Container Root
            GameObject containerRoot = new GameObject(cfg.name);
            containerRoot.transform.SetParent(showcaseRoot.transform);
            containerRoot.transform.position = cfg.pos;
            containerRoot.transform.rotation = Quaternion.identity;

            // Child 1: Glass Outer Vessel
            GameObject glassObj = Object.Instantiate(glassModel, containerRoot.transform);
            glassObj.name = "Glass";
            glassObj.transform.localPosition = Vector3.zero;
            glassObj.transform.localRotation = Quaternion.identity;
            glassObj.transform.localScale = Vector3.one;

            Renderer glassRend = glassObj.GetComponentInChildren<Renderer>();
            if (glassRend != null && glassMat != null)
            {
                glassRend.sharedMaterial = glassMat;
            }

            // Child 2: Liquid Inner Volume
            GameObject liquidObj = Object.Instantiate(fluidModel, containerRoot.transform);
            liquidObj.name = cfg.fluidChildName;
            liquidObj.transform.localPosition = Vector3.zero;
            liquidObj.transform.localRotation = Quaternion.identity;
            liquidObj.transform.localScale = Vector3.one;

            Renderer liquidRend = liquidObj.GetComponentInChildren<Renderer>();
            if (liquidRend != null && cfg.fluidMat != null)
            {
                liquidRend.sharedMaterial = cfg.fluidMat;
            }

            // Attach ViscousLiquidWobble component on container root
            ViscousLiquidWobble wobble = containerRoot.AddComponent<ViscousLiquidWobble>();

            // Use serialized object to assign references cleanly
            SerializedObject so = new SerializedObject(wobble);
            so.FindProperty("containerTransform").objectReferenceValue = containerRoot.transform;
            so.FindProperty("liquidRenderer").objectReferenceValue = liquidRend;
            so.FindProperty("waterMaterial").objectReferenceValue = waterMat;
            so.FindProperty("milkMaterial").objectReferenceValue = milkMat;
            so.FindProperty("vegOilMaterial").objectReferenceValue = vegOilMat;
            so.FindProperty("honeyMaterial").objectReferenceValue = honeyMat;
            so.FindProperty("ketchupMaterial").objectReferenceValue = ketchupMat;
            so.FindProperty("fluidType").enumValueIndex = (int)cfg.fluidType;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Apply fluid profile
            wobble.ApplyFluidProfile(cfg.fluidType, true);

            // Save as Prefab
            string prefabPath = $"Assets/Prefabs/{cfg.name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(containerRoot, prefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"Configured and saved prefab with distinct Glass & Liquid meshes: {prefabPath}");
        }

        // 6. Save active scene
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("Fluid Containers Showcase setup completed successfully with visible fluid volumes!");
    }
}
#endif
