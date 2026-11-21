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

    // Dynamic Top Meniscus Cap (for 100% Realism from ALL views, including top view!)
    private GameObject surfaceMeniscusCapObj = null;
    private MeshFilter surfaceCapMF = null;
    private MeshRenderer surfaceCapMR = null;
    private Material surfaceCapMaterial = null;

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
        new Week10FluidProfile("Heavy Maze Gel", 1420f, 25.0000f, 0.40f, 22.0f, 0.12f, new Color(0.85f, 0.12f, 0.35f, 0.95f));

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
        CalibrateBreachPoint();
        EnsureJetVisuals();
        UpdateFillCutPlane(true);
    }

    private void Start()
    {
        initialFillAmount = 1.0f;
        fillAmount = 1.0f;
        ApplyStructurePreset(structureType);
        CalibrateBreachPoint();
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
        if (surfaceCapMaterial != null)
        {
            if (Application.isPlaying) Destroy(surfaceCapMaterial);
            else DestroyImmediate(surfaceCapMaterial);
            surfaceCapMaterial = null;
        }
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
        if (surfaceCapMaterial != null)
        {
            Color capCol = prof.streamColor;
            if (fluidRenderer != null)
            {
                Material fMat = Application.isPlaying ? fluidRenderer.material : fluidRenderer.sharedMaterial;
                if (fMat != null && fMat.HasProperty("_Color"))
                {
                    Color mc = fMat.GetColor("_Color");
                    if (mc.a > 0.05f) capCol = mc;
                }
            }
            surfaceCapMaterial.color = capCol;
            surfaceCapMaterial.SetFloat("_Smoothness", 0.96f);
            surfaceCapMaterial.SetFloat("_Metallic", prof.densityKgM3 > 5000f ? 0.98f : 0.05f);
        }

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
                crackLocalPosition = new Vector3(0.14f, -0.02f, 0.0f);
                crackLocalNormal = new Vector3(1f, 0f, 0f);
                break;
            case Week10StructureType.StarShapedBox:
                geometricTortuosity = 2.20f;
                totalEnclosedVolumeLiters = 2.0f;
                crackLocalPosition = new Vector3(0.16f, -0.05f, 0.03f);
                crackLocalNormal = new Vector3(0.9f, 0f, 0.4f).normalized;
                break;
            case Week10StructureType.ToroidalTank:
                geometricTortuosity = 1.75f;
                totalEnclosedVolumeLiters = 3.2f;
                crackLocalPosition = new Vector3(0.24f, -0.05f, 0.0f);
                crackLocalNormal = new Vector3(1f, 0f, 0f).normalized;
                break;
            case Week10StructureType.YJunctionManifold:
                geometricTortuosity = 2.45f;
                totalEnclosedVolumeLiters = 2.8f;
                // Crack on lower main trunk manifold well above plinth
                crackLocalPosition = new Vector3(0.06f, -0.05f, 0.0f);
                crackLocalNormal = new Vector3(0.95f, 0.1f, 0f).normalized;
                break;
            case Week10StructureType.CapsuleTank:
                geometricTortuosity = 1.10f;
                totalEnclosedVolumeLiters = 4.0f;
                crackLocalPosition = new Vector3(0.10f, -0.06f, 0.08f);
                crackLocalNormal = new Vector3(0.8f, 0f, 0.6f).normalized;
                break;
            case Week10StructureType.TreeBranchedStructure:
                geometricTortuosity = 2.60f;
                totalEnclosedVolumeLiters = 2.2f;
                // Crack on lower cylindrical trunk wall well above base flange/plinth
                crackLocalPosition = new Vector3(0.055f, -0.06f, 0.0f);
                crackLocalNormal = new Vector3(1.0f, 0.0f, 0f).normalized;
                break;
            case Week10StructureType.MazeMultichamber:
                geometricTortuosity = 3.10f;
                totalEnclosedVolumeLiters = 3.5f;
                // Crack on outer chamber wall well above floor frame
                crackLocalPosition = new Vector3(0.18f, -0.05f, 0.0f);
                crackLocalNormal = new Vector3(1f, 0f, 0f);
                break;
        }
    }

    /// <summary>
    /// Dynamically evaluates the actual glass mesh vertices to position the breach aperture
    /// strictly on the outer vessel wall facing the camera, calibrated to ~38% of container height
    /// so the hole, fracture lines, and streaming jet are prominently visible above the floor/plinth.
    /// </summary>
    public void CalibrateBreachPoint()
    {
        if (glassRenderer == null) CacheReferences();
        if (glassRenderer == null) return;
        MeshFilter mf = glassRenderer.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;

        Bounds b = mf.sharedMesh.bounds;
        // Elevated target height: 38% up the container wall (safely above base flange / floor)
        float targetY = b.min.y + b.size.y * 0.38f;

        // For planar / polygonal vessels, direct outer wall alignment ensures clean placement on the exterior face
        if (structureType == Week10StructureType.MazeMultichamber)
        {
            crackLocalPosition = new Vector3(0.22f, targetY, 0.0f);
            crackLocalNormal = Vector3.right;
            return;
        }
        if (structureType == Week10StructureType.PetrolContainer_Jerrycan)
        {
            crackLocalPosition = new Vector3(0.16f, targetY, 0.0f);
            crackLocalNormal = Vector3.right;
            return;
        }
        if (structureType == Week10StructureType.StarShapedBox)
        {
            crackLocalPosition = new Vector3(0.16f, targetY, 0.03f);
            crackLocalNormal = new Vector3(0.9f, 0f, 0.4f).normalized;
            return;
        }

        Vector3[] verts = mf.sharedMesh.vertices;
        Vector3[] norms = mf.sharedMesh.normals;
        float yMinFilter = b.min.y + b.size.y * 0.25f;
        float yMaxFilter = b.min.y + b.size.y * 0.55f;

        float bestScore = float.MinValue;
        Vector3 bestP = crackLocalPosition;
        Vector3 bestN = crackLocalNormal;
        bool foundCandidate = false;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            // Strictly reject vertices on the base flange, pedestal lip, or floor (< 25% height)
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
            crackLocalPosition = bestP;
            crackLocalNormal = bestN.sqrMagnitude > 0.01f ? bestN.normalized : Vector3.right;
        }
        else
        {
            // For coarse meshes where no vertices lie within the height band, pin Y to targetY
            crackLocalPosition.y = targetY;
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
        CalibrateBreachPoint();
        EnsureJetVisuals();

        Vector3 crackWorldPos = (glassRenderer != null)
            ? glassRenderer.transform.TransformPoint(crackLocalPosition)
            : transform.TransformPoint(crackLocalPosition);
        Vector3 crackWorldNorm = (glassRenderer != null)
            ? glassRenderer.transform.TransformDirection(crackLocalNormal).normalized
            : transform.TransformDirection(crackLocalNormal).normalized;

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

        if (!originalGlassMesh.isReadable)
        {
            orificeLipLocalPos = crackLocalPosition;
            return;
        }

        Vector3[] srcVerts = originalGlassMesh.vertices;
        Vector3[] srcNorms = originalGlassMesh.normals;
        Vector2[] srcUVs = originalGlassMesh.uv;
        int[] srcTris = originalGlassMesh.triangles;
        if (srcVerts == null || srcVerts.Length == 0 || srcTris == null || srcTris.Length == 0) return;

        // 1. Find exact point on glass surface triangles closest to crackLocalPosition
        Vector3 P0 = crackLocalPosition;
        float bestTriDistSq = float.MaxValue;
        Vector3 bestTriNormal = crackLocalNormal;

        for (int i = 0; i < srcTris.Length; i += 3)
        {
            Vector3 vA = srcVerts[srcTris[i]];
            Vector3 vB = srcVerts[srcTris[i + 1]];
            Vector3 vC = srcVerts[srcTris[i + 2]];
            Vector3 pt = ClosestPointOnTriangle(crackLocalPosition, vA, vB, vC);
            float dSq = (pt - crackLocalPosition).sqrMagnitude;
            if (dSq < bestTriDistSq)
            {
                bestTriDistSq = dSq;
                P0 = pt;
                Vector3 triNorm = Vector3.Cross(vB - vA, vC - vA).normalized;
                if (triNorm.sqrMagnitude > 0.01f)
                {
                    if (Vector3.Dot(triNorm, crackLocalNormal) < 0f) triNorm = -triNorm;
                    bestTriNormal = triNorm;
                }
            }
        }

        // 2. Define surface tangent coordinate frame
        Vector3 N = bestTriNormal.sqrMagnitude > 0.01f ? bestTriNormal.normalized : crackLocalNormal.normalized;
        Vector3 Tx = Vector3.Cross(N, Vector3.up);
        if (Tx.sqrMagnitude < 0.001f) Tx = Vector3.Cross(N, Vector3.right);
        Tx.Normalize();
        Vector3 Ty = Vector3.Cross(N, Tx).normalized;

        // 3. Boolean-style triangle excision via noise-perturbed breach radius R(theta)
        float boundsHeight = originalGlassMesh.bounds.size.y;
        float R0 = Mathf.Clamp(boundsHeight * 0.085f, 0.032f, 0.048f);
        float phi = 1.35f;

        // Populate working geometry buffers and subdivide around puncture site on coarse meshes
        List<Vector3> workVerts = new List<Vector3>(srcVerts);
        List<Vector3> workNorms = new List<Vector3>(srcNorms != null && srcNorms.Length == srcVerts.Length ? srcNorms : new Vector3[srcVerts.Length]);
        List<Vector2> workUVs = new List<Vector2>(srcUVs != null && srcUVs.Length == srcVerts.Length ? srcUVs : new Vector2[srcVerts.Length]);
        List<int> workTris = new List<int>(srcTris);

        SubdivideTrianglesNearPoint(workVerts, workNorms, workUVs, workTris, P0, R0 * 2.5f, R0 * 0.45f, 4);

        List<int> excisedTris = new List<int>();
        List<int> retainedTris = new List<int>();

        for (int i = 0; i < workTris.Count; i += 3)
        {
            Vector3 vA = workVerts[workTris[i]];
            Vector3 vB = workVerts[workTris[i + 1]];
            Vector3 vC = workVerts[workTris[i + 2]];
            Vector3 centroid = (vA + vB + vC) / 3f;

            // Restrict culling strictly to within 5.5cm of the front wall surface (prevent piercing through back wall!)
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

            float rThreshold = R0 * (1f + 0.25f * Mathf.Sin(5f * theta + phi) + 0.15f * Mathf.Cos(9f * theta));

            // Also check if any vertex or centroid falls within the breach threshold
            float rA = Mathf.Sqrt(Mathf.Pow(Vector3.Dot(vA - P0, Tx), 2) + Mathf.Pow(Vector3.Dot(vA - P0, Ty), 2));
            float rB = Mathf.Sqrt(Mathf.Pow(Vector3.Dot(vB - P0, Tx), 2) + Mathf.Pow(Vector3.Dot(vB - P0, Ty), 2));
            float rC = Mathf.Sqrt(Mathf.Pow(Vector3.Dot(vC - P0, Tx), 2) + Mathf.Pow(Vector3.Dot(vC - P0, Ty), 2));

            if (r < rThreshold || (rA < rThreshold * 0.85f && rB < rThreshold * 0.85f))
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
        float searchRadius = R0 * 1.8f;

        for (int i = 0; i < retainedTris.Count; i += 3)
        {
            CheckAndAddBoundaryEdge(retainedTris[i], retainedTris[i + 1], edgeDirectedMap, workVerts, P0, searchRadius, boundaryEdges, boundaryVertIndices);
            CheckAndAddBoundaryEdge(retainedTris[i + 1], retainedTris[i + 2], edgeDirectedMap, workVerts, P0, searchRadius, boundaryEdges, boundaryVertIndices);
            CheckAndAddBoundaryEdge(retainedTris[i + 2], retainedTris[i], edgeDirectedMap, workVerts, P0, searchRadius, boundaryEdges, boundaryVertIndices);
        }

        // 5. Build clean punctured glass mesh directly from retained triangles (genuine open hole through glass wall)
        Mesh puncturedMesh = BuildPuncturedMesh(workVerts, workNorms, workUVs, retainedTris, out Dictionary<int, int> remap);
        mf.sharedMesh = puncturedMesh;
        glassRenderer.enabled = true;

        // 6. Find lowest point on the breach rim in mesh local space (P_orifice)
        Vector3 lowestLip = P0;
        float minLipLocalY = float.MaxValue;
        if (boundaryVertIndices.Count > 0)
        {
            foreach (int vIdx in boundaryVertIndices)
            {
                if (workVerts[vIdx].y < minLipLocalY)
                {
                    minLipLocalY = workVerts[vIdx].y;
                    lowestLip = workVerts[vIdx];
                }
            }
        }
        else
        {
            lowestLip = P0 - Ty * (R0 * 0.85f);
        }
        // Guard: ensure orifice lip elevation remains localized to the breach (never plunges below P0.y - R0 * 1.25)
        if (lowestLip.y < P0.y - R0 * 1.25f)
        {
            lowestLip.y = P0.y - R0 * 0.85f;
        }
        orificeLipLocalPos = lowestLip;

        // Remap boundary vertex indices to the newly constructed puncturedMesh vertex indexing
        HashSet<int> remappedBoundaryVertIndices = new HashSet<int>();
        if (boundaryVertIndices != null && remap != null)
        {
            foreach (int oldIdx in boundaryVertIndices)
            {
                if (remap.TryGetValue(oldIdx, out int newIdx))
                {
                    remappedBoundaryVertIndices.Add(newIdx);
                }
            }
        }

        // 7. Generate surface-conforming micro-fractures strictly on mesh triangles
        CreateSurfaceConformingFractures(puncturedMesh, remappedBoundaryVertIndices, P0, R0);
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

    private static void CheckAndAddBoundaryEdge(int a, int b, Dictionary<long, int> directedMap, IList<Vector3> verts, Vector3 center, float searchRadius, List<Vector2Int> boundaryEdges, HashSet<int> boundaryVerts)
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

    private static Mesh BuildPuncturedMesh(List<Vector3> srcVerts, List<Vector3> srcNorms, List<Vector2> srcUVs, List<int> retainedTris, out Dictionary<int, int> remap)
    {
        remap = new Dictionary<int, int>();
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
        mesh.name = "PuncturedGlassVessel";
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

    private void CreateSurfaceConformingFractures(Mesh mesh, HashSet<int> boundaryVerts, Vector3 breachCenter, float breachRadius)
    {
        Vector3[] verts = mesh != null ? mesh.vertices : null;
        if (verts == null || verts.Length == 0) return;
        Vector3[] norms = mesh.normals;
        int[] tris = mesh.triangles;

        // Build vertex adjacency graph from mesh triangles
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

        // Filter valid boundary vertices within range of verts array
        HashSet<int> activeBoundary = new HashSet<int>();
        if (boundaryVerts != null && boundaryVerts.Count > 0)
        {
            foreach (int bIdx in boundaryVerts)
            {
                if (bIdx >= 0 && bIdx < verts.Length)
                {
                    activeBoundary.Add(bIdx);
                }
            }
        }

        // If no valid boundary vertices found, find closest vertices around breach perimeter
        if (activeBoundary.Count == 0)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                float d = Vector3.Distance(verts[i], breachCenter);
                if (d >= breachRadius * 0.75f && d <= breachRadius * 1.6f)
                {
                    activeBoundary.Add(i);
                    if (activeBoundary.Count >= 8) break;
                }
            }
        }

        GameObject microFractureHolder = new GameObject("SurfaceConformingFractures");
        Transform parentT = (glassRenderer != null) ? glassRenderer.transform : transform;
        microFractureHolder.transform.SetParent(parentT, false);
        microFractureHolder.transform.localPosition = Vector3.zero;
        microFractureHolder.transform.localRotation = Quaternion.identity;
        microFractureHolder.transform.localScale = Vector3.one;

        Shader cShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material crackLineMat = new Material(cShader);
        Color cCol = new Color(0.95f, 0.98f, 1.0f, 0.95f);
        crackLineMat.color = cCol;
        if (crackLineMat.HasProperty("_BaseColor")) crackLineMat.SetColor("_BaseColor", cCol);
        if (crackLineMat.HasProperty("_Color")) crackLineMat.SetColor("_Color", cCol);

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
        if (fluidPuddleObj == null)
        {
            fluidPuddleObj = CreateProceduralPuddleObject(col);
            spawnedFractureObjects.Add(fluidPuddleObj);
        }

        // Puddle must always be strictly in world space and aligned horizontally
        if (fluidPuddleObj.transform.parent != null)
        {
            fluidPuddleObj.transform.SetParent(null, true);
        }
        fluidPuddleObj.transform.rotation = Quaternion.identity;

        // Raycast downward from above impactFloorPos to detect shelf or plinth top surface cleanly
        Vector3 rayOrigin = new Vector3(impactFloorPos.x, transform.position.y + 0.30f, impactFloorPos.z);
        float floorY = transform.position.y - 0.22f; // default shelf surface level (-0.07m)
        float maxAllowedFloorY = transform.position.y - 0.18f; // table/shelf can never be above this

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 2.5f);
        float bestFloorY = float.MinValue;
        bool hitGround = false;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            // Ignore container itself, children, shards, puddles, or fracture traces
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.gameObject.name.Contains("Shard") || hit.collider.gameObject.name.Contains("Puddle") || hit.collider.gameObject.name.Contains("Fracture")) continue;

            // Ground/table hit must be strictly at or below container pedestal level
            if (hit.point.y <= maxAllowedFloorY && hit.point.y > bestFloorY)
            {
                bestFloorY = hit.point.y;
                hitGround = true;
            }
        }

        if (hitGround) floorY = bestFloorY;
        floorY = Mathf.Clamp(floorY, -0.085f, maxAllowedFloorY);

        // Position puddle 0.003m (3mm) above the floor surface to guarantee no z-fighting or clipping
        fluidPuddleObj.transform.position = new Vector3(impactFloorPos.x, floorY + 0.003f, impactFloorPos.z);
        float drained = Mathf.Max(0f, initialFillAmount - fillAmount);
        float puddleDiameter = Mathf.Clamp(0.10f + drained * 0.55f, 0.10f, 0.52f);
        fluidPuddleObj.transform.localScale = new Vector3(puddleDiameter, 1f, puddleDiameter);
    }

    private GameObject CreateProceduralPuddleObject(Color col)
    {
        GameObject puddle = new GameObject("FluidOutflowPuddle");
        MeshFilter mf = puddle.AddComponent<MeshFilter>();
        MeshRenderer mr = puddle.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        mr.allowOcclusionWhenDynamic = false;

        const int segs = 36;
        // 3D volumetric meniscus disc: Center top dome, Top rim, Bottom base rim
        Vector3[] pVerts = new Vector3[1 + segs * 2];
        Vector3[] pNorms = new Vector3[1 + segs * 2];
        Vector2[] pUVs = new Vector2[1 + segs * 2];
        int[] pTris = new int[segs * 12];

        // 0: Center top dome vertex (5mm above base)
        pVerts[0] = new Vector3(0f, 0.005f, 0f);
        pNorms[0] = Vector3.up;
        pUVs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segs; i++)
        {
            float theta = ((float)i / segs) * Mathf.PI * 2f;
            float cos = Mathf.Cos(theta);
            float sin = Mathf.Sin(theta);
            // Organic fluid puddle perimeter with slight wavy perturbation
            float r = 0.5f * (1f + 0.05f * Mathf.Sin(3f * theta) + 0.03f * Mathf.Cos(5f * theta));

            // Top rim: 1 + i (at y = 0.0022m)
            pVerts[1 + i] = new Vector3(cos * r, 0.0022f, sin * r);
            Vector3 rimNorm = new Vector3(cos * 0.35f, 0.90f, sin * 0.35f).normalized;
            pNorms[1 + i] = rimNorm;
            pUVs[1 + i] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);

            // Bottom base rim: 1 + segs + i (at y = 0f)
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

            // 1. Top face fan (Clockwise facing UP +Y towards top/oblique camera)
            pTris[triOffset + 0] = 0;
            pTris[triOffset + 1] = topNext;
            pTris[triOffset + 2] = topCurr;

            // 2. Side bevel rim (two triangles facing outward)
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
        var pMat = new Material(litShader);
        pMat.name = "GlossyFluidPuddle_Mat";

        Color pCol = new Color(col.r, col.g, col.b, 0.94f);
        pMat.color = pCol;
        if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", pCol);
        if (pMat.HasProperty("_Color")) pMat.SetColor("_Color", pCol);
        if (pMat.HasProperty("_Smoothness")) pMat.SetFloat("_Smoothness", 0.98f);
        if (pMat.HasProperty("_Metallic")) pMat.SetFloat("_Metallic", densityKgM3 > 5000f ? 0.98f : 0.15f);
        if (pMat.HasProperty("_Cull")) pMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        mr.sharedMaterial = pMat;
        return puddle;
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
    /// Liquid drains only down to y_surface == y_orifice in world space. Tilting automatically resumes outflow!
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
        GetWorldVerticalBounds(out float worldYMin, out float worldYMax);
        float adaptedFill = ComputeAdaptedFillFraction(fillAmount);
        float ySurface = Mathf.Lerp(worldYMin, worldYMax, adaptedFill);

        if (orificeLipLocalPos == Vector3.zero)
        {
            orificeLipLocalPos = crackLocalPosition;
        }

        Vector3 orificeWorldPos = (glassRenderer != null)
            ? glassRenderer.transform.TransformPoint(orificeLipLocalPos)
            : transform.TransformPoint(orificeLipLocalPos);

        Vector3 orificeWorldNorm = (glassRenderer != null)
            ? glassRenderer.transform.TransformDirection(crackLocalNormal).normalized
            : transform.TransformDirection(crackLocalNormal).normalized;

        float yOrifice = orificeWorldPos.y;

        // Hydrostatic Head Delta h in WORLD space
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
        outwardJetExitVelocity = cd * Mathf.Sqrt(2f * 9.81f * deltaH) * (0.35f + 0.65f * trickleFactor);

        // 3. Volumetric Flow Rate & Drainage
        float crackRadiusM = (crackApertureMm * 0.001f) * 0.5f;
        float crackAreaM2 = Mathf.PI * crackRadiusM * crackRadiusM;
        float flowRateM3PerSec = crackAreaM2 * outwardJetExitVelocity;
        outwardFlowRateMilliLitersPerSec = flowRateM3PerSec * 1_000_000f;

        float totalVolM3 = Mathf.Max(0.0005f, totalEnclosedVolumeLiters * 0.001f);
        float baseDrainRate = (flowRateM3PerSec / totalVolM3) * leakDrainMultiplier;
        // Calibrated visible drain rate: provides ~20-25 seconds of glorious visible streaming
        float visibleDrainRate = Mathf.Max(baseDrainRate, (outwardJetExitVelocity / 1.5f) * 0.020f * leakDrainMultiplier);
        float drainFraction = visibleDrainRate * dt;
        fillAmount = Mathf.Clamp01(fillAmount - drainFraction);

        UpdateFillCutPlane(false);

        // 4. Parabolic Jet originating precisely at orifice lip
        RenderOutwardJet(orificeWorldPos, orificeWorldNorm, outwardJetExitVelocity, prof.streamColor, trickleFactor);
    }

    /// <summary>
    /// Renders a physical 3D parabolic fluid jet streaming outward from the lower lip of the orifice.
    /// Uses closed-form ballistic trajectory so points are evenly spaced from hole to plinth landing point.
    /// </summary>
    private void RenderOutwardJet(Vector3 startPos, Vector3 outwardDir, float exitSpeed, Color col, float trickleFactor)
    {
        if (outwardJetLine == null) EnsureJetVisuals();
        if (outwardJetLine == null) return;
        outwardJetLine.enabled = true;

        // 1. Ensure solid, vibrant, non-culled fluid stream material
        Shader lineShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                            ?? Shader.Find("Sprites/Default") 
                            ?? Shader.Find("Unlit/Color");
        if (outwardJetLine.sharedMaterial == null || outwardJetLine.sharedMaterial.shader != lineShader)
        {
            var jMat = new Material(lineShader);
            jMat.name = "VibrantOutflowJet_Mat";
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

        // 2. Closed-form ballistic trajectory calculation to floor/plinth level
        float groundY = transform.position.y - 0.22f; // default showcase shelf level (-0.07m)
        float maxAllowedFloorY = transform.position.y - 0.18f;

        RaycastHit[] hits = Physics.RaycastAll(startPos + Vector3.up * 0.05f + outwardDir * 0.06f, Vector3.down, 2.5f);
        float bestFloorY = float.MinValue;
        bool hitGround = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.gameObject.name.Contains("Shard") || h.collider.gameObject.name.Contains("Puddle") || h.collider.gameObject.name.Contains("Fracture")) continue;

            if (h.point.y <= maxAllowedFloorY && h.point.y > bestFloorY)
            {
                bestFloorY = h.point.y;
                hitGround = true;
            }
        }

        if (hitGround) groundY = bestFloorY;
        groundY = Mathf.Clamp(groundY, -0.085f, maxAllowedFloorY);

        Vector3 v0 = (outwardDir * exitSpeed) + (currentVelocity * 0.25f);
        if (densityKgM3 > 5000f) v0.y -= 0.20f;

        float g = 9.81f;
        float yDrop = Mathf.Max(0.01f, startPos.y - groundY);
        float discr = v0.y * v0.y + 2f * g * yDrop;
        float flightTime = (v0.y + Mathf.Sqrt(Mathf.Max(0f, discr))) / g;
        flightTime = Mathf.Clamp(flightTime, 0.08f, 0.75f);

        const int numPoints = 28;
        if (outwardJetLine.positionCount != numPoints) outwardJetLine.positionCount = numPoints;

        Vector3 landingPos = startPos;
        for (int i = 0; i < numPoints; i++)
        {
            float frac = (float)i / (numPoints - 1);
            float t = flightTime * frac;
            Vector3 p = startPos + v0 * t + 0.5f * new Vector3(0f, -g, 0f) * (t * t);

            // Dynamic turbulent wave offset along stream
            Vector3 waveOffset = Vector3.Cross(v0.normalized, Vector3.up) * (Mathf.Sin(Time.time * 28f + frac * 12f) * 0.0035f * frac);
            outwardJetLine.SetPosition(i, p + waveOffset);

            if (i == numPoints - 1)
            {
                landingPos = p;
                landingPos.y = groundY; // Guarantee stream strictly lands on table/shelf surface!
            }
        }

        // Calibrated rich stream thickness
        float baseWidth = Mathf.Clamp(crackApertureMm * 0.008f, 0.032f, 0.055f);
        outwardJetLine.startWidth = baseWidth * (0.35f + 0.65f * trickleFactor);
        outwardJetLine.endWidth = outwardJetLine.startWidth * 1.55f;

        // Position splash particles at exact floor landing impact
        if (splashParticles != null)
        {
            splashParticles.transform.position = landingPos;
            var main = splashParticles.main;
            main.startColor = solidStreamCol;
            if (!splashParticles.isPlaying) splashParticles.Play();
        }

        // Expand glossy liquid puddle directly beneath jet impact point
        CreateFluidPuddle(landingPos, col);
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

    public float ComputeAdaptedFillFraction(float fill)
    {
        if (fill <= 0.0001f) return 0f;
        if (fill >= 0.9999f) return 1f;

        float range = Mathf.Max(0.001f, localYMax - localYMin);
        float adaptedY = ComputeGeometryAdaptedSurfaceY(fill);
        return Mathf.Clamp01((adaptedY - localYMin) / range);
    }

    public void GetWorldVerticalBounds(out float worldYMin, out float worldYMax)
    {
        Renderer r = fluidRenderer != null ? fluidRenderer : (glassRenderer != null ? glassRenderer : GetComponentInChildren<Renderer>());
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
    /// Updates the Shader Graph cut plane properties for complete fill (0% to 100%)
    /// dynamically adapting to 3D rotation so the fluid surface ALWAYS remains horizontal in world space.
    /// </summary>
    public void UpdateFillCutPlane(bool forceUpdate = false)
    {
        if (fluidRenderer == null) CacheReferences();
        if (fluidRenderer == null) return;

        GetWorldVerticalBounds(out float worldYMin, out float worldYMax);

        float rendererPosY = fluidRenderer.transform.position.y;
        float ySurfaceWorld;
        float floatCutY;

        if (fillAmount >= 0.999f)
        {
            // Complete fill: shift cut plane slightly above the top ceiling to render 100% solid enclosed volume!
            ySurfaceWorld = worldYMax + 0.05f;
            floatCutY = ySurfaceWorld - rendererPosY;
            SendShaderProperties(0f, 0f, floatCutY);
            UpdateSurfaceMeniscusCap(worldYMax - 0.002f, 0f, 0f);
            return;
        }
        else if (fillAmount <= 0.001f)
        {
            // Fully evacuated: shift cut plane below floor
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
        if (fluidType == Week10FluidType.HeavyMazeGel)
        {
            totalWobbleX += gelClingX;
            totalWobbleZ += gelClingZ;
        }
        SendShaderProperties(totalWobbleX, totalWobbleZ, floatCutY);

        // Update Dynamic Top Surface Meniscus Cap for 100% Realism from ALL views (especially top view and 3D rotated views!)
        UpdateSurfaceMeniscusCap(ySurfaceWorld, totalWobbleX, totalWobbleZ);
    }

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

        Week10FluidProfile prof = GetActiveProfile();
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
                Color capCol = prof.streamColor;
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

        // Calculate cut plane in world space (aligned with gravity Vector3.up + wobble):
        Vector3 worldPlaneNormal = (Vector3.up + new Vector3(-wx, 0f, wz)).normalized;
        Vector3 worldPlanePoint = new Vector3(fluidRenderer.transform.position.x, ySurfaceWorld, fluidRenderer.transform.position.z);

        // Transform into mesh local space for MeshPlaneSlicer:
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
            }
        }

        if (outwardJetLine != null)
        {
            outwardJetLine.useWorldSpace = true;
            outwardJetLine.startWidth = 0.022f;
            outwardJetLine.endWidth = 0.038f;
            outwardJetLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (outwardJetLine.sharedMaterial == null || outwardJetLine.sharedMaterial.shader == null || outwardJetLine.sharedMaterial.shader.name.Contains("InternalError"))
            {
                Shader lineShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                var jetMat = new Material(lineShader);
                Color c = GetActiveProfile().streamColor;
                c.a = 1.0f;
                jetMat.color = c;
                if (jetMat.HasProperty("_BaseColor")) jetMat.SetColor("_BaseColor", c);
                if (jetMat.HasProperty("_Color")) jetMat.SetColor("_Color", c);
                outwardJetLine.material = jetMat;
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
                main.startLifetime = 0.45f;
                main.startSpeed = 1.1f;
                main.startSize = 0.018f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.startColor = GetActiveProfile().streamColor;

                var emission = splashParticles.emission;
                emission.rateOverTime = 45f;

                var shape = splashParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.04f;

                splashParticles.Stop();
            }
        }

        if (splashParticles != null)
        {
            var pRend = splashParticles.GetComponent<ParticleSystemRenderer>();
            if (pRend != null && (pRend.sharedMaterial == null || pRend.sharedMaterial.mainTexture == null))
            {
                Shader pShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                Material pMat = new Material(pShader);
                Texture2D dTex = GetOrCreateDropletTexture();
                pMat.mainTexture = dTex;
                if (pMat.HasProperty("_BaseMap")) pMat.SetTexture("_BaseMap", dTex);
                if (pMat.HasProperty("_MainTex")) pMat.SetTexture("_MainTex", dTex);
                Color sCol = GetActiveProfile().streamColor;
                sCol.a = 1.0f;
                pMat.color = sCol;
                if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", sCol);
                if (pMat.HasProperty("_Color")) pMat.SetColor("_Color", sCol);
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
