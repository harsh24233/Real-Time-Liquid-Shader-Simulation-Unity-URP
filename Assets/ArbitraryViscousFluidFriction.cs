#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum FluidSimType
{
    Water,
    VegetableOil,
    Honey,
    Ketchup,
    Milk,
    Custom
}

public enum SurfaceFrictionPreset
{
    PolishedGlass,     // Ultra-smooth borosilicate glass: minimal friction (f = 0.15)
    StandardGlass,     // Typical commercial container glass (f = 0.35)
    FrostedGlass,      // Etched / sandblasted glass: high surface roughness (f = 0.65)
    TexturedResin,     // 3D printed / matte polymer: very high boundary drag (f = 0.88)
    CustomFriction
}

public enum ArbitraryGeometryType
{
    Hourglass_Constriction,      // Pinched hyperbolic waist: high shear at constriction (lambda = 1.85)
    Toroid_DualWallLoop,         // Enclosed annular loop: dual boundary walls (lambda = 1.70)
    Triangular_PrismVertices,    // 3-sided prism: sharp tri-corners (lambda = 1.60)
    Teardrop_AerodynamicSmooth,  // Smooth continuous drop profile: low form drag (lambda = 1.15)
    Octagonal_FacetedEdges,      // 8-sided faceted prism: faceted corner eddies (lambda = 1.40)
    TwistedColumn_HelicalShear,  // Twisted 180° column: helical spiral wall shear (lambda = 1.95)
    DualBulb_ChamberOrifice,     // Stacked lobes: inter-chamber orifice throttling (lambda = 1.90)
    CardioidHeart_CreaseBoundary,// Heart cardioid: internal concave crease friction (lambda = 1.65)
    StarPrism_AcuteCornerDrag,   // 5-pointed star: 10 acute corners with high skin friction (lambda = 2.10)
    AlchemicalRetort_NeckThrottling,// Angled distillation spout: lateral boundary shear (lambda = 1.80)
    DiamondGem_FacetedBipyramidal,// Octahedral diamond: multi-faceted boundary planes (lambda = 1.75)
    EllipticalCanteen_RibbedWall, // Flattened canteen: horizontal corrugated rib friction (lambda = 2.05)
    CustomGeometry
}

[System.Serializable]
public struct PhysicalFluidProfile
{
    public string displayName;
    public float densityKgM3;      // Density rho in kg/m3
    public float dynamicViscosityPaS;// Viscosity mu in Pa*s
    public float naturalBaseFreq;  // Fundamental slosh frequency (Hz)
    public float baseDamping;      // Base linear damping
    public float maxSloshAngle;    // Max tilt limit
    public float wallAdhesionFactor;// Meniscus cling factor
    public float moveSensitivity;
    public float rotateSensitivity;

    public PhysicalFluidProfile(string name, float rho, float mu, float freq, float damp, float maxAngle, float cling, float moveSens, float rotSens)
    {
        displayName = name;
        densityKgM3 = rho;
        dynamicViscosityPaS = mu;
        naturalBaseFreq = freq;
        baseDamping = damp;
        maxSloshAngle = maxAngle;
        wallAdhesionFactor = cling;
        moveSensitivity = moveSens;
        rotateSensitivity = rotSens;
    }
}

/// <summary>
/// Advanced Real-Time Liquid Wobble & Boundary Friction Controller for Arbitrary Geometry Containers.
/// Incorporates Navier-Stokes boundary shear, Stokes boundary layer thickness,
/// fluid density (rho), dynamic viscosity (mu), surface roughness (f_surface),
/// and arbitrary geometry tortuosity / shape factor (lambda_geom).
/// </summary>
[ExecuteAlways]
[SelectionBase]
public class ArbitraryViscousFluidFriction : MonoBehaviour
{
    [Header("1. Fluid Physics & Rheology")]
    [Tooltip("Select fluid type (Water, Veg Oil, Honey, Ketchup, Milk, or Custom).")]
    [SerializeField] private FluidSimType fluidType = FluidSimType.Water;
    public FluidSimType FluidType => fluidType;

