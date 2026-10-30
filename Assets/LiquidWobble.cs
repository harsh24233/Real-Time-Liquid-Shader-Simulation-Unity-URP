#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
public class LiquidWobble : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Transform of the bottle to track movement from.")]
    [SerializeField] private Transform bottleTransform;

    [Tooltip("The Renderer containing the liquid water material.")]
    [SerializeField] private Renderer liquidRenderer;

    [Header("Wobble Settings")]
    [Tooltip("Maximum tilt limit for the wobble effect.")]
    [Range(0.05f, 1.5f)]
    [SerializeField] private float maxWobble = 0.5f;

    [Tooltip("Oscillation frequency of the wobble sine wave.")]
    [Range(0.5f, 10f)]
    [SerializeField] private float wobbleFrequency = 2.5f;

    [Tooltip("Decay time in seconds to settle back to zero.")]
    [Range(0.2f, 5f)]
    [SerializeField] private float decayDuration = 1.5f;

    [Tooltip("Sensitivity to translational movement velocity.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float moveSensitivity = 1.5f;

    [Tooltip("Sensitivity to rotational angular velocity.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float rotateSensitivity = 0.8f;

    [Header("Interactive Testing (Play Mode)")]
    [Tooltip("Allow moving the bottle using arrow keys in Play Mode (disabled by default so WASD controls player camera).")]
    [SerializeField] private bool enableKeyboardControls = false;

    [Tooltip("Allow dragging the bottle with the mouse in Game View.")]
    [SerializeField] private bool enableMouseDrag = true;

    [Tooltip("Move speed for keyboard controls.")]
    [SerializeField] private float moveSpeed = 3f;

    private Material mat;
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

#if UNITY_EDITOR
    private double lastEditorTime = 0;
#endif

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
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

        if (Application.isPlaying && mat != null &&
            liquidRenderer != null &&
            mat != liquidRenderer.sharedMaterial)
        {
            Destroy(mat);
            mat = null;
        }
    }

    private void ResetTracking()
    {
        if (bottleTransform != null)
        {
            lastPos = bottleTransform.position;
            lastRot = bottleTransform.eulerAngles;
        }
        else
        {
            lastPos = transform.position;
            lastRot = transform.eulerAngles;
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
        if (bottleTransform == null)
        {
            Transform current = transform;
            while (current.parent != null)
            {
                string pName = current.parent.name.ToLowerInvariant();
                if (pName.Contains("bottle") || pName.Contains("vessel") || pName.Contains("flask") || pName.Contains("jar"))
                {
                    bottleTransform = current.parent;
                    break;
                }
                current = current.parent;
            }

            if (bottleTransform == null)
            {
                bottleTransform = transform;
            }
        }

        if (liquidRenderer == null)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (IsLiquidRenderer(r))
                {
                    liquidRenderer = r;
                    break;
                }
            }

            if (liquidRenderer == null)
            {
                Transform waterChild = transform.Find("water");
                if (waterChild == null) waterChild = transform.Find("liquid");
                if (waterChild == null) waterChild = transform.Find("Water");
                if (waterChild == null) waterChild = transform.Find("Liquid");
                if (waterChild != null)
                {
                    liquidRenderer = waterChild.GetComponent<Renderer>();
                }
            }

            if (liquidRenderer == null)
            {
                Renderer thisRend = GetComponent<Renderer>();
                if (thisRend != null && IsLiquidRenderer(thisRend))
                {
                    liquidRenderer = thisRend;
                }
            }
        }

        if (liquidRenderer != null)
        {
            if (Application.isPlaying)
            {
                if (mat == null || mat == liquidRenderer.sharedMaterial)
                {
                    mat = liquidRenderer.material;
                }
            }
            else
            {
                mat = liquidRenderer.sharedMaterial;
            }
        }
    }

    private bool IsLiquidRenderer(Renderer r)
    {
        if (r == null || r.sharedMaterial == null) return false;
        Material m = r.sharedMaterial;
        string mName = m.name.ToLowerInvariant();
        string goName = r.gameObject.name.ToLowerInvariant();

        return m.HasProperty(WobbleXProp) ||
               mName.Contains("water") || mName.Contains("liquid") || mName.Contains("fluid") ||
               goName.Contains("water") || goName.Contains("liquid") || goName.Contains("fluid");
    }

    private void Start()
    {
        ResolveReferences();
        ResetTracking();
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
            bool moved = bottleTransform != null && (bottleTransform.position != lastPos || bottleTransform.eulerAngles != lastRot);

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
        if (bottleTransform == null) return;

        // If ShowcaseCameraController is present, it centrally manages player camera movement and bottle authority.
        if (Camera.main != null && Camera.main.GetComponent<ShowcaseCameraController>() != null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (enableKeyboardControls && keyboard != null)
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
                bottleTransform.position += moveDir * moveSpeed * Time.deltaTime;
            }

            if (keyboard.qKey.isPressed)
            {
                bottleTransform.Rotate(Vector3.forward, 90f * Time.deltaTime, Space.World);
            }
            if (keyboard.eKey.isPressed)
            {
                bottleTransform.Rotate(Vector3.forward, -90f * Time.deltaTime, Space.World);
            }
        }

        Mouse mouse = Mouse.current;
        if (enableMouseDrag && mouse != null && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragPlane = new Plane(Camera.main.transform.forward, bottleTransform.position);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    if (Vector3.Distance(hitPoint, bottleTransform.position) < 5f)
                    {
                        isDragging = true;
                        dragOffset = bottleTransform.position - hitPoint;
                    }
                }
            }
            else if (mouse.leftButton.isPressed && isDragging)
            {
                if (dragPlane.Raycast(ray, out float enter))
                {
                    bottleTransform.position = ray.GetPoint(enter) + dragOffset;
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
        if (bottleTransform == null)
        {
            ResolveReferences();
            if (bottleTransform == null) return;
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
        Vector3 currentPos = bottleTransform.position;
        Vector3 currentRot = bottleTransform.eulerAngles;

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

        // 2. Physical inertial tilt target driven by container motion
        // Forward (+Z) motion causes fluid to slosh backwards (-Z) -> WobbleX tilts negative
        float targetTiltX = Mathf.Clamp(
            (-smoothedVelocity.z * 0.22f - acceleration.z * 0.035f) * moveSensitivity +
            (smoothedAngularVelocity.x * rotateSensitivity * 0.35f),
            -maxWobble, maxWobble
        );

        // Right (+X) motion causes fluid to slosh leftwards (-X) -> WobbleZ tilts positive
        float targetTiltZ = Mathf.Clamp(
            (smoothedVelocity.x * 0.22f + acceleration.x * 0.035f) * moveSensitivity -
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

        // Rest threshold when motion ceases
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

        // 4. Send updated wobble to shader
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
            // Underdamped oscillation: exact analytical solution (100% unconditionally stable)
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
            // Critically damped / Overdamped: smooth relaxation
            float smoothTime = Mathf.Max(1f / gamma, 0.02f);
            amount = Mathf.SmoothDamp(amount, target, ref velocity, smoothTime, maxWobble * 8f, dt);
        }
    }

    private void ApplyWobble(float wx, float wz)
    {
        if (liquidRenderer == null) return;

        // 1. In Play Mode: update instantiated material unique to this renderer instance
        if (Application.isPlaying)
        {
            if (mat == null)
            {
                mat = liquidRenderer.material;
            }
            if (mat != null)
            {
                mat.SetFloat(WobbleXProp, wx);
                mat.SetFloat(WobbleZProp, wz);
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

        // 3. MaterialPropertyBlock for GPU instanced / block overrides
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        liquidRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(WobbleXProp, wx);
        propBlock.SetFloat(WobbleZProp, wz);
        liquidRenderer.SetPropertyBlock(propBlock);
    }

    [ContextMenu("Test Slosh Wobble")]
    public void TestSlosh()
    {
        float omegaN = 2f * Mathf.PI * Mathf.Max(wobbleFrequency, 0.1f);
        wobbleVelocityX = maxWobble * omegaN * 0.85f;
        wobbleVelocityZ = maxWobble * omegaN * 0.65f;
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
[CustomEditor(typeof(LiquidWobble))]
public class LiquidWobbleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LiquidWobble wobble = (LiquidWobble)target;

        EditorGUILayout.Space(12);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button("Trigger Test Wobble / Slosh", GUILayout.Height(36)))
        {
            wobble.TestSlosh();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif