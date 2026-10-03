#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Công cụ trong Editor để THAY CLIP CHÉM mà không đụng tới sát thương và thời điểm va chạm.
/// Cách làm: chép 3 file Mixamo mới đè lên NhanVat@Chem1.fbx, NhanVat@Chem2.fbx, NhanVat@Chem3.fbx
/// (Assets/_Game/Models/Characters/NhanVat/), rồi chạy menu Phàm Kiếm Lục → Thay clip chém.
/// Công cụ: đặt lại cách nhập (Humanoid, tạo Avatar từ chính file, không nhập material, không bake vị trí, lấy cả clip),
/// gán clip vào ba trạng thái Chem1-3 của NhanVat_Animator, và đo chuyển động tay phải để GỢI Ý ba mốc trong clip
/// (bắt đầu, trúng đòn, kết thúc) cho từng nhát. Sát thương và "thời gian thật" (tổng, trúng đòn) của từng nhát
/// nằm ở PlayerMelee → Swordsteps và KHÔNG đổi; chỉ cần nhập ba mốc gợi ý vào "Mốc trong clip" của nhát tương ứng.
/// </summary>
public static class SlashClipTools
{
    private const string Dir = "Assets/_Game/Models/Characters/NhanVat/";
    private const string ControllerPath = Dir + "NhanVat_Animator.controller";
    private const string ModelPrefab = "Assets/_Game/Prefabs/NhanVat_Model.prefab";
    private static readonly string[] Names = { "Chem1", "Chem2", "Chem3" };

    [MenuItem("Phàm Kiếm Lục/Thay clip chém (3 file NhanVat@Chem1-3.fbx)")]
    public static void ApplyMenu() { Debug.Log(Apply()); }

    /// <summary>Nhập lại ba file clip chém, gán vào Animator và trả về báo cáo (kèm mốc gợi ý).</summary>
    public static string Apply()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Names.Length; i++)
        {
            string path = Dir + "NhanVat@" + Names[i] + ".fbx";
            if (AssetImporter.GetAtPath(path) == null) { sb.AppendLine("Thiếu file " + path); continue; }
            ModelImporter imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importBlendShapes = false;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.clipAnimations = new ModelImporterClipAnimation[0];   // bỏ khoảng khung hình cũ, lấy lại cả clip
            imp.SaveAndReimport();
            ModelImporterClipAnimation[] defs = imp.defaultClipAnimations;
            for (int k = 0; k < defs.Length; k++)
            {
                defs[k].name = Names[i];
                defs[k].loopTime = false;
                defs[k].loopPose = false;
                defs[k].lockRootPositionXZ = false;     // không bake chuyển động ngang vào thân
                defs[k].lockRootHeightY = false;
                defs[k].lockRootRotation = false;
                defs[k].keepOriginalPositionY = false;  // Y theo bàn chân (như các clip Dam/Da)
                defs[k].heightFromFeet = true;
                defs[k].keepOriginalOrientation = true;
                defs[k].keepOriginalPositionXZ = false;
            }
            imp.clipAnimations = defs;
            imp.SaveAndReimport();
        }

        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        for (int i = 0; i < Names.Length; i++)
        {
            AnimationClip clip = LoadClip(Names[i]);
            ChildAnimatorState st = sm.states.FirstOrDefault(x => x.state.name == Names[i]);
            if (st.state == null || clip == null) { sb.AppendLine("Không gán được " + Names[i]); continue; }
            st.state.motion = clip;
            sb.AppendLine(Names[i] + ": dài " + clip.length.ToString("F2") + " giây, " + clip.frameRate + " khung/giây. " + Suggest(clip));
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    private static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(Dir + "NhanVat@" + name + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
    }

    /// <summary>
    /// Đo tốc độ bàn tay phải của clip (trên mô hình nhân vật) rồi gợi ý: trúng đòn = lúc tay nhanh nhất,
    /// bắt đầu = lúc tay bắt đầu tăng tốc trước đó, kết thúc = lúc tay đã chậm lại sau đó.
    /// </summary>
    public static string Suggest(AnimationClip clip)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab);
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            Animator anim = go.GetComponentInChildren<Animator>();
            Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            float dt = 1f / 30f;
            int n = Mathf.CeilToInt(clip.length / dt) + 1;
            float[] speed = new float[n];
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                clip.SampleAnimation(anim.gameObject, Mathf.Min(i * dt, clip.length));
                Vector3 p = go.transform.InverseTransformPoint(hand.position);
                speed[i] = i == 0 ? 0f : (p - prev).magnitude / dt;
                prev = p;
            }
            int peak = 0;
            for (int i = 1; i < n; i++) if (speed[i] > speed[peak]) peak = i;
            float thr = speed[peak] * 0.2f;
            int a = peak; while (a > 0 && speed[a] > thr) a--;
            int b = peak; while (b < n - 1 && speed[b] > thr) b++;
            float start = Mathf.Max(0f, a * dt - 0.1f);
            float hit = peak * dt;
            float end = Mathf.Min(clip.length, b * dt + 0.1f);
            return "Gợi ý mốc trong clip: bắt đầu " + start.ToString("F2") + ", trúng đòn " + hit.ToString("F2") + ", kết thúc " + end.ToString("F2") + " (tay nhanh nhất " + speed[peak].ToString("F1") + " m/s).";
        }
        finally { Object.DestroyImmediate(go); }
    }
}
#endif
