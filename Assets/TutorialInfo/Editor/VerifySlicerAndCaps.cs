#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class VerifySlicerAndCaps
{
    [MenuItem("Tools/Run Slicer & Meniscus Cap Audit")]
    public static void RunAudit()
    {
        string[] testMeshes = new string[]
        {
            "Assets/Models/Week9_PetrolContainer_Fluid.obj",
            "Assets/Models/Week9_StarBox_Fluid.obj",
            "Assets/Models/Week9_ToroidalTank_Fluid.obj",
            "Assets/Models/Week9_YJunctionManifold_Fluid.obj",
            "Assets/Models/Week9_CapsuleTank_Fluid.obj",
            "Assets/Models/Week9_TreeBranched_Fluid.obj",
            "Assets/Models/Week9_MazeMultichamber_Fluid.obj",
            "Assets/Models/Flask_Conical_Fluid.obj",
            "Assets/Models/Vessel_CardioidHeart_Fluid.obj",
            "Assets/Models/Vessel_TwistedColumn_Fluid.obj"
        };

        float[] testFills = new float[] { 0.25f, 0.50f, 0.75f };
        int totalTests = 0;
        int passedTests = 0;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("================ STARTING SLICER & MENISCUS CAP AUDIT ================");

        foreach (var path in testMeshes)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                sb.AppendLine($"[AUDIT SKIP] Mesh not found: {path}");
                continue;
            }

            Bounds b = mesh.bounds;
            float yMin = b.min.y;
            float yMax = b.max.y;

            foreach (float fill in testFills)
            {
                totalTests++;
                float testY = Mathf.Lerp(yMin, yMax, fill);
                Vector3 planePoint = new Vector3(0f, testY, 0f);
                Vector3 planeNormal = Vector3.up;

                Mesh capMesh = MeshPlaneSlicer.GenerateCapMesh(mesh, planePoint, planeNormal);
                if (capMesh != null && capMesh.vertexCount >= 3 && capMesh.triangles.Length >= 3)
                {
                    passedTests++;
                    sb.AppendLine($"[AUDIT PASS] {mesh.name} at {fill * 100}% fill: Verts={capMesh.vertexCount}, Tris={capMesh.triangles.Length / 3}");
                }
                else
                {
                    sb.AppendLine($"[AUDIT FAIL] {mesh.name} at {fill * 100}% fill: capMesh is null or empty!");
                }
            }
        }

        sb.AppendLine($"================ AUDIT COMPLETE: {passedTests}/{totalTests} PASSED ================");
        File.WriteAllText("Assets/audit_results.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }
}
#endif
