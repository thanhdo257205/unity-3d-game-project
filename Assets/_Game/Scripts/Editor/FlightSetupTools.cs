#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Chuẩn bị Animator cho Phong Lôi Sí (menu "Phàm Kiếm Lục/Dựng animation bay"):
/// - Nhập clip NhanVat@Bay.fbx: Humanoid, Avatar riêng tạo từ chính file (như các clip chém), không nhập material, lặp (Loop Time + Loop Pose),
///   bake Root Y và Rotation vào pose, KHÔNG bake XZ (chuyển động tiến của clip gốc được bỏ đi vì Apply Root Motion tắt; bake XZ làm thân trôi khỏi nhân vật).
/// - Thêm trạng thái "Bay" vào lớp nền của NhanVat_Animator.
/// - Thêm các trạng thái "Dam1_Bay", "Dam2_Bay", "Da_Bay", "Chem1_Bay", "Chem2_Bay", "Chem3_Bay" vào lớp thân trên (UpperBody) để vừa bay vừa chém
///   (cùng clip với trạng thái trên mặt đất, tốc độ theo tham số AtkSpeed).
/// Không ghi đè trạng thái đã có.
/// </summary>
public static class FlightSetupTools
{
    private const string Dir = "Assets/_Game/Models/Characters/NhanVat/";
    private const string ControllerPath = Dir + "NhanVat_Animator.controller";

    [MenuItem("Phàm Kiếm Lục/Dựng animation bay (Bay + chém khi bay)")]
    public static void Build()
    {
        ImportBayClip();
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine baseSm = ctrl.layers[0].stateMachine;

        AnimationClip bay = LoadClip("NhanVat@Bay.fbx", "Bay");
        if (bay != null && baseSm.states.All(s => s.state.name != "Bay"))
        {
            AnimatorState st = baseSm.AddState("Bay");
            st.motion = bay;
        }

        int upperIdx = ctrl.layers.ToList().FindIndex(l => l.name == "UpperBody");
        if (upperIdx >= 0)
        {
            AnimatorStateMachine up = ctrl.layers[upperIdx].stateMachine;
            foreach (string n in new[] { "Dam1", "Dam2", "Da", "Chem1", "Chem2", "Chem3" })
            {
                string nn = n + "_Bay";
                if (up.states.Any(s => s.state.name == nn)) continue;
                ChildAnimatorState src = baseSm.states.FirstOrDefault(s => s.state.name == n);
                if (src.state == null || src.state.motion == null) { Debug.LogWarning("Không tìm thấy trạng thái " + n); continue; }
                AnimatorState st = up.AddState(nn);
                st.motion = src.state.motion;
                st.speedParameterActive = true;
                st.speedParameter = "AtkSpeed";
            }
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[Bay] Đã dựng trạng thái Bay và các trạng thái chém khi bay.");
    }

    private static void ImportBayClip()
    {
        string path = Dir + "NhanVat@Bay.fbx";
        ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) { Debug.LogError("Thiếu " + path); return; }
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.importBlendShapes = false;
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;   // Avatar riêng (xương lệch vài mm so với Idle nên không "Copy From Other" được); Humanoid vẫn retarget sang nhân vật chính
        imp.importAnimation = true;
        imp.clipAnimations = new ModelImporterClipAnimation[0];
        imp.SaveAndReimport();
        ModelImporterClipAnimation[] defs = imp.defaultClipAnimations;
        for (int k = 0; k < defs.Length; k++)
        {
            defs[k].name = "Bay";
            defs[k].loopTime = true;
            defs[k].loopPose = true;
            defs[k].lockRootPositionXZ = false;     // chuyển động tiến của clip gốc bỏ đi (root motion tắt)
            defs[k].lockRootHeightY = true;         // giữ độ cao hông như clip
            defs[k].lockRootRotation = true;        // giữ tư thế nằm nghiêng như clip
            defs[k].keepOriginalPositionXZ = true;
            defs[k].keepOriginalPositionY = true;
            defs[k].keepOriginalOrientation = true;
        }
        imp.clipAnimations = defs;
        imp.SaveAndReimport();
    }

    private static AnimationClip LoadClip(string file, string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(Dir + file).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
    }
}
#endif
