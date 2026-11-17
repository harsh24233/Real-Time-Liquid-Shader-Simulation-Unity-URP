#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
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
    public float fillAmount = 0.50f;

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
        CacheReferences();
        ApplyFluidPreset(fluidType);
        ApplyStructurePreset(structureType);
        EnsureJetVisuals();
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
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
    }

    public void CacheReferences()
    {
        if (fluidRenderer == null)
        {
            var childFluid = transform.Find("Fluid");
            if (childFluid != null) fluidRenderer = childFluid.GetComponent<Renderer>();
            if (fluidRenderer == null) fluidRenderer = GetComponentInChildren<Renderer>();
        }

        if (glassRenderer == null)
        {
            var childGlass = transform.Find("Glass");
            if (childGlass != null) glassRenderer = childGlass.GetComponent<Renderer>();
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
    /// Cracks the enclosed system, causing outward fluid flow.
    /// </summary>
    [ContextMenu("Crack Enclosure")]
    public void TriggerCrack()
    {
        isCracked = true;
        if (crackVisualTransform != null) crackVisualTransform.gameObject.SetActive(true);
    }

    /// <summary>
    /// Repairs and reseals the structural breach.
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
        if (crackVisualTransform != null) crackVisualTransform.gameObject.SetActive(false);
    }

    public void TriggerStructuralCrack() => TriggerCrack();
    public void RepairStructuralCrack() => RepairCrack();

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
    /// Simulates Torricelli hydrostatic orifice discharge and real-time outward jet flow.
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

        // 1. Calculate World Positions of Crack and Liquid Surface
        Vector3 crackWorldPos = transform.TransformPoint(crackLocalPosition);
        Vector3 crackWorldNorm = transform.TransformDirection(crackLocalNormal).normalized;

        float adaptedLocalY = ComputeGeometryAdaptedSurfaceY(fillAmount);
        Vector3 liquidCenterLocal = new Vector3(0f, adaptedLocalY, 0f);
        Vector3 liquidCenterWorld = transform.TransformPoint(liquidCenterLocal);

        // Plane normal with wobble & slump tilts
        float totalWobbleX = wobbleX + gelClingX;
        float totalWobbleZ = wobbleZ + gelClingZ;
        Vector3 planeNormalLocal = new Vector3(totalWobbleZ, 1f, -totalWobbleX).normalized;
        Vector3 planeNormalWorld = transform.TransformDirection(planeNormalLocal).normalized;

        // Vertical distance from crack to liquid surface in gravity direction
        // delta_h = distance above crack along gravity
        float dotHeight = Vector3.Dot(liquidCenterWorld - crackWorldPos, Vector3.up);
        hydrostaticHeadMeters = Mathf.Max(0f, dotHeight);
        isCrackSubmerged = (dotHeight > 0.001f);

        if (!isCrackSubmerged)
        {
            // Crack is above the liquid surface! Pressurized jet cuts off.
            outwardJetExitVelocity = 0f;
            outwardFlowRateMilliLitersPerSec = 0f;
            if (outwardJetLine != null) outwardJetLine.positionCount = 0;
            if (splashParticles != null && splashParticles.isPlaying) splashParticles.Stop();
            return;
        }

        // 2. Torricelli's Law with Viscous Discharge Coefficient
        // v_exit = Cd * sqrt(2 * g * delta_h)
        Week10FluidProfile prof = GetActiveProfile();
        float g = 9.81f;
        float cd = prof.dischargeCoeffCd;
        outwardJetExitVelocity = cd * Mathf.Sqrt(2f * g * hydrostaticHeadMeters);

        // 3. Volumetric Flow Rate: Q = A_crack * v_exit
        float crackRadiusM = (crackApertureMm * 0.001f) * 0.5f;
        float crackAreaM2 = Mathf.PI * crackRadiusM * crackRadiusM;
        float flowRateM3PerSec = crackAreaM2 * outwardJetExitVelocity;
        outwardFlowRateMilliLitersPerSec = flowRateM3PerSec * 1_000_000f;

        // 4. Mass Conservation Drainage
        // delta_fill = Q * dt / TotalVolume
        float totalVolM3 = Mathf.Max(0.0005f, totalEnclosedVolumeLiters * 0.001f);
        float drainFraction = (flowRateM3PerSec / totalVolM3) * dt * leakDrainMultiplier;
        fillAmount = Mathf.Clamp01(fillAmount - drainFraction);

        // 5. Render Physical Parabolic Outward Jet Stream
        RenderOutwardJet(crackWorldPos, crackWorldNorm, outwardJetExitVelocity, prof.streamColor);
    }

    /// <summary>
    /// Renders a physical 3D parabolic fluid jet streaming outward from the crack under gravity.
    /// </summary>
    private void RenderOutwardJet(Vector3 startPos, Vector3 outwardDir, float exitSpeed, Color col)
    {
        if (outwardJetLine == null) return;

        const int numPoints = 22;
        if (outwardJetLine.positionCount != numPoints)
        {
            outwardJetLine.positionCount = numPoints;
        }

        // Initial velocity combining outward normal thrust and container kinematic momentum
        Vector3 initialVelocity = (outwardDir * exitSpeed) + (currentVelocity * 0.35f);
        // Slightly bias downwards if fluid is heavy (Mercury)
        if (densityKgM3 > 5000f) initialVelocity.y -= 0.25f;

        float timeStep = 0.022f;
        Vector3 currentPos = startPos;
        Vector3 currentVel = initialVelocity;
        Vector3 gravity = new Vector3(0f, -9.81f, 0f);

        // Floor collision level (shelf or ground)
        float floorY = transform.position.y - 0.25f;

        for (int i = 0; i < numPoints; i++)
        {
            outwardJetLine.SetPosition(i, currentPos);

            if (currentPos.y <= floorY && i > 1)
            {
                // Jet has impacted the shelf/ground surface
                for (int j = i; j < numPoints; j++)
                {
                    outwardJetLine.SetPosition(j, currentPos);
                }

                if (splashParticles != null)
                {
                    splashParticles.transform.position = currentPos;
                    if (!splashParticles.isPlaying) splashParticles.Play();
                }
                break;
            }

            currentVel += gravity * timeStep;
            currentPos += currentVel * timeStep;
        }

        outwardJetLine.startWidth = Mathf.Clamp(crackApertureMm * 0.0025f, 0.005f, 0.025f);
        outwardJetLine.endWidth = outwardJetLine.startWidth * 1.6f;
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
                outwardJetLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                outwardJetLine.startWidth = 0.012f;
                outwardJetLine.endWidth = 0.022f;
                outwardJetLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // 2. Crack Visual Transform (fissure decal / indicator)
        if (crackVisualTransform == null)
        {
            Transform existingCrack = transform.Find("CrackFractureVisual");
            if (existingCrack != null) crackVisualTransform = existingCrack;
            else
            {
                GameObject crackObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                crackObj.name = "CrackFractureVisual";
                crackObj.transform.SetParent(transform);
                crackObj.transform.localPosition = crackLocalPosition;
                crackObj.transform.localRotation = Quaternion.LookRotation(crackLocalNormal);
                crackObj.transform.localScale = new Vector3(0.045f, 0.045f, 0.045f);
                DestroyImmediate(crackObj.GetComponent<Collider>());
                crackVisualTransform = crackObj.transform;

                Material crackMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                crackMat.color = new Color(0.95f, 0.2f, 0.1f, 1f); // Emissive crack seam
                crackObj.GetComponent<Renderer>().sharedMaterial = crackMat;
                crackObj.SetActive(isCracked);
            }
        }

        // 3. Splash Particles
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

                var emission = splashParticles.emission;
                emission.rateOverTime = 30f;

                var shape = splashParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.03f;

                splashParticles.Stop();
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
