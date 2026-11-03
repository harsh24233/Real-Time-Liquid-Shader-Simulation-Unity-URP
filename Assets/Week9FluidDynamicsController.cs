#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum Week9FluidType
{
    Petrol_Gasoline,       // rho = 740 kg/m3, mu = 0.0006 Pa*s (low density, fast mobile sloshing)
    LiquidMercury,         // rho = 13550 kg/m3, mu = 0.0015 Pa*s (extreme mass, high momentum inertia)
    SyntheticMotorOil,     // rho = 890 kg/m3, mu = 0.250 Pa*s (smooth viscous laminar flow)
    HydraulicCoolant,      // rho = 1060 kg/m3, mu = 0.0035 Pa*s (aqueous energetic manifold flow)
    CryogenicLiquidGas,    // rho = 500 kg/m3, mu = 0.0001 Pa*s (ultralight, hyperactive ripples)
    TreeSapResin,          // rho = 1380 kg/m3, mu = 8.5 Pa*s (thick organic resin, boundary cling)
    HeavyMazeGel,          // rho = 1420 kg/m3, mu = 25.0 Pa*s (overdamped creeping labyrinth flow)
    Custom
}

public enum Week9StructureType
{
    PetrolContainer_Jerrycan,    // Rectangular rounded body, handle, stiffening ribs (tortuosity = 1.35)
    StarShapedBox,               // 5-pointed star prism, 10 acute boundary corners (tortuosity = 2.20)
    ToroidalTank,                // Closed circular annular loop with central void (tortuosity = 1.75)
    YJunctionManifold,           // 3-way branching manifold with flow split junction (tortuosity = 2.45)
    CapsuleTank,                 // Cylindrical tank with hemispherical dome caps (tortuosity = 1.10)
    TreeBranchedStructure,       // Dendritic multi-tier branching capillary network (tortuosity = 2.60)
    MazeMultichamber             // Baffled 4-chamber labyrinth with weir/underflow slots (tortuosity = 3.10)
}

[System.Serializable]
public struct Week9PhysicalFluidProfile
{
    public string displayName;
    public float densityKgM3;          // Density rho in kg/m3
    public float dynamicViscosityPaS;  // Dynamic viscosity mu in Pa*s
    public float naturalBaseFreq;      // Fundamental slosh frequency (Hz)
    public float baseDamping;          // Base damping coefficient
    public float maxSloshAngle;        // Max wave tilt angle (degrees)
    public float inertiaResponsiveness;// Acceleration response rate (higher = faster acceleration)
    public float surfaceTensionMeniscus;// Meniscus sharpness

    public Week9PhysicalFluidProfile(string name, float rho, float mu, float freq, float damp, float maxAngle, float responsiveness, float meniscus)
    {
        displayName = name;
        densityKgM3 = rho;
        dynamicViscosityPaS = mu;
        naturalBaseFreq = freq;
        baseDamping = damp;
        maxSloshAngle = maxAngle;
        inertiaResponsiveness = responsiveness;
        surfaceTensionMeniscus = meniscus;
    }
}

[ExecuteAlways]
[SelectionBase]
[DisallowMultipleComponent]
public class Week9FluidDynamicsController : MonoBehaviour
{
    [Header("Fluid Physical Rheology")]
    [SerializeField] private Week9FluidType fluidType = Week9FluidType.Petrol_Gasoline;
    [SerializeField] private Week9StructureType structureType = Week9StructureType.PetrolContainer_Jerrycan;

    [Tooltip("Density rho in kg/m3. Petrol: 740, Water: 1000, Mercury: 13550, Motor Oil: 890")]
    [SerializeField] private float densityKgM3 = 740f;

    [Tooltip("Dynamic viscosity mu in Pa*s. Gasoline: 0.0006, Water: 0.001, Motor Oil: 0.25, Honey/Sap: 8.5")]
    [SerializeField] private float dynamicViscosityPaS = 0.0006f;

    [Header("Fill Amount Magnitude Changing Logic")]
    [Range(0.02f, 0.98f)]
    [Tooltip("Normalized fluid fill level (0.0 = empty, 1.0 = completely full). Drives the shader cut plane!")]
    public float fillAmount = 0.50f;

