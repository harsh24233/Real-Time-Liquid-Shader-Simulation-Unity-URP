using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Camera navigation controller and in-game vessel manipulation authority.
/// - Controls player camera with WASD, Right-Click Mouse Look, Shift (Sprint), Space/C (Up/Down).
/// - Gives complete authority to move, drag, tilt, slosh, and reset bottles in Game mode.
/// - Renders interactive 3D Gizmo Move Handles in Game mode on any selected vessel.
/// </summary>
public class ShowcaseCameraController : MonoBehaviour
{
    public enum CameraViewMode
    {
        TieredOverview,       // Frames both the front 12-object line and back 5-object line
        Week2_ArbitraryLine,  // Frames the 12 new arbitrary geometry containers up close
        Week1_OriginalLine,   // Frames the 5 original containers
        IndividualVesselFocus,// Centers closely on a single selected vessel
        FreeFly               // Full WASD + Mouse Look spectator navigation
    }

    public enum GizmoAxis { None, X, Y, Z }

    [System.Serializable]
    public class VesselEntry
    {
        public string Name;
        public Transform Transform;
        public Vector3 InitialPosition;
        public Quaternion InitialRotation;
        public ArbitraryViscousFluidFriction ArbitrarySim;
        public ViscousLiquidWobble ViscousSim;
        public LiquidWobble LiquidSim;
        public Collider Collider;

        public void TriggerSlosh()
        {
            if (ArbitrarySim != null) ArbitrarySim.TriggerImpulseSlosh();
            if (ViscousSim != null) ViscousSim.TestSlosh();
            if (LiquidSim != null) LiquidSim.TestSlosh();
        }

        public void SetFluid(int idx)
        {
            if (ArbitrarySim != null) ArbitrarySim.SetFluid((FluidSimType)Mathf.Clamp(idx, 0, 4));
            if (ViscousSim != null) ViscousSim.SetFluidType((FluidDensityType)Mathf.Clamp(idx, 0, 4));
        }

        public void ResetToInitial()
        {
            if (Transform != null)
            {
                Transform.position = InitialPosition;
                Transform.rotation = InitialRotation;
            }
        }

        public string GetFluidName()
        {
            if (ArbitrarySim != null) return ArbitrarySim.FluidType.ToString();
            if (ViscousSim != null) return ViscousSim.FluidType.ToString();
            if (LiquidSim != null) return "Water";
            return "Fluid";
        }
    }

    [Header("Camera Configuration")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CameraViewMode currentMode = CameraViewMode.TieredOverview;
    [SerializeField] private float transitionSpeed = 5.0f;

    [Header("Free Camera Controls (WASD + Right-Click)")]
    [SerializeField] private float flySpeed = 5.0f;
    [SerializeField] private float sprintMultiplier = 2.5f;
    [SerializeField] private float lookSensitivity = 0.15f;

    [Header("Target Positions")]
    [SerializeField] private Vector3 overviewPos = new Vector3(0f, 1.5f, -8.8f);
    [SerializeField] private Vector3 overviewRot = new Vector3(8f, 0f, 0f);

    [SerializeField] private Vector3 week2LinePos = new Vector3(0f, 0.85f, -6.8f);
    [SerializeField] private Vector3 week2LineRot = new Vector3(5f, 0f, 0f);

    [SerializeField] private Vector3 week1LinePos = new Vector3(0f, 1.35f, -7.8f);
    [SerializeField] private Vector3 week1LineRot = new Vector3(4f, 0f, 0f);

    [Header("Vessel Authority & Manipulation")]
    [SerializeField] private int activeVesselIndex = -1;
    [SerializeField] private List<ArbitraryViscousFluidFriction> week2Vessels = new List<ArbitraryViscousFluidFriction>();
    [SerializeField] private List<VesselEntry> allVessels = new List<VesselEntry>();

    private Vector3 targetPos;
    private Quaternion targetRot;
    private float currentPitch = 8f;
    private float currentYaw = 0f;
    private bool isLookingWithMouse = false;
    private bool showHUD = true;

    // Dragging state
    private bool isDraggingVessel = false;
    private float dragPlaneDistance = 3.5f;
    private Vector3 dragOffset = Vector3.zero;
    private GizmoAxis activeGizmoAxis = GizmoAxis.None;

    // Texture for OnGUI line drawing
    private static Texture2D lineTex;
    private Rect hudRect = new Rect(16, 16, 460, 490);

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) targetCamera = GetComponent<Camera>();

