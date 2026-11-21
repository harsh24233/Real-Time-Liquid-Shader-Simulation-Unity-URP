using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// Universal Adaptive Fluid Controller:
/// Can be attached to ANY 3D shape or GameObject (bottles, flasks, tanks, torus, pipes, custom meshes).
/// Automatically adapts to the arbitrary shape's morphology, computes exact volumetric fill (0% to 100%),
/// renders 100% realistic solid liquid from ALL angles (front, side, and top-down view via dynamic meniscus caps),
/// and simulates physically accurate wobble, viscous slosh, and Torricelli breach outflow.
/// </summary>
[ExecuteAlways]
[SelectionBase]
[DisallowMultipleComponent]
public class UniversalAdaptiveFluidController : MonoBehaviour
{
    public enum FluidType
    {
        Petrol_Gasoline,
        LiquidMercury,
        SyntheticMotorOil,
        HydraulicCoolant,
        CryogenicLiquidGas,
        TreeSapResin,
        HeavyMazeGel
    }

    [System.Serializable]
    public struct FluidPreset
    {
        public string name;
        public float densityKgM3;
        public float dynamicViscosityPaS;
        public float dischargeCoeffCd;
        public float naturalBaseFreq;
        public float baseDamping;
        public Color fluidColor;
        public float metallic;
        public float smoothness;

        public FluidPreset(string name, float rho, float mu, float cd, float freq, float damp, Color col, float met, float smooth)
        {
            this.name = name;
            densityKgM3 = rho;
            dynamicViscosityPaS = mu;
            dischargeCoeffCd = cd;
            naturalBaseFreq = freq;
            baseDamping = damp;
            fluidColor = col;
            metallic = met;
            smoothness = smooth;
        }
    }

    [Header("1. Fluid Physics & Rheology Preset")]
    public FluidType fluidType = FluidType.Petrol_Gasoline;

    [Header("2. Volumetric Fill (0% to 100%)")]
    [Range(0f, 1f)]
    [Tooltip("Normalized volumetric fill fraction: 0.0 = completely evacuated, 1.0 = 100% completely filled and sealed.")]
    public float fillAmount = 1.0f;

    [Tooltip("Enable automated filling and draining cycle for testing.")]
    public bool enableAutoFillDrainCycle = false;
    [Range(0.01f, 0.5f)]
    public float autoFillCycleSpeed = 0.08f;

    [Header("3. Geometry & Mesh References")]
    [Tooltip("The outer container renderer (glass / transparent shell). If null, uses this GameObject's Renderer.")]
    [SerializeField] private Renderer containerRenderer;

    [Tooltip("The inner fluid volume renderer. If null, automatically generated or resolved.")]
    [SerializeField] private Renderer fluidRenderer;

    [Header("4. Dynamic Fracture & Outflow Settings")]
    public bool isCracked = false;
    [Range(1f, 40f)]
    public float breachDiameterMm = 12.0f;
    [Range(0.2f, 5.0f)]
    public float leakDrainMultiplier = 1.0f;

    // Public Telemetry & Aliases for HUD & Action Bar
    public void TriggerCrack() => TriggerPuncture();
    public float CurrentFillMagnitudeEnvelope => (fillAmount >= 0.999f || fillAmount <= 0.001f) ? 0f : 4f * fillAmount * (1f - fillAmount);
    public bool IsOutflowing => isCracked && hydrostaticHeadMeters > 0.0001f;
    public float CurrentHydrostaticHead => hydrostaticHeadMeters;
    public float CurrentExitVelocity => outwardJetExitVelocity;
    public float CurrentDischargeRateMlPerSec => outwardFlowRateMilliLitersPerSec;

    // Fluid Profiles
    public static readonly FluidPreset PresetPetrol =
        new FluidPreset("Petrol (Gasoline)", 740f, 0.0006f, 0.65f, 1.85f, 1.8f, new Color(0.98f, 0.75f, 0.12f, 0.95f), 0.05f, 0.96f);

    public static readonly FluidPreset PresetMercury =
        new FluidPreset("Liquid Mercury", 13550f, 0.0015f, 0.60f, 1.35f, 1.5f, new Color(0.88f, 0.90f, 0.93f, 1.0f), 0.98f, 0.98f);

    public static readonly FluidPreset PresetMotorOil =
        new FluidPreset("Synthetic Motor Oil", 890f, 0.2500f, 0.58f, 1.25f, 4.8f, new Color(0.25f, 0.12f, 0.02f, 0.98f), 0.05f, 0.95f);

    public static readonly FluidPreset PresetCoolant =
        new FluidPreset("Hydraulic Coolant", 1060f, 0.0035f, 0.62f, 1.75f, 2.2f, new Color(0.05f, 0.92f, 0.45f, 0.92f), 0.05f, 0.96f);

    public static readonly FluidPreset PresetCryoGas =
        new FluidPreset("Cryogenic Liquid Gas", 500f, 0.0001f, 0.70f, 2.10f, 1.2f, new Color(0.30f, 0.82f, 1.0f, 0.90f), 0.05f, 0.98f);

    public static readonly FluidPreset PresetTreeSap =
        new FluidPreset("Tree Sap Resin", 1380f, 8.5000f, 0.45f, 0.55f, 14.0f, new Color(0.88f, 0.42f, 0.05f, 0.96f), 0.05f, 0.94f);

    public static readonly FluidPreset PresetHeavyGel =
        new FluidPreset("Heavy Maze Gel", 1420f, 25.0000f, 0.40f, 0.40f, 22.0f, new Color(0.85f, 0.12f, 0.35f, 0.95f), 0.05f, 0.95f);

    // Dynamic Top Meniscus Cap (guarantees solid liquid appearance from top-down view!)
    private GameObject surfaceMeniscusCapObj = null;
    private MeshFilter surfaceCapMF = null;
    private MeshRenderer surfaceCapMR = null;
    private Material surfaceCapMaterial = null;

    // Outflow & Breach State
    private Vector3 breachLocalPos = Vector3.zero;
    private Vector3 breachLocalNormal = Vector3.forward;
    private Vector3 orificeLipLocalPos = Vector3.zero;
    private Mesh originalContainerMesh = null;
    private LineRenderer outwardJetLine = null;
    private ParticleSystem splashParticles = null;
    private GameObject fluidPuddleObj = null;
    private List<GameObject> spawnedShards = new List<GameObject>();
    private List<GameObject> spawnedFractureObjects = new List<GameObject>();
    private static Texture2D dropletTexture = null;

    // Kinematics & Physics State
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 currentVelocity;
    private Vector3 previousVelocity;
    private Vector3 currentAngularVelocity;
    private Vector3 previousAngularVelocity;
    private float wobbleX = 0f;
    private float wobbleZ = 0f;
    private float wobbleVelocityX = 0f;
    private float wobbleVelocityZ = 0f;
    private Vector3 gelSurfaceNormal = Vector3.up;
    private float gelHesitationTimer = 0f;
    private float gelClingX = 0f;
    private float gelClingZ = 0f;

    // Shader Property IDs
    private MaterialPropertyBlock propBlock;
    private static readonly int WobbleXProp = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZProp = Shader.PropertyToID("_WobbleZ");
    private static readonly int FloatFillProp = Shader.PropertyToID("_Float");

    // Automated Hypsometric Volume Profiling
    private float localYMin = -0.2f;
    private float localYMax = 0.2f;
    private float[] hypsometricVolumeTable = null; // Normalized cumulative volume at N slices
    private const int HypsometricSlices = 64;

    private float initialFillAmount = 1.0f;
    private float autoCycleDir = 1f;
    private float hydrostaticHeadMeters = 0f;
    private float outwardJetExitVelocity = 0f;
    private float outwardFlowRateMilliLitersPerSec = 0f;

    private void Awake()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        propBlock = new MaterialPropertyBlock();
        initialFillAmount = fillAmount;

