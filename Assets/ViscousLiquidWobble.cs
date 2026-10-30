#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum FluidDensityType
{
    Water,
    Milk,
    VegetableOil,
    Honey,
    Ketchup,
    Custom
}

[System.Serializable]
public struct FluidPhysicsProfile
{
    public string fluidName;
    public float densityKgM3;
    public float viscosityPaS;
    public float wobbleFrequency;
    public float decayDuration;
    public float maxWobble;
    public float moveSensitivity;
    public float rotateSensitivity;

    public FluidPhysicsProfile(string name, float density, float viscosity, float freq, float decay, float maxW, float moveSens, float rotSens)
    {
        fluidName = name;
        densityKgM3 = density;
        viscosityPaS = viscosity;
        wobbleFrequency = freq;
        decayDuration = decay;
        maxWobble = maxW;
        moveSensitivity = moveSens;
        rotateSensitivity = rotSens;
    }
}

[ExecuteAlways]
public class ViscousLiquidWobble : MonoBehaviour
{
    [Header("Fluid & Density Type")]
    [SerializeField] private FluidDensityType fluidType = FluidDensityType.Water;
    public FluidDensityType FluidType => fluidType;

    [Tooltip("Readout of physical density in kg/m³")]
    [SerializeField] private float fluidDensity = 1000f;

    [Tooltip("Readout of dynamic viscosity in Pa·s")]
    [SerializeField] private float fluidViscosity = 0.001f;

    [Header("References")]
    [Tooltip("Container transform to track movements from (defaults to this Transform).")]
    [SerializeField] private Transform containerTransform;

    [Tooltip("MeshRenderer of the internal liquid geometry.")]
    [SerializeField] private Renderer liquidRenderer;

    [Header("Materials for Fluid Types")]
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Material milkMaterial;
    [SerializeField] private Material vegOilMaterial;
    [SerializeField] private Material honeyMaterial;
    [SerializeField] private Material ketchupMaterial;

    [Header("Current Wobble Dynamics")]
    [Range(0.05f, 1.5f)]
    [SerializeField] private float maxWobble = 0.65f;

    [Range(0.2f, 8f)]
    [SerializeField] private float wobbleFrequency = 3.2f;

    [Range(0.1f, 5f)]
    [SerializeField] private float decayDuration = 2.2f;

    [Range(0.1f, 5f)]
    [SerializeField] private float moveSensitivity = 1.8f;

    [Range(0.1f, 5f)]
    [SerializeField] private float rotateSensitivity = 1.0f;

    [Header("Interactive Controls (Play Mode)")]
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

    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 dragOffset;

    private FluidDensityType lastConfiguredType = (FluidDensityType)(-1);

#if UNITY_EDITOR
    private double lastEditorTime = 0;
#endif

    public static readonly FluidPhysicsProfile WaterProfile =
        new FluidPhysicsProfile("Water", 1000f, 0.001f, 3.2f, 2.2f, 0.65f, 1.8f, 1.0f);

    public static readonly FluidPhysicsProfile MilkProfile =
        new FluidPhysicsProfile("Milk", 1030f, 0.003f, 3.0f, 1.8f, 0.60f, 1.6f, 0.9f);

    public static readonly FluidPhysicsProfile VegOilProfile =
        new FluidPhysicsProfile("Vegetable Oil", 920f, 0.05f, 2.2f, 1.3f, 0.45f, 1.2f, 0.6f);

    public static readonly FluidPhysicsProfile HoneyProfile =
        new FluidPhysicsProfile("Honey", 1420f, 10.0f, 1.2f, 0.5f, 0.30f, 0.7f, 0.35f);

    public static readonly FluidPhysicsProfile KetchupProfile =
        new FluidPhysicsProfile("Ketchup", 1150f, 50.0f, 0.75f, 0.3f, 0.20f, 0.4f, 0.2f);

