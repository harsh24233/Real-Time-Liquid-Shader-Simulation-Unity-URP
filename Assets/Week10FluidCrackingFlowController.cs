#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Week 10 Fluid Structure Types representing complex arbitrary enclosed geometries.
/// </summary>
public enum Week10StructureType
{
    PetrolContainer_Jerrycan, // Rectangular body, handle void, angled spout
    StarShapedBox,            // 5-pointed star prism with 10 acute ridges
    ToroidalTank,             // Annular ring torus with central void
    YJunctionManifold,        // 3-way branching pipe with symmetric bifurcation
    CapsuleTank,              // Cylindrical barrel with hemispherical end-caps
    TreeBranchedStructure,    // Multi-tier dendritic branching hierarchy
    MazeMultichamber          // 4-chamber labyrinth with underflow & overflow weirs
}

/// <summary>
/// Week 10 Physical Fluid Types with scientifically calibrated density, viscosity, and color response.
/// </summary>
public enum Week10FluidType
{
    Petrol_Gasoline,
    LiquidMercury,
    SyntheticMotorOil,
    HydraulicCoolant,
    CryogenicLiquidGas,
    TreeSapResin,
    HeavyMazeGel
}

/// <summary>
/// Physical Fluid Profile holding hydrodynamic constants for Week 10 simulation.
/// </summary>
[System.Serializable]
public struct Week10FluidProfile
{
    public string displayName;
    public float densityKgM3;
    public float dynamicViscosityPaS;
    public float naturalBaseFreq;
    public float baseDamping;
    public float dischargeCoeffCd; // Torricelli orifice discharge coefficient
    public Color streamColor;      // Color for outward jet particles and ribbon stream

    public Week10FluidProfile(string name, float rho, float mu, float freq, float damp, float cd, Color col)
    {
        displayName = name;
        densityKgM3 = rho;
        dynamicViscosityPaS = mu;
        naturalBaseFreq = freq;
        baseDamping = damp;
        dischargeCoeffCd = cd;
        streamColor = col;
    }
}

/// <summary>
/// Week 10 Advanced Fluid Dynamics, Structural Cracking & Outward Flow Controller.
/// Features:
/// 1. Complete Fill Shader Pipeline (0.0 to 1.0 range, solid sealed filling at 100% with no top gap).
/// 2. Internal Geometry Morphological Adaptation (accurate volume-preserving height & communicating vessel equalization).
/// 3. Structural Fracture & Outward Flow:
///    - Torricelli hydrostatic orifice discharge: v_exit = Cd * sqrt(2 * g * delta_h).
///    - Mass flow drainage: dV/dt = -Cd * A_crack * v_exit, decreasing fillAmount in real time.
///    - Directional outward fluid jet stream (parabolic line + splash particles) shooting from crack.
///    - Dynamic cut-off when container is tilted so crack is above the liquid surface!
/// </summary>
[ExecuteAlways]
[SelectionBase]
public class Week10FluidCrackingFlowController : MonoBehaviour
{
    [Header("1. Enclosed Geometry & Fluid Configuration")]
    [Tooltip("Arbitrary enclosed structure geometry.")]
    [SerializeField] private Week10StructureType structureType = Week10StructureType.YJunctionManifold;

    [Tooltip("Physical fluid material inside the enclosed container.")]
    [SerializeField] private Week10FluidType fluidType = Week10FluidType.HydraulicCoolant;

    [Header("2. Complete Fill Pipeline (0% to 100%)")]
    [Range(0f, 1f)]
    [Tooltip("Normalized fluid volume fraction: 0.0 = completely evacuated, 1.0 = 100% completely filled and sealed.")]
    public float fillAmount = 1.0f;

    [Tooltip("Enable automated filling and draining cycle for testing.")]
    public bool enableAutoFillDrainCycle = false;
    [SerializeField] private float autoCycleSpeed = 0.15f;
    [SerializeField] private Vector2 autoFillRange = new Vector2(0.0f, 1.0f);

    [Header("3. Physical Constants & Bounds")]
    [SerializeField] private float densityKgM3 = 1060f;
    [SerializeField] private float dynamicViscosityPaS = 0.0035f;
    [SerializeField] private float geometricTortuosity = 2.45f;
    [SerializeField] private float totalEnclosedVolumeLiters = 2.5f;

    [SerializeField] private float localYMin = -0.25f;
    [SerializeField] private float localYMax = 0.25f;

    [Header("4. Structural Fracture & Cracking Dynamics")]
    [Tooltip("Whether the enclosed container has cracked, breaching the system boundary.")]
    public bool isCracked = false;

    [Tooltip("Local position of the structural crack on the container wall.")]
    [SerializeField] private Vector3 crackLocalPosition = new Vector3(0.18f, -0.10f, 0f);

    [Tooltip("Local outward surface normal of the crack wall.")]
    [SerializeField] private Vector3 crackLocalNormal = new Vector3(1f, 0f, 0f);

    [Range(0.5f, 10f)]
    [Tooltip("Aperture diameter of the crack breach in millimeters.")]
    public float crackApertureMm = 3.5f;

    [Tooltip("Rate multiplier for outward drainage.")]
    [SerializeField] private float leakDrainMultiplier = 1.0f;

    [Header("5. Kinematic Motion Sensitivities")]
    [SerializeField] private float translationSensitivity = 1.2f;
    [SerializeField] private float rotationSensitivity = 0.7f;
    [SerializeField] private float maxWobbleDisplacement = 0.45f;

    [Header("6. Visual & Outward Flow References")]
    [SerializeField] private Renderer fluidRenderer;
    [SerializeField] private Renderer glassRenderer;
    [SerializeField] private LineRenderer outwardJetLine;
    [SerializeField] private ParticleSystem splashParticles;
    [SerializeField] private Transform crackVisualTransform;

    // Physical state tracking
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 currentVelocity;
    private Vector3 currentAngularVelocity;
    private Vector3 previousVelocity;
    private Vector3 previousAngularVelocity;

    // 2-axis oscillator states
    private float wobbleX = 0f;
    private float wobbleZ = 0f;
    private float wobbleVelocityX = 0f;
    private float wobbleVelocityZ = 0f;

    // Fill factors
    private float currentFillMagnitudeEnvelope = 1f;
    private float currentResonantFrequency = 1f;
    private float currentEffectiveDamping = 1f;

    // Viscous creep & gravitational slump states
    private Vector3 gelSurfaceNormal = Vector3.up;
    private float gelHesitationTimer = 0f;
    private float gelClingX = 0f;
    private float gelClingZ = 0f;

    // Outflow Telemetry
    private bool isCrackSubmerged = false;
    private float hydrostaticHeadMeters = 0f;
    private float outwardJetExitVelocity = 0f;
    private float outwardFlowRateMilliLitersPerSec = 0f;

    private MaterialPropertyBlock propBlock;
    private static readonly int WobbleXProp = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZProp = Shader.PropertyToID("_WobbleZ");
    private static readonly int FloatFillProp = Shader.PropertyToID("_Float");

    // Dynamic Shattered Shards, Physical Glass Parts & Restart Management
    private List<GameObject> spawnedShards = new List<GameObject>();
    private List<GameObject> spawnedFractureObjects = new List<GameObject>();
    private GameObject fluidPuddleObj = null;
    private float initialFillAmount = 1.0f;

    // Procedural Mesh Puncture & Orifice Lip Geometry
    private Mesh originalGlassMesh = null;
    private Vector3 orificeLipLocalPos = Vector3.zero;
    private static Texture2D dropletTexture = null;

    // Pre-calibrated Week 10 Physical Profiles
    public static readonly Week10FluidProfile ProfilePetrol =
        new Week10FluidProfile("Petrol (Gasoline)", 740f, 0.0006f, 1.85f, 1.8f, 0.65f, new Color(0.95f, 0.65f, 0.15f, 0.85f));

    public static readonly Week10FluidProfile ProfileMercury =
        new Week10FluidProfile("Liquid Mercury", 13550f, 0.0015f, 1.35f, 1.5f, 0.62f, new Color(0.88f, 0.90f, 0.93f, 1.0f));

    public static readonly Week10FluidProfile ProfileMotorOil =
        new Week10FluidProfile("Synthetic Motor Oil", 890f, 0.2500f, 1.25f, 4.8f, 0.45f, new Color(0.35f, 0.16f, 0.02f, 0.95f));

    public static readonly Week10FluidProfile ProfileCoolant =
        new Week10FluidProfile("Hydraulic Coolant", 1060f, 0.0035f, 1.75f, 2.2f, 0.62f, new Color(0.1f, 0.95f, 0.35f, 0.85f));

    public static readonly Week10FluidProfile ProfileCryoGas =
        new Week10FluidProfile("Cryogenic Liquid Gas", 500f, 0.0001f, 2.10f, 1.2f, 0.70f, new Color(0.45f, 0.85f, 1.0f, 0.75f));