    [Tooltip("Physical density rho of the fluid (kg/m3).")]
    [SerializeField] private float fluidDensity = 1000f;

    [Tooltip("Dynamic viscosity mu of the fluid (Pa*s).")]
    [SerializeField] private float dynamicViscosity = 0.001f;

    [Tooltip("Calculated kinematic viscosity nu = mu / rho (m2/s).")]
    [SerializeField] private float kinematicViscosity = 1e-6f;

    [Header("2. Arbitrary Geometry & Surface Friction")]
    [Tooltip("Surface roughness / wall friction characteristic of the enclosed container.")]
    [SerializeField] private SurfaceFrictionPreset surfaceMaterial = SurfaceFrictionPreset.StandardGlass;

    [Range(0.05f, 1.5f)]
    [Tooltip("Interior wall friction coefficient (f_surface). Higher values resist boundary fluid flow.")]
    [SerializeField] private float surfaceFrictionCoeff = 0.35f;

    [Tooltip("Arbitrary geometry enclosure type.")]
    [SerializeField] private ArbitraryGeometryType geometryType = ArbitraryGeometryType.Hourglass_Constriction;

    [Range(0.5f, 3.5f)]
    [Tooltip("Geometric tortuosity & hydraulic friction shape factor (lambda_geom). Accounts for internal corners, narrow channels, and surface-area-to-volume ratio.")]
    [SerializeField] private float geometryFrictionFactor = 1.85f;

    [Range(0f, 1f)]
    [Tooltip("Wall adhesion / meniscus lag. Fluid clings to container walls before sliding back down.")]
    [SerializeField] private float wallAdhesion = 0.25f;

    [Header("3. Visual & Mesh References")]
    [Tooltip("Root transform of the container to track movement from.")]
    [SerializeField] private Transform containerTransform;

    [Tooltip("MeshRenderer of the inner fluid mesh.")]
    [SerializeField] private Renderer fluidRenderer;

    [Tooltip("MeshRenderer of the outer glass enclosure (optional).")]
    [SerializeField] private Renderer glassRenderer;

    [Header("4. Material Bindings")]
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Material vegOilMaterial;
    [SerializeField] private Material honeyMaterial;
    [SerializeField] private Material ketchupMaterial;
    [SerializeField] private Material milkMaterial;