        RefreshAllVessels();
        SetViewMode(currentMode);
    }

    public void FindWeek2Vessels()
    {
        RefreshAllVessels();
    }

    public void RefreshAllVessels()
    {
        allVessels.Clear();

        // 1. Find Week 2 arbitrary geometry vessels
        var week2List = FindObjectsByType<ArbitraryViscousFluidFriction>();
        System.Array.Sort(week2List, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        foreach (var v in week2List)
        {
            if (v != null) RegisterVessel(v.name, v.transform, v, null, null);
        }

        // 2. Find Week 1 containers
        var week1List = FindObjectsByType<ViscousLiquidWobble>();
        System.Array.Sort(week1List, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        foreach (var v in week1List)
        {
            if (v != null) RegisterVessel(v.name, v.transform, null, v, null);
        }

        // 3. Find LiquidWobble original bottles
        var origList = FindObjectsByType<LiquidWobble>();
        foreach (var v in origList)
        {
            if (v != null) RegisterVessel(v.name, v.transform, null, null, v);
        }

        // Maintain week2Vessels serialized list for backwards compatibility
        week2Vessels.Clear();
        week2Vessels.AddRange(week2List);
    }

    private void RegisterVessel(string name, Transform t, ArbitraryViscousFluidFriction arb, ViscousLiquidWobble visc, LiquidWobble liq)
    {
        if (t == null) return;
        if (allVessels.Exists(e => e.Transform == t)) return;

        // Ensure every vessel has a collider so raycasting is 100% reliable
        Collider col = t.GetComponentInChildren<Collider>();
        if (col == null)
        {
            Renderer rend = t.GetComponentInChildren<Renderer>();
            BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
            if (rend != null)
            {
                Bounds b = rend.bounds;
                box.center = t.InverseTransformPoint(b.center);
                Vector3 s = t.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                box.size = Vector3.Max(box.size, new Vector3(0.35f, 0.45f, 0.35f));
            }
            else
            {
                box.size = new Vector3(0.4f, 0.6f, 0.4f);
            }
            col = box;
        }

        var entry = new VesselEntry
        {
            Name = name,
            Transform = t,
            InitialPosition = t.position,
            InitialRotation = t.rotation,
            ArbitrarySim = arb,
            ViscousSim = visc,
            LiquidSim = liq,
            Collider = col
        };
        allVessels.Add(entry);
    }

    public VesselEntry GetSelectedVessel()
    {
        if (activeVesselIndex >= 0 && activeVesselIndex < allVessels.Count)
        {
            return allVessels[activeVesselIndex];
        }
        return null;
    }

    private VesselEntry FindVesselFromTransform(Transform hitTrans)
    {
        if (hitTrans == null) return null;
        foreach (var v in allVessels)
        {
            if (v.Transform == null) continue;
            if (hitTrans == v.Transform || hitTrans.IsChildOf(v.Transform))
            {
                return v;
            }
        }
        return null;
    }

    private void Update()
    {
        HandleCameraAndGlobalInput();
        HandleVesselAuthorityInput();
        HandleMouseBottleInteraction();
        UpdateCameraTransform();
    }

    private void HandleCameraAndGlobalInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        // View mode presets
        if (kb.f1Key.wasPressedThisFrame) SetViewMode(CameraViewMode.TieredOverview);
        if (kb.f2Key.wasPressedThisFrame) SetViewMode(CameraViewMode.Week2_ArbitraryLine);
        if (kb.f3Key.wasPressedThisFrame) SetViewMode(CameraViewMode.Week1_OriginalLine);
        if (kb.f4Key.wasPressedThisFrame) SetViewMode(CameraViewMode.IndividualVesselFocus);
        if (kb.f5Key.wasPressedThisFrame) SetViewMode(CameraViewMode.FreeFly);

        // Toggle HUD
        if (kb.hKey.wasPressedThisFrame) showHUD = !showHUD;

        // Cycle vessel with Tab
        if (kb.tabKey.wasPressedThisFrame) CycleVessel(1);

        // Global impulse slosh with Space if no vessel is selected
        if (kb.spaceKey.wasPressedThisFrame && activeVesselIndex < 0)
        {
            TriggerAllSlosh();
        }

        // Reset all bottles with Backspace
        if (kb.backspaceKey.wasPressedThisFrame)
        {
            ResetAllBottles();
        }

        // Camera flight movement via W, A, S, D, Space/E, C/Ctrl/Q
        Vector3 moveInput = Vector3.zero;
        if (kb.wKey.isPressed) moveInput += targetCamera.transform.forward;
        if (kb.sKey.isPressed) moveInput -= targetCamera.transform.forward;
        if (kb.dKey.isPressed) moveInput += targetCamera.transform.right;
        if (kb.aKey.isPressed) moveInput -= targetCamera.transform.right;
        if (kb.spaceKey.isPressed || kb.eKey.isPressed) moveInput += Vector3.up;
        if (kb.leftCtrlKey.isPressed || kb.cKey.isPressed || kb.qKey.isPressed) moveInput -= Vector3.up;

        bool wantsCameraMove = moveInput.sqrMagnitude > 0.001f;

        // Free look with Right Mouse Button
        bool isRightClick = mouse != null && mouse.rightButton.isPressed;
        if (isRightClick)
        {
            if (!isLookingWithMouse)
            {
                isLookingWithMouse = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Vector2 delta = mouse.delta.ReadValue();
            currentYaw += delta.x * lookSensitivity;
            currentPitch -= delta.y * lookSensitivity;
            currentPitch = Mathf.Clamp(currentPitch, -85f, 85f);
            targetRot = Quaternion.Euler(currentPitch, currentYaw, 0f);

            if (currentMode != CameraViewMode.FreeFly)
            {
                currentMode = CameraViewMode.FreeFly;
                targetPos = targetCamera.transform.position;
            }
        }
        else if (isLookingWithMouse)
        {
            isLookingWithMouse = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Apply camera flight translation
        if (wantsCameraMove)
        {
            if (currentMode != CameraViewMode.FreeFly)
            {
                currentMode = CameraViewMode.FreeFly;
                targetPos = targetCamera.transform.position;
                targetRot = targetCamera.transform.rotation;
                Vector3 e = targetCamera.transform.eulerAngles;
                currentPitch = e.x > 180f ? e.x - 360f : e.x;
                currentYaw = e.y;
            }

            float speed = flySpeed * (kb.leftShiftKey.isPressed ? sprintMultiplier : 1f);
            targetPos += moveInput.normalized * speed * Time.deltaTime;
        }
#endif
    }

    private void HandleVesselAuthorityInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        VesselEntry sel = GetSelectedVessel();
        if (sel == null || sel.Transform == null) return;

        // 1. Reset selected bottle with R key
        if (kb.rKey.wasPressedThisFrame)
        {
            sel.ResetToInitial();
        }

        // 2. Slosh selected bottle with Space key
        if (kb.spaceKey.wasPressedThisFrame && !kb.wKey.isPressed && !kb.sKey.isPressed && !kb.aKey.isPressed && !kb.dKey.isPressed)
        {
            sel.TriggerSlosh();
        }

        // 3. Arrow Keys to nudge selected bottle in 3D
        float nudgeSpeed = 2.5f * Time.deltaTime;
        Vector3 nudge = Vector3.zero;
        if (kb.leftArrowKey.isPressed) nudge += Vector3.left;
        if (kb.rightArrowKey.isPressed) nudge += Vector3.right;
        if (kb.upArrowKey.isPressed)
        {
            if (kb.leftShiftKey.isPressed) nudge += Vector3.forward;
            else nudge += Vector3.up;
        }
        if (kb.downArrowKey.isPressed)
        {
            if (kb.leftShiftKey.isPressed) nudge += Vector3.back;
            else nudge += Vector3.down;
        }
        if (kb.pageUpKey.isPressed) nudge += Vector3.forward;
        if (kb.pageDownKey.isPressed) nudge += Vector3.back;

        if (nudge.sqrMagnitude > 0.001f)
        {
            sel.Transform.position += nudge.normalized * nudgeSpeed;
        }

        // 4. Tilt bottle with Q / E (when not flying camera)
        if (!kb.wKey.isPressed && !kb.sKey.isPressed)
        {
            if (kb.qKey.isPressed) sel.Transform.Rotate(Vector3.forward, 90f * Time.deltaTime, Space.World);
            if (kb.eKey.isPressed) sel.Transform.Rotate(Vector3.forward, -90f * Time.deltaTime, Space.World);
        }

        // 5. Switch fluid profile with 1-5 number keys
        if (kb.digit1Key.wasPressedThisFrame) sel.SetFluid(0); // Water
        if (kb.digit2Key.wasPressedThisFrame) sel.SetFluid(1); // VegOil / Milk
        if (kb.digit3Key.wasPressedThisFrame) sel.SetFluid(2); // Honey
        if (kb.digit4Key.wasPressedThisFrame) sel.SetFluid(3); // Ketchup
        if (kb.digit5Key.wasPressedThisFrame) sel.SetFluid(4); // Milk
#endif
    }

    private void HandleMouseBottleInteraction()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null || targetCamera == null) return;

        Vector2 mouseScreenPos = mouse.position.ReadValue();
        Vector2 guiMousePos = new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y);

        bool insideHUD = showHUD && hudRect.Contains(guiMousePos);
        Ray ray = targetCamera.ScreenPointToRay(mouseScreenPos);

        // Left Mouse Button Down
        if (mouse.leftButton.wasPressedThisFrame && !insideHUD)
        {
            // 1. Check Gizmo axis click
            VesselEntry sel = GetSelectedVessel();
            if (sel != null && sel.Transform != null)
            {
                GizmoAxis clickedAxis = CheckGizmoAxisClick(guiMousePos, sel.Transform.position);
                if (clickedAxis != GizmoAxis.None)
                {
                    activeGizmoAxis = clickedAxis;
                    return;
                }
            }

            // 2. Raycast to select & drag bottle
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                VesselEntry hitVessel = FindVesselFromTransform(hit.collider.transform);
                if (hitVessel != null)
                {
                    activeVesselIndex = allVessels.IndexOf(hitVessel);
                    isDraggingVessel = true;

                    // Compute distance from camera along forward axis
                    dragPlaneDistance = Vector3.Dot(targetCamera.transform.forward, hitVessel.Transform.position - targetCamera.transform.position);
                    float denom = Vector3.Dot(ray.direction, targetCamera.transform.forward);
                    Vector3 planePoint = ray.origin + ray.direction * (dragPlaneDistance / Mathf.Max(0.001f, denom));
                    dragOffset = hitVessel.Transform.position - planePoint;
                    return;
                }
            }
        }

        // While Left Mouse Button is Pressed
        if (mouse.leftButton.isPressed)
        {
            VesselEntry sel = GetSelectedVessel();
            if (sel != null && sel.Transform != null)
            {
                // A. Gizmo Axis Dragging
                if (activeGizmoAxis != GizmoAxis.None)
                {
                    Vector2 mouseDelta = mouse.delta.ReadValue();
                    Vector3 axisWorld = activeGizmoAxis == GizmoAxis.X ? Vector3.right :
                                        activeGizmoAxis == GizmoAxis.Y ? Vector3.up : Vector3.forward;

                    Vector3 scrTip = targetCamera.WorldToScreenPoint(sel.Transform.position + axisWorld);
                    Vector3 scrBase = targetCamera.WorldToScreenPoint(sel.Transform.position);
                    Vector2 scrDir = new Vector2(scrTip.x - scrBase.x, scrTip.y - scrBase.y).normalized;

                    float moveAmount = Vector2.Dot(mouseDelta, scrDir) * 0.015f;
                    sel.Transform.position += axisWorld * moveAmount;
                    return;
                }

                // B. Free Dragging in 3D
                if (isDraggingVessel)
                {
                    // Scroll wheel adjusts depth
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        dragPlaneDistance = Mathf.Clamp(dragPlaneDistance + scroll * 0.002f, 0.5f, 25f);
                    }

                    float denom = Vector3.Dot(ray.direction, targetCamera.transform.forward);
                    if (denom > 0.001f)
                    {
                        Vector3 planePoint = ray.origin + ray.direction * (dragPlaneDistance / denom);
                        sel.Transform.position = planePoint + dragOffset;
                    }
                }
            }
        }

        // Left Mouse Button Released
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isDraggingVessel = false;
            activeGizmoAxis = GizmoAxis.None;
        }