    public static readonly Week10FluidProfile ProfileTreeSap =
        new Week10FluidProfile("Tree Sap Resin", 1380f, 8.5000f, 0.55f, 14.0f, 0.22f, new Color(0.92f, 0.45f, 0.05f, 0.95f));

    public static readonly Week10FluidProfile ProfileHeavyGel =
        new Week10FluidProfile("Heavy Maze Gel", 1420f, 25.0000f, 0.40f, 22.0f, 0.12f, new Color(0.45f, 0.10f, 0.65f, 0.98f));

    private void Awake()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        propBlock = new MaterialPropertyBlock();
        initialFillAmount = 1.0f;
        fillAmount = 1.0f;
        CacheReferences();
        ApplyFluidPreset(fluidType);
        ApplyStructurePreset(structureType);
        EnsureJetVisuals();
        UpdateFillCutPlane(true);
    }

    private void Start()
    {
        initialFillAmount = 1.0f;
        fillAmount = 1.0f;
        if (Application.isPlaying)
        {
            isCracked = false;
            CleanupAllFractureObjects();
            if (crackVisualTransform != null) crackVisualTransform.gameObject.SetActive(false);
            if (outwardJetLine != null) outwardJetLine.positionCount = 0;
            if (splashParticles != null) splashParticles.Stop();
        }
        UpdateFillCutPlane(true);
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsureJetVisuals();
        UpdateFillCutPlane(true);
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
#endif
    }

    private void OnDisable()
    {
        CleanupAllFractureObjects();
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
    }

    private void OnDestroy()
    {
        CleanupAllFractureObjects();
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
    }

    public void CacheReferences()
    {
        var childFluid = transform.Find("Fluid");
        if (childFluid != null)
        {
            fluidRenderer = childFluid.GetComponentInChildren<Renderer>();
        }

        var childGlass = transform.Find("Glass");
        if (childGlass != null)
        {
            glassRenderer = childGlass.GetComponentInChildren<Renderer>();
        }

        if (fluidRenderer == null)
        {
            var rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r != glassRenderer && !r.name.Contains("Glass") && !r.name.Contains("Plinth") && !r.name.Contains("Jet") && !r.name.Contains("Crack"))
                {
                    fluidRenderer = r;
                    break;
                }
            }
        }

        // Ensure Glass ALWAYS maintains the glass material
        if (glassRenderer != null)
        {
            if (originalGlassMesh == null)
            {
                var mf = glassRenderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    originalGlassMesh = mf.sharedMesh;
                }
            }

#if UNITY_EDITOR
            if (glassRenderer.sharedMaterial == null || !glassRenderer.sharedMaterial.name.ToLower().Contains("glass"))
            {
                var glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
                if (glassMat != null)
                {
                    if (Application.isPlaying) glassRenderer.material = glassMat;
                    else glassRenderer.sharedMaterial = glassMat;
                }
            }
#endif
        }

        if (fluidRenderer != null)
        {
            var filter = fluidRenderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                localYMin = filter.sharedMesh.bounds.min.y;
                localYMax = filter.sharedMesh.bounds.max.y;
            }
            else
            {
                var bounds = fluidRenderer.localBounds;
                if (bounds.size.y > 0.05f)
                {
                    localYMin = bounds.min.y;
                    localYMax = bounds.max.y;
                }
            }
        }
    }

    public void ApplyFluidPreset(Week10FluidType type)
    {
        fluidType = type;
        Week10FluidProfile prof = GetActiveProfile();
        densityKgM3 = prof.densityKgM3;
        dynamicViscosityPaS = prof.dynamicViscosityPaS;

        gelSurfaceNormal = Vector3.up;
        gelHesitationTimer = 0f;
        gelClingX = 0f;
        gelClingZ = 0f;

#if UNITY_EDITOR
        string matPath = type switch
        {
            Week10FluidType.Petrol_Gasoline => "Assets/Materials/Week10/Week10_Petrol_Fluid.mat",
            Week10FluidType.LiquidMercury => "Assets/Materials/Week10/Week10_Mercury_Fluid.mat",
            Week10FluidType.SyntheticMotorOil => "Assets/Materials/Week10/Week10_MotorOil_Fluid.mat",
            Week10FluidType.HydraulicCoolant => "Assets/Materials/Week10/Week10_Coolant_Fluid.mat",
            Week10FluidType.CryogenicLiquidGas => "Assets/Materials/Week10/Week10_CryoGas_Fluid.mat",
            Week10FluidType.TreeSapResin => "Assets/Materials/Week10/Week10_TreeSap_Fluid.mat",
            Week10FluidType.HeavyMazeGel => "Assets/Materials/Week10/Week10_MazeGel_Fluid.mat",
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
        if (outwardJetLine != null)
        {
            outwardJetLine.startColor = prof.streamColor;
            outwardJetLine.endColor = new Color(prof.streamColor.r, prof.streamColor.g, prof.streamColor.b, 0.4f);
        }
    }

    public void ApplyStructurePreset(Week10StructureType type)
    {
        structureType = type;
        switch (type)
        {
            case Week10StructureType.PetrolContainer_Jerrycan:
                geometricTortuosity = 1.35f;
                totalEnclosedVolumeLiters = 5.0f;
                crackLocalPosition = new Vector3(0.14f, -0.12f, 0.0f);
                crackLocalNormal = new Vector3(1f, 0f, 0f);
                break;
            case Week10StructureType.StarShapedBox:
                geometricTortuosity = 2.20f;
                totalEnclosedVolumeLiters = 2.0f;
                crackLocalPosition = new Vector3(0.18f, -0.08f, 0.05f);
                crackLocalNormal = new Vector3(0.9f, 0f, 0.4f).normalized;
                break;
            case Week10StructureType.ToroidalTank:
                geometricTortuosity = 1.75f;
                totalEnclosedVolumeLiters = 3.2f;
                crackLocalPosition = new Vector3(0.24f, -0.10f, 0.0f);
                crackLocalNormal = new Vector3(1f, -0.2f, 0f).normalized;
                break;
            case Week10StructureType.YJunctionManifold:
                geometricTortuosity = 2.45f;
                totalEnclosedVolumeLiters = 2.8f;
                // Crack at right branching fork junction
                crackLocalPosition = new Vector3(0.12f, -0.02f, 0.0f);
                crackLocalNormal = new Vector3(0.85f, 0.52f, 0f).normalized;
                break;
            case Week10StructureType.CapsuleTank:
                geometricTortuosity = 1.10f;
                totalEnclosedVolumeLiters = 4.0f;
                crackLocalPosition = new Vector3(0.0f, -0.16f, 0.12f);
                crackLocalNormal = new Vector3(0f, -0.4f, 0.9f).normalized;
                break;
            case Week10StructureType.TreeBranchedStructure:
                geometricTortuosity = 2.60f;
                totalEnclosedVolumeLiters = 2.2f;
                crackLocalPosition = new Vector3(0.15f, -0.05f, 0.0f);
                crackLocalNormal = new Vector3(0.9f, 0.2f, 0f).normalized;
                break;
            case Week10StructureType.MazeMultichamber:
                geometricTortuosity = 3.10f;
                totalEnclosedVolumeLiters = 3.5f;
                // Crack in lower outer chamber baffle wall
                crackLocalPosition = new Vector3(0.20f, -0.11f, 0.0f);
                crackLocalNormal = new Vector3(1f, 0f, 0f);
                break;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1. Hotkey Controls: '[' and ']' for fill, 'K' to crack, 'R' to repair
        HandleInteractiveInputs(dt);

        // 2. Automated Fill / Drain Cycle (if enabled)
        if (enableAutoFillDrainCycle && Application.isPlaying && !isCracked)
        {
            float t = Time.time * autoCycleSpeed * Mathf.PI * 2f;
            float sineNorm = (Mathf.Sin(t) + 1f) * 0.5f;
            fillAmount = Mathf.Lerp(autoFillRange.x, autoFillRange.y, sineNorm);
        }

        // 3. Kinematics & Inertial Forces
        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;

        Vector3 rawVelocity = (currentPos - lastPosition) / dt;
        currentVelocity = Vector3.Lerp(currentVelocity, rawVelocity, Mathf.Clamp01(dt * 8f));
        Vector3 acceleration = (currentVelocity - previousVelocity) / dt;
        acceleration = Vector3.ClampMagnitude(acceleration, 25f);

        Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 rawAngVel = (axis * (angleDeg * Mathf.Deg2Rad)) / dt;
        currentAngularVelocity = Vector3.Lerp(currentAngularVelocity, rawAngVel, Mathf.Clamp01(dt * 8f));

        // 4. Coupled Slosh Dynamics & Internal Geometry Adaptation
        ComputeFillAmountCoupledDynamics(acceleration, currentAngularVelocity, dt);
        UpdateViscousCreepSlump(deltaRot, dt);

        // 5. Outward Fluid Flow & Hydrostatic Drainage
        SimulateFractureOutflow(dt);

        if (fluidPuddleObj != null && isCracked)
        {
            float drained = Mathf.Max(0f, initialFillAmount - fillAmount);
            float puddleDiameter = Mathf.Clamp(0.06f + drained * 0.45f, 0.06f, 0.38f);
            fluidPuddleObj.transform.localScale = new Vector3(puddleDiameter, 0.001f, puddleDiameter);
        }

        // 6. Complete Fill Shader Modulation
        UpdateFillCutPlane(false);

        lastPosition = currentPos;
        lastRotation = currentRot;
        previousVelocity = currentVelocity;
        previousAngularVelocity = currentAngularVelocity;
    }

    private void HandleInteractiveInputs(float dt)
    {
        float fillChange = 0f;
        bool toggleCrack = false;
        bool triggerRepair = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftBracketKey.isPressed) fillChange -= 0.30f * dt;
            if (kb.rightBracketKey.isPressed) fillChange += 0.30f * dt;
            if (kb.kKey.wasPressedThisFrame) toggleCrack = true;
            if (kb.rKey.wasPressedThisFrame) triggerRepair = true;
        }
#else
        if (Input.GetKey(KeyCode.LeftBracket)) fillChange -= 0.30f * dt;
        if (Input.GetKey(KeyCode.RightBracket)) fillChange += 0.30f * dt;
        if (Input.GetKeyDown(KeyCode.K)) toggleCrack = true;
        if (Input.GetKeyDown(KeyCode.R)) triggerRepair = true;
#endif

        if (Mathf.Abs(fillChange) > 0.00001f)
        {
            // Full Complete Fill support: seamless 0.0 to 1.0!
            fillAmount = Mathf.Clamp01(fillAmount + fillChange);
        }

        if (toggleCrack) TriggerCrack();
        if (triggerRepair) RepairCrack();
    }

    /// <summary>
    /// Cracks and physically punctures the glass enclosure, excising an irregular jagged hole,
    /// generating beveled rim thickness, bursting sharp shards outward, and allowing hydrostatic outflow.
    /// </summary>
    [ContextMenu("Crack Enclosure")]
    public void TriggerCrack()
    {
        if (isCracked) return;
        isCracked = true;
        EnsureJetVisuals();

        Vector3 crackWorldPos = transform.TransformPoint(crackLocalPosition);
        Vector3 crackWorldNorm = transform.TransformDirection(crackLocalNormal).normalized;

        FractureGlassEnclosure(crackWorldPos, crackWorldNorm);
        SpawnSharpGlassShards(crackWorldPos, crackWorldNorm);
    }

    /// <summary>
    /// Repairs and reseals the structural breach, restoring the glass container in one piece.
    /// </summary>
    [ContextMenu("Repair & Seal Enclosure")]
    public void RepairCrack()
    {
        isCracked = false;
        isCrackSubmerged = false;
        hydrostaticHeadMeters = 0f;
        outwardJetExitVelocity = 0f;
        outwardFlowRateMilliLitersPerSec = 0f;
        if (outwardJetLine != null) outwardJetLine.positionCount = 0;
        if (splashParticles != null) splashParticles.Stop();
        CleanupAllFractureObjects();
    }

    public void TriggerStructuralCrack() => TriggerCrack();
    public void RepairStructuralCrack() => RepairCrack();

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

    private void FractureGlassEnclosure(Vector3 crackWorldPos, Vector3 crackWorldNorm)
    {
        if (glassRenderer == null) CacheReferences();
        if (glassRenderer == null) return;

        MeshFilter mf = glassRenderer.GetComponent<MeshFilter>();
        if (mf == null) return;

        if (originalGlassMesh == null) originalGlassMesh = mf.sharedMesh;
        if (originalGlassMesh == null) return;

        Vector3[] srcVerts = originalGlassMesh.vertices;
        Vector3[] srcNorms = originalGlassMesh.normals;
        Vector2[] srcUVs = originalGlassMesh.uv;
        int[] srcTris = originalGlassMesh.triangles;
        if (srcVerts == null || srcVerts.Length == 0 || srcTris == null || srcTris.Length == 0) return;

        // 1. Find vertex on glass surface closest to crackLocalPosition
        Vector3 P0 = crackLocalPosition;
        float minDistSq = float.MaxValue;
        for (int i = 0; i < srcVerts.Length; i++)
        {
            float d = (srcVerts[i] - crackLocalPosition).sqrMagnitude;
            if (d < minDistSq)
            {
                minDistSq = d;
                P0 = srcVerts[i];
            }
        }

        // 2. Define surface tangent coordinate frame
        Vector3 N = crackLocalNormal.normalized;
        Vector3 Tx = Vector3.Cross(N, Vector3.up);
        if (Tx.sqrMagnitude < 0.001f) Tx = Vector3.Cross(N, Vector3.right);
        Tx.Normalize();
        Vector3 Ty = Vector3.Cross(N, Tx).normalized;

        // 3. Boolean-style triangle culling via noise-perturbed breach radius R(theta)
        float boundsHeight = originalGlassMesh.bounds.size.y;
        float R0 = Mathf.Clamp(boundsHeight * 0.16f, 0.045f, 0.075f);
        float phi = 1.35f;

        List<int> excisedTris = new List<int>();
        List<int> retainedTris = new List<int>();

        for (int i = 0; i < srcTris.Length; i += 3)
        {
            Vector3 vA = srcVerts[srcTris[i]];
            Vector3 vB = srcVerts[srcTris[i + 1]];
            Vector3 vC = srcVerts[srcTris[i + 2]];
            Vector3 centroid = (vA + vB + vC) / 3f;

            float u = Vector3.Dot(centroid - P0, Tx);
            float v = Vector3.Dot(centroid - P0, Ty);
            float theta = Mathf.Atan2(v, u);
            float r = Mathf.Sqrt(u * u + v * v);

            float rThreshold = R0 * (1f + 0.25f * Mathf.Sin(5f * theta + phi) + 0.15f * Mathf.Cos(9f * theta));

            if (r < rThreshold)
            {
                excisedTris.Add(srcTris[i]);
                excisedTris.Add(srcTris[i + 1]);
                excisedTris.Add(srcTris[i + 2]);
            }
            else
            {
                retainedTris.Add(srcTris[i]);
                retainedTris.Add(srcTris[i + 1]);
                retainedTris.Add(srcTris[i + 2]);
            }
        }

        // Safety fallback: ensure at least some triangles are excised
        if (excisedTris.Count == 0)
        {
            for (int i = 0; i < Mathf.Min(srcTris.Length, 12); i++)
            {
                excisedTris.Add(srcTris[i]);
            }
        }

        // 4. Find hole boundary rim directed edges in retained triangles
        Dictionary<long, int> edgeDirectedMap = new Dictionary<long, int>();
        for (int i = 0; i < retainedTris.Count; i += 3)
        {
            AddDirectedEdge(edgeDirectedMap, retainedTris[i], retainedTris[i + 1]);
            AddDirectedEdge(edgeDirectedMap, retainedTris[i + 1], retainedTris[i + 2]);
            AddDirectedEdge(edgeDirectedMap, retainedTris[i + 2], retainedTris[i]);
        }

        List<Vector2Int> boundaryEdges = new List<Vector2Int>();
        HashSet<int> boundaryVertIndices = new HashSet<int>();
        float searchRadius = R0 * 1.6f;

        for (int i = 0; i < retainedTris.Count; i += 3)
        {
            CheckAndAddBoundaryEdge(retainedTris[i], retainedTris[i + 1], edgeDirectedMap, srcVerts, P0, searchRadius, boundaryEdges, boundaryVertIndices);
            CheckAndAddBoundaryEdge(retainedTris[i + 1], retainedTris[i + 2], edgeDirectedMap, srcVerts, P0, searchRadius, boundaryEdges, boundaryVertIndices);
            CheckAndAddBoundaryEdge(retainedTris[i + 2], retainedTris[i], edgeDirectedMap, srcVerts, P0, searchRadius, boundaryEdges, boundaryVertIndices);
        }

        // 5. Find lowest point on the breach rim in world space (P_orifice)
        Vector3 lowestLip = P0;
        float minWorldY = float.MaxValue;
        foreach (int vIdx in boundaryVertIndices)
        {
            Vector3 wPos = transform.TransformPoint(srcVerts[vIdx]);
            if (wPos.y < minWorldY)
            {
                minWorldY = wPos.y;
                lowestLip = srcVerts[vIdx];
            }
        }
        orificeLipLocalPos = lowestLip;

        // 6. Generate beveled glass thickness rim (t = 4 mm extruded inward along -N)
        float wallThickness = 0.004f;
        List<Vector3> rimVerts = new List<Vector3>();
        List<Vector3> rimNorms = new List<Vector3>();
        List<Vector2> rimUVs = new List<Vector2>();
        List<int> rimTris = new List<int>();

        for (int e = 0; e < boundaryEdges.Count; e++)
        {
            int iA = boundaryEdges[e].x;
            int iB = boundaryEdges[e].y;

            Vector3 vA = srcVerts[iA];
            Vector3 vB = srcVerts[iB];
            Vector3 vA_in = vA - N * wallThickness;
            Vector3 vB_in = vB - N * wallThickness;

            Vector3 edgeDir = (vB - vA).normalized;
            Vector3 rimNorm = Vector3.Cross(edgeDir, -N).normalized;

            int baseIdx = rimVerts.Count;
            rimVerts.Add(vA);
            rimVerts.Add(vB);
            rimVerts.Add(vA_in);
            rimVerts.Add(vB_in);

            rimNorms.Add(rimNorm);
            rimNorms.Add(rimNorm);
            rimNorms.Add(rimNorm);
            rimNorms.Add(rimNorm);

            Vector2 uvA = srcUVs != null && srcUVs.Length > iA ? srcUVs[iA] : Vector2.zero;
            Vector2 uvB = srcUVs != null && srcUVs.Length > iB ? srcUVs[iB] : Vector2.right;
            rimUVs.Add(uvA);
            rimUVs.Add(uvB);
            rimUVs.Add(uvA);
            rimUVs.Add(uvB);

            // Two triangles for beveled quad
            rimTris.Add(baseIdx + 0);
            rimTris.Add(baseIdx + 1);
            rimTris.Add(baseIdx + 3);

            rimTris.Add(baseIdx + 0);
            rimTris.Add(baseIdx + 3);
            rimTris.Add(baseIdx + 2);
        }

        // 7. Construct and assign the punctured mesh directly to container
        Mesh puncturedMesh = BuildPuncturedMesh(originalGlassMesh, retainedTris, rimVerts, rimNorms, rimUVs, rimTris);
        mf.sharedMesh = puncturedMesh;
        glassRenderer.enabled = true;

        // 8. Create excised blown-out wall fragment with physics
        Material glassMat = glassRenderer.sharedMaterial;
#if UNITY_EDITOR
        if (glassMat == null) glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
#endif
        Mesh chunkMesh = BuildExcisedChunkMesh(originalGlassMesh, excisedTris, N, wallThickness);
        if (chunkMesh != null)
        {
            GameObject chunkObj = new GameObject($"{name}_BlownOutGlassChunk");
            chunkObj.transform.SetParent(transform.parent != null ? transform.parent : null);
            chunkObj.transform.position = glassRenderer.transform.position;
            chunkObj.transform.rotation = glassRenderer.transform.rotation;
            chunkObj.transform.localScale = glassRenderer.transform.lossyScale;

            MeshFilter cmf = chunkObj.AddComponent<MeshFilter>();
            cmf.sharedMesh = chunkMesh;
            MeshRenderer cmr = chunkObj.AddComponent<MeshRenderer>();
            cmr.sharedMaterial = glassMat;

            Rigidbody rb = chunkObj.AddComponent<Rigidbody>();
            rb.mass = 0.12f;
            Vector3 blastDir = (crackWorldNorm * 2.2f + Vector3.up * 0.65f + Random.insideUnitSphere * 0.35f).normalized;
            rb.linearVelocity = blastDir * Random.Range(1.8f, 2.8f);
            rb.angularVelocity = Random.insideUnitSphere * Random.Range(15f, 35f);

            chunkObj.AddComponent<BoxCollider>();
            spawnedFractureObjects.Add(chunkObj);
        }

        // 9. Generate surface-conforming micro-fractures strictly on mesh triangles
        CreateSurfaceConformingFractures(originalGlassMesh, boundaryVertIndices, P0, R0);
    }

    private static long MakeDirectedEdgeKey(int a, int b)
    {
        return ((long)a << 32) | (long)(uint)b;
    }

    private static void AddDirectedEdge(Dictionary<long, int> map, int a, int b)
    {
        long key = MakeDirectedEdgeKey(a, b);
        if (map.ContainsKey(key)) map[key]++;
        else map[key] = 1;
    }

    private static void CheckAndAddBoundaryEdge(int a, int b, Dictionary<long, int> directedMap, Vector3[] verts, Vector3 center, float searchRadius, List<Vector2Int> boundaryEdges, HashSet<int> boundaryVerts)
    {
        long oppositeKey = MakeDirectedEdgeKey(b, a);
        if (!directedMap.ContainsKey(oppositeKey))
        {
            Vector3 mid = (verts[a] + verts[b]) * 0.5f;
            if (Vector3.Distance(mid, center) <= searchRadius)
            {
                boundaryEdges.Add(new Vector2Int(a, b));
                boundaryVerts.Add(a);
                boundaryVerts.Add(b);
            }
        }
    }

    private static Mesh BuildPuncturedMesh(Mesh srcMesh, List<int> retainedTris, List<Vector3> rimVerts, List<Vector3> rimNorms, List<Vector2> rimUVs, List<int> rimTris)
    {
        Vector3[] srcVerts = srcMesh.vertices;
        Vector3[] srcNorms = srcMesh.normals;
        Vector2[] srcUVs = srcMesh.uv;

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
                if (srcNorms != null && srcNorms.Length > oldIdx) newNorms.Add(srcNorms[oldIdx]);
                if (srcUVs != null && srcUVs.Length > oldIdx) newUVs.Add(srcUVs[oldIdx]);
            }
            newTris.Add(newIdx);
        }

        int rimBaseOffset = newVerts.Count;
        newVerts.AddRange(rimVerts);
        newNorms.AddRange(rimNorms);
        newUVs.AddRange(rimUVs);

        for (int i = 0; i < rimTris.Count; i++)
        {
            newTris.Add(rimTris[i] + rimBaseOffset);
        }

        Mesh mesh = new Mesh();
        mesh.name = "PuncturedGlassVessel";
        mesh.SetVertices(newVerts);
        if (newNorms.Count == newVerts.Count) mesh.SetNormals(newNorms);
        else mesh.RecalculateNormals();
        if (newUVs.Count == newVerts.Count) mesh.SetUVs(0, newUVs);
        mesh.SetTriangles(newTris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildExcisedChunkMesh(Mesh srcMesh, List<int> excisedTris, Vector3 normal, float thickness)
    {
        if (excisedTris == null || excisedTris.Count == 0) return null;

        Vector3[] srcVerts = srcMesh.vertices;
        Vector3[] srcNorms = srcMesh.normals;
        Vector2[] srcUVs = srcMesh.uv;

        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector3> newNorms = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>();
        List<int> newTris = new List<int>();

        for (int i = 0; i < excisedTris.Count; i++)
        {
            int oldIdx = excisedTris[i];
            if (!remap.TryGetValue(oldIdx, out int newIdx))
            {
                newIdx = newVerts.Count;
                remap[oldIdx] = newIdx;
                newVerts.Add(srcVerts[oldIdx]);
                if (srcNorms != null && srcNorms.Length > oldIdx) newNorms.Add(srcNorms[oldIdx]);
                if (srcUVs != null && srcUVs.Length > oldIdx) newUVs.Add(srcUVs[oldIdx]);
            }
            newTris.Add(newIdx);
        }

        // Extrude back face with thickness
        int frontCount = newVerts.Count;
        for (int i = 0; i < frontCount; i++)
        {
            newVerts.Add(newVerts[i] - normal * thickness);
            newNorms.Add(-normal);
            newUVs.Add(newUVs[i]);
        }

        // Add back face triangles with reversed winding
        int triCount = newTris.Count;
        for (int i = 0; i < triCount; i += 3)
        {
            newTris.Add(newTris[i] + frontCount);
            newTris.Add(newTris[i + 2] + frontCount);
            newTris.Add(newTris[i + 1] + frontCount);
        }

        Mesh mesh = new Mesh();
        mesh.name = "ExcisedGlassChunk";
        mesh.SetVertices(newVerts);
        if (newNorms.Count == newVerts.Count) mesh.SetNormals(newNorms);
        else mesh.RecalculateNormals();
        if (newUVs.Count == newVerts.Count) mesh.SetUVs(0, newUVs);
        mesh.SetTriangles(newTris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateSurfaceConformingFractures(Mesh mesh, HashSet<int> boundaryVerts, Vector3 breachCenter, float breachRadius)
    {
        if (boundaryVerts == null || boundaryVerts.Count == 0) return;

        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        int[] tris = mesh.triangles;

        // Build vertex adjacency graph from mesh triangles
        Dictionary<int, List<int>> neighbors = new Dictionary<int, List<int>>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            AddNeighbor(neighbors, tris[i], tris[i + 1]);
            AddNeighbor(neighbors, tris[i + 1], tris[i + 2]);
            AddNeighbor(neighbors, tris[i + 2], tris[i]);
        }

        GameObject microFractureHolder = new GameObject("SurfaceConformingFractures");
        microFractureHolder.transform.SetParent(transform);
        microFractureHolder.transform.localPosition = Vector3.zero;
        microFractureHolder.transform.localRotation = Quaternion.identity;
        microFractureHolder.transform.localScale = Vector3.one;

        Material crackLineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        crackLineMat.color = new Color(0.96f, 0.98f, 1.0f, 0.85f);

        int branchCount = 0;
        int maxBranches = 8;
        float maxDist = breachRadius * 2.2f;

        foreach (int startVert in boundaryVerts)
        {
            if (branchCount >= maxBranches) break;

            List<Vector3> linePts = new List<Vector3>();
            int current = startVert;
            Vector3 startNrm = norms != null && norms.Length > current ? norms[current] : Vector3.up;
            linePts.Add(verts[current] + startNrm * 0.0006f);

            HashSet<int> visited = new HashSet<int> { current };

            for (int step = 0; step < 4; step++)
            {
                if (!neighbors.TryGetValue(current, out List<int> adjList)) break;

                int bestNext = -1;
                float bestDist = Vector3.Distance(verts[current], breachCenter);

                for (int n = 0; n < adjList.Count; n++)
                {
                    int neighbor = adjList[n];
                    if (visited.Contains(neighbor)) continue;

                    float d = Vector3.Distance(verts[neighbor], breachCenter);
                    if (d > bestDist && d <= maxDist)
                    {
                        bestDist = d;
                        bestNext = neighbor;
                    }
                }

                if (bestNext == -1) break;

                visited.Add(bestNext);
                current = bestNext;
                Vector3 nrm = norms != null && norms.Length > current ? norms[current] : Vector3.up;
                linePts.Add(verts[current] + nrm * 0.0006f);
            }

            if (linePts.Count >= 2)
            {
                GameObject lineObj = new GameObject($"FractureTrace_{branchCount}");
                lineObj.transform.SetParent(microFractureHolder.transform);
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.material = crackLineMat;
                lr.startWidth = 0.0022f;
                lr.endWidth = 0.0006f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.positionCount = linePts.Count;
                lr.SetPositions(linePts.ToArray());
                branchCount++;
            }
        }

        spawnedFractureObjects.Add(microFractureHolder);
    }

    private static void AddNeighbor(Dictionary<int, List<int>> map, int a, int b)
    {
        if (!map.TryGetValue(a, out List<int> listA))
        {
            listA = new List<int>();
            map[a] = listA;
        }
        if (!listA.Contains(b)) listA.Add(b);

        if (!map.TryGetValue(b, out List<int> listB))
        {
            listB = new List<int>();
            map[b] = listB;
        }
        if (!listB.Contains(a)) listB.Add(a);
    }

    private void SpawnSharpGlassShards(Vector3 crackWorldPos, Vector3 crackWorldNorm)
    {
        Material glassMat = glassRenderer != null ? glassRenderer.sharedMaterial : null;
#if UNITY_EDITOR
        if (glassMat == null) glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/glass.mat");
#endif

        int shardCount = 14;
        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = new GameObject($"GlassShard_{name}_{i}");
            shard.transform.position = crackWorldPos + Random.insideUnitSphere * 0.025f;
            shard.transform.rotation = Random.rotation;

            MeshFilter mf = shard.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateGlassShardMesh();

            MeshRenderer mr = shard.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glassMat;

            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.04f;
            Vector3 blastDir = (crackWorldNorm * 1.9f + Random.insideUnitSphere * 0.75f).normalized;
            float blastSpeed = Random.Range(1.4f, 3.2f);
            rb.linearVelocity = blastDir * blastSpeed + (currentVelocity * 0.4f);
            rb.angularVelocity = Random.insideUnitSphere * Random.Range(20f, 60f);

            shard.AddComponent<BoxCollider>();
            spawnedShards.Add(shard);
        }
    }

    private static Mesh CreateGlassShardMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "SharpGlassShard";

        float length = Random.Range(0.035f, 0.075f);
        float width = Random.Range(0.012f, 0.030f);
        float thickness = Random.Range(0.003f, 0.006f);

        Vector3 v0 = new Vector3(0f, length * 0.7f, 0f);
        Vector3 v1 = new Vector3(-width * 0.5f, -length * 0.3f, 0f);
        Vector3 v2 = new Vector3(width * 0.5f + Random.Range(-0.004f, 0.004f), -length * 0.3f + Random.Range(-0.008f, 0.008f), 0f);

        float ht = thickness * 0.5f;
        Vector3[] verts = new Vector3[6]
        {
            new Vector3(v0.x, v0.y, ht),
            new Vector3(v1.x, v1.y, ht),
            new Vector3(v2.x, v2.y, ht),
            new Vector3(v0.x, v0.y, -ht),
            new Vector3(v1.x, v1.y, -ht),
            new Vector3(v2.x, v2.y, -ht)
        };

        int[] tris = new int[]
        {
            0, 1, 2, // front
            3, 5, 4, // back
            0, 3, 4, 0, 4, 1, // side 1
            1, 4, 5, 1, 5, 2, // side 2
            2, 5, 3, 2, 3, 0  // side 3
        };

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateFluidPuddle(Vector3 impactFloorPos, Color col)
    {
        if (fluidPuddleObj != null)
        {
            float currentScale = fluidPuddleObj.transform.localScale.x;
            if (currentScale < 0.45f)
            {
                float newScale = Mathf.MoveTowards(currentScale, 0.45f, Time.deltaTime * 0.06f);
                fluidPuddleObj.transform.localScale = new Vector3(newScale, 0.001f, newScale);
            }
            return;
        }

        fluidPuddleObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fluidPuddleObj.name = "FluidOutflowPuddle";
        fluidPuddleObj.transform.position = new Vector3(impactFloorPos.x, impactFloorPos.y + 0.0015f, impactFloorPos.z);
        fluidPuddleObj.transform.localScale = new Vector3(0.06f, 0.001f, 0.06f);
        DestroyImmediate(fluidPuddleObj.GetComponent<Collider>());

        var puddleMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        puddleMat.color = col;
        puddleMat.SetFloat("_Smoothness", 0.96f);
        fluidPuddleObj.GetComponent<Renderer>().material = puddleMat;
    }

    public void CleanupShards()
    {
        CleanupAllFractureObjects();
    }

    public void CleanupAllFractureObjects()
    {
        if (spawnedFractureObjects != null)
        {
            for (int i = 0; i < spawnedFractureObjects.Count; i++)
            {
                if (spawnedFractureObjects[i] != null)
                {
                    if (Application.isPlaying) Destroy(spawnedFractureObjects[i]);
                    else DestroyImmediate(spawnedFractureObjects[i]);
                }
            }
            spawnedFractureObjects.Clear();
        }

        if (spawnedShards != null)
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
        }

        if (fluidPuddleObj != null)
        {
            if (Application.isPlaying) Destroy(fluidPuddleObj);
            else DestroyImmediate(fluidPuddleObj);
            fluidPuddleObj = null;
        }

        // Restore original unpunctured mesh to glass container
        if (glassRenderer != null)
        {
            var mf = glassRenderer.GetComponent<MeshFilter>();
            if (mf != null && originalGlassMesh != null)
            {
                mf.sharedMesh = originalGlassMesh;
            }
            glassRenderer.enabled = true;
        }

        if (outwardJetLine != null) outwardJetLine.positionCount = 0;
        if (splashParticles != null && splashParticles.isPlaying) splashParticles.Stop();
    }

    [ContextMenu("Trigger Impulse Slosh")]
    public void TriggerImpulseSlosh()
    {
        TriggerImpulseSlosh(0.35f);
    }

    public void TriggerImpulseSlosh(float impulse)
    {
        if (fillAmount >= 0.999f || fillAmount <= 0.001f) return;
        float envelope = Mathf.Max(0.2f, currentFillMagnitudeEnvelope);
        float viscousFactor = 1f / (1f + dynamicViscosityPaS * 0.35f);
        wobbleVelocityX += impulse * 3.5f * envelope * viscousFactor;
        wobbleVelocityZ += impulse * 2.8f * envelope * viscousFactor;
    }

    /// <summary>
    /// Simulates Torricelli hydrostatic orifice discharge with strict equilibrium cutoff.
    /// Liquid drains only down to y_surface == y_orifice. Tilting automatically resumes outflow!
    /// </summary>
    private void SimulateFractureOutflow(float dt)
    {
        if (!isCracked || fillAmount <= 0.0005f)
        {
            isCrackSubmerged = false;
            hydrostaticHeadMeters = 0f;
            outwardJetExitVelocity = 0f;
            outwardFlowRateMilliLitersPerSec = 0f;
            if (outwardJetLine != null) outwardJetLine.positionCount = 0;
            if (splashParticles != null && splashParticles.isPlaying) splashParticles.Stop();
            return;
        }

        // 1. Calculate World Positions of Liquid Surface and Orifice Lip
        float adaptedLocalY = ComputeGeometryAdaptedSurfaceY(fillAmount);
        Vector3 liquidSurfaceWorld = transform.TransformPoint(new Vector3(0f, adaptedLocalY, 0f));
        float ySurface = liquidSurfaceWorld.y;

        Vector3 orificeWorldPos = transform.TransformPoint(orificeLipLocalPos);
        Vector3 orificeWorldNorm = transform.TransformDirection(crackLocalNormal).normalized;
        float yOrifice = orificeWorldPos.y;

        // Hydrostatic Head Delta h
        float deltaH = ySurface - yOrifice;
        hydrostaticHeadMeters = Mathf.Max(0f, deltaH);

        // Strict Equilibrium Stop Condition: if liquid level drops to or below the orifice lip, STOP!
        if (deltaH <= 0.0005f)
        {
            isCrackSubmerged = false;
            outwardJetExitVelocity = 0f;
            outwardFlowRateMilliLitersPerSec = 0f;
            if (outwardJetLine != null) outwardJetLine.positionCount = 0;
            if (splashParticles != null && splashParticles.isPlaying) splashParticles.Stop();
            return;
        }

        isCrackSubmerged = true;

        // 2. Torricelli Velocity with Smooth Meniscus Trickle Attenuation as deltaH -> 0
        Week10FluidProfile prof = GetActiveProfile();
        float trickleFactor = Mathf.Clamp01(deltaH / 0.025f);
        float cd = prof.dischargeCoeffCd;
        outwardJetExitVelocity = cd * Mathf.Sqrt(2f * 9.81f * deltaH) * (0.25f + 0.75f * trickleFactor);

        // 3. Volumetric Flow Rate & Drainage
        float crackRadiusM = (crackApertureMm * 0.001f) * 0.5f;
        float crackAreaM2 = Mathf.PI * crackRadiusM * crackRadiusM;
        float flowRateM3PerSec = crackAreaM2 * outwardJetExitVelocity;
        outwardFlowRateMilliLitersPerSec = flowRateM3PerSec * 1_000_000f;

        float totalVolM3 = Mathf.Max(0.0005f, totalEnclosedVolumeLiters * 0.001f);
        float baseDrainRate = (flowRateM3PerSec / totalVolM3) * leakDrainMultiplier;
        float visibleDrainRate = Mathf.Max(baseDrainRate, (outwardJetExitVelocity / 1.5f) * 0.055f * leakDrainMultiplier);
        float drainFraction = visibleDrainRate * dt;
        fillAmount = Mathf.Clamp01(fillAmount - drainFraction);

        UpdateFillCutPlane(false);

        // 4. Parabolic Jet originating precisely at orifice lip
        RenderOutwardJet(orificeWorldPos, orificeWorldNorm, outwardJetExitVelocity, prof.streamColor, trickleFactor);
    }

    /// <summary>
    /// Renders a physical 3D parabolic fluid jet streaming outward from the lower lip of the orifice.
    /// </summary>
    private void RenderOutwardJet(Vector3 startPos, Vector3 outwardDir, float exitSpeed, Color col, float trickleFactor)
    {
        if (outwardJetLine == null) return;

        const int numPoints = 22;
        if (outwardJetLine.positionCount != numPoints) outwardJetLine.positionCount = numPoints;

        Vector3 initialVelocity = (outwardDir * exitSpeed) + (currentVelocity * 0.35f);
        if (densityKgM3 > 5000f) initialVelocity.y -= 0.25f;

        float timeStep = 0.022f;
        Vector3 currentPos = startPos;
        Vector3 currentVel = initialVelocity;
        Vector3 gravity = new Vector3(0f, -9.81f, 0f);

        float floorY = transform.position.y - 0.25f;

        for (int i = 0; i < numPoints; i++)
        {
            outwardJetLine.SetPosition(i, currentPos);

            if (currentPos.y <= floorY && i > 1)
            {
                for (int j = i; j < numPoints; j++)
                {
                    outwardJetLine.SetPosition(j, currentPos);
                }

                if (splashParticles != null)
                {
                    splashParticles.transform.position = currentPos;
                    var main = splashParticles.main;
                    main.startColor = col;
                    if (!splashParticles.isPlaying) splashParticles.Play();
                }

                // Raycast downward to accurately place expanding puddle on shelf/plinth
                if (Physics.Raycast(currentPos + Vector3.up * 0.15f, Vector3.down, out RaycastHit hit, 1.5f))
                {
                    CreateFluidPuddle(hit.point, col);
                }
                else
                {
                    CreateFluidPuddle(currentPos, col);
                }
                break;
            }

            currentVel += gravity * timeStep;
            currentPos += currentVel * timeStep;
        }

        float baseWidth = Mathf.Clamp(crackApertureMm * 0.0025f, 0.008f, 0.026f);
        outwardJetLine.startWidth = baseWidth * (0.3f + 0.7f * trickleFactor);
        outwardJetLine.endWidth = outwardJetLine.startWidth * 1.8f;
        outwardJetLine.startColor = col;
        outwardJetLine.endColor = new Color(col.r, col.g, col.b, col.a * 0.45f);

        if (outwardJetLine.sharedMaterial != null) outwardJetLine.sharedMaterial.color = col;
    }

    /// <summary>
    /// Scientific Internal Geometry Adaptation:
    /// Maps normalized fillAmount (0..1) to physical localY cut plane based on
    /// the specific internal morphology and cross-sectional distribution of the enclosed structure.
    /// </summary>
    public float ComputeGeometryAdaptedSurfaceY(float fill)
    {
        if (fill <= 0.0001f) return localYMin - 0.05f;
        if (fill >= 0.9999f) return localYMax + 0.05f;

        float range = localYMax - localYMin;

        switch (structureType)
        {
            case Week10StructureType.PetrolContainer_Jerrycan:
            {
                // Wide rectangular body below shoulder (75% volume), narrow spout/neck at top (25% volume)
                float shoulder = localYMin + range * 0.72f;
                if (fill < 0.75f)
                    return Mathf.Lerp(localYMin, shoulder, fill / 0.75f);
                else
                    return Mathf.Lerp(shoulder, localYMax, (fill - 0.75f) / 0.25f);
            }
            case Week10StructureType.StarShapedBox:
            {
                // Star prism ridges create non-linear boundary area distribution
                float cubic = fill * fill * (3f - 2f * fill); // Smooth S-curve volume transition
                return Mathf.Lerp(localYMin, localYMax, cubic);
            }
            case Week10StructureType.ToroidalTank:
            {
                // Annular ring: below inner hole, dual split columns through center, merging pool at top
                float yNorm = fill;
                // Torus cross-sectional integral correction
                float adapted = Mathf.Sin((fill - 0.5f) * Mathf.PI) * 0.5f + 0.5f;
                return Mathf.Lerp(localYMin, localYMax, Mathf.Lerp(fill, adapted, 0.45f));
            }
            case Week10StructureType.YJunctionManifold:
            {
                // Bottom single trunk (35% volume), top dual branching pipes (65% volume)
                float junctionY = localYMin + range * 0.35f;
                if (fill < 0.35f)
                    return Mathf.Lerp(localYMin, junctionY, fill / 0.35f);
                else
                    return Mathf.Lerp(junctionY, localYMax, (fill - 0.35f) / 0.65f);
            }
            case Week10StructureType.CapsuleTank:
            {
                // Hemispherical dome caps taper at bottom and top; cylindrical barrel in center
                float capRatio = 0.22f;
                if (fill < capRatio)
                {
                    float u = fill / capRatio;
                    return Mathf.Lerp(localYMin, localYMin + range * capRatio, Mathf.Pow(u, 0.65f));
                }
                else if (fill > 1f - capRatio)
                {
                    float u = (fill - (1f - capRatio)) / capRatio;
                    return Mathf.Lerp(localYMax - range * capRatio, localYMax, 1f - Mathf.Pow(1f - u, 0.65f));
                }
                else
                {
                    float u = (fill - capRatio) / (1f - 2f * capRatio);
                    return Mathf.Lerp(localYMin + range * capRatio, localYMax - range * capRatio, u);
                }
            }
            case Week10StructureType.TreeBranchedStructure:
            {
                // Dendritic branching: trunk base, 3 primary limbs, 6 capillary limbs
                float t1 = 0.30f;
                float t2 = 0.70f;
                if (fill < t1) return Mathf.Lerp(localYMin, localYMin + range * 0.30f, fill / t1);
                else if (fill < t2) return Mathf.Lerp(localYMin + range * 0.30f, localYMin + range * 0.70f, (fill - t1) / (t2 - t1));
                else return Mathf.Lerp(localYMin + range * 0.70f, localYMax, (fill - t2) / (1f - t2));
            }
            case Week10StructureType.MazeMultichamber:
            {
                // Multi-baffled labyrinth: underflow slots (25%), chamber partitions (50%), overflow crest (25%)
                float sCurve = fill * fill * (3f - 2f * fill);
                return Mathf.Lerp(localYMin, localYMax, sCurve);
            }
            default:
                return Mathf.Lerp(localYMin, localYMax, fill);
        }
    }

    private void ComputeFillAmountCoupledDynamics(Vector3 acceleration, Vector3 angularVel, float dt)
    {
        // A. Complete Fill Slosh Envelope:
        // Psi(F) = 4 * F * (1 - F) -> suppressed both at empty (F=0) AND at complete full (F=1)!
        currentFillMagnitudeEnvelope = 4f * fillAmount * (1f - fillAmount);
        currentFillMagnitudeEnvelope = Mathf.Clamp01(currentFillMagnitudeEnvelope);

        // B. Resonant Frequency Dispersion
        Week10FluidProfile prof = GetActiveProfile();
        float frequencyScale = Mathf.Sqrt(Mathf.Clamp01(Mathf.Abs(Mathf.Tan(fillAmount * Mathf.PI * 0.48f)) * 0.5f + 0.1f));
        currentResonantFrequency = Mathf.Max(0.15f, prof.naturalBaseFreq * frequencyScale);

        // C. Viscosity Coupled Damping
        float stokesShearDamping = geometricTortuosity * Mathf.Sqrt(Mathf.Max(0.0001f, densityKgM3 * dynamicViscosityPaS * currentResonantFrequency)) * 0.05f;
        float viscousBulkDamping = dynamicViscosityPaS * 1.5f;
        currentEffectiveDamping = prof.baseDamping + stokesShearDamping + viscousBulkDamping;

        // D. Inertial Coupling
        float densityInertiaFactor = Mathf.Clamp(Mathf.Pow(1000f / Mathf.Max(100f, densityKgM3), 0.35f), 0.35f, 1.8f);
        float viscousInertiaSuppression = 1f / (1f + dynamicViscosityPaS * 0.40f);

        Vector3 angularAcc = (angularVel - previousAngularVelocity) / Mathf.Max(dt, 0.0001f);
        angularAcc = Vector3.ClampMagnitude(angularAcc, 40f);

        float activeMaxLimit = maxWobbleDisplacement * Mathf.Max(0.05f, currentFillMagnitudeEnvelope);

        // Suppress slosh entirely if completely full (F >= 0.999f) or empty (F <= 0.001f)
        if (fillAmount >= 0.999f || fillAmount <= 0.001f)
        {
            wobbleX = 0f;
            wobbleZ = 0f;
            wobbleVelocityX = 0f;
            wobbleVelocityZ = 0f;
            return;
        }

        float targetTiltX = Mathf.Clamp(
            (-currentVelocity.z * 0.18f - acceleration.z * 0.025f) * translationSensitivity * densityInertiaFactor +
            (angularVel.x * 0.55f + angularAcc.x * 0.05f) * rotationSensitivity,
            -activeMaxLimit, activeMaxLimit
        ) * currentFillMagnitudeEnvelope * viscousInertiaSuppression;

        float targetTiltZ = Mathf.Clamp(
            (currentVelocity.x * 0.18f + acceleration.x * 0.025f) * translationSensitivity * densityInertiaFactor -
            (angularVel.z * 0.55f + angularAcc.z * 0.05f) * rotationSensitivity,
            -activeMaxLimit, activeMaxLimit
        ) * currentFillMagnitudeEnvelope * viscousInertiaSuppression;

        // E. Unconditionally Stable Exact Analytical + SmoothDamp Oscillator
        float omegaN = 2f * Mathf.PI * Mathf.Max(currentResonantFrequency, 0.05f);
        float gamma = currentEffectiveDamping;

        UpdateOscillator(targetTiltX, omegaN, gamma, dt, ref wobbleX, ref wobbleVelocityX, activeMaxLimit);
        UpdateOscillator(targetTiltZ, omegaN, gamma, dt, ref wobbleZ, ref wobbleVelocityZ, activeMaxLimit);

        if (acceleration.sqrMagnitude < 0.0005f && currentAngularVelocity.sqrMagnitude < 0.0005f)
        {
            if (Mathf.Abs(wobbleX) < 0.0002f && Mathf.Abs(wobbleVelocityX) < 0.0005f && Mathf.Abs(targetTiltX) < 0.0002f)
            {
                wobbleX = 0f;
                wobbleVelocityX = 0f;
            }
            if (Mathf.Abs(wobbleZ) < 0.0002f && Mathf.Abs(wobbleVelocityZ) < 0.0005f && Mathf.Abs(targetTiltZ) < 0.0002f)
            {
                wobbleZ = 0f;
                wobbleVelocityZ = 0f;
            }
        }
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
            float maxSpeed = maxLimit * (1.5f / (1f + dynamicViscosityPaS * 0.2f));
            amount = Mathf.SmoothDamp(amount, target, ref velocity, smoothTime, maxSpeed, dt);
            amount = Mathf.Clamp(amount, -maxLimit, maxLimit);
        }
    }

    private void UpdateViscousCreepSlump(Quaternion deltaRot, float dt)
    {
        bool isViscous = (fluidType == Week10FluidType.HeavyMazeGel || dynamicViscosityPaS >= 0.20f);

        if (!isViscous || fillAmount >= 0.999f || fillAmount <= 0.001f)
        {
            gelSurfaceNormal = Vector3.up;
            gelHesitationTimer = 0f;
            gelClingX = 0f;
            gelClingZ = 0f;
            return;
        }

        float effectiveYieldDelay = fluidType == Week10FluidType.HeavyMazeGel ? 0.85f :
                                    fluidType == Week10FluidType.TreeSapResin ? 0.50f : 0f;
        float effectiveSlumpDuration = fluidType == Week10FluidType.HeavyMazeGel ? 4.8f :
                                       fluidType == Week10FluidType.TreeSapResin ? 3.5f : 0.85f;

        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        if (Mathf.Abs(angleDeg) > 0.02f)
        {
            gelSurfaceNormal = deltaRot * gelSurfaceNormal;
            gelSurfaceNormal.Normalize();

            if (effectiveYieldDelay > 0f)
            {
                gelHesitationTimer = effectiveYieldDelay;
            }
        }

        if (gelHesitationTimer > 0f)
        {
            gelHesitationTimer -= dt;
        }
        else
        {
            float sinTheta = Mathf.Sqrt(gelSurfaceNormal.x * gelSurfaceNormal.x + gelSurfaceNormal.z * gelSurfaceNormal.z);
            if (sinTheta > 0.001f)
            {
                float shearRate = Mathf.Pow(sinTheta, 1.2f);
                float slumpSpeedDeg = (90f / Mathf.Max(0.5f, effectiveSlumpDuration)) * shearRate;
                slumpSpeedDeg = Mathf.Max(slumpSpeedDeg, 0.5f);

                gelSurfaceNormal = Vector3.RotateTowards(gelSurfaceNormal, Vector3.up, slumpSpeedDeg * Mathf.Deg2Rad * dt, 0f);
                gelSurfaceNormal.Normalize();
            }
            else
            {
                gelSurfaceNormal = Vector3.up;
            }
        }

        if (gelSurfaceNormal.y < 0.15f)
        {
            gelSurfaceNormal.y = 0.15f;
            gelSurfaceNormal.Normalize();
        }

        float safeNy = Mathf.Max(gelSurfaceNormal.y, 0.15f);
        gelClingZ = Mathf.Clamp(gelSurfaceNormal.x / safeNy, -1.25f, 1.25f);
        gelClingX = Mathf.Clamp(-gelSurfaceNormal.z / safeNy, -1.25f, 1.25f);
    }

    /// <summary>
    /// Updates the Shader Graph cut plane properties for complete fill (0% to 100%).
    /// </summary>
    public void UpdateFillCutPlane(bool forceUpdate = false)
    {
        if (fluidRenderer == null) CacheReferences();
        if (fluidRenderer == null) return;

        float localFillY;
        if (fillAmount >= 0.999f)
        {
            // Complete fill: shift cut plane slightly above the top ceiling to render 100% solid enclosed volume!
            localFillY = localYMax + 0.05f;
            SendShaderProperties(0f, 0f, localFillY);
            return;
        }
        else if (fillAmount <= 0.001f)
        {
            // Fully evacuated: shift cut plane below floor
            localFillY = localYMin - 0.05f;
            SendShaderProperties(0f, 0f, localFillY);
            return;
        }
        else
        {
            localFillY = ComputeGeometryAdaptedSurfaceY(fillAmount);
        }

        float totalWobbleX = wobbleX + gelClingX;
        float totalWobbleZ = wobbleZ + gelClingZ;
        SendShaderProperties(totalWobbleX, totalWobbleZ, localFillY);
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

        if (Application.isEditor && !Application.isPlaying)
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
        // 1. LineRenderer for continuous physical jet stream
        if (outwardJetLine == null)
        {
            Transform existingLine = transform.Find("OutwardJetStream");
            if (existingLine != null) outwardJetLine = existingLine.GetComponent<LineRenderer>();

            if (outwardJetLine == null)
            {
                GameObject jetObj = new GameObject("OutwardJetStream");
                jetObj.transform.SetParent(transform);
                jetObj.transform.localPosition = Vector3.zero;
                outwardJetLine = jetObj.AddComponent<LineRenderer>();
                outwardJetLine.useWorldSpace = true;
                var jetMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                jetMat.color = GetActiveProfile().streamColor;
                jetMat.SetFloat("_Surface", 1f);
                jetMat.SetFloat("_Blend", 0f);
                jetMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                jetMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                jetMat.SetInt("_ZWrite", 0);
                jetMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                jetMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                outwardJetLine.material = jetMat;
                outwardJetLine.startWidth = 0.012f;
                outwardJetLine.endWidth = 0.022f;
                outwardJetLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // 2. Clean up any legacy quad decals
        Transform existingCrack = transform.Find("CrackFractureVisual");
        if (existingCrack != null)
        {
            if (Application.isPlaying) Destroy(existingCrack.gameObject);
            else DestroyImmediate(existingCrack.gameObject);
        }
        crackVisualTransform = null;

        // 3. Splash Particles (Circular Soft-Alpha Droplet Beads - zero white square quads)
        if (splashParticles == null)
        {
            Transform existingSplash = transform.Find("CrackSplashParticles");
            if (existingSplash != null) splashParticles = existingSplash.GetComponent<ParticleSystem>();
            else
            {
                GameObject splashObj = new GameObject("CrackSplashParticles");
                splashObj.transform.SetParent(transform);
                splashObj.transform.localPosition = Vector3.zero;
                splashParticles = splashObj.AddComponent<ParticleSystem>();

                var main = splashParticles.main;
                main.startLifetime = 0.35f;
                main.startSpeed = 0.8f;
                main.startSize = 0.015f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.startColor = GetActiveProfile().streamColor;

                var emission = splashParticles.emission;
                emission.rateOverTime = 30f;

                var shape = splashParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.03f;

                splashParticles.Stop();
            }
        }

        if (splashParticles != null)
        {
            var pRend = splashParticles.GetComponent<ParticleSystemRenderer>();
            if (pRend != null && (pRend.sharedMaterial == null || pRend.sharedMaterial.mainTexture == null))
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                pMat.mainTexture = GetOrCreateDropletTexture();
                pMat.SetFloat("_Surface", 1f);
                pMat.SetFloat("_Blend", 0f);
                pMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                pMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                pMat.SetInt("_ZWrite", 0);
                pMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                pMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                pRend.material = pMat;
            }
        }
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
        bool hadWobble = Mathf.Abs(wobbleX) > 0.0002f || Mathf.Abs(wobbleVelocityX) > 0.0005f ||
                         Mathf.Abs(wobbleZ) > 0.0002f || Mathf.Abs(wobbleVelocityZ) > 0.0005f;

        Vector3 rawVelocity = (currentPos - lastPosition) / dt;
        currentVelocity = Vector3.Lerp(currentVelocity, rawVelocity, Mathf.Clamp01(dt * 8f));
        Vector3 acceleration = (currentVelocity - previousVelocity) / dt;
        acceleration = Vector3.ClampMagnitude(acceleration, 25f);

        Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 rawAngVel = (axis * (angleDeg * Mathf.Deg2Rad)) / dt;
        currentAngularVelocity = Vector3.Lerp(currentAngularVelocity, rawAngVel, Mathf.Clamp01(dt * 8f));

        ComputeFillAmountCoupledDynamics(acceleration, currentAngularVelocity, dt);
        UpdateViscousCreepSlump(deltaRot, dt);
        SimulateFractureOutflow(dt);
        UpdateFillCutPlane(false);

        lastPosition = currentPos;
        lastRotation = currentRot;
        previousVelocity = currentVelocity;
        previousAngularVelocity = currentAngularVelocity;

        bool hasWobble = Mathf.Abs(wobbleX) > 0.0002f || Mathf.Abs(wobbleVelocityX) > 0.0005f ||
                         Mathf.Abs(wobbleZ) > 0.0002f || Mathf.Abs(wobbleVelocityZ) > 0.0005f ||
                         Mathf.Abs(gelClingX) > 0.001f || Mathf.Abs(gelClingZ) > 0.001f || gelHesitationTimer > 0f ||
                         isCracked;

        if (moved || hadWobble || hasWobble)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
#endif

    public Week10FluidProfile GetActiveProfile()
    {
        switch (fluidType)
        {
            case Week10FluidType.Petrol_Gasoline: return ProfilePetrol;
            case Week10FluidType.LiquidMercury: return ProfileMercury;
            case Week10FluidType.SyntheticMotorOil: return ProfileMotorOil;
            case Week10FluidType.HydraulicCoolant: return ProfileCoolant;
            case Week10FluidType.CryogenicLiquidGas: return ProfileCryoGas;
            case Week10FluidType.TreeSapResin: return ProfileTreeSap;
            case Week10FluidType.HeavyMazeGel: return ProfileHeavyGel;
            default: return ProfileCoolant;
        }
    }

    // Telemetry Getters
    public float CurrentFillMagnitudeEnvelope => currentFillMagnitudeEnvelope;
    public float CurrentResonantFrequency => currentResonantFrequency;
    public float CurrentEffectiveDamping => currentEffectiveDamping;
    public float KinematicViscosity => dynamicViscosityPaS / Mathf.Max(1f, densityKgM3);
    public Week10FluidType FluidType => fluidType;
    public Week10StructureType StructureType => structureType;
    public Week10StructureType vesselGeometry => structureType;
    public float Density => densityKgM3;
    public float DynamicViscosity => dynamicViscosityPaS;
    public bool IsCrackSubmerged => isCrackSubmerged;
    public bool IsOutflowing => isCracked && isCrackSubmerged && outwardJetExitVelocity > 0.05f;
    public float HydrostaticHeadMeters => hydrostaticHeadMeters;
    public float CurrentHydrostaticHead => hydrostaticHeadMeters;
    public float OutwardJetExitVelocity => outwardJetExitVelocity;
    public float CurrentExitVelocity => outwardJetExitVelocity;
    public float OutwardFlowRateMilliLitersPerSec => outwardFlowRateMilliLitersPerSec;
    public float CurrentDischargeRateMlPerSec => outwardFlowRateMilliLitersPerSec;
    public bool IsCompleteFill => fillAmount >= 0.999f;
    public bool IsEvacuated => fillAmount <= 0.001f;
}

#if UNITY_EDITOR
[CustomEditor(typeof(Week10FluidCrackingFlowController))]
public class Week10FluidCrackingFlowControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Week10FluidCrackingFlowController sim = (Week10FluidCrackingFlowController)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Week 10 Interactive Control Panel", EditorStyles.boldLabel);

        // Crack / Repair Buttons
        EditorGUILayout.BeginHorizontal();
        if (!sim.isCracked)
        {
            GUI.backgroundColor = new Color(1f, 0.4f, 0.3f);
            if (GUILayout.Button("Crack Enclosure [K]", GUILayout.Height(34)))
            {
                sim.TriggerCrack();
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("Repair & Reseal Enclosure [R]", GUILayout.Height(34)))
            {
                sim.RepairCrack();
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Complete Fill Presets
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Complete Fill Presets (0% to 100%):", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("0% Evacuated")) { sim.fillAmount = 0.0f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        if (GUILayout.Button("25% Shallow")) { sim.fillAmount = 0.25f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        if (GUILayout.Button("50% Peak Slosh")) { sim.fillAmount = 0.50f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        if (GUILayout.Button("75% Deep")) { sim.fillAmount = 0.75f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        if (GUILayout.Button("100% Full Sealed")) { sim.fillAmount = 1.0f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        EditorGUILayout.EndHorizontal();

        // Outflow Telemetry
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Real-Time Outward Flow Telemetry", EditorStyles.boldLabel);
        string crackStatus = !sim.isCracked ? "<color=green>SEALED & INTACT</color>" :
                             sim.IsCrackSubmerged ? $"<color=red><b>FRACTURED & LEAKING</b></color> (Head: {sim.HydrostaticHeadMeters * 100f:F1} cm)" :
                             "<color=orange>CRACKED (ABOVE LIQUID SURFACE - JET CUTOFF)</color>";
        EditorGUILayout.LabelField($"System Integrity: {crackStatus}", new GUIStyle(EditorStyles.label) { richText = true });

        if (sim.isCracked && sim.IsCrackSubmerged)
        {
            EditorGUILayout.LabelField($"Outward Jet Velocity (Torricelli): <b>{sim.OutwardJetExitVelocity:F2} m/s</b>", new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.LabelField($"Discharge Rate: <b>{sim.OutwardFlowRateMilliLitersPerSec:F1} mL/s</b>", new GUIStyle(EditorStyles.label) { richText = true });
        }
    }
}
#endif
