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
    [Tooltip("Allow moving the bottle using WASD or arrow keys in Play Mode.")]
    [SerializeField] private bool enableKeyboardControls = true;

    [Tooltip("Allow dragging the bottle with the mouse in Game View.")]
    [SerializeField] private bool enableMouseDrag = true;

    [Tooltip("Move speed for keyboard controls.")]
    [SerializeField] private float moveSpeed = 3f;

    private Material mat;
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
        if (Application.isPlaying && mat != null && liquidRenderer != null && mat != liquidRenderer.sharedMaterial)
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
        if (bottleTransform == null)
        {
            if (transform.parent != null && (transform.parent.name.ToLower().Contains("bottle") || transform.name.ToLower().Contains("water")))
            {
                bottleTransform = transform.parent;
            }
            else
            {
                bottleTransform = transform;
            }
        }

        if (liquidRenderer == null)
        {
            Transform waterChild = transform.Find("water");
            if (waterChild != null)
            {
                liquidRenderer = waterChild.GetComponent<Renderer>();
            }

            if (liquidRenderer == null)
            {
                Renderer thisRend = GetComponent<Renderer>();
                Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
                foreach (var r in childRenderers)
                {
                    if (r != thisRend && IsLiquidRenderer(r))
                    {
                        liquidRenderer = r;
                        break;
                    }
                }

                if (liquidRenderer == null && thisRend != null && IsLiquidRenderer(thisRend))
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
        if (r == null) return false;
        Material m = r.sharedMaterial;
        if (m == null) return false;
        return m.HasProperty(WobbleXProp) ||
               m.HasProperty(RotXProp) ||
               m.name.ToLower().Contains("water") ||
               r.gameObject.name.ToLower().Contains("water");
    }

    private void Start()
    {
        ResolveReferences();
        if (bottleTransform != null)
        {
            lastPos = bottleTransform.position;
            lastRot = bottleTransform.eulerAngles;
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
            bool moved = bottleTransform != null && (bottleTransform.position != lastPos || bottleTransform.eulerAngles != lastRot);

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
        if (bottleTransform == null) return;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (enableKeyboardControls && keyboard != null)
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

        var mouse = Mouse.current;
        if (enableMouseDrag && mouse != null && Camera.main != null)
        {
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragPlane = new Plane(Camera.main.transform.forward, bottleTransform.position);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    if (Vector3.Distance(hitPoint, bottleTransform.position) < 4f)
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
        if (bottleTransform == null || dt <= 0f)
        {
            ResolveReferences();
            if (bottleTransform == null) return;
        }

        // 1. Calculate linear and angular velocities
        Vector3 velocity = (bottleTransform.position - lastPos) / dt;
        Vector3 acceleration = (velocity - lastVelocity) / dt;

        Vector3 currentRot = bottleTransform.eulerAngles;
        Vector3 angularVelocity = Vector3.zero;
        angularVelocity.x = Mathf.DeltaAngle(lastRot.x, currentRot.x) * Mathf.Deg2Rad / dt;
        angularVelocity.z = Mathf.DeltaAngle(lastRot.z, currentRot.z) * Mathf.Deg2Rad / dt;
        Vector3 angularAcceleration = (angularVelocity - lastAngularVelocity) / dt;

        // 2. Add impulse based on motion
        float impulseX = (-velocity.z * moveSensitivity * 0.5f) - (acceleration.z * moveSensitivity * 0.02f)
                       + (angularVelocity.x * rotateSensitivity * 0.2f) - (angularAcceleration.x * rotateSensitivity * 0.02f);
        float impulseZ = (velocity.x * moveSensitivity * 0.5f) - (acceleration.x * moveSensitivity * 0.02f)
                       + (angularVelocity.z * rotateSensitivity * 0.2f) - (angularAcceleration.z * rotateSensitivity * 0.02f);

        wobbleAmountToAddX += impulseX;
        wobbleAmountToAddZ += impulseZ;

        wobbleAmountToAddX = Mathf.Clamp(wobbleAmountToAddX, -maxWobble, maxWobble);
        wobbleAmountToAddZ = Mathf.Clamp(wobbleAmountToAddZ, -maxWobble, maxWobble);

        // 3. Sine wave oscillation
        pulseTime += dt * 2f * Mathf.PI * wobbleFrequency;
        float sinFactor = Mathf.Sin(pulseTime);
        wobbleAmountX = wobbleAmountToAddX * sinFactor;
        wobbleAmountZ = wobbleAmountToAddZ * sinFactor;

        // 4. Exponential decay
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

        // 5. Send values to shader
        ApplyWobble(wobbleAmountX, wobbleAmountZ);

        lastPos = bottleTransform.position;
        lastRot = currentRot;
        lastVelocity = velocity;
        lastAngularVelocity = angularVelocity;
    }

    private void ApplyWobble(float wx, float wz)
    {
        // 1. Material Instance (used in Play Mode & GPU Resident Drawer)
        if (mat != null)
        {
            mat.SetFloat(WobbleXProp, wx);
            mat.SetFloat(WobbleZProp, wz);
            mat.SetFloat(RotXProp, wx);
            mat.SetFloat(RotZProp, wz);
        }

        // 2. Shared Material (used in Edit Mode for instant Scene View preview)
        if (!Application.isPlaying && liquidRenderer != null && liquidRenderer.sharedMaterial != null)
        {
            liquidRenderer.sharedMaterial.SetFloat(WobbleXProp, wx);
            liquidRenderer.sharedMaterial.SetFloat(WobbleZProp, wz);
            liquidRenderer.sharedMaterial.SetFloat(RotXProp, wx);
            liquidRenderer.sharedMaterial.SetFloat(RotZProp, wz);
        }

        // 3. MaterialPropertyBlock
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

        // 4. Global Shader variables
        Shader.SetGlobalFloat(WobbleXProp, wx);
        Shader.SetGlobalFloat(WobbleZProp, wz);
        Shader.SetGlobalFloat(RotXProp, wx);
        Shader.SetGlobalFloat(RotZProp, wz);
    }

    [ContextMenu("Test Slosh Wobble")]
    public void TestSlosh()
    {
        wobbleAmountToAddX = maxWobble * 0.8f;
        wobbleAmountToAddZ = maxWobble * 0.8f;
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