#endif
    }

    private GizmoAxis CheckGizmoAxisClick(Vector2 guiMousePos, Vector3 vesselWorldPos)
    {
        Vector3 origin = targetCamera.WorldToScreenPoint(vesselWorldPos);
        if (origin.z <= 0.1f) return GizmoAxis.None;

        float handleLen = 0.5f;
        Vector3 scrX = targetCamera.WorldToScreenPoint(vesselWorldPos + Vector3.right * handleLen);
        Vector3 scrY = targetCamera.WorldToScreenPoint(vesselWorldPos + Vector3.up * handleLen);
        Vector3 scrZ = targetCamera.WorldToScreenPoint(vesselWorldPos + Vector3.forward * handleLen);

        Vector2 ptX = new Vector2(scrX.x, Screen.height - scrX.y);
        Vector2 ptY = new Vector2(scrY.x, Screen.height - scrY.y);
        Vector2 ptZ = new Vector2(scrZ.x, Screen.height - scrZ.y);

        float radius = 22f;
        if (Vector2.Distance(guiMousePos, ptX) < radius) return GizmoAxis.X;
        if (Vector2.Distance(guiMousePos, ptY) < radius) return GizmoAxis.Y;
        if (Vector2.Distance(guiMousePos, ptZ) < radius) return GizmoAxis.Z;

        return GizmoAxis.None;
    }

    public void SetViewMode(CameraViewMode mode)
    {
        currentMode = mode;
        switch (mode)
        {
            case CameraViewMode.TieredOverview:
                targetPos = overviewPos;
                targetRot = Quaternion.Euler(overviewRot);
                break;
            case CameraViewMode.Week2_ArbitraryLine:
                targetPos = week2LinePos;
                targetRot = Quaternion.Euler(week2LineRot);
                break;
            case CameraViewMode.Week1_OriginalLine:
                targetPos = week1LinePos;
                targetRot = Quaternion.Euler(week1LineRot);
                break;
            case CameraViewMode.IndividualVesselFocus:
                UpdateIndividualFocus();
                break;
            case CameraViewMode.FreeFly:
                targetPos = targetCamera.transform.position;
                targetRot = targetCamera.transform.rotation;
                Vector3 e = targetCamera.transform.eulerAngles;
                currentPitch = e.x > 180f ? e.x - 360f : e.x;
                currentYaw = e.y;
                break;
        }
    }

    public void CycleVessel(int direction)
    {
        if (allVessels.Count == 0) RefreshAllVessels();
        if (allVessels.Count == 0) return;

        if (activeVesselIndex < 0) activeVesselIndex = 0;
        else activeVesselIndex = (activeVesselIndex + direction + allVessels.Count) % allVessels.Count;

        SetViewMode(CameraViewMode.IndividualVesselFocus);
    }

    private void UpdateIndividualFocus()
    {
        if (allVessels.Count == 0) RefreshAllVessels();
        if (allVessels.Count == 0) return;

        if (activeVesselIndex < 0 || activeVesselIndex >= allVessels.Count)
        {
            activeVesselIndex = 0;
        }

        var v = allVessels[activeVesselIndex];
        if (v != null && v.Transform != null)
        {
            Vector3 vPos = v.Transform.position;
            targetPos = vPos + new Vector3(0f, 0.15f, -1.35f);
            targetRot = Quaternion.Euler(4f, 0f, 0f);
        }
    }

    private void UpdateCameraTransform()
    {
        if (targetCamera == null) return;

        if (currentMode == CameraViewMode.IndividualVesselFocus)
        {
            UpdateIndividualFocus();
        }

        float speed = (currentMode == CameraViewMode.FreeFly) ? 18f : transitionSpeed;
        targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, targetPos, Time.deltaTime * speed);
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, targetRot, Time.deltaTime * speed);
    }

    public void TriggerAllSlosh()
    {
        foreach (var v in allVessels)
        {
            if (v != null) v.TriggerSlosh();
        }
    }

    public void ResetAllBottles()
    {
        foreach (var v in allVessels)
        {
            if (v != null) v.ResetToInitial();
        }
    }

    private static void DrawScreenLine(Vector2 pointA, Vector2 pointB, Color color, float width = 2f)
    {
        if (lineTex == null)
        {
            lineTex = new Texture2D(1, 1);
            lineTex.SetPixel(0, 0, Color.white);
            lineTex.Apply();
        }
        Color saved = GUI.color;
        GUI.color = color;
        Vector2 d = pointB - pointA;
        float angle = Mathf.Rad2Deg * Mathf.Atan2(d.y, d.x);
        GUIUtility.RotateAroundPivot(angle, pointA);
        GUI.DrawTexture(new Rect(pointA.x, pointA.y - width * 0.5f, d.magnitude, width), lineTex);
        GUIUtility.RotateAroundPivot(-angle, pointA);
        GUI.color = saved;
    }

    private void DrawGizmoHandle(Vector2 point, string label, Color color)
    {
        Color saved = GUI.backgroundColor;
        GUI.backgroundColor = color;
        GUI.Box(new Rect(point.x - 12, point.y - 12, 24, 24), $"<b>{label}</b>");
        GUI.backgroundColor = saved;
    }

    private void DrawInGameGizmo(VesselEntry sel)
    {
        if (sel == null || sel.Transform == null || isDraggingVessel) return;

        Vector3 worldPos = sel.Transform.position;
        Vector3 scrOrigin = targetCamera.WorldToScreenPoint(worldPos);
        if (scrOrigin.z <= 0.1f) return;

        Vector2 guiOrigin = new Vector2(scrOrigin.x, Screen.height - scrOrigin.y);

        float handleLen = 0.5f;
        Vector3 scrX = targetCamera.WorldToScreenPoint(worldPos + Vector3.right * handleLen);
        Vector3 scrY = targetCamera.WorldToScreenPoint(worldPos + Vector3.up * handleLen);
        Vector3 scrZ = targetCamera.WorldToScreenPoint(worldPos + Vector3.forward * handleLen);

        Vector2 ptX = new Vector2(scrX.x, Screen.height - scrX.y);
        Vector2 ptY = new Vector2(scrY.x, Screen.height - scrY.y);
        Vector2 ptZ = new Vector2(scrZ.x, Screen.height - scrZ.y);

        // Draw 3D Move Axis Lines
        DrawScreenLine(guiOrigin, ptX, new Color(1f, 0.25f, 0.25f, 0.9f), 3f);
        DrawScreenLine(guiOrigin, ptY, new Color(0.25f, 1f, 0.25f, 0.9f), 3f);
        DrawScreenLine(guiOrigin, ptZ, new Color(0.30f, 0.65f, 1f, 0.9f), 3f);

        // Draw Handle Buttons
        DrawGizmoHandle(ptX, "X", new Color(1f, 0.3f, 0.3f, 1f));
        DrawGizmoHandle(ptY, "Y", new Color(0.3f, 1f, 0.3f, 1f));
        DrawGizmoHandle(ptZ, "Z", new Color(0.3f, 0.7f, 1f, 1f));

        // Floating name tag
        GUI.Label(new Rect(guiOrigin.x - 120, guiOrigin.y - 40, 240, 24),
            $"<b><color=#FFE066>▼ {sel.Name} ({sel.GetFluidName()})</color></b>");
    }

    private void OnGUI()
    {
        VesselEntry sel = GetSelectedVessel();
        if (sel != null && sel.Transform != null)
        {
            DrawInGameGizmo(sel);
        }

        if (!showHUD || !Application.isPlaying) return;

        GUI.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.92f);
        GUILayout.BeginArea(hudRect, GUI.skin.box);
        GUI.backgroundColor = Color.white;

        GUILayout.Label("<b>LIQUID SHADER SIMULATION - BOTTLE AUTHORITY</b>", GUI.skin.label);
        GUILayout.Label($"<b>Camera:</b> {currentMode} | <b>WASD:</b> Move Camera | <b>Right-Click:</b> Free Look", GUI.skin.label);

        // Camera Preset Buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Overview [F1]")) SetViewMode(CameraViewMode.TieredOverview);
        if (GUILayout.Button("12-Vessels [F2]")) SetViewMode(CameraViewMode.Week2_ArbitraryLine);
        if (GUILayout.Button("5-Bottles [F3]")) SetViewMode(CameraViewMode.Week1_OriginalLine);
        if (GUILayout.Button("Free-Fly [F5]")) SetViewMode(CameraViewMode.FreeFly);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Selected Bottle Authority Section
        if (sel != null && sel.Transform != null)
        {
            GUI.color = new Color(1f, 0.9f, 0.4f);
            GUILayout.Label($"<b>★ BOTTLE AUTHORITY: {sel.Name}</b>", GUI.skin.label);
            GUI.color = Color.white;

            GUILayout.Label($"<b>Fluid:</b> {sel.GetFluidName()} | <b>Pos:</b> ({sel.Transform.position.x:F2}, {sel.Transform.position.y:F2}, {sel.Transform.position.z:F2})");
            GUILayout.Label("<i>Controls: Left-Click & Drag to shake/move | Mouse Wheel for depth | Q/E to tilt</i>");

            // Nudge Buttons along Axes
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("← X-")) sel.Transform.position += Vector3.left * 0.15f;
            if (GUILayout.Button("X+ →")) sel.Transform.position += Vector3.right * 0.15f;
            if (GUILayout.Button("↓ Y-")) sel.Transform.position += Vector3.down * 0.15f;
            if (GUILayout.Button("↑ Y+")) sel.Transform.position += Vector3.up * 0.15f;
            if (GUILayout.Button("Z- (Back)")) sel.Transform.position += Vector3.back * 0.15f;
            if (GUILayout.Button("Z+ (Fwd)")) sel.Transform.position += Vector3.forward * 0.15f;
            GUILayout.EndHorizontal();

            // Tilt & Slosh Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tilt Left (Q)")) sel.Transform.Rotate(Vector3.forward, 15f, Space.World);
            if (GUILayout.Button("Tilt Right (E)")) sel.Transform.Rotate(Vector3.forward, -15f, Space.World);
            if (GUILayout.Button("Level Out")) sel.Transform.rotation = Quaternion.Euler(0f, sel.Transform.eulerAngles.y, 0f);
            if (GUILayout.Button("Impulse Slosh")) sel.TriggerSlosh();
            GUILayout.EndHorizontal();

            // Fluid Profile Switcher
            GUILayout.Label("<b>Switch Fluid Profile on Selected Bottle:</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Water")) sel.SetFluid(0);
            if (GUILayout.Button("VegOil")) sel.SetFluid(1);
            if (GUILayout.Button("Honey")) sel.SetFluid(2);
            if (GUILayout.Button("Ketchup")) sel.SetFluid(3);
            if (GUILayout.Button("Milk")) sel.SetFluid(4);
            GUILayout.EndHorizontal();

            // Reset & Focus
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUILayout.Button("Reset This Bottle [R]")) sel.ResetToInitial();
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Focus Camera [F4]")) SetViewMode(CameraViewMode.IndividualVesselFocus);
            if (GUILayout.Button("Deselect")) activeVesselIndex = -1;
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Space(4);
            GUILayout.Label("<b>Click on ANY bottle</b> in the scene to select and move it with authority!");
            GUILayout.Label("• <b>Left-Click & Drag:</b> Move/shake any bottle in 3D.");
            GUILayout.Label("• <b>Mouse Wheel:</b> Adjust depth (push/pull bottle).");
            GUILayout.Label("• <b>3D Move Gizmo:</b> Click & drag X/Y/Z arrows to slide along axes.");
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Prev [← / Tab]")) CycleVessel(-1);
        if (GUILayout.Button("Next [→]")) CycleVessel(1);
        if (GUILayout.Button("Slosh All [Space]")) TriggerAllSlosh();
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("Reset All Bottles [Bksp]")) ResetAllBottles();
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
