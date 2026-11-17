#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupWeek10Showcase
{
    [InitializeOnLoadMethod]
    private static void AutoRunIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (GameObject.Find("Week10_EnclosedCracking_Showcase") == null)
            {
                Debug.Log("Week 10 showcase not detected in active scene. Auto-running RunSetupWeek10Showcase...");
                RunSetupWeek10Showcase();
            }
        };
    }

    [MenuItem("Tools/Setup Week 10 - Enclosed Cracking & Outward Flow Showcase")]
    public static void RunSetupWeek10Showcase()
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        if (EditorSceneManager.GetActiveScene().path != scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        string prefabsDir = "Assets/Prefabs/Week10";
        if (!Directory.Exists(prefabsDir))
        {
            Directory.CreateDirectory(prefabsDir);
        }
        AssetDatabase.Refresh();

        // 1. Load Glass and Week 10 Fluid Materials
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
        Material petrolMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_Petrol_Fluid.mat");
        Material mercuryMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_Mercury_Fluid.mat");
        Material motorOilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_MotorOil_Fluid.mat");
        Material coolantMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_Coolant_Fluid.mat");
        Material cryoGasMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_CryoGas_Fluid.mat");
        Material treeSapMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_TreeSap_Fluid.mat");
        Material mazeGelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_MazeGel_Fluid.mat");

        if (glassMat == null || petrolMat == null || mercuryMat == null || motorOilMat == null ||
            coolantMat == null || cryoGasMat == null || treeSapMat == null || mazeGelMat == null)
        {
            Debug.LogError("SetupWeek10Showcase: Could not find one or more materials in Assets/Materials/Week10!");
            return;
        }

        // 2. Define the 7 Structure Configurations
        var configs = new (
            string name,
            string glassModelPath,
            string fluidModelPath,
            Week10FluidType fluidType,
            Week10StructureType structType,
            Material fluidMat,
            Vector3 pos,
            float initialFill
        )[]
        {
            (
                "Week10_PetrolContainer_Jerrycan",
                "Assets/Models/Week9_PetrolContainer_Glass.obj",
                "Assets/Models/Week9_PetrolContainer_Fluid.obj",
                Week10FluidType.Petrol_Gasoline,
                Week10StructureType.PetrolContainer_Jerrycan,
                petrolMat,
                new Vector3(-3.0f, 0.15f, -0.6f),
                0.75f
            ),
            (
                "Week10_StarShapedBox",
                "Assets/Models/Week9_StarBox_Glass.obj",
                "Assets/Models/Week9_StarBox_Fluid.obj",
                Week10FluidType.LiquidMercury,
                Week10StructureType.StarShapedBox,
                mercuryMat,
                new Vector3(-2.0f, 0.15f, -0.6f),
                0.65f
            ),
            (
                "Week10_ToroidalTank",
                "Assets/Models/Week9_ToroidalTank_Glass.obj",
                "Assets/Models/Week9_ToroidalTank_Fluid.obj",
                Week10FluidType.SyntheticMotorOil,
                Week10StructureType.ToroidalTank,
                motorOilMat,
                new Vector3(-1.0f, 0.15f, -0.6f),
                0.55f
            ),
            (
                "Week10_YJunctionManifold", // Important 3-way piping manifold
                "Assets/Models/Week9_YJunctionManifold_Glass.obj",
                "Assets/Models/Week9_YJunctionManifold_Fluid.obj",
                Week10FluidType.HydraulicCoolant,
                Week10StructureType.YJunctionManifold,
                coolantMat,
                new Vector3(0.0f, 0.15f, -0.6f), // Front Center
                0.80f
            ),
            (
                "Week10_CapsuleTank",
                "Assets/Models/Week9_CapsuleTank_Glass.obj",
                "Assets/Models/Week9_CapsuleTank_Fluid.obj",
                Week10FluidType.CryogenicLiquidGas,
                Week10StructureType.CapsuleTank,
                cryoGasMat,
                new Vector3(1.0f, 0.15f, -0.6f),
                0.60f
            ),
            (
                "Week10_TreeBranchedStructure",
                "Assets/Models/Week9_TreeBranched_Glass.obj",
                "Assets/Models/Week9_TreeBranched_Fluid.obj",
                Week10FluidType.TreeSapResin,
                Week10StructureType.TreeBranchedStructure,
                treeSapMat,
                new Vector3(2.0f, 0.15f, -0.6f),
                0.50f
            ),
            (
                "Week10_MazeMultichamber",
                "Assets/Models/Week9_MazeMultichamber_Glass.obj",
                "Assets/Models/Week9_MazeMultichamber_Fluid.obj",
                Week10FluidType.HeavyMazeGel,
                Week10StructureType.MazeMultichamber,
                mazeGelMat,
                new Vector3(3.0f, 0.15f, -0.6f),
                0.70f
            )
        };

        // 3. Create or clean Week 10 Showcase Root
        GameObject week10Root = GameObject.Find("Week10_EnclosedCracking_Showcase");
        if (week10Root == null)
        {
            week10Root = new GameObject("Week10_EnclosedCracking_Showcase");
        }
        else
        {
            while (week10Root.transform.childCount > 0)
            {
                Object.DestroyImmediate(week10Root.transform.GetChild(0).gameObject);
            }
        }

        // 4. Showcase Shelf for Week 10 (Z = -0.6m, Y = -0.09m)
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Showcase_Shelf_Week10";
        shelf.transform.SetParent(week10Root.transform);
        shelf.transform.position = new Vector3(0f, -0.09f, -0.6f);
        shelf.transform.localScale = new Vector3(7.4f, 0.04f, 0.70f);

        Renderer shelfRend = shelf.GetComponent<Renderer>();
        if (shelfRend != null && glassMat != null)
        {
            shelfRend.sharedMaterial = glassMat;
        }

        // 5. Assemble and Configure the 7 Week 10 Enclosed Systems
        for (int i = 0; i < configs.Length; i++)
        {
            var cfg = configs[i];

            // Display plinth under each vessel
            GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = $"Plinth_{cfg.name}";
            plinth.transform.SetParent(week10Root.transform);
            plinth.transform.position = new Vector3(cfg.pos.x, -0.075f, cfg.pos.z);
            plinth.transform.localScale = new Vector3(0.28f, 0.015f, 0.28f);

            string prefabPath = $"{prefabsDir}/{cfg.name}.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject containerRoot;

            if (prefabAsset != null)
            {
                containerRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, week10Root.transform);
                containerRoot.name = cfg.name;
                containerRoot.transform.position = cfg.pos;
                containerRoot.transform.rotation = Quaternion.identity;
            }
            else
            {
                Debug.LogError($"SetupWeek10Showcase: Could not find prefab at {prefabPath}");
                continue;
            }

            Week10FluidCrackingFlowController controller = containerRoot.GetComponent<Week10FluidCrackingFlowController>();
            if (controller != null)
            {
                controller.CacheReferences();
                controller.fillAmount = cfg.initialFill;
                controller.UpdateFillCutPlane(true);
            }

            Debug.Log($"Placed Week 10 Vessel ({i + 1}/{configs.Length}): {cfg.name}");
        }

        // 6. Update ShowcaseCameraController
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            ShowcaseCameraController camCtrl = mainCam.GetComponent<ShowcaseCameraController>();
            if (camCtrl != null)
            {
                camCtrl.RefreshAllVessels();
            }
        }

        // 7. Save Scene
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SUCCESS: Week 10 Enclosed Cracking & Outward Flow Showcase successfully assembled in scene!");
    }
}
#endif