    [Header("5. Dynamic Slosh Response")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float maxWobble = 0.65f;

    [Range(0.2f, 8f)]
    [SerializeField] private float currentFrequency = 3.0f;

    [Range(0.1f, 6f)]
    [SerializeField] private float decayDuration = 2.0f;

    [Range(0.1f, 5f)]
    [SerializeField] private float moveSensitivity = 1.8f;

    [Range(0.1f, 5f)]
    [SerializeField] private float rotateSensitivity = 1.0f;

    [Header("6. Real-Time Telemetry (Read-Only)")]
    [SerializeField] private float currentSloshAngleX = 0f;
    [SerializeField] private float currentSloshAngleZ = 0f;
    [SerializeField] private float effectiveDampingRatio = 0f;
    [SerializeField] private float totalWallDragResistance = 0f;

    [Header("7. Interactive Controls (Play Mode)")]
    [SerializeField] private bool enableKeyboardControls = false;
    [SerializeField] private bool enableMouseDrag = true;
    [SerializeField] private float moveSpeed = 3f;

    private Material activeMaterial;
    private MaterialPropertyBlock propBlock;

    private static readonly int WobbleXProp = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZProp = Shader.PropertyToID("_WobbleZ");

    private Vector3 lastPos;
    private Vector3 lastRot;
    private Vector3 smoothedVelocity;
    private Vector3 smoothedAngularVelocity;
    private Vector3 lastVelocity;

    private float wobbleAmountX = 0f;
    private float wobbleAmountZ = 0f;
    private float wobbleVelocityX = 0f;
    private float wobbleVelocityZ = 0f;

    // Meniscus wall adhesion cling offset
    private float wallClingX = 0f;
    private float wallClingZ = 0f;

    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 dragOffset;

    private FluidSimType lastConfiguredFluid = (FluidSimType)(-1);
    private SurfaceFrictionPreset lastConfiguredSurface = (SurfaceFrictionPreset)(-1);
    private ArbitraryGeometryType lastConfiguredGeom = (ArbitraryGeometryType)(-1);

#if UNITY_EDITOR
    private double lastEditorTime = 0;
#endif

    // Pre-calibrated Physical Profiles based on Real-World Fluid Mechanics
    public static readonly PhysicalFluidProfile WaterProfile =
        new PhysicalFluidProfile("Water", 1000f, 0.001f, 3.4f, 1.8f, 0.65f, 0.15f, 1.8f, 1.0f);

    public static readonly PhysicalFluidProfile VegOilProfile =
        new PhysicalFluidProfile("Vegetable Oil", 920f, 0.050f, 2.3f, 1.2f, 0.45f, 0.40f, 1.3f, 0.65f);

    public static readonly PhysicalFluidProfile HoneyProfile =
        new PhysicalFluidProfile("Honey", 1420f, 10.0f, 1.1f, 0.45f, 0.30f, 0.85f, 0.7f, 0.35f);

    public static readonly PhysicalFluidProfile KetchupProfile =
        new PhysicalFluidProfile("Ketchup", 1150f, 50.0f, 0.70f, 0.22f, 0.20f, 0.95f, 0.4f, 0.20f);

    public static readonly PhysicalFluidProfile MilkProfile =
        new PhysicalFluidProfile("Milk", 1030f, 0.003f, 3.1f, 1.6f, 0.60f, 0.22f, 1.6f, 0.90f);

    private void Awake()
    {
        ResolveReferences();
        ApplyPhysicsModel(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyPhysicsModel(false);
        ResetTracking();

#if UNITY_EDITOR
        lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
        ApplyWobble(0f, 0f);
    }

    private void OnDestroy()
    {
        ApplyWobble(0f, 0f);
        if (Application.isPlaying && activeMaterial != null && fluidRenderer != null && activeMaterial != fluidRenderer.sharedMaterial)
        {
            Destroy(activeMaterial);
            activeMaterial = null;
        }
    }

    private void OnValidate()
    {
        if (fluidType != lastConfiguredFluid || surfaceMaterial != lastConfiguredSurface || geometryType != lastConfiguredGeom)
        {
            ApplyPhysicsModel(true);
        }
    }

    public void SetFluid(FluidSimType type)
    {
        fluidType = type;
        ApplyPhysicsModel(true);
    }

    public void SetSurfaceFriction(SurfaceFrictionPreset preset)
    {
        surfaceMaterial = preset;
        ApplyPhysicsModel(false);
    }

    public void SetGeometryType(ArbitraryGeometryType type)
    {
        geometryType = type;
        ApplyPhysicsModel(false);
    }

    /// <summary>
    /// Computes physical damping, frequency, and boundary shear based on fluid rheology,
    /// surface friction coefficient, and arbitrary geometry tortuosity factor.
    /// </summary>
    public void ApplyPhysicsModel(bool updateMaterial)
    {
        // 1. Fluid Rheology
        PhysicalFluidProfile p;
        Material targetMat = null;

        switch (fluidType)
        {
            case FluidSimType.Water:
                p = WaterProfile;
                targetMat = waterMaterial;
                break;
            case FluidSimType.VegetableOil:
                p = VegOilProfile;
                targetMat = vegOilMaterial;
                break;
            case FluidSimType.Honey:
                p = HoneyProfile;
                targetMat = honeyMaterial;
                break;
            case FluidSimType.Ketchup:
                p = KetchupProfile;
                targetMat = ketchupMaterial;
                break;
            case FluidSimType.Milk:
                p = MilkProfile;
                targetMat = milkMaterial;
                break;
            default:
                p = new PhysicalFluidProfile("Custom", fluidDensity, dynamicViscosity, currentFrequency, decayDuration, maxWobble, wallAdhesion, moveSensitivity, rotateSensitivity);
                break;
        }

        if (fluidType != FluidSimType.Custom)
        {
            fluidDensity = p.densityKgM3;
            dynamicViscosity = p.dynamicViscosityPaS;
            kinematicViscosity = dynamicViscosity / Mathf.Max(fluidDensity, 1f);
            maxWobble = p.maxSloshAngle;
            moveSensitivity = p.moveSensitivity;
            rotateSensitivity = p.rotateSensitivity;
            wallAdhesion = p.wallAdhesionFactor;
        }

        // 2. Surface Material Friction Coefficient
        switch (surfaceMaterial)
        {
            case SurfaceFrictionPreset.PolishedGlass:
                surfaceFrictionCoeff = 0.15f;
                break;
            case SurfaceFrictionPreset.StandardGlass:
                surfaceFrictionCoeff = 0.35f;
                break;
            case SurfaceFrictionPreset.FrostedGlass:
                surfaceFrictionCoeff = 0.65f;
                break;
            case SurfaceFrictionPreset.TexturedResin:
                surfaceFrictionCoeff = 0.88f;
                break;
        }

        // 3. Arbitrary Geometry Tortuosity & Friction Factor
        switch (geometryType)
        {
            case ArbitraryGeometryType.Hourglass_Constriction:
                geometryFrictionFactor = 1.85f;
                break;
            case ArbitraryGeometryType.Toroid_DualWallLoop:
                geometryFrictionFactor = 1.70f;
                break;
            case ArbitraryGeometryType.Triangular_PrismVertices:
                geometryFrictionFactor = 1.60f;
                break;
            case ArbitraryGeometryType.Teardrop_AerodynamicSmooth:
                geometryFrictionFactor = 1.15f;
                break;
            case ArbitraryGeometryType.Octagonal_FacetedEdges:
                geometryFrictionFactor = 1.40f;
                break;
            case ArbitraryGeometryType.TwistedColumn_HelicalShear:
                geometryFrictionFactor = 1.95f;
                break;
            case ArbitraryGeometryType.DualBulb_ChamberOrifice:
                geometryFrictionFactor = 1.90f;
                break;
            case ArbitraryGeometryType.CardioidHeart_CreaseBoundary:
                geometryFrictionFactor = 1.65f;
                break;
            case ArbitraryGeometryType.StarPrism_AcuteCornerDrag:
                geometryFrictionFactor = 2.10f;
                break;
            case ArbitraryGeometryType.AlchemicalRetort_NeckThrottling:
                geometryFrictionFactor = 1.80f;
                break;
            case ArbitraryGeometryType.DiamondGem_FacetedBipyramidal:
                geometryFrictionFactor = 1.75f;
                break;
            case ArbitraryGeometryType.EllipticalCanteen_RibbedWall:
                geometryFrictionFactor = 2.05f;
                break;
        }

        // 4. Physical Coupling:
        // Stokes Oscillating Boundary Layer Resistance: F_wall = f_surface * lambda_geom * sqrt(rho * mu * omega)
        float baseFreq = p.naturalBaseFreq;
        float boundaryShearResistance = surfaceFrictionCoeff * geometryFrictionFactor * Mathf.Sqrt(Mathf.Max(fluidDensity * dynamicViscosity * baseFreq, 1e-6f));
        totalWallDragResistance = boundaryShearResistance;

        // Effective slosh frequency scales down as internal boundary drag and viscosity increase
        float frequencyDampingFactor = 1.0f / (1.0f + 0.12f * boundaryShearResistance);
        currentFrequency = baseFreq * frequencyDampingFactor;

        // Effective decay duration settles faster with high surface friction and viscosity
        float frictionDecayReduction = 1.0f + 0.85f * boundaryShearResistance + (dynamicViscosity * 0.1f);
        decayDuration = Mathf.Max(p.baseDamping / frictionDecayReduction, 0.15f);

        // Effective damping ratio zeta readout
        effectiveDampingRatio = Mathf.Clamp01(1.0f / (decayDuration * currentFrequency * 2.0f * Mathf.PI));

        lastConfiguredFluid = fluidType;
        lastConfiguredSurface = surfaceMaterial;
        lastConfiguredGeom = geometryType;

        // 5. Material assignment
        if (updateMaterial && targetMat != null && fluidRenderer != null)
        {
            fluidRenderer.sharedMaterial = targetMat;
            if (Application.isPlaying)
            {
                activeMaterial = fluidRenderer.material;
            }
            else
            {
                activeMaterial = fluidRenderer.sharedMaterial;
            }
        }
    }

    private void ResetTracking()
    {
        if (containerTransform != null)
        {
            lastPos = containerTransform.position;
            lastRot = containerTransform.eulerAngles;
        }
        smoothedVelocity = Vector3.zero;
        smoothedAngularVelocity = Vector3.zero;
        lastVelocity = Vector3.zero;
        wobbleAmountX = 0f;
        wobbleAmountZ = 0f;
        wobbleVelocityX = 0f;
        wobbleVelocityZ = 0f;
        wallClingX = 0f;
        wallClingZ = 0f;
    }

    private void ResolveReferences()
    {
        if (containerTransform == null)
        {
            containerTransform = transform;
        }

        if (fluidRenderer == null)
        {
            // Look for child liquid renderer
            Renderer[] rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                string n = r.gameObject.name.ToLower();
                if (r.transform != transform && !n.Contains("glass") && !n.Contains("shelf"))
                {
                    fluidRenderer = r;
                    break;
                }
            }
        }

        if (glassRenderer == null)
        {
            Renderer[] rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r.gameObject.name.ToLower().Contains("glass"))
                {
                    glassRenderer = r;
                    break;
                }
            }
        }

