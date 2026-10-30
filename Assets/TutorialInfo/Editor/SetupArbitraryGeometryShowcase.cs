#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupArbitraryGeometryShowcase
{
    [InitializeOnLoadMethod]
    private static void AutoRunIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (GameObject.Find("Week2_ArbitraryGeometry_Showcase") == null)
            {
                Debug.Log("Week 2 showcase not detected in active scene. Auto-running SetupWeek2Showcase...");
                SetupWeek2Showcase();
            }
        };
    }

    [MenuItem("Tools/Setup Week 2 - Arbitrary Geometry Showcase")]
    public static void SetupWeek2Showcase()
    {
        // 1. Ensure Week 2 Prefabs directory exists
        string prefabsDir = "Assets/Prefabs/Week2";
        if (!Directory.Exists(prefabsDir))
        {
            Directory.CreateDirectory(prefabsDir);
        }
        AssetDatabase.Refresh();

        // 2. Load Materials
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
        Material waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Water_Fluid.mat");
        Material milkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Milk_Fluid.mat");
        Material vegOilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/VegOil_Fluid.mat");
        Material honeyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Honey_Fluid.mat");
        Material ketchupMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ketchup_Fluid.mat");

        if (glassMat == null || waterMat == null || vegOilMat == null || honeyMat == null || ketchupMat == null)
        {
            Debug.LogError("SetupWeek2Showcase: Could not find one or more fluid/glass materials in Assets!");
            return;
        }

        // 3. Define the 12 arbitrary geometry configurations:
        // Positioned along a new, dedicated front row (Z = -3.8f, Y = 0.45f)
        // Spaced across X from -4.95 to +4.95 with step = 0.90m
        var vesselConfigs = new (
            string name,
            string glassModelPath,
            string fluidModelPath,
            string fluidChildName,
            FluidSimType fluidType,
            Material fluidMat,
            SurfaceFrictionPreset surfaceFriction,
            ArbitraryGeometryType geometryType,
            Vector3 pos
        )[]
        {
            (
                "Vessel_Hourglass_Water",
                "Assets/Models/Vessel_Hourglass_Glass.obj",
                "Assets/Models/Vessel_Hourglass_Fluid.obj",
                "Water",
                FluidSimType.Water,
                waterMat,
                SurfaceFrictionPreset.PolishedGlass,
                ArbitraryGeometryType.Hourglass_Constriction,
                new Vector3(-4.95f, 0.45f, -3.8f)
            ),
            (
                "Vessel_ToroidRing_VegOil",
                "Assets/Models/Vessel_ToroidRing_Glass.obj",
                "Assets/Models/Vessel_ToroidRing_Fluid.obj",
                "VegOil",
                FluidSimType.VegetableOil,
                vegOilMat,
                SurfaceFrictionPreset.StandardGlass,
                ArbitraryGeometryType.Toroid_DualWallLoop,
                new Vector3(-4.05f, 0.45f, -3.8f)
            ),
            (
                "Vessel_TriangularPrism_Honey",
                "Assets/Models/Vessel_TriangularPrism_Glass.obj",
                "Assets/Models/Vessel_TriangularPrism_Fluid.obj",
                "Honey",
                FluidSimType.Honey,
                honeyMat,
                SurfaceFrictionPreset.FrostedGlass,
                ArbitraryGeometryType.Triangular_PrismVertices,
                new Vector3(-3.15f, 0.45f, -3.8f)
            ),
            (
                "Vessel_Teardrop_Ketchup",
                "Assets/Models/Vessel_Teardrop_Glass.obj",
                "Assets/Models/Vessel_Teardrop_Fluid.obj",
                "Ketchup",
                FluidSimType.Ketchup,
                ketchupMat,
                SurfaceFrictionPreset.TexturedResin,
                ArbitraryGeometryType.Teardrop_AerodynamicSmooth,
                new Vector3(-2.25f, 0.45f, -3.8f)
            ),
            (
                "Vessel_OctagonalDecanter_Water",
                "Assets/Models/Vessel_OctagonalDecanter_Glass.obj",
                "Assets/Models/Vessel_OctagonalDecanter_Fluid.obj",
                "Water",
                FluidSimType.Water,
                waterMat,
                SurfaceFrictionPreset.StandardGlass,
                ArbitraryGeometryType.Octagonal_FacetedEdges,
                new Vector3(-1.35f, 0.45f, -3.8f)
            ),
            (
                "Vessel_TwistedColumn_VegOil",
                "Assets/Models/Vessel_TwistedColumn_Glass.obj",
                "Assets/Models/Vessel_TwistedColumn_Fluid.obj",
                "VegOil",
                FluidSimType.VegetableOil,
                vegOilMat,
                SurfaceFrictionPreset.FrostedGlass,
                ArbitraryGeometryType.TwistedColumn_HelicalShear,
                new Vector3(-0.45f, 0.45f, -3.8f)
            ),
            (
                "Vessel_DualBulb_Honey",
                "Assets/Models/Vessel_DualBulb_Glass.obj",
                "Assets/Models/Vessel_DualBulb_Fluid.obj",
                "Honey",
                FluidSimType.Honey,
                honeyMat,
                SurfaceFrictionPreset.StandardGlass,
                ArbitraryGeometryType.DualBulb_ChamberOrifice,
                new Vector3(0.45f, 0.45f, -3.8f)
            ),
            (
                "Vessel_CardioidHeart_Ketchup",
                "Assets/Models/Vessel_CardioidHeart_Glass.obj",
                "Assets/Models/Vessel_CardioidHeart_Fluid.obj",
                "Ketchup",
                FluidSimType.Ketchup,
                ketchupMat,
                SurfaceFrictionPreset.PolishedGlass,
                ArbitraryGeometryType.CardioidHeart_CreaseBoundary,
                new Vector3(1.35f, 0.45f, -3.8f)
            ),
            (
                "Vessel_StarPrism_Water",
                "Assets/Models/Vessel_StarPrism_Glass.obj",
                "Assets/Models/Vessel_StarPrism_Fluid.obj",
                "Water",
                FluidSimType.Water,
                waterMat,
                SurfaceFrictionPreset.TexturedResin,
                ArbitraryGeometryType.StarPrism_AcuteCornerDrag,
                new Vector3(2.25f, 0.45f, -3.8f)
            ),
            (
                "Vessel_AlchemicalRetort_VegOil",
                "Assets/Models/Vessel_AlchemicalRetort_Glass.obj",
                "Assets/Models/Vessel_AlchemicalRetort_Fluid.obj",
                "VegOil",
                FluidSimType.VegetableOil,
                vegOilMat,
                SurfaceFrictionPreset.PolishedGlass,
                ArbitraryGeometryType.AlchemicalRetort_NeckThrottling,
                new Vector3(3.15f, 0.45f, -3.8f)
            ),
            (
                "Vessel_DiamondGem_Honey",
                "Assets/Models/Vessel_DiamondGem_Glass.obj",
                "Assets/Models/Vessel_DiamondGem_Fluid.obj",
                "Honey",
                FluidSimType.Honey,
                honeyMat,
                SurfaceFrictionPreset.PolishedGlass,
                ArbitraryGeometryType.DiamondGem_FacetedBipyramidal,
                new Vector3(4.05f, 0.45f, -3.8f)
            ),
            (
                "Vessel_EllipticalCanteen_Ketchup",
                "Assets/Models/Vessel_EllipticalCanteen_Glass.obj",
                "Assets/Models/Vessel_EllipticalCanteen_Fluid.obj",
                "Ketchup",
                FluidSimType.Ketchup,
                ketchupMat,
                SurfaceFrictionPreset.StandardGlass,
                ArbitraryGeometryType.EllipticalCanteen_RibbedWall,
                new Vector3(4.95f, 0.45f, -3.8f)
            )
        };

        // 4. Create or find Week 2 Showcase Root (Keeping existing showcase completely untouched!)
        GameObject week2Root = GameObject.Find("Week2_ArbitraryGeometry_Showcase");
        if (week2Root == null)
        {
            week2Root = new GameObject("Week2_ArbitraryGeometry_Showcase");
        }
        else
        {
            // Clear prior children if re-running
            while (week2Root.transform.childCount > 0)
            {
                Object.DestroyImmediate(week2Root.transform.GetChild(0).gameObject);
            }
        }

        // 5. Add a dedicated, elegant showcase shelf for this front line
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Showcase_Shelf_Week2";
        shelf.transform.SetParent(week2Root.transform);
        shelf.transform.position = new Vector3(0f, 0.31f, -3.8f);
        shelf.transform.localScale = new Vector3(11.4f, 0.04f, 0.70f);

        // Assign a clean neutral material or glass material
        Renderer shelfRend = shelf.GetComponent<Renderer>();
        if (shelfRend != null && glassMat != null)
        {
            shelfRend.sharedMaterial = glassMat;
        }

        // 6. Instantiate, assemble, configure, and create Prefabs for all 12 arbitrary vessels
        for (int i = 0; i < vesselConfigs.Length; i++)
        {
            var cfg = vesselConfigs[i];

            GameObject glassModel = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.glassModelPath);
            GameObject fluidModel = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.fluidModelPath);

            if (glassModel == null || fluidModel == null)
            {
                Debug.LogError($"Could not load model for {cfg.name}: glass={glassModel}, fluid={fluidModel}");
                continue;
            }

            // Display plinth under each vessel
            GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = $"Plinth_{cfg.name}";
            plinth.transform.SetParent(week2Root.transform);
            plinth.transform.position = new Vector3(cfg.pos.x, 0.325f, cfg.pos.z);
            plinth.transform.localScale = new Vector3(0.24f, 0.015f, 0.24f);

            // Container Root
            GameObject containerRoot = new GameObject(cfg.name);
            containerRoot.transform.SetParent(week2Root.transform);
            containerRoot.transform.position = cfg.pos;
            containerRoot.transform.rotation = Quaternion.identity;

            // Child 1: Outer Glass Enclosure
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

            // Child 2: Inner Liquid Volume
            GameObject fluidObj = Object.Instantiate(fluidModel, containerRoot.transform);
            fluidObj.name = cfg.fluidChildName;
            fluidObj.transform.localPosition = Vector3.zero;
            fluidObj.transform.localRotation = Quaternion.identity;
            fluidObj.transform.localScale = Vector3.one;

            Renderer fluidRend = fluidObj.GetComponentInChildren<Renderer>();
            if (fluidRend != null && cfg.fluidMat != null)
            {
                fluidRend.sharedMaterial = cfg.fluidMat;
            }

            // Attach ArbitraryViscousFluidFriction component
            ArbitraryViscousFluidFriction sim = containerRoot.AddComponent<ArbitraryViscousFluidFriction>();

            // Assign references cleanly via SerializedObject
            SerializedObject so = new SerializedObject(sim);
            so.FindProperty("containerTransform").objectReferenceValue = containerRoot.transform;
            so.FindProperty("fluidRenderer").objectReferenceValue = fluidRend;
            so.FindProperty("glassRenderer").objectReferenceValue = glassRend;
            so.FindProperty("waterMaterial").objectReferenceValue = waterMat;
            so.FindProperty("vegOilMaterial").objectReferenceValue = vegOilMat;
            so.FindProperty("honeyMaterial").objectReferenceValue = honeyMat;
            so.FindProperty("ketchupMaterial").objectReferenceValue = ketchupMat;
            so.FindProperty("milkMaterial").objectReferenceValue = milkMat;
            so.FindProperty("fluidType").enumValueIndex = (int)cfg.fluidType;
            so.FindProperty("surfaceMaterial").enumValueIndex = (int)cfg.surfaceFriction;
            so.FindProperty("geometryType").enumValueIndex = (int)cfg.geometryType;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Apply physical profile
            sim.ApplyPhysicsModel(true);

            // Save as Prefab
            string prefabPath = $"{prefabsDir}/{cfg.name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(containerRoot, prefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"Created Week 2 Arbitrary Geometry Prefab ({i + 1}/12): {prefabPath}");
        }

        // 7. Ensure Main Camera has ShowcaseCameraController attached
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            ShowcaseCameraController camCtrl = mainCam.GetComponent<ShowcaseCameraController>();
            if (camCtrl == null)
            {
                camCtrl = mainCam.gameObject.AddComponent<ShowcaseCameraController>();
            }
            camCtrl.FindWeek2Vessels();
        }

        // 8. Save active scene
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SUCCESS: Week 2 Arbitrary Geometry Showcase (12 distinct vessels in a separate line) fully set up!");
    }
}
#endif
