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
    [SerializeField] private bool enableKeyboardControls = true;
    [SerializeField] private bool enableMouseDrag = true;
    [SerializeField] private float moveSpeed = 3f;

    private Material activeMaterial;
    private MaterialPropertyBlock propBlock;

    private static readonly int WobbleXProp = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZProp = Shader.PropertyToID("_WobbleZ");
    private static readonly int RotXProp = Shader.PropertyToID("_rotx");
    private static readonly int RotZProp = Shader.PropertyToID("_rotz");

    private Vector3 lastPos;
    private Vector3 lastRot;
    private Vector3 lastVelocity;
    private Vector3 lastAngularVelocity;

    private float wobbleAmountX = 0f;
    private float wobbleAmountZ = 0f;
    private float wobbleAmountToAddX = 0f;
    private float wobbleAmountToAddZ = 0f;
    private float pulseTime = 0f;

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
        lastVelocity = Vector3.zero;
        lastAngularVelocity = Vector3.zero;
        wobbleAmountToAddX = 0f;
        wobbleAmountToAddZ = 0f;
        wobbleAmountX = 0f;
        wobbleAmountZ = 0f;
        pulseTime = 0f;
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

            bool hadWobble = Mathf.Abs(wobbleAmountToAddX) > 0.0001f || Mathf.Abs(wobbleAmountToAddZ) > 0.0001f;
            bool moved = containerTransform != null && (containerTransform.position != lastPos || containerTransform.eulerAngles != lastRot);

            SimulateWobble(dt);

            bool hasWobble = Mathf.Abs(wobbleAmountToAddX) > 0.0001f || Mathf.Abs(wobbleAmountToAddZ) > 0.0001f;
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

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;

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
        if (containerTransform == null || dt <= 0f)
        {
            ResolveReferences();
            if (containerTransform == null) return;
        }

        Vector3 velocity = (containerTransform.position - lastPos) / dt;
        Vector3 acceleration = (velocity - lastVelocity) / dt;

        Vector3 currentRot = containerTransform.eulerAngles;
        Vector3 angularVelocity = Vector3.zero;
        angularVelocity.x = Mathf.DeltaAngle(lastRot.x, currentRot.x) * Mathf.Deg2Rad / dt;
        angularVelocity.z = Mathf.DeltaAngle(lastRot.z, currentRot.z) * Mathf.Deg2Rad / dt;
        Vector3 angularAcceleration = (angularVelocity - lastAngularVelocity) / dt;

        float impulseX = (-velocity.z * moveSensitivity * 0.5f) - (acceleration.z * moveSensitivity * 0.02f)
                       + (angularVelocity.x * rotateSensitivity * 0.2f) - (angularAcceleration.x * rotateSensitivity * 0.02f);
        float impulseZ = (velocity.x * moveSensitivity * 0.5f) - (acceleration.x * moveSensitivity * 0.02f)
                       + (angularVelocity.z * rotateSensitivity * 0.2f) - (angularAcceleration.z * rotateSensitivity * 0.02f);

        wobbleAmountToAddX += impulseX;
        wobbleAmountToAddZ += impulseZ;

        wobbleAmountToAddX = Mathf.Clamp(wobbleAmountToAddX, -maxWobble, maxWobble);
        wobbleAmountToAddZ = Mathf.Clamp(wobbleAmountToAddZ, -maxWobble, maxWobble);

        pulseTime += dt * 2f * Mathf.PI * wobbleFrequency;
        float sinFactor = Mathf.Sin(pulseTime);
        wobbleAmountX = wobbleAmountToAddX * sinFactor;
        wobbleAmountZ = wobbleAmountToAddZ * sinFactor;

        float decayRate = 4.5f / Mathf.Max(decayDuration, 0.01f);
        wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0f, dt * decayRate);
        wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0f, dt * decayRate);

        if (Mathf.Abs(wobbleAmountToAddX) < 0.0001f)
        {
            wobbleAmountToAddX = 0f;
            wobbleAmountX = 0f;
        }
        if (Mathf.Abs(wobbleAmountToAddZ) < 0.0001f)
        {
            wobbleAmountToAddZ = 0f;
            wobbleAmountZ = 0f;
        }

        ApplyWobble(wobbleAmountX, wobbleAmountZ);

        lastPos = containerTransform.position;
        lastRot = currentRot;
        lastVelocity = velocity;
        lastAngularVelocity = angularVelocity;
    }

    private void ApplyWobble(float wx, float wz)
    {
        if (activeMaterial != null)
        {
            activeMaterial.SetFloat(WobbleXProp, wx);
            activeMaterial.SetFloat(WobbleZProp, wz);
            activeMaterial.SetFloat(RotXProp, wx);
            activeMaterial.SetFloat(RotZProp, wz);
        }

        if (!Application.isPlaying && liquidRenderer != null && liquidRenderer.sharedMaterial != null)
        {
            liquidRenderer.sharedMaterial.SetFloat(WobbleXProp, wx);
            liquidRenderer.sharedMaterial.SetFloat(WobbleZProp, wz);
            liquidRenderer.sharedMaterial.SetFloat(RotXProp, wx);
            liquidRenderer.sharedMaterial.SetFloat(RotZProp, wz);
        }

        if (liquidRenderer != null)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            liquidRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(WobbleXProp, wx);
            propBlock.SetFloat(WobbleZProp, wz);
            propBlock.SetFloat(RotXProp, wx);
            propBlock.SetFloat(RotZProp, wz);
            liquidRenderer.SetPropertyBlock(propBlock);
        }

        Shader.SetGlobalFloat(WobbleXProp, wx);
        Shader.SetGlobalFloat(WobbleZProp, wz);
        Shader.SetGlobalFloat(RotXProp, wx);
        Shader.SetGlobalFloat(RotZProp, wz);
    }

    [ContextMenu("Trigger Test Slosh")]
    public void TestSlosh()
    {
        wobbleAmountToAddX = maxWobble * 0.85f;
        wobbleAmountToAddZ = maxWobble * 0.85f;
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