        // Auto-load fluid materials if needed
        if (waterMaterial == null) waterMaterial = LoadMaterial("Water_Fluid");
        if (vegOilMaterial == null) vegOilMaterial = LoadMaterial("VegOil_Fluid");
        if (honeyMaterial == null) honeyMaterial = LoadMaterial("Honey_Fluid");
        if (ketchupMaterial == null) ketchupMaterial = LoadMaterial("Ketchup_Fluid");
        if (milkMaterial == null) milkMaterial = LoadMaterial("Milk_Fluid");

        if (fluidRenderer != null)
        {
            if (Application.isPlaying)
            {
                if (activeMaterial == null || activeMaterial == fluidRenderer.sharedMaterial)
                {
                    activeMaterial = fluidRenderer.material;
                }
            }
            else
            {
                activeMaterial = fluidRenderer.sharedMaterial;
            }
        }
    }

    private Material LoadMaterial(string matName)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Material>($"Assets/{matName}.mat");
#else
        return null;
#endif
    }

    private void Start()
    {
        ResolveReferences();
        ApplyPhysicsModel(false);
        if (containerTransform != null)
        {
            lastPos = containerTransform.position;
            lastRot = containerTransform.eulerAngles;
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            HandlePlayerInput();
            SimulateFluidMotion(Time.deltaTime);
        }
    }

#if UNITY_EDITOR
    private void OnEditorUpdate()
    {
        if (!Application.isPlaying)
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = lastEditorTime > 0 ? Mathf.Clamp((float)(now - lastEditorTime), 0.001f, 0.05f) : 0.016f;
            lastEditorTime = now;

            bool hadWobble = Mathf.Abs(wobbleAmountX) > 0.0002f || Mathf.Abs(wobbleVelocityX) > 0.0005f ||
                             Mathf.Abs(wobbleAmountZ) > 0.0002f || Mathf.Abs(wobbleVelocityZ) > 0.0005f;
            bool moved = containerTransform != null && (containerTransform.position != lastPos || containerTransform.eulerAngles != lastRot);

            SimulateFluidMotion(dt);

            bool hasWobble = Mathf.Abs(wobbleAmountX) > 0.0002f || Mathf.Abs(wobbleVelocityX) > 0.0005f ||
                             Mathf.Abs(wobbleAmountZ) > 0.0002f || Mathf.Abs(wobbleVelocityZ) > 0.0005f;
            if (moved || hadWobble || hasWobble)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }
    }
#endif

    private void HandlePlayerInput()
    {
        if (containerTransform == null) return;

        // If ShowcaseCameraController is present, it centrally manages player camera movement and bottle authority.
        if (Camera.main != null && Camera.main.GetComponent<ShowcaseCameraController>() != null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Number keys 1-5 to switch fluid profile
            if (keyboard.digit1Key.wasPressedThisFrame) SetFluid(FluidSimType.Water);
            if (keyboard.digit2Key.wasPressedThisFrame) SetFluid(FluidSimType.VegetableOil);
            if (keyboard.digit3Key.wasPressedThisFrame) SetFluid(FluidSimType.Honey);
            if (keyboard.digit4Key.wasPressedThisFrame) SetFluid(FluidSimType.Ketchup);
            if (keyboard.digit5Key.wasPressedThisFrame) SetFluid(FluidSimType.Milk);

            // Space key to trigger impulse slosh
            if (keyboard.spaceKey.wasPressedThisFrame) TriggerImpulseSlosh();

            if (enableKeyboardControls)
            {
                float h = 0f;
                float v = 0f;

                if (keyboard.leftArrowKey.isPressed) h -= 1f;
                if (keyboard.rightArrowKey.isPressed) h += 1f;
                if (keyboard.upArrowKey.isPressed) v += 1f;
                if (keyboard.downArrowKey.isPressed) v -= 1f;

                if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
                {
                    Vector3 moveDir = new Vector3(h, 0f, v).normalized;
                    containerTransform.position += moveDir * moveSpeed * Time.deltaTime;
                }

                if (keyboard.qKey.isPressed) containerTransform.Rotate(Vector3.forward, 90f * Time.deltaTime, Space.World);
                if (keyboard.eKey.isPressed) containerTransform.Rotate(Vector3.forward, -90f * Time.deltaTime, Space.World);
            }
        }

        var mouse = Mouse.current;
        if (enableMouseDrag && mouse != null && Camera.main != null)
        {
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragPlane = new Plane(Camera.main.transform.forward, containerTransform.position);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    if (Vector3.Distance(hitPoint, containerTransform.position) < 3.5f)
                    {
                        isDragging = true;
                        dragOffset = containerTransform.position - hitPoint;
                    }
                }
            }
            else if (mouse.leftButton.isPressed && isDragging)
            {
                if (dragPlane.Raycast(ray, out float enter))
                {
                    containerTransform.position = ray.GetPoint(enter) + dragOffset;
                }
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }
        }