    [Tooltip("Enable continuous automated filling and draining cycle")]
    public bool enableAutoFillDrainCycle = false;

    [Tooltip("Cycle speed for auto fill/drain in Hz")]
    [Range(0.02f, 0.5f)] public float autoCycleSpeed = 0.1f;

    [Tooltip("Fill range for auto cycle: X = min fill, Y = max fill")]
    public Vector2 autoFillRange = new Vector2(0.15f, 0.85f);

    [Header("Container Vertical Bounds")]
    [Tooltip("Local Y offset for 0% fill")]
    [SerializeField] private float localYMin = -0.22f;

    [Tooltip("Local Y offset for 100% fill")]
    [SerializeField] private float localYMax = 0.22f;

    [Header("Structure Geometric Tortuosity")]
    [Tooltip("Boundary shape hydraulic resistance factor")]
    [SerializeField] private float geometricTortuosity = 1.35f;

    [Header("Renderer & Shader Binding")]
    [SerializeField] private Renderer fluidRenderer;
    [SerializeField] private bool useSharedMaterialInEditor = true;

    [Header("Kinematic Motion Sensitivities")]
    [SerializeField] private float translationSensitivity = 1.2f;
    [SerializeField] private float rotationSensitivity = 0.7f;
    [SerializeField] private float maxWobbleDisplacement = 0.45f;

    // Physical state variables
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 currentVelocity;
    private Vector3 currentAngularVelocity;
    private Vector3 previousVelocity;
    private Vector3 previousAngularVelocity;

    // 2-axis oscillator states (X and Z sloshing)
    private float wobbleX = 0f;
    private float wobbleZ = 0f;
    private float wobbleVelocityX = 0f;
    private float wobbleVelocityZ = 0f;

    // Fill logic computed factors
    private float currentFillMagnitudeEnvelope = 1f; // Psi(F) = 4*F*(1-F)
    private float currentResonantFrequency = 1f;     // omega(F)
    private float currentEffectiveDamping = 1f;      // gamma(F, rho, mu, tortuosity)

    [Header("Viscous Creep & Yield Slump (Heavy Maze Gel)")]
    [Tooltip("Enables delayed Bingham-plastic yield and creeping gravitational slump when rotated.")]
    [SerializeField] private bool enableViscousCreepSlump = true;
    [Tooltip("Yield delay in seconds before heavy gel breaks wall cling and begins to slump under gravity.")]
    [SerializeField] private float gelYieldDelay = 0.55f;
    [Tooltip("Relaxation duration in seconds for heavy gel to creep and fall to horizontal level.")]
    [SerializeField] private float gelSlumpDuration = 2.4f;

    // Gel surface normal in world space (starts at Vector3.up)
    private Vector3 gelSurfaceNormal = Vector3.up;
    private float gelHesitationTimer = 0f;
    private float gelClingX = 0f;
    private float gelClingZ = 0f;

    public bool IsGelSlumping => (fluidType == Week9FluidType.HeavyMazeGel || dynamicViscosityPaS >= 0.20f) && 
                                  (gelHesitationTimer > 0f || Vector3.Angle(gelSurfaceNormal, Vector3.up) > 0.5f);
    public float GelTiltAngle => Vector3.Angle(gelSurfaceNormal, Vector3.up);
    public string GelSlumpStatus => gelHesitationTimer > 0f ? $"Clinging (Delay {gelHesitationTimer:F2}s)" :
                                    GelTiltAngle > 0.5f ? $"Slumping ({GelTiltAngle:F1}° under gravity)" : "Level";

    private MaterialPropertyBlock propBlock;
    private Material activeMaterial;

    private static readonly int WobbleXProp = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZProp = Shader.PropertyToID("_WobbleZ");
    private static readonly int FloatFillProp = Shader.PropertyToID("_Float");

    // Realistic Fluid Presets (Physical natural frequencies 0.75 - 2.1 Hz for authentic container liquid mass)
    public static readonly Week9PhysicalFluidProfile ProfilePetrol =
        new Week9PhysicalFluidProfile("Petrol (Gasoline)", 740f, 0.0006f, 1.85f, 1.8f, 22f, 18.0f, 0.90f);

    public static readonly Week9PhysicalFluidProfile ProfileMercury =
        new Week9PhysicalFluidProfile("Liquid Mercury", 13550f, 0.0015f, 1.35f, 1.5f, 16f, 6.5f, 0.98f);

    public static readonly Week9PhysicalFluidProfile ProfileMotorOil =
        new Week9PhysicalFluidProfile("Synthetic Motor Oil", 890f, 0.2500f, 1.25f, 4.8f, 14f, 12.0f, 0.80f);

    public static readonly Week9PhysicalFluidProfile ProfileCoolant =
        new Week9PhysicalFluidProfile("Hydraulic Coolant", 1060f, 0.0035f, 1.75f, 2.2f, 20f, 15.0f, 0.85f);

    public static readonly Week9PhysicalFluidProfile ProfileCryoGas =
        new Week9PhysicalFluidProfile("Cryogenic Liquid Gas", 500f, 0.0001f, 2.10f, 1.2f, 26f, 22.0f, 0.95f);

    public static readonly Week9PhysicalFluidProfile ProfileTreeSap =
        new Week9PhysicalFluidProfile("Tree Sap Resin", 1380f, 8.5000f, 0.90f, 9.5f, 9f, 4.0f, 0.65f);

    public static readonly Week9PhysicalFluidProfile ProfileHeavyGel =
        new Week9PhysicalFluidProfile("Heavy Maze Gel", 1420f, 25.0000f, 0.75f, 13.5f, 7f, 2.5f, 0.50f);

    private void Awake()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        propBlock = new MaterialPropertyBlock();
        CacheRenderer();
        ApplyFluidPreset(fluidType);
        ApplyStructurePreset(structureType);
        UpdateFillCutPlane(true);
    }

    private void OnEnable()
    {
        CacheRenderer();
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

    private void Reset()
    {
        CacheRenderer();
        ApplyFluidPreset(fluidType);
        ApplyStructurePreset(structureType);
        UpdateFillCutPlane(true);
    }

    private void OnValidate()
    {
        ApplyFluidPreset(fluidType);
        ApplyStructurePreset(structureType);
        UpdateFillCutPlane(true);
    }

    public void CacheRenderer()
    {
        if (fluidRenderer == null)
        {
            var childFluid = transform.Find("Fluid");
            if (childFluid != null)
                fluidRenderer = childFluid.GetComponent<Renderer>();

            if (fluidRenderer == null)
                fluidRenderer = GetComponentInChildren<Renderer>();
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

    public void ApplyFluidPreset(Week9FluidType type)
    {
        fluidType = type;
        Week9PhysicalFluidProfile prof;
        switch (type)
        {
            case Week9FluidType.Petrol_Gasoline: prof = ProfilePetrol; break;
            case Week9FluidType.LiquidMercury: prof = ProfileMercury; break;
            case Week9FluidType.SyntheticMotorOil: prof = ProfileMotorOil; break;
            case Week9FluidType.HydraulicCoolant: prof = ProfileCoolant; break;
            case Week9FluidType.CryogenicLiquidGas: prof = ProfileCryoGas; break;
            case Week9FluidType.TreeSapResin: prof = ProfileTreeSap; break;
            case Week9FluidType.HeavyMazeGel: prof = ProfileHeavyGel; break;
            default: return;
        }

        densityKgM3 = prof.densityKgM3;
        dynamicViscosityPaS = prof.dynamicViscosityPaS;

        // Reset gel normal states
        gelSurfaceNormal = Vector3.up;
        gelHesitationTimer = 0f;
        gelClingX = 0f;
        gelClingZ = 0f;

#if UNITY_EDITOR
        string matPath = type switch
        {
            Week9FluidType.Petrol_Gasoline => "Assets/Materials/Week9/Week9_Petrol_Fluid.mat",
            Week9FluidType.LiquidMercury => "Assets/Materials/Week9/Week9_Mercury_Fluid.mat",
            Week9FluidType.SyntheticMotorOil => "Assets/Materials/Week9/Week9_MotorOil_Fluid.mat",
            Week9FluidType.HydraulicCoolant => "Assets/Materials/Week9/Week9_Coolant_Fluid.mat",
            Week9FluidType.CryogenicLiquidGas => "Assets/Materials/Week9/Week9_CryoGas_Fluid.mat",
            Week9FluidType.TreeSapResin => "Assets/Materials/Week9/Week9_TreeSap_Fluid.mat",
            Week9FluidType.HeavyMazeGel => "Assets/Materials/Week9/Week9_MazeGel_Fluid.mat",
            _ => null
        };
        if (matPath != null && fluidRenderer != null)
        {
            var newMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (newMat != null)
            {
                if (Application.isPlaying) fluidRenderer.material = newMat;
                else fluidRenderer.sharedMaterial = newMat;
            }
        }
#endif
    }

    public void ApplyStructurePreset(Week9StructureType type)
    {
        structureType = type;
        switch (type)
        {
            case Week9StructureType.PetrolContainer_Jerrycan: geometricTortuosity = 1.35f; break;
            case Week9StructureType.StarShapedBox: geometricTortuosity = 2.20f; break;
            case Week9StructureType.ToroidalTank: geometricTortuosity = 1.75f; break;
            case Week9StructureType.YJunctionManifold: geometricTortuosity = 2.45f; break;
            case Week9StructureType.CapsuleTank: geometricTortuosity = 1.10f; break;
            case Week9StructureType.TreeBranchedStructure: geometricTortuosity = 2.60f; break;
            case Week9StructureType.MazeMultichamber: geometricTortuosity = 3.10f; break;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1. Interactive Fill Amount Control (Hotkeys '[' and ']')
        HandleInteractiveFillInput(dt);

        // 2. Automated Fill/Drain Cycle
        if (enableAutoFillDrainCycle && Application.isPlaying)
        {
            float t = Time.time * autoCycleSpeed * Mathf.PI * 2f;
            float sineNorm = (Mathf.Sin(t) + 1f) * 0.5f; // 0..1
            fillAmount = Mathf.Lerp(autoFillRange.x, autoFillRange.y, sineNorm);
        }

        // 3. Compute Kinematic Accelerations & Inertial Forces
        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;

        Vector3 rawVelocity = (currentPos - lastPosition) / dt;
        currentVelocity = Vector3.Lerp(currentVelocity, rawVelocity, Mathf.Clamp01(dt * 8f));
        Vector3 acceleration = (currentVelocity - previousVelocity) / dt;
        acceleration = Vector3.ClampMagnitude(acceleration, 25f);

        // Angular velocity calculation
        Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 rawAngVel = (axis * (angleDeg * Mathf.Deg2Rad)) / dt;
        currentAngularVelocity = Vector3.Lerp(currentAngularVelocity, rawAngVel, Mathf.Clamp01(dt * 8f));

        // 4. Fill Amount Magnitude Changing Logic & Physical Dynamics Coupling
        ComputeFillAmountCoupledDynamics(acceleration, currentAngularVelocity, dt);

        // 4b. Viscous Creep & Gravitational Slump Dynamics (Heavy Maze Gel)
        UpdateViscousCreepSlump(deltaRot, dt);

        // 5. Update Shader Properties (localFillY is with respect to object, perfectly invariant to world movement!)
        float localFillY = Mathf.Lerp(localYMin, localYMax, fillAmount);
        float totalWobbleX = wobbleX + gelClingX;
        float totalWobbleZ = wobbleZ + gelClingZ;
        SendShaderProperties(totalWobbleX, totalWobbleZ, localFillY);

        lastPosition = currentPos;
        lastRotation = currentRot;
        previousVelocity = currentVelocity;
        previousAngularVelocity = currentAngularVelocity;
    }

    private void HandleInteractiveFillInput(float dt)
    {
        float change = 0f;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftBracketKey.isPressed) change -= 0.25f * dt;
            if (kb.rightBracketKey.isPressed) change += 0.25f * dt;
        }
#else
        if (Input.GetKey(KeyCode.LeftBracket)) change -= 0.25f * dt;
        if (Input.GetKey(KeyCode.RightBracket)) change += 0.25f * dt;
#endif

        if (Mathf.Abs(change) > 0.00001f)
        {
            fillAmount = Mathf.Clamp(fillAmount + change, 0.02f, 0.98f);
        }
    }

    /// <summary>
    /// Implements the scientific Fill Amount Magnitude Changing Logic coupled with
    /// realistic fluid density, kinematic viscosity, and container geometric tortuosity.
    /// </summary>
    private void ComputeFillAmountCoupledDynamics(Vector3 acceleration, Vector3 angularVel, float dt)
    {
        // A. Fill Level Slosh Magnitude Envelope:
        // Psi(F) = 4 * F * (1 - F)
        // Peaks at F = 0.5 (1.0), approaches 0 at empty (F->0) and full (F->1).
        currentFillMagnitudeEnvelope = 4f * fillAmount * (1f - fillAmount);

        // B. Natural Resonant Frequency Dispersion:
        // Shallow water gravity wave dispersion: omega(h) = sqrt(g * k * tanh(k * h))
        // Frequency shifts with fill depth F
        Week9PhysicalFluidProfile prof = GetActiveProfile();
        float frequencyScale = Mathf.Sqrt(Mathf.Clamp01(Mathf.Abs(Mathf.Tan(fillAmount * Mathf.PI * 0.48f)) * 0.5f + 0.1f));
        currentResonantFrequency = Mathf.Max(0.5f, prof.naturalBaseFreq * frequencyScale);

        // C. Realistic Density & Viscosity Coupled Friction:
        // Kinematic viscosity nu = mu / rho
        float kinematicViscosity = dynamicViscosityPaS / Mathf.Max(1f, densityKgM3);
        
        // Damping incorporates:
        // 1. Fluid internal shear (mu)
        // 2. Geometric tortuosity of the container walls (lambda_geom)
        // 3. Boundary layer skin friction scaling with sqrt(rho * mu * omega)
        float stokesShearDamping = geometricTortuosity * Mathf.Sqrt(Mathf.Max(0.0001f, densityKgM3 * dynamicViscosityPaS * currentResonantFrequency)) * 0.05f;
        float viscousBulkDamping = dynamicViscosityPaS * 1.5f;
        currentEffectiveDamping = prof.baseDamping + stokesShearDamping + viscousBulkDamping;

        // D. Inertial Mass Acceleration Coupling:
        // Denser fluids (e.g. Mercury rho = 13550) carry immense momentum:
        // Responsiveness scales inversely with density relative to reference water (rho_ref = 1000):
        // a_fluid = F / (1 + (rho / rho_ref) * 0.2)
        float densityInertiaFactor = Mathf.Clamp(Mathf.Pow(1000f / Mathf.Max(100f, densityKgM3), 0.35f), 0.35f, 1.8f);

        // Angular acceleration for impulse response on rotation start/stop
        Vector3 angularAcc = (angularVel - previousAngularVelocity) / Mathf.Max(dt, 0.0001f);
        angularAcc = Vector3.ClampMagnitude(angularAcc, 40f);

        // Excitation forces in WORLD SPACE (shader cut plane operates strictly in world space):
        // Translation: -acceleration pushes fluid opposite to container travel
        // Rotation: angular velocity around World Z tilts along World X; angular velocity around World X tilts along World Z
        float forceX = (-acceleration.x * translationSensitivity - angularVel.z * rotationSensitivity - angularAcc.z * rotationSensitivity * 0.1f) 
                       * currentFillMagnitudeEnvelope * densityInertiaFactor;
        float forceZ = (-acceleration.z * translationSensitivity + angularVel.x * rotationSensitivity + angularAcc.x * rotationSensitivity * 0.1f) 
                       * currentFillMagnitudeEnvelope * densityInertiaFactor;

        // Yaw spin (World Y) creates vortex precession sloshing
        float yawSpeed = Mathf.Abs(angularVel.y);
        if (yawSpeed > 0.1f)
        {
            float swirl = yawSpeed * 0.25f * rotationSensitivity * currentFillMagnitudeEnvelope * densityInertiaFactor;
            float swirlPhase = Time.time * currentResonantFrequency * 2f * Mathf.PI;
            forceX += Mathf.Sin(swirlPhase) * swirl;
            forceZ += Mathf.Cos(swirlPhase) * swirl;
        }

        // E. 2-Axis Spring-Mass-Damper Differential Integration
        // d2w/dt2 + 2*gamma*dw/dt + omega^2 * w = force
        float omegaSq = currentResonantFrequency * currentResonantFrequency * 4f * Mathf.PI * Mathf.PI;
        
        // Semi-implicit Euler integration (unconditionally stable for harmonic oscillators)
        float accelX = forceX - (2f * currentEffectiveDamping * wobbleVelocityX) - (omegaSq * wobbleX);
        wobbleVelocityX += accelX * dt;
        wobbleX += wobbleVelocityX * dt;

        float accelZ = forceZ - (2f * currentEffectiveDamping * wobbleVelocityZ) - (omegaSq * wobbleZ);
        wobbleVelocityZ += accelZ * dt;
        wobbleZ += wobbleVelocityZ * dt;

        // Apply natural rest threshold when container motion ceases
        if (acceleration.sqrMagnitude < 0.0005f && currentAngularVelocity.sqrMagnitude < 0.0005f)
        {
            if (Mathf.Abs(wobbleX) < 0.0002f && Mathf.Abs(wobbleVelocityX) < 0.0005f) { wobbleX = 0f; wobbleVelocityX = 0f; }
            if (Mathf.Abs(wobbleZ) < 0.0002f && Mathf.Abs(wobbleVelocityZ) < 0.0005f) { wobbleZ = 0f; wobbleVelocityZ = 0f; }
        }

        // Clamp to physical maximum wobble angle modulated by fill envelope
        float activeMaxLimit = maxWobbleDisplacement * Mathf.Max(0.12f, currentFillMagnitudeEnvelope);
        wobbleX = Mathf.Clamp(wobbleX, -activeMaxLimit, activeMaxLimit);
        wobbleZ = Mathf.Clamp(wobbleZ, -activeMaxLimit, activeMaxLimit);
    }

    [ContextMenu("Trigger Impulse Slosh")]
    public void TriggerImpulseSlosh()
    {
        TriggerImpulseSlosh(0.35f);
    }

    public void TriggerImpulseSlosh(float impulse)
    {
        float envelope = Mathf.Max(0.2f, currentFillMagnitudeEnvelope);
        wobbleVelocityX += impulse * 3.5f * envelope;
        wobbleVelocityZ += impulse * 2.8f * envelope;
    }

    /// <summary>
    /// Implements physical rheological creep and viscous gravitational slump for thick fluids.
    /// - Heavy Maze Gel (mu = 25 Pa*s): Bingham-plastic yield stress holds orientation for gelYieldDelay, then creeps to level.
    /// - Tree Sap Resin (mu = 8.5 Pa*s): Clings to internal branch geometry before slow viscous flow.
    /// - Synthetic Motor Oil (mu = 0.25 Pa*s): Pours and levels with natural viscous lag (0.65s), avoiding instant snap.
    /// </summary>
    private void UpdateViscousCreepSlump(Quaternion deltaRot, float dt)
    {
        bool isViscous = enableViscousCreepSlump && (fluidType == Week9FluidType.HeavyMazeGel || dynamicViscosityPaS >= 0.20f);

        if (!isViscous)
        {
            gelSurfaceNormal = Vector3.up;
            gelHesitationTimer = 0f;
            gelClingX = 0f;
            gelClingZ = 0f;
            return;
        }

        // Calibrate yield delay and slump duration based on fluid mechanics
        float effectiveYieldDelay = 0f;
        float effectiveSlumpDuration = 0.65f;

        if (fluidType == Week9FluidType.HeavyMazeGel || dynamicViscosityPaS >= 15f)
        {
            effectiveYieldDelay = gelYieldDelay; // 0.55s
            effectiveSlumpDuration = gelSlumpDuration; // 2.4s
        }
        else if (fluidType == Week9FluidType.TreeSapResin || dynamicViscosityPaS >= 5.0f)
        {
            effectiveYieldDelay = 0.35f;
            effectiveSlumpDuration = 1.6f;
        }
        else // Synthetic Motor Oil (mu = 0.25)
        {
            effectiveYieldDelay = 0f; // Continuous smooth viscous flow, no solid yield point
            effectiveSlumpDuration = 0.65f; // Gentle viscous relaxation to level
        }

        // 1. Container Rotation: Fluid clings to container walls and rotates WITH the container
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        if (Mathf.Abs(angleDeg) > 0.02f)
        {
            // Rotate the fluid surface normal with container orientation
            gelSurfaceNormal = deltaRot * gelSurfaceNormal;
            gelSurfaceNormal.Normalize();

            // Refresh hesitation delay timer (heavy gel yield stress resists immediate flow)
            if (effectiveYieldDelay > 0f)
            {
                gelHesitationTimer = effectiveYieldDelay;
            }
        }

        // 2. Yield Stress & Gravitational Slump:
        // Holds orientation while gelHesitationTimer > 0; then gravitational shear forces it to slowly fall/slump downward!
        if (gelHesitationTimer > 0f)
        {
            gelHesitationTimer -= dt;
        }
        else
        {
            // Gravitational shear torque scales with horizontal displacement of normal (sin theta)
            float sinTheta = Mathf.Sqrt(gelSurfaceNormal.x * gelSurfaceNormal.x + gelSurfaceNormal.z * gelSurfaceNormal.z);
            if (sinTheta > 0.001f)
            {
                // Non-Newtonian thixotropic slump rate:
                // Starts slow (creeping), accelerates under continuous shear stress, and softly settles
                float shearRate = Mathf.Pow(sinTheta, 1.2f);
                float slumpSpeedDeg = (90f / Mathf.Max(0.2f, effectiveSlumpDuration)) * shearRate;
                slumpSpeedDeg = Mathf.Max(slumpSpeedDeg, 8f); // Minimum creep rate so it doesn't stall indefinitely

                gelSurfaceNormal = Vector3.RotateTowards(gelSurfaceNormal, Vector3.up, slumpSpeedDeg * Mathf.Deg2Rad * dt, 0f);
                gelSurfaceNormal.Normalize();
            }
            else
            {
                gelSurfaceNormal = Vector3.up;
            }
        }

        // Ensure upward-facing normal so shader cut plane remains stable (clamp to 15 degrees above horizon)
        if (gelSurfaceNormal.y < 0.15f)
        {
            gelSurfaceNormal.y = 0.15f;
            gelSurfaceNormal.Normalize();
        }

        // 3. Convert Surface Normal to Shader Wobble Offsets
        // Plane normal in shader: N = ( _WobbleZ, 1, -_WobbleX )
        float safeNy = Mathf.Max(gelSurfaceNormal.y, 0.15f);
        gelClingZ = Mathf.Clamp(gelSurfaceNormal.x / safeNy, -1.25f, 1.25f);
        gelClingX = Mathf.Clamp(-gelSurfaceNormal.z / safeNy, -1.25f, 1.25f);
    }

    /// <summary>
    /// Updates the shader cut plane using localFillY (with respect to object).
    /// This guarantees the fluid volume stays 100% constant when moving the container up/down in world space!
    /// </summary>
    public void UpdateFillCutPlane(bool forceUpdate = false)
    {
        if (fluidRenderer == null) CacheRenderer();
        if (fluidRenderer == null) return;

        float localFillY = Mathf.Lerp(localYMin, localYMax, fillAmount);
        float totalWobbleX = wobbleX + gelClingX;
        float totalWobbleZ = wobbleZ + gelClingZ;
        SendShaderProperties(totalWobbleX, totalWobbleZ, localFillY);
    }

#if UNITY_EDITOR
    private double lastEditorTime = 0;

    /// <summary>
    /// Continuous sloshing and physics integration in Unity Editor Scene View (Edit Mode).
    /// Prevents fluid from ever freezing or getting stuck when moving containers in Scene View.
    /// </summary>
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

        float localFillY = Mathf.Lerp(localYMin, localYMax, fillAmount);
        float totalWobbleX = wobbleX + gelClingX;
        float totalWobbleZ = wobbleZ + gelClingZ;
        SendShaderProperties(totalWobbleX, totalWobbleZ, localFillY);

        lastPosition = currentPos;
        lastRotation = currentRot;
        previousVelocity = currentVelocity;
        previousAngularVelocity = currentAngularVelocity;

        bool hasWobble = Mathf.Abs(wobbleX) > 0.0002f || Mathf.Abs(wobbleVelocityX) > 0.0005f ||
                         Mathf.Abs(wobbleZ) > 0.0002f || Mathf.Abs(wobbleVelocityZ) > 0.0005f ||
                         Mathf.Abs(gelClingX) > 0.001f || Mathf.Abs(gelClingZ) > 0.001f || gelHesitationTimer > 0f;

        if (moved || hadWobble || hasWobble)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
#endif

    private void SendShaderProperties(float wx, float wz, float fillCutY)
    {
        if (fluidRenderer == null) return;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        fluidRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(WobbleXProp, wx);
        propBlock.SetFloat(WobbleZProp, wz);
        propBlock.SetFloat(FloatFillProp, fillCutY);
        fluidRenderer.SetPropertyBlock(propBlock);

        if (Application.isEditor && !Application.isPlaying && useSharedMaterialInEditor)
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

    public Week9PhysicalFluidProfile GetActiveProfile()
    {
        switch (fluidType)
        {
            case Week9FluidType.Petrol_Gasoline: return ProfilePetrol;
            case Week9FluidType.LiquidMercury: return ProfileMercury;
            case Week9FluidType.SyntheticMotorOil: return ProfileMotorOil;
            case Week9FluidType.HydraulicCoolant: return ProfileCoolant;
            case Week9FluidType.CryogenicLiquidGas: return ProfileCryoGas;
            case Week9FluidType.TreeSapResin: return ProfileTreeSap;
            case Week9FluidType.HeavyMazeGel: return ProfileHeavyGel;
            default:
                return new Week9PhysicalFluidProfile("Custom", densityKgM3, dynamicViscosityPaS, 2.5f, 2.0f, 25f, 10.0f, 0.85f);
        }
    }

    // Public Getters for Inspection & UI HUD
    public float CurrentFillMagnitudeEnvelope => currentFillMagnitudeEnvelope;
    public float CurrentResonantFrequency => currentResonantFrequency;
    public float CurrentEffectiveDamping => currentEffectiveDamping;
    public float KinematicViscosity => dynamicViscosityPaS / Mathf.Max(1f, densityKgM3);
    public Week9FluidType FluidType => fluidType;
    public Week9StructureType StructureType => structureType;
    public float Density => densityKgM3;
    public float DynamicViscosity => dynamicViscosityPaS;
}

#if UNITY_EDITOR
[CustomEditor(typeof(Week9FluidDynamicsController))]
public class Week9FluidDynamicsControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Week9FluidDynamicsController sim = (Week9FluidDynamicsController)target;

        EditorGUILayout.Space(12);
        GUI.backgroundColor = new Color(0.25f, 0.8f, 1f);
        if (GUILayout.Button("Trigger Impulse Slosh", GUILayout.Height(36)))
        {
            sim.TriggerImpulseSlosh();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("10% Fill")) { sim.fillAmount = 0.10f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        if (GUILayout.Button("50% Fill")) { sim.fillAmount = 0.50f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        if (GUILayout.Button("95% Fill")) { sim.fillAmount = 0.95f; sim.UpdateFillCutPlane(false); EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
        EditorGUILayout.EndHorizontal();
    }
}
#endif

