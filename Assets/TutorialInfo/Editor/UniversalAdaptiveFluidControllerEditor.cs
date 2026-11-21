#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UniversalAdaptiveFluidController))]
[CanEditMultipleObjects]
public class UniversalAdaptiveFluidControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        UniversalAdaptiveFluidController ctrl = (UniversalAdaptiveFluidController)target;

        serializedObject.Update();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("<b>★ UNIVERSAL ADAPTIVE FLUID CONTROLLER</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
        EditorGUILayout.HelpBox("Adapts automatically to ANY arbitrary 3D geometry with 100% physically accurate volume profiling, top meniscus capping, sloshing dynamics, and Torricelli outflow.", MessageType.Info);

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fluidType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("breachDiameterMm"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leakDrainMultiplier"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("<b>Volumetric Fill & Shape Adaptation:</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });

        EditorGUI.BeginChangeCheck();
        float newFill = EditorGUILayout.Slider("Fill Amount (0%..100%)", ctrl.fillAmount, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(ctrl, "Change Fill Amount");
            ctrl.fillAmount = newFill;
            ctrl.UpdateFillCutPlane(true);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("0% Empty")) { Undo.RecordObject(ctrl, "0% Fill"); ctrl.fillAmount = 0.0f; ctrl.UpdateFillCutPlane(true); }
        if (GUILayout.Button("25%")) { Undo.RecordObject(ctrl, "25% Fill"); ctrl.fillAmount = 0.25f; ctrl.UpdateFillCutPlane(true); }
        if (GUILayout.Button("50%")) { Undo.RecordObject(ctrl, "50% Fill"); ctrl.fillAmount = 0.50f; ctrl.UpdateFillCutPlane(true); }
        if (GUILayout.Button("75%")) { Undo.RecordObject(ctrl, "75% Fill"); ctrl.fillAmount = 0.75f; ctrl.UpdateFillCutPlane(true); }
        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.5f);
        if (GUILayout.Button("★ 100% Full")) { Undo.RecordObject(ctrl, "100% Fill"); ctrl.fillAmount = 1.0f; ctrl.UpdateFillCutPlane(true); }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableAutoFillDrainCycle"));
        if (ctrl.enableAutoFillDrainCycle)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoFillCycleSpeed"));
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("<b>Physical Simulation & Fracture Actions:</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⚡ Trigger Slosh Impulse"))
        {
            ctrl.TriggerImpulseSlosh(0.45f);
        }

        if (ctrl.isCracked)
        {
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("🔄 Repair & Reseal"))
            {
                ctrl.RepairCrack();
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = new Color(1.0f, 0.35f, 0.25f);
            if (GUILayout.Button("💥 Puncture & Crack Vessel"))
            {
                ctrl.TriggerPuncture();
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("↺ Reset Vessel to Initial"))
        {
            ctrl.ResetVessel();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("<b>Mesh References:</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
        EditorGUILayout.PropertyField(serializedObject.FindProperty("containerRenderer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fluidRenderer"));

        if (GUILayout.Button("Auto-Resolve / Setup Inset Fluid Cavity"))
        {
            Undo.RecordObject(ctrl, "Resolve Meshes");
            ctrl.ResolveAndSetupMeshes();
            ctrl.ComputeHypsometricVolumeProfile();
            ctrl.UpdateFillCutPlane(true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