#endif
    }

    /// <summary>
    /// Evaluates fluid inertial momentum, boundary wall shear, geometry skin friction,
    /// and meniscus adhesion lag over time step dt.
    /// </summary>
    private void SimulateFluidMotion(float dt)
    {
        if (containerTransform == null)
        {
            ResolveReferences();
            if (containerTransform == null) return;
            ResetTracking();
            return;
        }

        if (fluidRenderer == null)
        {
            ResolveReferences();
            if (fluidRenderer == null) return;
        }

        if (dt <= 0f) return;
        dt = Mathf.Clamp(dt, 0.001f, 0.05f);

        // 1. Calculate linear and angular kinematics safely with exponential smoothing
        Vector3 currentPos = containerTransform.position;
        Vector3 currentRot = containerTransform.eulerAngles;

        Vector3 rawVelocity = (currentPos - lastPos) / dt;
        Vector3 rawAngularVelocity = new Vector3(
            Mathf.DeltaAngle(lastRot.x, currentRot.x),
            Mathf.DeltaAngle(lastRot.y, currentRot.y),
            Mathf.DeltaAngle(lastRot.z, currentRot.z)
        ) * Mathf.Deg2Rad / dt;

        float filterWeight = Mathf.Clamp01(dt * 20f);
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, filterWeight);
        smoothedAngularVelocity = Vector3.Lerp(smoothedAngularVelocity, rawAngularVelocity, filterWeight);

        Vector3 acceleration = (smoothedVelocity - lastVelocity) / dt;
        acceleration = Vector3.ClampMagnitude(acceleration, 25f);

        // 2. Physical inertial slosh target:
        // Fluid inertia scales with Density (rho). Denser fluids carry higher mass momentum under acceleration.
        float densityMomentumFactor = Mathf.Clamp(fluidDensity / 1000f, 0.7f, 1.4f);

        float targetTiltX = Mathf.Clamp(
            (-smoothedVelocity.z * 0.22f - acceleration.z * 0.035f) * moveSensitivity * densityMomentumFactor +
            (smoothedAngularVelocity.x * rotateSensitivity * 0.35f),
            -maxWobble, maxWobble
        );

        float targetTiltZ = Mathf.Clamp(
            (smoothedVelocity.x * 0.22f + acceleration.x * 0.035f) * moveSensitivity * densityMomentumFactor -
            (smoothedAngularVelocity.z * rotateSensitivity * 0.35f),
            -maxWobble, maxWobble
        );

        // 3. Physical Coupling of Surface Friction & Arbitrary Geometry Tortuosity:
        float omegaN = 2f * Mathf.PI * Mathf.Max(currentFrequency, 0.1f);

        float boundaryShearResistance = surfaceFrictionCoeff * geometryFrictionFactor *
            Mathf.Sqrt(Mathf.Max(fluidDensity * dynamicViscosity * currentFrequency, 1e-6f));
        totalWallDragResistance = boundaryShearResistance;

        // Effective damping rate gamma
        float gamma = (4.2f / Mathf.Max(decayDuration, 0.05f)) * (1.0f + 0.35f * boundaryShearResistance);
        effectiveDampingRatio = Mathf.Clamp01(gamma / (2f * omegaN));

        // 4. Exact Analytical Damped Harmonic Oscillator Integration
        UpdateOscillator(targetTiltX, omegaN, gamma, dt, ref wobbleAmountX, ref wobbleVelocityX);
        UpdateOscillator(targetTiltZ, omegaN, gamma, dt, ref wobbleAmountZ, ref wobbleVelocityZ);

        wobbleAmountX = Mathf.Clamp(wobbleAmountX, -maxWobble, maxWobble);
        wobbleAmountZ = Mathf.Clamp(wobbleAmountZ, -maxWobble, maxWobble);

        // 5. Meniscus Wall Adhesion Cling:
        if (wallAdhesion > 0.01f)
        {
            float targetClingX = wobbleAmountX * wallAdhesion * 0.25f;
            float targetClingZ = wobbleAmountZ * wallAdhesion * 0.25f;
            wallClingX = Mathf.Lerp(wallClingX, targetClingX, dt * 5f);
            wallClingZ = Mathf.Lerp(wallClingZ, targetClingZ, dt * 5f);
            float adhesionRelaxRate = Mathf.Max(1.5f / (1f + wallAdhesion * 3f), 0.3f);
            wallClingX = Mathf.Lerp(wallClingX, 0f, dt * adhesionRelaxRate);
            wallClingZ = Mathf.Lerp(wallClingZ, 0f, dt * adhesionRelaxRate);
        }
        else
        {
            wallClingX = 0f;
            wallClingZ = 0f;
        }

        // Zero out negligible residuals when at rest
        if (Mathf.Abs(wobbleAmountX) < 0.0002f && Mathf.Abs(wobbleVelocityX) < 0.0005f && Mathf.Abs(targetTiltX) < 0.0002f)
        {
            wobbleAmountX = 0f;
            wobbleVelocityX = 0f;
        }
        if (Mathf.Abs(wobbleAmountZ) < 0.0002f && Mathf.Abs(wobbleVelocityZ) < 0.0005f && Mathf.Abs(targetTiltZ) < 0.0002f)
        {
            wobbleAmountZ = 0f;
            wobbleVelocityZ = 0f;
        }

        float finalWobbleX = Mathf.Clamp(wobbleAmountX + wallClingX, -maxWobble, maxWobble);
        float finalWobbleZ = Mathf.Clamp(wobbleAmountZ + wallClingZ, -maxWobble, maxWobble);

        currentSloshAngleX = finalWobbleX;
        currentSloshAngleZ = finalWobbleZ;

        // 6. Apply to renderer
        ApplyWobble(finalWobbleX, finalWobbleZ);

        lastPos = currentPos;
        lastRot = currentRot;
        lastVelocity = smoothedVelocity;
    }

    private void UpdateOscillator(float target, float omegaN, float gamma, float dt, ref float amount, ref float velocity)
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

            amount = target + newE;
            velocity = newV;
        }
        else
        {
            float smoothTime = Mathf.Max(1f / gamma, 0.02f);
            amount = Mathf.SmoothDamp(amount, target, ref velocity, smoothTime, maxWobble * 8f, dt);
        }
    }

    private void ApplyWobble(float wx, float wz)
    {
        if (fluidRenderer == null) return;

        // 1. In Play Mode: update the instantiated material instance unique to this renderer
        if (Application.isPlaying)
        {
            if (activeMaterial == null)
            {
                activeMaterial = fluidRenderer.material;
            }
            if (activeMaterial != null)
            {
                activeMaterial.SetFloat(WobbleXProp, wx);
                activeMaterial.SetFloat(WobbleZProp, wz);
            }
        }
        else
        {
            // 2. In Edit Mode: update sharedMaterial for instant Scene View preview
            if (fluidRenderer.sharedMaterial != null)
            {
                fluidRenderer.sharedMaterial.SetFloat(WobbleXProp, wx);
                fluidRenderer.sharedMaterial.SetFloat(WobbleZProp, wz);
            }
        }

        // 3. MaterialPropertyBlock isolated to this renderer
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        fluidRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(WobbleXProp, wx);
        propBlock.SetFloat(WobbleZProp, wz);
        fluidRenderer.SetPropertyBlock(propBlock);
    }

    [ContextMenu("Trigger Impulse Slosh")]
    public void TriggerImpulseSlosh()
    {
        float omega = 2f * Mathf.PI * Mathf.Max(currentFrequency, 0.1f);
        wobbleVelocityX = maxWobble * omega * 0.85f;
        wobbleVelocityZ = maxWobble * omega * 0.65f;
        wobbleAmountX = maxWobble * 0.45f;
        wobbleAmountZ = maxWobble * 0.35f;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SimulateFluidMotion(0.016f);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ArbitraryViscousFluidFriction))]
public class ArbitraryViscousFluidFrictionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ArbitraryViscousFluidFriction controller = (ArbitraryViscousFluidFriction)target;

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Arbitrary Geometry Liquid Physics Simulation:\n" +
            "Simulates coupled fluid density, dynamic viscosity, interior surface roughness friction (f_surface), and geometry tortuosity (lambda_geom).",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Interactive Testing & Slosh Controls", EditorStyles.boldLabel);

        GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
        if (GUILayout.Button("Trigger Impulse Slosh / Wobble", GUILayout.Height(36)))
        {
            controller.TriggerImpulseSlosh();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Water (Low Visc)"))
        {
            controller.SetFluid(FluidSimType.Water);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Veg Oil (Med Visc)"))
        {
            controller.SetFluid(FluidSimType.VegetableOil);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Honey (High Visc & Density)"))
        {
            controller.SetFluid(FluidSimType.Honey);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Ketchup (Non-Newtonian Paste)"))
        {
            controller.SetFluid(FluidSimType.Ketchup);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Surface Friction Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Polished Glass"))
        {
            controller.SetSurfaceFriction(SurfaceFrictionPreset.PolishedGlass);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Frosted Glass"))
        {
            controller.SetSurfaceFriction(SurfaceFrictionPreset.FrostedGlass);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Textured Resin"))
        {
            controller.SetSurfaceFriction(SurfaceFrictionPreset.TexturedResin);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