        ResolveAndSetupMeshes();
        ComputeHypsometricVolumeProfile();
        ApplyFluidPreset(fluidType);
        UpdateFillCutPlane(true);
    }

    private void Start()
    {
        initialFillAmount = fillAmount;
        if (Application.isPlaying)
        {
            isCracked = false;
            CleanupAllFractureObjects();
        }
        UpdateFillCutPlane(true);
    }

    private void OnEnable()
    {
        ResolveAndSetupMeshes();
        ComputeHypsometricVolumeProfile();
        UpdateFillCutPlane(true);
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
        if (surfaceCapMaterial != null)
        {
            if (Application.isPlaying) Destroy(surfaceCapMaterial);
            else DestroyImmediate(surfaceCapMaterial);
            surfaceCapMaterial = null;
        }
    }

    private void OnValidate()
    {
        ComputeHypsometricVolumeProfile();
        ApplyFluidPreset(fluidType);
        UpdateFillCutPlane(true);
    }

    /// <summary>
    /// Automatically resolves or generates container and fluid mesh structures.
    /// Can be added to any raw 3D mesh: generates an inset fluid cavity automatically!
    /// </summary>
    public void ResolveAndSetupMeshes()
    {
        if (containerRenderer == null)
        {
            containerRenderer = GetComponent<Renderer>();
        }

        if (containerRenderer != null && originalContainerMesh == null)
        {
            MeshFilter cmf = containerRenderer.GetComponent<MeshFilter>();
            if (cmf != null && cmf.sharedMesh != null)
            {
                originalContainerMesh = cmf.sharedMesh;
            }
        }

        // Look for existing child fluid renderer
        if (fluidRenderer == null)
        {
            Transform fluidChild = transform.Find("Fluid") ?? transform.Find("Liquid") ?? transform.Find("FluidMesh");
            if (fluidChild != null)
            {
                fluidRenderer = fluidChild.GetComponent<Renderer>();
            }
        }

        // If no fluid renderer exists, automatically generate an inset fluid mesh from container
        if (fluidRenderer == null && containerRenderer != null)
        {
            MeshFilter cmf = containerRenderer.GetComponent<MeshFilter>();
            if (cmf != null && cmf.sharedMesh != null)
            {
                GameObject fluidObj = new GameObject("FluidMesh");
                fluidObj.transform.SetParent(transform, false);
                fluidObj.transform.localPosition = Vector3.zero;
                fluidObj.transform.localRotation = Quaternion.identity;
                fluidObj.transform.localScale = Vector3.one * 0.985f; // Inset snugly inside container walls

                MeshFilter fmf = fluidObj.AddComponent<MeshFilter>();
                fmf.sharedMesh = cmf.sharedMesh;

                fluidRenderer = fluidObj.AddComponent<MeshRenderer>();

                // Assign default fluid material
#if UNITY_EDITOR
                Material defaultMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Week10/Week10_Petrol_Fluid.mat");
                if (defaultMat != null) fluidRenderer.sharedMaterial = defaultMat;
#endif
            }
        }

        // Compute local Y bounds
        if (fluidRenderer != null)
        {
            MeshFilter fmf = fluidRenderer.GetComponent<MeshFilter>();
            if (fmf != null && fmf.sharedMesh != null)
            {
                Bounds b = fmf.sharedMesh.bounds;
                localYMin = b.min.y;
                localYMax = b.max.y;
            }
        }
        else if (containerRenderer != null)
        {
            MeshFilter cmf = containerRenderer.GetComponent<MeshFilter>();
            if (cmf != null && cmf.sharedMesh != null)
            {
                Bounds b = cmf.sharedMesh.bounds;
                localYMin = b.min.y;
                localYMax = b.max.y;
            }
        }

        // Setup default breach point at lower side of container
        breachLocalPos = new Vector3(0.12f, Mathf.Lerp(localYMin, localYMax, 0.25f), 0f);
        breachLocalNormal = Vector3.right;
    }

    /// <summary>
    /// Universal Automated Hypsometric Volume Integration:
    /// Numerically integrates cross-sectional area A(y) across 64 vertical slices of the arbitrary mesh,
    /// generating a continuous cumulative volume curve V(y).
    /// Works automatically for ANY complex, irregular, or branching geometry!
    /// </summary>
    public void ComputeHypsometricVolumeProfile()
    {
        Mesh targetMesh = null;
        if (fluidRenderer != null)
        {
            MeshFilter mf = fluidRenderer.GetComponent<MeshFilter>();
            if (mf != null) targetMesh = mf.sharedMesh;
        }
        if (targetMesh == null && containerRenderer != null)
        {
            MeshFilter mf = containerRenderer.GetComponent<MeshFilter>();
            if (mf != null) targetMesh = mf.sharedMesh;
        }
        if (targetMesh == null) return;

        Bounds b = targetMesh.bounds;
        localYMin = b.min.y;
        localYMax = b.max.y;
        float h = localYMax - localYMin;
        if (h <= 0.001f) return;

        hypsometricVolumeTable = new float[HypsometricSlices];
        float[] areas = new float[HypsometricSlices];

        // Sample horizontal cross-sectional areas
        for (int i = 0; i < HypsometricSlices; i++)
        {
            float t = (float)i / (HypsometricSlices - 1);
            float y = Mathf.Lerp(localYMin, localYMax, t);
            Mesh cap = MeshPlaneSlicer.GenerateCapMesh(targetMesh, new Vector3(0f, y, 0f), Vector3.up);
            if (cap != null && cap.triangles != null)
            {
                Vector3[] v = cap.vertices;
                int[] tri = cap.triangles;
                float area = 0f;
                for (int k = 0; k < tri.Length; k += 3)
                {
                    Vector3 cross = Vector3.Cross(v[tri[k + 1]] - v[tri[k]], v[tri[k + 2]] - v[tri[k]]);
                    area += cross.magnitude * 0.5f;
                }
                areas[i] = area;
            }
            else
            {
                areas[i] = i == 0 ? 0.001f : areas[i - 1];
            }
        }

        // Integrate trapezoidal volume
        float totalVol = 0f;
        float dy = h / (HypsometricSlices - 1);
        hypsometricVolumeTable[0] = 0f;
        for (int i = 1; i < HypsometricSlices; i++)
        {
            float sliceVol = (areas[i - 1] + areas[i]) * 0.5f * dy;
            totalVol += sliceVol;
            hypsometricVolumeTable[i] = totalVol;
        }

        // Normalize to [0..1]
        if (totalVol > 0.0001f)
        {
            for (int i = 0; i < HypsometricSlices; i++)
            {
                hypsometricVolumeTable[i] /= totalVol;
            }
        }
        else
        {
            // Linear fallback
            for (int i = 0; i < HypsometricSlices; i++)
            {
                hypsometricVolumeTable[i] = (float)i / (HypsometricSlices - 1);
            }
        }
    }

    /// <summary>
    /// Maps a desired volume fill fraction (0..1) to the exact physical height y on the arbitrary vessel.
    /// </summary>
    public float VolumeFractionToLocalY(float fill)
    {
        if (fill <= 0.0001f) return localYMin - 0.05f;
        if (fill >= 0.9999f) return localYMax + 0.05f;

        if (hypsometricVolumeTable == null || hypsometricVolumeTable.Length < 2)
        {
            return Mathf.Lerp(localYMin, localYMax, fill);
        }

        // Binary search on hypsometric volume table
        int low = 0;
        int high = hypsometricVolumeTable.Length - 1;
        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (hypsometricVolumeTable[mid] <= fill) low = mid;
            else high = mid;
        }

        float f0 = hypsometricVolumeTable[low];
        float f1 = hypsometricVolumeTable[high];
        float t = f1 > f0 ? (fill - f0) / (f1 - f0) : 0f;

        float y0 = Mathf.Lerp(localYMin, localYMax, (float)low / (HypsometricSlices - 1));
        float y1 = Mathf.Lerp(localYMin, localYMax, (float)high / (HypsometricSlices - 1));
        return Mathf.Lerp(y0, y1, t);
    }

    public float ComputeAdaptedFillFraction(float fill)
    {
        if (fill <= 0.0001f) return 0f;
        if (fill >= 0.9999f) return 1f;

        float range = Mathf.Max(0.001f, localYMax - localYMin);
        float adaptedY = VolumeFractionToLocalY(fill);
        return Mathf.Clamp01((adaptedY - localYMin) / range);
    }

    public void GetWorldVerticalBounds(out float worldYMin, out float worldYMax)
    {
        Renderer r = fluidRenderer != null ? fluidRenderer : (containerRenderer != null ? containerRenderer : GetComponentInChildren<Renderer>());
        if (r != null)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;
                Transform t = r.transform;

                float minY = float.MaxValue;
                float maxY = float.MinValue;
                for (int xi = -1; xi <= 1; xi += 2)
                {
                    for (int yi = -1; yi <= 1; yi += 2)
                    {
                        for (int zi = -1; zi <= 1; zi += 2)
                        {
                            Vector3 cornerLocal = c + new Vector3(e.x * xi, e.y * yi, e.z * zi);
                            float wy = t.TransformPoint(cornerLocal).y;
                            if (wy < minY) minY = wy;
                            if (wy > maxY) maxY = wy;
                        }
                    }
                }
                worldYMin = minY;
                worldYMax = maxY;
                return;
            }
            Bounds rb = r.bounds;
            worldYMin = rb.min.y;
            worldYMax = rb.max.y;
            return;
        }
        worldYMin = transform.position.y - 0.2f;
        worldYMax = transform.position.y + 0.2f;
    }

    public void ApplyFluidPreset(FluidType type)
    {
        fluidType = type;
        FluidPreset prof = GetActivePreset();

        if (surfaceCapMaterial != null)
        {
            surfaceCapMaterial.color = prof.fluidColor;
            surfaceCapMaterial.SetFloat("_Smoothness", prof.smoothness);
            surfaceCapMaterial.SetFloat("_Metallic", prof.metallic);
        }

#if UNITY_EDITOR
        string matPath = type switch
        {
            FluidType.Petrol_Gasoline => "Assets/Materials/Week10/Week10_Petrol_Fluid.mat",
            FluidType.LiquidMercury => "Assets/Materials/Week10/Week10_Mercury_Fluid.mat",
            FluidType.SyntheticMotorOil => "Assets/Materials/Week10/Week10_MotorOil_Fluid.mat",
            FluidType.HydraulicCoolant => "Assets/Materials/Week10/Week10_Coolant_Fluid.mat",
            FluidType.CryogenicLiquidGas => "Assets/Materials/Week10/Week10_CryoGas_Fluid.mat",
            FluidType.TreeSapResin => "Assets/Materials/Week10/Week10_TreeSap_Fluid.mat",
            FluidType.HeavyMazeGel => "Assets/Materials/Week10/Week10_MazeGel_Fluid.mat",
            _ => null
        };
        if (matPath != null && fluidRenderer != null)
        {
            var newMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (newMat != null)
            {
                if (Application.isPlaying) fluidRenderer.material = newMat;
                else fluidRenderer.sharedMaterial = newMat;
            }
        }
#endif
    }

    public FluidPreset GetActivePreset()
    {
        return fluidType switch
        {
            FluidType.Petrol_Gasoline => PresetPetrol,
            FluidType.LiquidMercury => PresetMercury,
            FluidType.SyntheticMotorOil => PresetMotorOil,
            FluidType.HydraulicCoolant => PresetCoolant,
            FluidType.CryogenicLiquidGas => PresetCryoGas,
            FluidType.TreeSapResin => PresetTreeSap,
            FluidType.HeavyMazeGel => PresetHeavyGel,
            _ => PresetPetrol
        };
    }

    /// <summary>
    /// Updates the shader cut plane and generates the dynamic top surface meniscus cap.
    /// Dynamically adapts to 3D rotation so fluid surface ALWAYS stays horizontal in world space.
    /// </summary>
    public void UpdateFillCutPlane(bool forceUpdate = false)
    {
        if (fluidRenderer == null) ResolveAndSetupMeshes();
        if (fluidRenderer == null) return;

        GetWorldVerticalBounds(out float worldYMin, out float worldYMax);

        float rendererPosY = fluidRenderer.transform.position.y;
        float ySurfaceWorld;
        float floatCutY;

        if (fillAmount >= 0.999f)
        {
            ySurfaceWorld = worldYMax + 0.05f;
            floatCutY = ySurfaceWorld - rendererPosY;
            SendShaderProperties(0f, 0f, floatCutY);
            if (surfaceMeniscusCapObj != null) surfaceMeniscusCapObj.SetActive(false);
            return;
        }
        else if (fillAmount <= 0.001f)
        {
            ySurfaceWorld = worldYMin - 0.05f;
            floatCutY = ySurfaceWorld - rendererPosY;
            SendShaderProperties(0f, 0f, floatCutY);
            if (surfaceMeniscusCapObj != null) surfaceMeniscusCapObj.SetActive(false);
            return;
        }
        else
        {
            float adaptedFill = ComputeAdaptedFillFraction(fillAmount);
            ySurfaceWorld = Mathf.Lerp(worldYMin, worldYMax, adaptedFill);
            floatCutY = ySurfaceWorld - rendererPosY;
        }

        float totalWobbleX = wobbleX;
        float totalWobbleZ = wobbleZ;
        if (fluidType == FluidType.HeavyMazeGel)
        {
            totalWobbleX += gelClingX;
            totalWobbleZ += gelClingZ;
        }
        SendShaderProperties(totalWobbleX, totalWobbleZ, floatCutY);

        // Generate or update Dynamic Surface Meniscus Cap
        UpdateSurfaceMeniscusCap(ySurfaceWorld, totalWobbleX, totalWobbleZ);
    }

    private void SendShaderProperties(float wx, float wz, float fillCutY)
    {
        if (fluidRenderer == null) return;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        fluidRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(WobbleXProp, wx);
        propBlock.SetFloat(WobbleZProp, wz);
        propBlock.SetFloat(FloatFillProp, fillCutY);
        fluidRenderer.SetPropertyBlock(propBlock);

        if (Application.isPlaying)
        {
            if (fluidRenderer.material != null)
            {
                if (fluidRenderer.material.HasProperty(WobbleXProp))
                    fluidRenderer.material.SetFloat(WobbleXProp, wx);
                if (fluidRenderer.material.HasProperty(WobbleZProp))
                    fluidRenderer.material.SetFloat(WobbleZProp, wz);
                if (fluidRenderer.material.HasProperty(FloatFillProp))
                    fluidRenderer.material.SetFloat(FloatFillProp, fillCutY);
            }
        }
        else if (Application.isEditor)
        {
            if (fluidRenderer.sharedMaterial != null)
            {
                if (fluidRenderer.sharedMaterial.HasProperty(WobbleXProp))
                    fluidRenderer.sharedMaterial.SetFloat(WobbleXProp, wx);
                if (fluidRenderer.sharedMaterial.HasProperty(WobbleZProp))
                    fluidRenderer.sharedMaterial.SetFloat(WobbleZProp, wz);
                if (fluidRenderer.sharedMaterial.HasProperty(FloatFillProp))
                    fluidRenderer.sharedMaterial.SetFloat(FloatFillProp, fillCutY);
            }
        }
    }

    /// <summary>
    /// Dynamically slices the fluid mesh at the liquid level and constructs a solid surface cap mesh.
    /// This eliminates hollow voids and makes the liquid appear 100% solid and real from ANY view, including top view!
    /// </summary>
    private void UpdateSurfaceMeniscusCap(float ySurfaceWorld, float wx, float wz)
    {
        if (fluidRenderer == null) return;
        MeshFilter fmf = fluidRenderer.GetComponent<MeshFilter>();
        if (fmf == null || fmf.sharedMesh == null) return;

        if (surfaceMeniscusCapObj == null)
        {
            Transform existing = fluidRenderer.transform.Find("SurfaceMeniscusCap");
            if (existing != null)
            {
                surfaceMeniscusCapObj = existing.gameObject;
            }
            else
            {
                surfaceMeniscusCapObj = new GameObject("SurfaceMeniscusCap");
                surfaceMeniscusCapObj.transform.SetParent(fluidRenderer.transform, false);
                surfaceMeniscusCapObj.transform.localPosition = Vector3.zero;
                surfaceMeniscusCapObj.transform.localRotation = Quaternion.identity;
                surfaceMeniscusCapObj.transform.localScale = Vector3.one;
            }
        }

        if (surfaceCapMF == null && surfaceMeniscusCapObj != null)
        {
            surfaceCapMF = surfaceMeniscusCapObj.GetComponent<MeshFilter>();
            if (surfaceCapMF == null) surfaceCapMF = surfaceMeniscusCapObj.AddComponent<MeshFilter>();
        }

        if (surfaceCapMR == null && surfaceMeniscusCapObj != null)
        {
            surfaceCapMR = surfaceMeniscusCapObj.GetComponent<MeshRenderer>();
            if (surfaceCapMR == null) surfaceCapMR = surfaceMeniscusCapObj.AddComponent<MeshRenderer>();
        }

        if (surfaceCapMR != null)
        {
            surfaceCapMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surfaceCapMR.receiveShadows = false;
            surfaceCapMR.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            surfaceCapMR.allowOcclusionWhenDynamic = false;
        }

        FluidPreset prof = GetActivePreset();
        if (surfaceCapMR != null)
        {
            if (surfaceCapMaterial == null || surfaceCapMR.sharedMaterial == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");
                surfaceCapMaterial = new Material(litShader);
                surfaceCapMaterial.name = "DynamicSurfaceMeniscusCap_Material";
                surfaceCapMR.sharedMaterial = surfaceCapMaterial;
            }

            // Configure Meniscus Material:
            // For Mercury: Mirror liquid metal. For other fluids: Translucent, glossy, refractive liquid meniscus!
            bool isMetal = prof.densityKgM3 > 5000f;
            if (isMetal)
            {
                surfaceCapMaterial.color = new Color(0.92f, 0.94f, 0.96f, 1.0f);
                surfaceCapMaterial.SetFloat("_Metallic", 0.98f);
                surfaceCapMaterial.SetFloat("_Smoothness", 0.98f);
                surfaceCapMaterial.SetFloat("_Surface", 0f); // Opaque liquid metal
                surfaceCapMaterial.SetFloat("_Cull", 0f);
                surfaceCapMaterial.SetInt("_ZWrite", 1);
                surfaceCapMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 2;
                surfaceCapMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                Color capCol = prof.fluidColor;
                if (fluidRenderer != null)
                {
                    Material fMat = Application.isPlaying ? fluidRenderer.material : fluidRenderer.sharedMaterial;
                    if (fMat != null)
                    {
                        if (fMat.HasProperty("_Color"))
                        {
                            Color mc = fMat.GetColor("_Color");
                            if (mc.a > 0.05f && (mc.r > 0.01f || mc.g > 0.01f || mc.b > 0.01f))
                                capCol = mc;
                        }
                        else if (fMat.HasProperty("_BaseColor"))
                        {
                            Color bc = fMat.GetColor("_BaseColor");
                            if (bc.a > 0.05f && (bc.r > 0.01f || bc.g > 0.01f || bc.b > 0.01f))
                                capCol = bc;
                        }
                    }
                }
                capCol.a = 0.88f;

                surfaceCapMaterial.color = capCol;
                surfaceCapMaterial.SetFloat("_Metallic", 0.05f);
                surfaceCapMaterial.SetFloat("_Smoothness", 0.96f);
                surfaceCapMaterial.SetFloat("_Surface", 1f); // Transparent meniscus
                surfaceCapMaterial.SetFloat("_Blend", 0f);   // Alpha blend
                surfaceCapMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                surfaceCapMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                surfaceCapMaterial.SetFloat("_Cull", 0f);
                surfaceCapMaterial.SetInt("_ZWrite", 0);
                surfaceCapMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5;
                surfaceCapMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
        }

        Vector3 worldPlaneNormal = (Vector3.up + new Vector3(-wx, 0f, wz)).normalized;
        Vector3 worldPlanePoint = new Vector3(fluidRenderer.transform.position.x, ySurfaceWorld, fluidRenderer.transform.position.z);

        Vector3 localPlaneNormal = fluidRenderer.transform.InverseTransformDirection(worldPlaneNormal).normalized;
        Vector3 localPlanePoint = fluidRenderer.transform.InverseTransformPoint(worldPlanePoint);

        Mesh capMesh = MeshPlaneSlicer.GenerateCapMesh(fmf.sharedMesh, localPlanePoint, localPlaneNormal);
        if (capMesh != null && capMesh.vertexCount > 2)
        {
            surfaceCapMF.sharedMesh = capMesh;
            surfaceCapMR.enabled = true;
            if (surfaceMeniscusCapObj != null && !surfaceMeniscusCapObj.activeSelf)
                surfaceMeniscusCapObj.SetActive(true);
        }
        else
        {
            if (surfaceCapMR != null) surfaceCapMR.enabled = false;
            if (surfaceCapMF != null) surfaceCapMF.sharedMesh = null;
            if (surfaceMeniscusCapObj != null && surfaceMeniscusCapObj.activeSelf)
                surfaceMeniscusCapObj.SetActive(false);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0.0001f) return;

        if (enableAutoFillDrainCycle && Application.isPlaying)
        {
            fillAmount += autoCycleDir * autoFillCycleSpeed * dt;
            if (fillAmount >= 1.0f) { fillAmount = 1.0f; autoCycleDir = -1f; }
            else if (fillAmount <= 0.05f) { fillAmount = 0.05f; autoCycleDir = 1f; }
        }

        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;

        Vector3 rawVelocity = (currentPos - lastPosition) / dt;
        currentVelocity = Vector3.Lerp(currentVelocity, rawVelocity, Mathf.Clamp01(dt * 10f));
        Vector3 acceleration = (currentVelocity - previousVelocity) / dt;
        acceleration = Vector3.ClampMagnitude(acceleration, 25f);

        Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 rawAngVel = (axis * (angleDeg * Mathf.Deg2Rad)) / dt;
        currentAngularVelocity = Vector3.Lerp(currentAngularVelocity, rawAngVel, Mathf.Clamp01(dt * 10f));

        ComputeSloshOscillation(acceleration, currentAngularVelocity, dt);
        UpdateViscousCreepSlump(deltaRot, dt);

        if (isCracked)
        {
            SimulateHydrostaticOutflow(dt);
        }

        if (fluidPuddleObj != null && isCracked)
        {
            float drained = Mathf.Max(0f, initialFillAmount - fillAmount);
            float puddleDiameter = Mathf.Clamp(0.10f + drained * 0.55f, 0.10f, 0.52f);
            fluidPuddleObj.transform.localScale = new Vector3(puddleDiameter, 1f, puddleDiameter);

            // Enforce puddle sits firmly on table/shelf surface (never floating in mid-air)
            float maxAllowedPuddleY = transform.position.y - 0.18f;
            if (fluidPuddleObj.transform.position.y > maxAllowedPuddleY)
            {
                Vector3 pPos = fluidPuddleObj.transform.position;
                fluidPuddleObj.transform.position = new Vector3(pPos.x, transform.position.y - 0.22f + 0.003f, pPos.z);
            }
        }

        UpdateFillCutPlane(false);

        lastPosition = currentPos;
        lastRotation = currentRot;
        previousVelocity = currentVelocity;
        previousAngularVelocity = currentAngularVelocity;
    }

#if UNITY_EDITOR
    private double lastEditorTime = 0;
    private void OnEditorUpdate()
    {
        if (Application.isPlaying) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = lastEditorTime > 0 ? Mathf.Clamp((float)(now - lastEditorTime), 0.001f, 0.05f) : 0.016f;
        lastEditorTime = now;

        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;

        bool moved = (currentPos != lastPosition) || (currentRot != lastRotation);
        Vector3 rawVelocity = (currentPos - lastPosition) / dt;
        currentVelocity = Vector3.Lerp(currentVelocity, rawVelocity, Mathf.Clamp01(dt * 8f));
        Vector3 acceleration = (currentVelocity - previousVelocity) / dt;
        acceleration = Vector3.ClampMagnitude(acceleration, 25f);

        Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 rawAngVel = (axis * (angleDeg * Mathf.Deg2Rad)) / dt;
        currentAngularVelocity = Vector3.Lerp(currentAngularVelocity, rawAngVel, Mathf.Clamp01(dt * 8f));

        ComputeSloshOscillation(acceleration, currentAngularVelocity, dt);
        UpdateViscousCreepSlump(deltaRot, dt);
        UpdateFillCutPlane(false);

        lastPosition = currentPos;
        lastRotation = currentRot;
        previousVelocity = currentVelocity;
        previousAngularVelocity = currentAngularVelocity;

        if (moved || Mathf.Abs(wobbleX) > 0.0005f || Mathf.Abs(wobbleZ) > 0.0005f)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
#endif

    private void ComputeSloshOscillation(Vector3 acceleration, Vector3 angularVel, float dt)
    {
        if (fillAmount >= 0.999f || fillAmount <= 0.001f)
        {
            wobbleX = 0f;
            wobbleZ = 0f;
            wobbleVelocityX = 0f;
            wobbleVelocityZ = 0f;
            return;
        }

        float envelope = 4f * fillAmount * (1f - fillAmount);
        envelope = Mathf.Clamp01(envelope);

        FluidPreset prof = GetActivePreset();
        float naturalFreq = prof.naturalBaseFreq * Mathf.Sqrt(Mathf.Clamp01(Mathf.Abs(Mathf.Tan(fillAmount * Mathf.PI * 0.48f)) * 0.5f + 0.1f));
        float damping = prof.baseDamping + prof.dynamicViscosityPaS * 1.5f;

        float maxLimit = 0.45f * Mathf.Max(0.05f, envelope);

        float targetX = Mathf.Clamp(-acceleration.z * 0.04f + angularVel.x * 0.35f, -maxLimit, maxLimit) * envelope;
        float targetZ = Mathf.Clamp(acceleration.x * 0.04f - angularVel.z * 0.35f, -maxLimit, maxLimit) * envelope;

        float omegaN = 2f * Mathf.PI * Mathf.Max(naturalFreq, 0.1f);
        UpdateOscillator(targetX, omegaN, damping, dt, ref wobbleX, ref wobbleVelocityX, maxLimit);
        UpdateOscillator(targetZ, omegaN, damping, dt, ref wobbleZ, ref wobbleVelocityZ, maxLimit);
    }

    private void UpdateOscillator(float target, float omegaN, float gamma, float dt, ref float amount, ref float velocity, float maxLimit)
    {
        float e = amount - target;
        if (omegaN > gamma)
        {
            float omegaD = Mathf.Sqrt(omegaN * omegaN - gamma * gamma);
            float decay = Mathf.Exp(-gamma * dt);
            float cosVal = Mathf.Cos(omegaD * dt);
            float sinVal = Mathf.Sin(omegaD * dt);
            float newE = decay * (e * cosVal + ((velocity + gamma * e) / omegaD) * sinVal);
            float newV = decay * (velocity * cosVal - ((omegaN * omegaN * e + gamma * velocity) / omegaD) * sinVal);
            amount = Mathf.Clamp(target + newE, -maxLimit, maxLimit);
            velocity = newV;
        }
        else
        {
            float smoothTime = Mathf.Clamp(2.0f / Mathf.Max(gamma, 1f), 0.25f, 3.0f);
            amount = Mathf.SmoothDamp(amount, target, ref velocity, smoothTime, 2.0f, dt);
            amount = Mathf.Clamp(amount, -maxLimit, maxLimit);
        }
    }

    private void UpdateViscousCreepSlump(Quaternion deltaRot, float dt)
    {
        FluidPreset prof = GetActivePreset();
        if (prof.dynamicViscosityPaS < 0.20f || fillAmount >= 0.999f || fillAmount <= 0.001f)
        {
            gelSurfaceNormal = Vector3.up;
            gelHesitationTimer = 0f;
            gelClingX = 0f;
            gelClingZ = 0f;
            return;
        }

        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        if (Mathf.Abs(angleDeg) > 0.02f)
        {
            gelSurfaceNormal = deltaRot * gelSurfaceNormal;
            gelSurfaceNormal.Normalize();
            if (prof.dynamicViscosityPaS > 10f) gelHesitationTimer = 0.85f;
        }

        if (gelHesitationTimer > 0f)
        {
            gelHesitationTimer -= dt;
        }
        else
        {
            gelSurfaceNormal = Vector3.RotateTowards(gelSurfaceNormal, Vector3.up, (60f / Mathf.Max(1f, prof.dynamicViscosityPaS * 0.2f)) * Mathf.Deg2Rad * dt, 0f);
            gelSurfaceNormal.Normalize();
        }

        float safeNy = Mathf.Max(gelSurfaceNormal.y, 0.15f);
        gelClingZ = Mathf.Clamp(gelSurfaceNormal.x / safeNy, -1.25f, 1.25f);
        gelClingX = Mathf.Clamp(-gelSurfaceNormal.z / safeNy, -1.25f, 1.25f);
    }

    [ContextMenu("Trigger Slosh Impulse")]
    public void TriggerImpulseSlosh(float impulse = 0.4f)
    {
        if (fillAmount >= 0.999f || fillAmount <= 0.001f) return;
        float envelope = Mathf.Max(0.2f, 4f * fillAmount * (1f - fillAmount));
        wobbleVelocityX += impulse * 3.5f * envelope;
        wobbleVelocityZ += impulse * 2.8f * envelope;
    }

    [ContextMenu("Puncture & Crack Vessel")]
    public void TriggerPuncture()
    {
        CalibrateBreachPoint();
        Vector3 hitPoint = transform.TransformPoint(breachLocalPos);
        Vector3 hitNorm = transform.TransformDirection(breachLocalNormal).normalized;
        TriggerPuncture(hitPoint, hitNorm);
    }

    public void CalibrateBreachPoint()
    {
        if (containerRenderer == null) ResolveAndSetupMeshes();
        if (containerRenderer == null) return;
        MeshFilter mf = containerRenderer.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;

        Vector3[] verts = mf.sharedMesh.vertices;
        Vector3[] norms = mf.sharedMesh.normals;
        Bounds b = mf.sharedMesh.bounds;
        // Elevated target height: 38% up the container wall (safely above base flange / floor)
        float targetY = b.min.y + b.size.y * 0.38f;
        float yMinFilter = b.min.y + b.size.y * 0.25f;
        float yMaxFilter = b.min.y + b.size.y * 0.55f;

        float bestScore = float.MinValue;
        Vector3 bestP = breachLocalPos;
        Vector3 bestN = breachLocalNormal;
        bool foundCandidate = false;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            // Strictly reject vertices on the base flange, floor, or upper reaches
            if (v.y < yMinFilter || v.y > yMaxFilter) continue;

            float dy = Mathf.Abs(v.y - targetY);
            Vector3 n = (norms != null && norms.Length > i) ? norms[i] : Vector3.right;
            // Favor vertices on outer wall facing forward-right (+X, -Z towards camera)
            float score = (v.x * 2.2f) - (v.z * 1.6f) - (dy * 16.0f) + (n.x * 1.8f) - (n.z * 1.2f);
            if (score > bestScore)
            {
                bestScore = score;
                bestP = v;
                bestN = n;
                foundCandidate = true;
            }
        }

        if (foundCandidate)
        {
            breachLocalPos = bestP;
            breachLocalNormal = bestN.sqrMagnitude > 0.01f ? bestN.normalized : Vector3.right;
        }
        else
        {
            // For coarse meshes where no vertices lie within the height band, pin Y to targetY
            breachLocalPos.y = targetY;
        }
    }

    public void TriggerPuncture(Vector3 worldHitPos, Vector3 worldHitNorm)
    {
        if (isCracked) return;
        isCracked = true;

        breachLocalPos = transform.InverseTransformPoint(worldHitPos);
        breachLocalNormal = transform.InverseTransformDirection(worldHitNorm).normalized;

        PunctureContainerMesh(worldHitPos, worldHitNorm);
        SpawnGlassShards(worldHitPos, worldHitNorm);
        EnsureJetVisuals();
    }

    [ContextMenu("Repair & Reseal")]
    public void RepairCrack()
    {
        isCracked = false;
        CleanupAllFractureObjects();
        if (outwardJetLine != null) outwardJetLine.positionCount = 0;
        if (splashParticles != null) splashParticles.Stop();
        if (containerRenderer != null && originalContainerMesh != null)
        {
            MeshFilter cmf = containerRenderer.GetComponent<MeshFilter>();
            if (cmf != null) cmf.sharedMesh = originalContainerMesh;
            containerRenderer.enabled = true;
        }
    }

    [ContextMenu("Reset Vessel")]
    public void ResetVessel()
    {
        RepairCrack();
        fillAmount = initialFillAmount > 0.001f ? initialFillAmount : 1.0f;
        wobbleX = 0f;
        wobbleZ = 0f;
        wobbleVelocityX = 0f;
        wobbleVelocityZ = 0f;
        gelClingX = 0f;
        gelClingZ = 0f;
        gelHesitationTimer = 0f;
        UpdateFillCutPlane(true);
    }

    private void PunctureContainerMesh(Vector3 crackWorldPos, Vector3 crackWorldNorm)
    {
        if (containerRenderer == null) return;
        MeshFilter cmf = containerRenderer.GetComponent<MeshFilter>();
        if (cmf == null) return;
        if (originalContainerMesh == null) originalContainerMesh = cmf.sharedMesh;
        if (originalContainerMesh == null) return;

        Vector3[] srcVerts = originalContainerMesh.vertices;
        Vector3[] srcNorms = originalContainerMesh.normals;
        Vector2[] srcUVs = originalContainerMesh.uv;
        int[] srcTris = originalContainerMesh.triangles;
        if (srcVerts == null || srcTris == null) return;

        // 1. Find exact point on glass surface triangles closest to breachLocalPos
        Vector3 P0 = breachLocalPos;
        float bestTriDistSq = float.MaxValue;
        Vector3 bestTriNormal = breachLocalNormal;

        for (int i = 0; i < srcTris.Length; i += 3)
        {
            Vector3 vA = srcVerts[srcTris[i]];
            Vector3 vB = srcVerts[srcTris[i + 1]];
            Vector3 vC = srcVerts[srcTris[i + 2]];
            Vector3 pt = ClosestPointOnTriangle(breachLocalPos, vA, vB, vC);
            float dSq = (pt - breachLocalPos).sqrMagnitude;
            if (dSq < bestTriDistSq)
            {
                bestTriDistSq = dSq;
                P0 = pt;
                Vector3 triNorm = Vector3.Cross(vB - vA, vC - vA).normalized;
                if (triNorm.sqrMagnitude > 0.01f)
                {
                    if (Vector3.Dot(triNorm, breachLocalNormal) < 0f) triNorm = -triNorm;
                    bestTriNormal = triNorm;
                }
            }
        }

        float R0 = Mathf.Clamp(originalContainerMesh.bounds.size.y * 0.085f, 0.032f, 0.048f);

        Vector3 N = bestTriNormal.sqrMagnitude > 0.01f ? bestTriNormal.normalized : breachLocalNormal.normalized;
        Vector3 Tx = Vector3.Cross(N, Vector3.up);
        if (Tx.sqrMagnitude < 0.001f) Tx = Vector3.Cross(N, Vector3.right);
        Tx.Normalize();
        Vector3 Ty = Vector3.Cross(N, Tx).normalized;

        // Subdivide large triangles near P0 to enable clean localized circular puncture on coarse meshes
        List<Vector3> workVerts = new List<Vector3>(srcVerts);
        List<Vector3> workNorms = new List<Vector3>(srcNorms != null && srcNorms.Length == srcVerts.Length ? srcNorms : new Vector3[srcVerts.Length]);
        List<Vector2> workUVs = new List<Vector2>(srcUVs != null && srcUVs.Length == srcVerts.Length ? srcUVs : new Vector2[srcVerts.Length]);
        List<int> workTris = new List<int>(srcTris);

        SubdivideTrianglesNearPoint(workVerts, workNorms, workUVs, workTris, P0, R0 * 2.5f, R0 * 0.45f, 4);

        List<int> retainedTris = new List<int>();
        List<int> excisedTris = new List<int>();

        for (int i = 0; i < workTris.Count; i += 3)
        {
            Vector3 vA = workVerts[workTris[i]];
            Vector3 vB = workVerts[workTris[i + 1]];
            Vector3 vC = workVerts[workTris[i + 2]];
            Vector3 centroid = (vA + vB + vC) / 3f;

            float w = Vector3.Dot(centroid - P0, N);
            if (Mathf.Abs(w) > 0.055f)
            {
                retainedTris.Add(workTris[i]);
                retainedTris.Add(workTris[i + 1]);
                retainedTris.Add(workTris[i + 2]);
                continue;
            }

            float u = Vector3.Dot(centroid - P0, Tx);
            float v = Vector3.Dot(centroid - P0, Ty);
            float theta = Mathf.Atan2(v, u);
            float r = Mathf.Sqrt(u * u + v * v);

            float rThresh = R0 * (1f + 0.25f * Mathf.Sin(5f * theta + 1.35f) + 0.15f * Mathf.Cos(9f * theta));
            if (r < rThresh)
            {
                excisedTris.Add(workTris[i]);
                excisedTris.Add(workTris[i + 1]);
                excisedTris.Add(workTris[i + 2]);
            }
            else
            {
                retainedTris.Add(workTris[i]);
                retainedTris.Add(workTris[i + 1]);
                retainedTris.Add(workTris[i + 2]);
            }
        }

        // Safety fallback for low-poly meshes: ensure triangle closest to P0 is excised
        if (excisedTris.Count == 0 && retainedTris.Count >= 6)
        {
            int closestTriIdx = 0;
            float closestDist = float.MaxValue;
            for (int i = 0; i < retainedTris.Count; i += 3)
            {
                Vector3 c = (workVerts[retainedTris[i]] + workVerts[retainedTris[i + 1]] + workVerts[retainedTris[i + 2]]) / 3f;
                float d = Vector3.Distance(c, P0);
                if (d < closestDist)
                {
                    closestDist = d;
                    closestTriIdx = i;
                }
            }

            excisedTris.Add(retainedTris[closestTriIdx]);
            excisedTris.Add(retainedTris[closestTriIdx + 1]);
            excisedTris.Add(retainedTris[closestTriIdx + 2]);
            retainedTris.RemoveRange(closestTriIdx, 3);
        }

        // Orifice lip is set to lowest point on inner rim, guarded against dropping below P0.y - 1.25*R0
        Vector3 lowestLip = P0 - Ty * (R0 * 0.85f);
        if (lowestLip.y < P0.y - R0 * 1.25f) lowestLip.y = P0.y - R0 * 0.85f;
        orificeLipLocalPos = lowestLip;

        // Assign punctured mesh
        if (retainedTris.Count > 0)
        {
            Mesh punctured = BuildPuncturedMesh(workVerts, workNorms, workUVs, retainedTris);
            cmf.sharedMesh = punctured;
            CreateSurfaceConformingFractures(punctured, retainedTris, P0, R0);
        }
    }

    private static Mesh BuildPuncturedMesh(List<Vector3> srcVerts, List<Vector3> srcNorms, List<Vector2> srcUVs, List<int> retainedTris)
    {
        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector3> newNorms = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>();
        List<int> newTris = new List<int>();

        for (int i = 0; i < retainedTris.Count; i++)
        {
            int oldIdx = retainedTris[i];
            if (!remap.TryGetValue(oldIdx, out int newIdx))
            {
                newIdx = newVerts.Count;
                remap[oldIdx] = newIdx;
                newVerts.Add(srcVerts[oldIdx]);
                if (srcNorms != null && srcNorms.Count > oldIdx) newNorms.Add(srcNorms[oldIdx]);
                if (srcUVs != null && srcUVs.Count > oldIdx) newUVs.Add(srcUVs[oldIdx]);
            }
            newTris.Add(newIdx);
        }

        Mesh mesh = new Mesh();
        mesh.name = "UniversalPuncturedMesh";
        mesh.SetVertices(newVerts);
        if (newNorms.Count == newVerts.Count) mesh.SetNormals(newNorms);
        else mesh.RecalculateNormals();
        if (newUVs.Count == newVerts.Count) mesh.SetUVs(0, newUVs);
        mesh.SetTriangles(newTris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;

        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return a + v * ab;
        }

        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return a + w * ac;
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + w * (c - b);
        }

        float denom = 1f / (va + vb + vc);
        float vVal = vb * denom;
        float wVal = vc * denom;
        return a + ab * vVal + ac * wVal;
    }

    private static void SubdivideTrianglesNearPoint(
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector2> uvs,
        List<int> tris,
        Vector3 center,
        float searchRadius,
        float maxEdgeLength,
        int maxPasses = 4)
    {
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();

        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool subdividedAny = false;
            int triCount = tris.Count;
            List<int> newTris = new List<int>(triCount);
            midpointCache.Clear();

            for (int i = 0; i < triCount; i += 3)
            {
                int iA = tris[i];
                int iB = tris[i + 1];
                int iC = tris[i + 2];

                Vector3 vA = verts[iA];
                Vector3 vB = verts[iB];
                Vector3 vC = verts[iC];
                Vector3 triCenter = (vA + vB + vC) / 3f;

                float dist = Vector3.Distance(triCenter, center);
                float lenAB = Vector3.Distance(vA, vB);
                float lenBC = Vector3.Distance(vB, vC);
                float lenCA = Vector3.Distance(vC, vA);
                float maxLen = Mathf.Max(lenAB, Mathf.Max(lenBC, lenCA));

                if (dist < searchRadius && maxLen > maxEdgeLength)
                {
                    subdividedAny = true;
                    int mAB = GetOrCreateMidpoint(iA, iB, verts, norms, uvs, midpointCache);
                    int mBC = GetOrCreateMidpoint(iB, iC, verts, norms, uvs, midpointCache);
                    int mCA = GetOrCreateMidpoint(iC, iA, verts, norms, uvs, midpointCache);

                    newTris.Add(iA); newTris.Add(mAB); newTris.Add(mCA);
                    newTris.Add(mAB); newTris.Add(iB); newTris.Add(mBC);
                    newTris.Add(mCA); newTris.Add(mBC); newTris.Add(iC);
                    newTris.Add(mAB); newTris.Add(mBC); newTris.Add(mCA);
                }
                else
                {
                    newTris.Add(iA);
                    newTris.Add(iB);
                    newTris.Add(iC);
                }
            }

            tris.Clear();
            tris.AddRange(newTris);

            if (!subdividedAny) break;
        }
    }

    private static int GetOrCreateMidpoint(
        int i1, int i2,
        List<Vector3> verts,
        List<Vector3> norms,
        List<Vector2> uvs,
        Dictionary<long, int> cache)
    {
        int min = Mathf.Min(i1, i2);
        int max = Mathf.Max(i1, i2);
        long key = ((long)min << 32) | (long)(uint)max;

        if (cache.TryGetValue(key, out int existingIndex))
            return existingIndex;

        Vector3 midPos = (verts[i1] + verts[i2]) * 0.5f;
        Vector3 midNorm = Vector3.Normalize(norms[i1] + norms[i2]);
        Vector2 midUV = (uvs[i1] + uvs[i2]) * 0.5f;

        int newIndex = verts.Count;
        verts.Add(midPos);
        norms.Add(midNorm);
        uvs.Add(midUV);

        cache[key] = newIndex;
        return newIndex;
    }

    private void CreateSurfaceConformingFractures(Mesh mesh, List<int> retainedTris, Vector3 breachCenter, float breachRadius)
    {
        Vector3[] verts = mesh != null ? mesh.vertices : null;
        if (verts == null || verts.Length == 0) return;
        Vector3[] norms = mesh.normals;
        int[] tris = mesh != null ? mesh.triangles : null;

        Dictionary<int, List<int>> neighbors = new Dictionary<int, List<int>>();
        if (tris != null)
        {
            for (int i = 0; i < tris.Length; i += 3)
            {
                AddNeighbor(neighbors, tris[i], tris[i + 1]);
                AddNeighbor(neighbors, tris[i + 1], tris[i + 2]);
                AddNeighbor(neighbors, tris[i + 2], tris[i]);
            }
        }

        HashSet<int> activeBoundary = new HashSet<int>();
        for (int i = 0; i < verts.Length; i++)
        {
            float d = Vector3.Distance(verts[i], breachCenter);
            if (d >= breachRadius * 0.75f && d <= breachRadius * 1.6f)
            {
                activeBoundary.Add(i);
                if (activeBoundary.Count >= 8) break;
            }
        }

        GameObject microFractureHolder = new GameObject("SurfaceConformingFractures");
        Transform parentT = (containerRenderer != null) ? containerRenderer.transform : transform;
        microFractureHolder.transform.SetParent(parentT, false);
        microFractureHolder.transform.localPosition = Vector3.zero;
        microFractureHolder.transform.localRotation = Quaternion.identity;
        microFractureHolder.transform.localScale = Vector3.one;

        Shader cShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material crackLineMat = new Material(cShader);
        Color cCol = new Color(0.95f, 0.98f, 1.0f, 0.95f);
        crackLineMat.color = cCol;

        int branchCount = 0;
        int maxBranches = 8;
        float maxDist = breachRadius * 2.4f;

        foreach (int startVert in activeBoundary)
        {
            if (startVert < 0 || startVert >= verts.Length) continue;
            if (branchCount >= maxBranches) break;

            List<Vector3> linePts = new List<Vector3>();
            int current = startVert;
            Vector3 startNrm = norms != null && norms.Length > current ? norms[current] : Vector3.up;
            linePts.Add(verts[current] + startNrm * 0.00025f);

            HashSet<int> visited = new HashSet<int> { current };

            for (int step = 0; step < 4; step++)
            {
                if (!neighbors.TryGetValue(current, out List<int> adjList)) break;

                int bestNext = -1;
                float bestDist = Vector3.Distance(verts[current], breachCenter);

                for (int n = 0; n < adjList.Count; n++)
                {
                    int neighbor = adjList[n];
                    if (neighbor < 0 || neighbor >= verts.Length) continue;
                    if (visited.Contains(neighbor)) continue;

                    float d = Vector3.Distance(verts[neighbor], breachCenter);
                    if (d > bestDist && d <= maxDist)
                    {
                        bestDist = d;
                        bestNext = neighbor;
                    }
                }

                if (bestNext == -1 || bestNext < 0 || bestNext >= verts.Length) break;

                visited.Add(bestNext);
                current = bestNext;
                Vector3 nrm = norms != null && norms.Length > current ? norms[current] : Vector3.up;
                linePts.Add(verts[current] + nrm * 0.00025f);
            }

            if (linePts.Count >= 2)
            {
                GameObject lineObj = new GameObject($"FractureTrace_{branchCount}");
                lineObj.transform.SetParent(microFractureHolder.transform, false);
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.material = crackLineMat;
                lr.startWidth = 0.0010f;
                lr.endWidth = 0.00025f;
                lr.startColor = cCol;
                lr.endColor = new Color(cCol.r, cCol.g, cCol.b, 0.35f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.positionCount = linePts.Count;
                lr.SetPositions(linePts.ToArray());
                branchCount++;
            }
        }

        spawnedShards.Add(microFractureHolder);
    }

    private static void AddNeighbor(Dictionary<int, List<int>> map, int a, int b)
    {
        if (!map.TryGetValue(a, out List<int> listA))
        {
            listA = new List<int>();
            map[a] = listA;
        }
        if (!listA.Contains(b)) listA.Add(b);
    }

    private void SpawnGlassShards(Vector3 crackPos, Vector3 crackNorm)
    {
        Material mat = containerRenderer != null ? containerRenderer.sharedMaterial : null;
        for (int i = 0; i < 12; i++)
        {
            GameObject shard = new GameObject($"Shard_{i}");
            shard.transform.position = crackPos + UnityEngine.Random.insideUnitSphere * 0.02f;
            shard.transform.rotation = UnityEngine.Random.rotation;

            MeshFilter mf = shard.AddComponent<MeshFilter>();
            Mesh sm = new Mesh();
            float len = UnityEngine.Random.Range(0.03f, 0.06f);
            sm.vertices = new Vector3[] { new Vector3(0, len, 0.002f), new Vector3(-0.015f, 0, 0.002f), new Vector3(0.015f, 0, 0.002f), new Vector3(0, len, -0.002f), new Vector3(-0.015f, 0, -0.002f), new Vector3(0.015f, 0, -0.002f) };
            sm.triangles = new int[] { 0, 1, 2, 3, 5, 4, 0, 3, 4, 0, 4, 1, 1, 4, 5, 1, 5, 2, 2, 5, 3, 2, 3, 0 };
            sm.RecalculateNormals();
            mf.sharedMesh = sm;

            MeshRenderer mr = shard.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.03f;
            rb.linearVelocity = (crackNorm * 2.2f + UnityEngine.Random.insideUnitSphere * 0.8f);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 30f;
            shard.AddComponent<BoxCollider>();
            spawnedShards.Add(shard);
        }
    }

    private void SimulateHydrostaticOutflow(float dt)
    {
        if (!isCracked || fillAmount <= 0.0005f)
        {
            outwardJetExitVelocity = 0f;
            if (outwardJetLine != null) outwardJetLine.positionCount = 0;
            if (splashParticles != null && splashParticles.isPlaying) splashParticles.Stop();
            return;
        }

        if (orificeLipLocalPos == Vector3.zero)
        {
            orificeLipLocalPos = breachLocalPos;
        }

        GetWorldVerticalBounds(out float worldYMin, out float worldYMax);
        float adaptedFill = ComputeAdaptedFillFraction(fillAmount);
        float ySurface = Mathf.Lerp(worldYMin, worldYMax, adaptedFill);

        Vector3 orificeWorld = (containerRenderer != null)
            ? containerRenderer.transform.TransformPoint(orificeLipLocalPos)
            : transform.TransformPoint(orificeLipLocalPos);

        Vector3 orificeNorm = (containerRenderer != null)
            ? containerRenderer.transform.TransformDirection(breachLocalNormal).normalized
            : transform.TransformDirection(breachLocalNormal).normalized;

        float deltaH = ySurface - orificeWorld.y;
        hydrostaticHeadMeters = Mathf.Max(0f, deltaH);

        // Strict Hydrostatic Equilibrium: stop when fluid level reaches bottom edge of orifice!
        if (deltaH <= 0.0005f)
        {
            outwardJetExitVelocity = 0f;
            outwardFlowRateMilliLitersPerSec = 0f;
            if (outwardJetLine != null) outwardJetLine.positionCount = 0;
            if (splashParticles != null && splashParticles.isPlaying) splashParticles.Stop();
            return;
        }

        FluidPreset prof = GetActivePreset();
        float trickle = Mathf.Clamp01(deltaH / 0.025f);
        outwardJetExitVelocity = prof.dischargeCoeffCd * Mathf.Sqrt(2f * 9.81f * deltaH) * (0.25f + 0.75f * trickle);

        float crackRadiusM = (breachDiameterMm * 0.001f) * 0.5f;
        float flowRateM3 = Mathf.PI * crackRadiusM * crackRadiusM * outwardJetExitVelocity;
        outwardFlowRateMilliLitersPerSec = flowRateM3 * 1_000_000f;

        float drainFraction = (outwardJetExitVelocity / 1.5f) * 0.020f * leakDrainMultiplier * dt;
        fillAmount = Mathf.Clamp01(fillAmount - drainFraction);

        UpdateFillCutPlane(false);
        RenderOutwardJet(orificeWorld, orificeNorm, outwardJetExitVelocity, prof.fluidColor, trickle);
    }

    private void RenderOutwardJet(Vector3 startPos, Vector3 outDir, float speed, Color col, float trickle)
    {
        if (outwardJetLine == null) EnsureJetVisuals();
        if (outwardJetLine == null) return;
        outwardJetLine.enabled = true;

        Shader lineShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                            ?? Shader.Find("Sprites/Default") 
                            ?? Shader.Find("Unlit/Color");
        if (outwardJetLine.sharedMaterial == null || outwardJetLine.sharedMaterial.shader != lineShader)
        {
            var jMat = new Material(lineShader);
            jMat.name = "UniversalOutflowJet_Mat";
            outwardJetLine.material = jMat;
        }

        Color solidStreamCol = new Color(col.r, col.g, col.b, 1.0f);
        outwardJetLine.startColor = solidStreamCol;
        outwardJetLine.endColor = solidStreamCol;
        if (outwardJetLine.sharedMaterial != null)
        {
            outwardJetLine.sharedMaterial.color = solidStreamCol;
            if (outwardJetLine.sharedMaterial.HasProperty("_BaseColor"))
                outwardJetLine.sharedMaterial.SetColor("_BaseColor", solidStreamCol);
            if (outwardJetLine.sharedMaterial.HasProperty("_Color"))
                outwardJetLine.sharedMaterial.SetColor("_Color", solidStreamCol);
        }

        // Closed-form ballistic trajectory
        float groundY = transform.position.y - 0.22f;
        float maxAllowedFloorY = transform.position.y - 0.18f;

        RaycastHit[] hits = Physics.RaycastAll(startPos + Vector3.up * 0.05f + outDir * 0.06f, Vector3.down, 2.5f);
        float bestHitFloorY = float.MinValue;
        bool hitGround = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.gameObject.name.Contains("Shard") || h.collider.gameObject.name.Contains("Puddle") || h.collider.gameObject.name.Contains("Fracture")) continue;

            if (h.point.y <= maxAllowedFloorY && h.point.y > bestHitFloorY)
            {
                bestHitFloorY = h.point.y;
                hitGround = true;
            }
        }

        if (hitGround) groundY = bestHitFloorY;
        groundY = Mathf.Clamp(groundY, -0.085f, maxAllowedFloorY);

        Vector3 v0 = (outDir * speed) + (currentVelocity * 0.25f);
        float g = 9.81f;
        float yDrop = Mathf.Max(0.01f, startPos.y - groundY);
        float discr = v0.y * v0.y + 2f * g * yDrop;
        float flightTime = (v0.y + Mathf.Sqrt(Mathf.Max(0f, discr))) / g;
        flightTime = Mathf.Clamp(flightTime, 0.08f, 0.75f);

        const int numPts = 28;
        if (outwardJetLine.positionCount != numPts) outwardJetLine.positionCount = numPts;

        Vector3 landingPos = startPos;
        for (int i = 0; i < numPts; i++)
        {
            float frac = (float)i / (numPts - 1);
            float t = flightTime * frac;
            Vector3 p = startPos + v0 * t + 0.5f * new Vector3(0f, -g, 0f) * (t * t);

            Vector3 waveOffset = Vector3.Cross(v0.normalized, Vector3.up) * (Mathf.Sin(Time.time * 28f + frac * 12f) * 0.0035f * frac);
            outwardJetLine.SetPosition(i, p + waveOffset);
            if (i == numPts - 1)
            {
                landingPos = p;
                landingPos.y = groundY; // Guarantee stream strictly lands on table/shelf surface!
            }
        }

        float baseWidth = Mathf.Clamp(breachDiameterMm * 0.008f, 0.032f, 0.055f);
        outwardJetLine.startWidth = baseWidth * (0.35f + 0.65f * trickle);
        outwardJetLine.endWidth = outwardJetLine.startWidth * 1.55f;

        if (splashParticles != null)
        {
            splashParticles.transform.position = landingPos;
            var main = splashParticles.main;
            main.startColor = solidStreamCol;
            if (!splashParticles.isPlaying) splashParticles.Play();
        }

        CreatePuddle(landingPos, col);
    }

    private void CreatePuddle(Vector3 pos, Color col)
    {
        if (fluidPuddleObj == null)
        {
            fluidPuddleObj = CreateProceduralPuddleObject(col);
            spawnedShards.Add(fluidPuddleObj);
        }

        // Puddle must always be strictly in world space and aligned horizontally
        if (fluidPuddleObj.transform.parent != null)
        {
            fluidPuddleObj.transform.SetParent(null, true);
        }
        fluidPuddleObj.transform.rotation = Quaternion.identity;

        Vector3 rayOrigin = new Vector3(pos.x, transform.position.y + 0.30f, pos.z);
        float floorY = transform.position.y - 0.22f;
        float maxAllowedFloorY = transform.position.y - 0.18f;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 2.5f);
        float bestFloorY = float.MinValue;
        bool hitGround = false;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.gameObject.name.Contains("Shard") || hit.collider.gameObject.name.Contains("Puddle") || hit.collider.gameObject.name.Contains("Fracture")) continue;

            if (hit.point.y <= maxAllowedFloorY && hit.point.y > bestFloorY)
            {
                bestFloorY = hit.point.y;
                hitGround = true;
            }
        }

        if (hitGround) floorY = bestFloorY;
        floorY = Mathf.Clamp(floorY, -0.085f, maxAllowedFloorY);

        fluidPuddleObj.transform.position = new Vector3(pos.x, floorY + 0.003f, pos.z);
        float drained = Mathf.Max(0f, initialFillAmount - fillAmount);
        float puddleDiameter = Mathf.Clamp(0.10f + drained * 0.55f, 0.10f, 0.52f);
        fluidPuddleObj.transform.localScale = new Vector3(puddleDiameter, 1f, puddleDiameter);
    }

    private GameObject CreateProceduralPuddleObject(Color col)
    {
        GameObject puddle = new GameObject("UniversalFluidPuddle");
        MeshFilter mf = puddle.AddComponent<MeshFilter>();
        MeshRenderer mr = puddle.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        mr.allowOcclusionWhenDynamic = false;

        const int segs = 36;
        Vector3[] pVerts = new Vector3[1 + segs * 2];
        Vector3[] pNorms = new Vector3[1 + segs * 2];
        Vector2[] pUVs = new Vector2[1 + segs * 2];
        int[] pTris = new int[segs * 12];

        pVerts[0] = new Vector3(0f, 0.005f, 0f);
        pNorms[0] = Vector3.up;
        pUVs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segs; i++)
        {
            float theta = ((float)i / segs) * Mathf.PI * 2f;
            float cos = Mathf.Cos(theta);
            float sin = Mathf.Sin(theta);
            float r = 0.5f * (1f + 0.05f * Mathf.Sin(3f * theta) + 0.03f * Mathf.Cos(5f * theta));

            pVerts[1 + i] = new Vector3(cos * r, 0.0022f, sin * r);
            pNorms[1 + i] = new Vector3(cos * 0.35f, 0.90f, sin * 0.35f).normalized;
            pUVs[1 + i] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);

            pVerts[1 + segs + i] = new Vector3(cos * (r * 1.02f), 0f, sin * (r * 1.02f));
            pNorms[1 + segs + i] = Vector3.down;
            pUVs[1 + segs + i] = pUVs[1 + i];
        }

        int triOffset = 0;
        for (int i = 0; i < segs; i++)
        {
            int next = (i + 1) % segs;
            int topCurr = 1 + i;
            int topNext = 1 + next;
            int botCurr = 1 + segs + i;
            int botNext = 1 + segs + next;

            // 1. Top face fan (Clockwise facing UP +Y)
            pTris[triOffset + 0] = 0;
            pTris[triOffset + 1] = topNext;
            pTris[triOffset + 2] = topCurr;

            // 2. Side bevel rim
            pTris[triOffset + 3] = topCurr;
            pTris[triOffset + 4] = topNext;
            pTris[triOffset + 5] = botCurr;

            pTris[triOffset + 6] = topNext;
            pTris[triOffset + 7] = botNext;
            pTris[triOffset + 8] = botCurr;

            // 3. Bottom face (Clockwise facing DOWN -Y)
            pTris[triOffset + 9] = botCurr;
            pTris[triOffset + 10] = botNext;
            pTris[triOffset + 11] = 0;

            triOffset += 12;
        }

        Mesh pMesh = new Mesh();
        pMesh.name = "VolumetricPuddleMesh";
        pMesh.vertices = pVerts;
        pMesh.normals = pNorms;
        pMesh.uv = pUVs;
        pMesh.triangles = pTris;
        pMesh.RecalculateBounds();
        mf.sharedMesh = pMesh;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(litShader);
        mat.name = "UniversalFluidPuddle_Mat";

        Color pCol = new Color(col.r, col.g, col.b, 0.94f);
        mat.color = pCol;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", pCol);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", pCol);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.98f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.12f);
        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        mr.sharedMaterial = mat;
        return puddle;
    }

    public void CleanupAllFractureObjects()
    {
        for (int i = 0; i < spawnedShards.Count; i++)
        {
            if (spawnedShards[i] != null)
            {
                if (Application.isPlaying) Destroy(spawnedShards[i]);
                else DestroyImmediate(spawnedShards[i]);
            }
        }
        spawnedShards.Clear();

        if (fluidPuddleObj != null)
        {
            if (Application.isPlaying) Destroy(fluidPuddleObj);
            else DestroyImmediate(fluidPuddleObj);
            fluidPuddleObj = null;
        }

        if (outwardJetLine != null) outwardJetLine.positionCount = 0;
        if (splashParticles != null && splashParticles.isPlaying) splashParticles.Stop();
    }

    private static Texture2D GetOrCreateDropletTexture()
    {
        if (dropletTexture != null) return dropletTexture;
        int res = 64;
        dropletTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
        dropletTexture.name = "SoftDropletAlpha";
        dropletTexture.wrapMode = TextureWrapMode.Clamp;
        dropletTexture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[res * res];
        float center = (res - 1) * 0.5f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distSqr = dx * dx + dy * dy;
                float alpha = Mathf.Clamp01(Mathf.Exp(-4f * distSqr));
                if (distSqr > 1f) alpha = 0f;
                pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        dropletTexture.SetPixels(pixels);
        dropletTexture.Apply();
        return dropletTexture;
    }

    private void EnsureJetVisuals()
    {
        if (outwardJetLine == null)
        {
            Transform lineObj = transform.Find("UniversalOutwardJetStream");
            if (lineObj != null) outwardJetLine = lineObj.GetComponent<LineRenderer>();
            else
            {
                GameObject jetObj = new GameObject("UniversalOutwardJetStream");
                jetObj.transform.SetParent(transform, false);
                outwardJetLine = jetObj.AddComponent<LineRenderer>();
            }
        }

        if (outwardJetLine != null)
        {
            outwardJetLine.useWorldSpace = true;
            outwardJetLine.startWidth = 0.020f;
            outwardJetLine.endWidth = 0.035f;
            outwardJetLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (outwardJetLine.sharedMaterial == null || outwardJetLine.sharedMaterial.shader == null || outwardJetLine.sharedMaterial.shader.name.Contains("InternalError"))
            {
                Shader lineShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                var jMat = new Material(lineShader);
                Color c = GetActivePreset().fluidColor;
                c.a = 1.0f;
                jMat.color = c;
                if (jMat.HasProperty("_BaseColor")) jMat.SetColor("_BaseColor", c);
                if (jMat.HasProperty("_Color")) jMat.SetColor("_Color", c);
                outwardJetLine.material = jMat;
            }
        }

        if (splashParticles == null)
        {
            Transform splashObj = transform.Find("UniversalSplashParticles");
            if (splashObj != null) splashParticles = splashObj.GetComponent<ParticleSystem>();
            else
            {
                GameObject spObj = new GameObject("UniversalSplashParticles");
                spObj.transform.SetParent(transform, false);
                splashParticles = spObj.AddComponent<ParticleSystem>();
                var main = splashParticles.main;
                main.startLifetime = 0.40f;
                main.startSpeed = 1.0f;
                main.startSize = 0.016f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.startColor = GetActivePreset().fluidColor;

                var emission = splashParticles.emission;
                emission.rateOverTime = 40f;

                var pRend = splashParticles.GetComponent<ParticleSystemRenderer>();
                if (pRend != null)
                {
                    Shader pShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    Material pMat = new Material(pShader);
                    Texture2D dTex = GetOrCreateDropletTexture();
                    pMat.mainTexture = dTex;
                    if (pMat.HasProperty("_BaseMap")) pMat.SetTexture("_BaseMap", dTex);
                    if (pMat.HasProperty("_MainTex")) pMat.SetTexture("_MainTex", dTex);
                    Color sCol = GetActivePreset().fluidColor;
                    sCol.a = 1.0f;
                    pMat.color = sCol;
                    if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", sCol);
                    if (pMat.HasProperty("_Color")) pMat.SetColor("_Color", sCol);
                    pRend.material = pMat;
                }
                splashParticles.Stop();
            }
        }
    }
}