    private void Awake()
    {
        ResolveReferences();
        ApplyFluidProfile(fluidType, false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyFluidProfile(fluidType, false);
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
        if (Application.isPlaying && activeMaterial != null && liquidRenderer != null && activeMaterial != liquidRenderer.sharedMaterial)
        {
            Destroy(activeMaterial);
            activeMaterial = null;
        }
    }

    private void OnValidate()
    {
        if (fluidType != lastConfiguredType && fluidType != FluidDensityType.Custom)
        {
            ApplyFluidProfile(fluidType, true);
        }
    }

    public void SetFluidType(FluidDensityType type)
    {
        fluidType = type;
        ApplyFluidProfile(type, true);
    }

    public void ApplyFluidProfile(FluidDensityType type, bool updateMaterial)
    {
        FluidPhysicsProfile p;
        Material targetMat = null;

        switch (type)
        {
            case FluidDensityType.Water:
                p = WaterProfile;
                targetMat = waterMaterial;
                break;
            case FluidDensityType.Milk:
                p = MilkProfile;
                targetMat = milkMaterial;
                break;
            case FluidDensityType.VegetableOil:
                p = VegOilProfile;
                targetMat = vegOilMaterial;
                break;
            case FluidDensityType.Honey:
                p = HoneyProfile;
                targetMat = honeyMaterial;
                break;
            case FluidDensityType.Ketchup:
                p = KetchupProfile;
                targetMat = ketchupMaterial;
                break;
            default:
                return;
        }

        fluidDensity = p.densityKgM3;
        fluidViscosity = p.viscosityPaS;
        wobbleFrequency = p.wobbleFrequency;
        decayDuration = p.decayDuration;
        maxWobble = p.maxWobble;
        moveSensitivity = p.moveSensitivity;
        rotateSensitivity = p.rotateSensitivity;
        lastConfiguredType = type;

        if (updateMaterial && targetMat != null && liquidRenderer != null)
        {
            liquidRenderer.sharedMaterial = targetMat;
            if (Application.isPlaying)
            {
                activeMaterial = liquidRenderer.material;
            }
            else
            {
                activeMaterial = liquidRenderer.sharedMaterial;
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
    }

    private void ResolveReferences()
    {
        if (containerTransform == null)
        {
            containerTransform = transform;
        }

        if (liquidRenderer == null)
        {
            Transform liquidChild = transform.Find("Milk");
            if (liquidChild == null) liquidChild = transform.Find("Water");
            if (liquidChild == null) liquidChild = transform.Find("VegOil");
            if (liquidChild == null) liquidChild = transform.Find("Honey");
            if (liquidChild == null) liquidChild = transform.Find("Ketchup");
            if (liquidChild == null) liquidChild = transform.Find("Liquid");
            if (liquidChild == null) liquidChild = transform.Find("water");
            if (liquidChild != null)
            {
                liquidRenderer = liquidChild.GetComponentInChildren<Renderer>();
            }

            if (liquidRenderer == null)
            {
                Renderer[] rends = GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    string n = r.gameObject.name.ToLower();
                    string parentN = r.transform.parent != null ? r.transform.parent.name.ToLower() : "";
                    if (r.transform != transform && !n.Contains("glass") && !parentN.Contains("glass"))
                    {
                        liquidRenderer = r;
                        break;
                    }
                }
            }
        }

        // Auto-load fluid materials if not bound
        if (waterMaterial == null) waterMaterial = LoadMaterial("Water_Fluid");
        if (milkMaterial == null) milkMaterial = LoadMaterial("Milk_Fluid");
        if (vegOilMaterial == null) vegOilMaterial = LoadMaterial("VegOil_Fluid");
        if (honeyMaterial == null) honeyMaterial = LoadMaterial("Honey_Fluid");
        if (ketchupMaterial == null) ketchupMaterial = LoadMaterial("Ketchup_Fluid");

        if (liquidRenderer != null)
        {
            if (liquidRenderer.sharedMaterial == null && waterMaterial != null)
            {
                liquidRenderer.sharedMaterial = waterMaterial;
            }

            if (Application.isPlaying)
            {
                if (activeMaterial == null || activeMaterial == liquidRenderer.sharedMaterial)
                {
                    activeMaterial = liquidRenderer.material;
                }
            }
            else
            {
                activeMaterial = liquidRenderer.sharedMaterial;
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
            SimulateWobble(Time.deltaTime);
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

            SimulateWobble(dt);

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
            // Switch fluid types using 1, 2, 3, 4, 5
            if (keyboard.digit1Key.wasPressedThisFrame) SetFluidType(FluidDensityType.Water);
            if (keyboard.digit2Key.wasPressedThisFrame) SetFluidType(FluidDensityType.Milk);
            if (keyboard.digit3Key.wasPressedThisFrame) SetFluidType(FluidDensityType.VegetableOil);
            if (keyboard.digit4Key.wasPressedThisFrame) SetFluidType(FluidDensityType.Honey);
            if (keyboard.digit5Key.wasPressedThisFrame) SetFluidType(FluidDensityType.Ketchup);

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
                    if (Vector3.Distance(hitPoint, containerTransform.position) < 3f)
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

    private void SimulateWobble(float dt)
    {
        if (containerTransform == null)
        {
            ResolveReferences();
            if (containerTransform == null) return;
            ResetTracking();
            return;
        }

        if (liquidRenderer == null)
        {
            ResolveReferences();
            if (liquidRenderer == null) return;
        }

        if (dt <= 0f) return;
        dt = Mathf.Clamp(dt, 0.001f, 0.05f);

        // 1. Measure motion with exponential smoothing to suppress single-frame spikes
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

        // 2. Physical inertial tilt target driven by container motion scaled by fluid density
        float densityFactor = Mathf.Clamp(fluidDensity / 1000f, 0.7f, 1.4f);

        float targetTiltX = Mathf.Clamp(
            (-smoothedVelocity.z * 0.22f - acceleration.z * 0.035f) * moveSensitivity * densityFactor +
            (smoothedAngularVelocity.x * rotateSensitivity * 0.35f),
            -maxWobble, maxWobble
        );

        float targetTiltZ = Mathf.Clamp(
            (smoothedVelocity.x * 0.22f + acceleration.x * 0.035f) * moveSensitivity * densityFactor -
            (smoothedAngularVelocity.z * rotateSensitivity * 0.35f),
            -maxWobble, maxWobble
        );

        // 3. Exact Analytical Damped Harmonic Oscillator Integration (100% unconditionally stable)
        float omegaN = 2f * Mathf.PI * Mathf.Max(wobbleFrequency, 0.1f);
        float gamma = 4.2f / Mathf.Max(decayDuration, 0.05f);

        UpdateOscillator(targetTiltX, omegaN, gamma, dt, ref wobbleAmountX, ref wobbleVelocityX);
        UpdateOscillator(targetTiltZ, omegaN, gamma, dt, ref wobbleAmountZ, ref wobbleVelocityZ);

        wobbleAmountX = Mathf.Clamp(wobbleAmountX, -maxWobble, maxWobble);
        wobbleAmountZ = Mathf.Clamp(wobbleAmountZ, -maxWobble, maxWobble);

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

        ApplyWobble(wobbleAmountX, wobbleAmountZ);

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
        if (liquidRenderer == null) return;

        // 1. In Play Mode: update instantiated material unique to this container
        if (Application.isPlaying)
        {
            if (activeMaterial == null)
            {
                activeMaterial = liquidRenderer.material;
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
            if (liquidRenderer.sharedMaterial != null)
            {
                liquidRenderer.sharedMaterial.SetFloat(WobbleXProp, wx);
                liquidRenderer.sharedMaterial.SetFloat(WobbleZProp, wz);
            }
        }

        // 3. MaterialPropertyBlock
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        liquidRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(WobbleXProp, wx);
        propBlock.SetFloat(WobbleZProp, wz);
        liquidRenderer.SetPropertyBlock(propBlock);
    }

    [ContextMenu("Trigger Test Slosh")]
    public void TestSlosh()
    {
        float omega = 2f * Mathf.PI * Mathf.Max(wobbleFrequency, 0.1f);
        wobbleVelocityX = maxWobble * omega * 0.85f;
        wobbleVelocityZ = maxWobble * omega * 0.65f;
        wobbleAmountX = maxWobble * 0.45f;
        wobbleAmountZ = maxWobble * 0.35f;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SimulateWobble(0.016f);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ViscousLiquidWobble))]
public class ViscousLiquidWobbleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ViscousLiquidWobble wobble = (ViscousLiquidWobble)target;

        EditorGUILayout.Space(12);
        GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
        if (GUILayout.Button("Trigger Test Slosh / Wobble", GUILayout.Height(36)))
        {
            wobble.TestSlosh();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif
