#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupUniversalFluidShowcase
{
    [MenuItem("Tools/Spawn Universal Adaptive Fluid Test Vessels")]
    public static void SpawnTestVessels()
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        if (EditorSceneManager.GetActiveScene().path != scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        GameObject root = GameObject.Find("Universal_Adaptive_Fluid_TestRow");
        if (root == null)
        {
            root = new GameObject("Universal_Adaptive_Fluid_TestRow");
        }
        else
        {
            while (root.transform.childCount > 0)
            {
                Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            }
        }

        // Test Plinth row (Z = -1.6m, in front of Week 10 vessels)
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "TestRow_Shelf";
        shelf.transform.SetParent(root.transform);
        shelf.transform.position = new Vector3(0f, -0.09f, -1.6f);
        shelf.transform.localScale = new Vector3(5.5f, 0.04f, 0.70f);

        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
        if (glassMat != null) shelf.GetComponent<Renderer>().sharedMaterial = glassMat;

        var testConfigs = new (
            string name,
            string glassPath,
            string fluidPath,
            UniversalAdaptiveFluidController.FluidType fluidType,
            Vector3 pos,
            float initialFill
        )[]
        {
            (
                "Universal_ConicalFlask",
                "Assets/Models/Flask_Conical_Glass.obj",
                "Assets/Models/Flask_Conical_Fluid.obj",
                UniversalAdaptiveFluidController.FluidType.Petrol_Gasoline,
                new Vector3(-1.8f, 0.15f, -1.6f),
                0.75f
            ),
            (
                "Universal_CardioidHeart",
                "Assets/Models/Vessel_CardioidHeart_Glass.obj",
                "Assets/Models/Vessel_CardioidHeart_Fluid.obj",
                UniversalAdaptiveFluidController.FluidType.HeavyMazeGel,
                new Vector3(-0.6f, 0.15f, -1.6f),
                0.60f
            ),
            (
                "Universal_TwistedColumn",
                "Assets/Models/Vessel_TwistedColumn_Glass.obj",
                "Assets/Models/Vessel_TwistedColumn_Fluid.obj",
                UniversalAdaptiveFluidController.FluidType.SyntheticMotorOil,
                new Vector3(0.6f, 0.15f, -1.6f),
                0.65f
            ),
            (
                "Universal_SphericalFlask",
                "Assets/Models/Flask_Spherical_Glass.obj",
                "Assets/Models/Flask_Spherical_Fluid.obj",
                UniversalAdaptiveFluidController.FluidType.HydraulicCoolant,
                new Vector3(1.8f, 0.15f, -1.6f),
                0.80f
            )
        };

        for (int i = 0; i < testConfigs.Length; i++)
        {
            var cfg = testConfigs[i];

            // Plinth
            GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = $"Plinth_{cfg.name}";
            plinth.transform.SetParent(root.transform);
            plinth.transform.position = new Vector3(cfg.pos.x, -0.075f, cfg.pos.z);
            plinth.transform.localScale = new Vector3(0.28f, 0.015f, 0.28f);

            // Container GameObject
            GameObject vesselObj = new GameObject(cfg.name);
            vesselObj.transform.SetParent(root.transform);
            vesselObj.transform.position = cfg.pos;
            vesselObj.transform.rotation = Quaternion.identity;

            Mesh glassMesh = AssetDatabase.LoadAssetAtPath<Mesh>(cfg.glassPath);
            MeshFilter gmf = vesselObj.AddComponent<MeshFilter>();
            gmf.sharedMesh = glassMesh;

            MeshRenderer gmr = vesselObj.AddComponent<MeshRenderer>();
            gmr.sharedMaterial = glassMat;

            // Child Fluid GameObject
            GameObject fluidObj = new GameObject("Fluid");
            fluidObj.transform.SetParent(vesselObj.transform, false);
            fluidObj.transform.localPosition = Vector3.zero;
            fluidObj.transform.localRotation = Quaternion.identity;
            fluidObj.transform.localScale = Vector3.one;

            Mesh fluidMesh = AssetDatabase.LoadAssetAtPath<Mesh>(cfg.fluidPath);
            MeshFilter fmf = fluidObj.AddComponent<MeshFilter>();
            fmf.sharedMesh = fluidMesh;

            MeshRenderer fmr = fluidObj.AddComponent<MeshRenderer>();

            // Add Collider for selection
            BoxCollider col = vesselObj.AddComponent<BoxCollider>();
            if (glassMesh != null)
            {
                col.center = glassMesh.bounds.center;
                col.size = glassMesh.bounds.size;
            }

            // Attach UniversalAdaptiveFluidController!
            UniversalAdaptiveFluidController ctrl = vesselObj.AddComponent<UniversalAdaptiveFluidController>();
            ctrl.fluidType = cfg.fluidType;
            ctrl.fillAmount = cfg.initialFill;
            ctrl.ResolveAndSetupMeshes();
            ctrl.ComputeHypsometricVolumeProfile();
            ctrl.ApplyFluidPreset(cfg.fluidType);
            ctrl.UpdateFillCutPlane(true);

            Debug.Log($"Spawned Universal Adaptive Fluid Test Vessel: {cfg.name}");
        }

        // Refresh Camera Controller
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            ShowcaseCameraController camCtrl = mainCam.GetComponent<ShowcaseCameraController>();
            if (camCtrl != null) camCtrl.RefreshAllVessels();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("SUCCESS: Universal Adaptive Fluid Test Row successfully created in scene!");
    }
}
#endif
