#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupWeek9Showcase
{
    [InitializeOnLoadMethod]
    private static void AutoRunIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (GameObject.Find("Week9_RealisticDynamics_Showcase") == null)
            {
                Debug.Log("Week 9 showcase not detected in active scene. Auto-running RunSetupWeek9Showcase...");
                RunSetupWeek9Showcase();
            }
        };
    }

    [MenuItem("Tools/Setup Week 9 - Realistic Fluid Dynamics & Dynamic Fill Showcase")]
    public static void RunSetupWeek9Showcase()
    {
        string prefabsDir = "Assets/Prefabs/Week9";
        if (!Directory.Exists(prefabsDir))
        {
            Directory.CreateDirectory(prefabsDir);
        }
        AssetDatabase.Refresh();

        // 1. Load Glass and Fluid Materials
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
        Material petrolMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week9/Week9_Petrol_Fluid.mat");
        Material mercuryMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week9/Week9_Mercury_Fluid.mat");
        Material motorOilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week9/Week9_MotorOil_Fluid.mat");
        Material coolantMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week9/Week9_Coolant_Fluid.mat");
        Material cryoGasMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week9/Week9_CryoGas_Fluid.mat");
        Material treeSapMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week9/Week9_TreeSap_Fluid.mat");
        Material mazeGelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week9/Week9_MazeGel_Fluid.mat");

        if (glassMat == null || petrolMat == null || mercuryMat == null || motorOilMat == null ||
            coolantMat == null || cryoGasMat == null || treeSapMat == null || mazeGelMat == null)
        {
            Debug.LogError("SetupWeek9Showcase: Could not find one or more materials in Assets/Materials/Week9!");
            return;
        }

        // 2. Define the 7 Structure Configurations
        var configs = new (
            string name,
            string glassModelPath,
            string fluidModelPath,
            Week9FluidType fluidType,
            Week9StructureType structType,
            Material fluidMat,
            Vector3 pos,
            float initialFill
        )[]
        {
            (
                "Week9_PetrolContainer_Jerrycan",
                "Assets/Models/Week9_PetrolContainer_Glass.obj",
                "Assets/Models/Week9_PetrolContainer_Fluid.obj",
                Week9FluidType.Petrol_Gasoline,
                Week9StructureType.PetrolContainer_Jerrycan,
                petrolMat,
                new Vector3(-3.0f, 0.15f, -1.8f),
                0.55f
            ),
            (
                "Week9_StarShapedBox",
                "Assets/Models/Week9_StarBox_Glass.obj",
                "Assets/Models/Week9_StarBox_Fluid.obj",
                Week9FluidType.LiquidMercury,
                Week9StructureType.StarShapedBox,
                mercuryMat,
                new Vector3(-2.0f, 0.15f, -1.8f),
                0.50f
            ),
            (
                "Week9_ToroidalTank",
                "Assets/Models/Week9_ToroidalTank_Glass.obj",
                "Assets/Models/Week9_ToroidalTank_Fluid.obj",
                Week9FluidType.SyntheticMotorOil,
                Week9StructureType.ToroidalTank,
                motorOilMat,
                new Vector3(-1.0f, 0.15f, -1.8f),
                0.48f
            ),
            (
                "Week9_YJunctionManifold", // Important 3-way piping manifold
                "Assets/Models/Week9_YJunctionManifold_Glass.obj",
                "Assets/Models/Week9_YJunctionManifold_Fluid.obj",
                Week9FluidType.HydraulicCoolant,
                Week9StructureType.YJunctionManifold,
                coolantMat,
                new Vector3(0.0f, 0.15f, -1.8f), // Front Center prominence
                0.52f
            ),
            (
                "Week9_CapsuleTank",
                "Assets/Models/Week9_CapsuleTank_Glass.obj",
                "Assets/Models/Week9_CapsuleTank_Fluid.obj",
                Week9FluidType.CryogenicLiquidGas,
                Week9StructureType.CapsuleTank,
                cryoGasMat,
                new Vector3(1.0f, 0.15f, -1.8f),
                0.50f
            ),
            (
                "Week9_TreeBranchedStructure",
                "Assets/Models/Week9_TreeBranched_Glass.obj",
                "Assets/Models/Week9_TreeBranched_Fluid.obj",
                Week9FluidType.TreeSapResin,
                Week9StructureType.TreeBranchedStructure,
                treeSapMat,
                new Vector3(2.0f, 0.15f, -1.8f),
                0.45f
            ),
            (
                "Week9_MazeMultichamber",
                "Assets/Models/Week9_MazeMultichamber_Glass.obj",
                "Assets/Models/Week9_MazeMultichamber_Fluid.obj",
                Week9FluidType.HeavyMazeGel,
                Week9StructureType.MazeMultichamber,
                mazeGelMat,
                new Vector3(3.0f, 0.15f, -1.8f),
                0.55f
            )
        };

        // 3. Create or clean Week 9 Showcase Root (Non-destructive to Week 7/8!)
        GameObject week9Root = GameObject.Find("Week9_RealisticDynamics_Showcase");
        if (week9Root == null)
        {
            week9Root = new GameObject("Week9_RealisticDynamics_Showcase");
        }
        else
        {
            while (week9Root.transform.childCount > 0)
            {
                Object.DestroyImmediate(week9Root.transform.GetChild(0).gameObject);
            }
        }

        // 4. Add Dedicated Front Showcase Shelf for Week 9 (Z = -1.8m, Y = -0.09m)
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Showcase_Shelf_Week9";
        shelf.transform.SetParent(week9Root.transform);
        shelf.transform.position = new Vector3(0f, -0.09f, -1.8f);
        shelf.transform.localScale = new Vector3(7.4f, 0.04f, 0.70f);

        Renderer shelfRend = shelf.GetComponent<Renderer>();
        if (shelfRend != null && glassMat != null)
        {
            shelfRend.sharedMaterial = glassMat;
        }

        // 5. Assemble, Configure, and Prefabricate all 7 Structures
        for (int i = 0; i < configs.Length; i++)
        {
            var cfg = configs[i];

            GameObject glassModel = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.glassModelPath);
            GameObject fluidModel = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.fluidModelPath);

            if (glassModel == null || fluidModel == null)
            {
                Debug.LogError($"Could not load OBJ models for {cfg.name}: glass={glassModel}, fluid={fluidModel}");
                continue;
            }

            // Display plinth under each vessel
            GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = $"Plinth_{cfg.name}";
            plinth.transform.SetParent(week9Root.transform);
            plinth.transform.position = new Vector3(cfg.pos.x, -0.075f, cfg.pos.z);
            plinth.transform.localScale = new Vector3(0.28f, 0.015f, 0.28f);

            // Container Root
            GameObject containerRoot = new GameObject(cfg.name);
            containerRoot.transform.SetParent(week9Root.transform);
            containerRoot.transform.position = cfg.pos;
            containerRoot.transform.rotation = Quaternion.identity;

            // Outer Glass Enclosure
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

            // Inner Fluid Volume
            GameObject fluidObj = Object.Instantiate(fluidModel, containerRoot.transform);
            fluidObj.name = "Fluid";
            fluidObj.transform.localPosition = Vector3.zero;
            fluidObj.transform.localRotation = Quaternion.identity;
            fluidObj.transform.localScale = Vector3.one;

            Renderer fluidRend = fluidObj.GetComponentInChildren<Renderer>();
            if (fluidRend != null && cfg.fluidMat != null)
            {
                fluidRend.sharedMaterial = cfg.fluidMat;
            }

            // Box Collider for Game-mode Raycast & Gizmo Drag Authority
            BoxCollider box = containerRoot.AddComponent<BoxCollider>();
            if (glassRend != null)
            {
                Bounds b = glassRend.bounds;
                Vector3 s = containerRoot.transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                box.size = Vector3.Max(box.size, new Vector3(0.35f, 0.50f, 0.35f));
            }
            else
            {
                box.size = new Vector3(0.40f, 0.55f, 0.40f);
            }

            // Attach Week9FluidDynamicsController
            Week9FluidDynamicsController controller = containerRoot.AddComponent<Week9FluidDynamicsController>();

            // Configure via SerializedObject
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("fluidRenderer").objectReferenceValue = fluidRend;
            so.FindProperty("fluidType").enumValueIndex = (int)cfg.fluidType;
            so.FindProperty("structureType").enumValueIndex = (int)cfg.structType;
            so.FindProperty("fillAmount").floatValue = cfg.initialFill;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Apply physical properties
            controller.ApplyFluidPreset(cfg.fluidType);
            controller.ApplyStructurePreset(cfg.structType);
            controller.fillAmount = cfg.initialFill;
            controller.UpdateFillCutPlane(true);

            // Save Prefab
            string prefabPath = $"{prefabsDir}/{cfg.name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(containerRoot, prefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"Created Week 9 Prefab ({i + 1}/{configs.Length}): {prefabPath}");
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

        Debug.Log("SUCCESS: Week 9 Realistic Fluid Dynamics & Dynamic Fill Showcase successfully assembled in scene!");
    }
}
#endif
