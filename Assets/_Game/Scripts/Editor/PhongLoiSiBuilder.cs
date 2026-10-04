#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Dựng mô hình Phong Lôi Sí từ file FBX xuất từ Blender (Assets/_Game/Models/PhapBao/PhongLoiSi/PhongLoiSi.fbx):
/// - Đặt cách nhập FBX (Generic, không nhập material, chia lại 4 clip Open / Flap / Glide / Close, Flap và Glide lặp).
/// - Đặt cách nhập texture (Albedo sRGB, Normal map, MetallicSmoothness không sRGB, ảnh alpha lông không sRGB).
/// - Tạo material: hai xương cánh dùng URP Lit (Albedo + Normal + Metallic/Smoothness); lông vũ dùng shader PhamKiemLuc/LongVuLit
///   (kiểu Lit nhưng đọc màu từ vertex color, emission xanh lục ở mép lông).
/// - Tạo Animator controller PhongLoiSi_Animator và prefab Prefabs/PhongLoiSi.prefab.
/// Không ghi đè material/prefab đã có (xóa file cũ rồi chạy lại nếu muốn dựng lại).
/// Nguồn mô hình: 风雷翅 của 芒果小子 (模之屋), dùng cá nhân phi thương mại — xem GHI_CONG.txt cùng thư mục.
/// </summary>
public static class PhongLoiSiBuilder
{
    private const string Dir = "Assets/_Game/Models/PhapBao/PhongLoiSi/";
    private const string Fbx = Dir + "PhongLoiSi.fbx";
    private const string Tex = Dir + "Textures/";
    private const string MatDir = Dir + "Materials/";
    private const string ControllerPath = Dir + "PhongLoiSi_Animator.controller";
    private const string PrefabPath = "Assets/_Game/Prefabs/PhongLoiSi.prefab";

    [MenuItem("Phàm Kiếm Lục/Dựng Phong Lôi Sí (FBX, material, prefab)")]
    public static void Build()
    {
        SetupTextures();
        SetupFbx();
        Material long_ = EnsureFeatherMaterial();
        Material mL = EnsureArmMaterial("XuongCanhTrai", 0.55f);
        Material mR = EnsureArmMaterial("XuongCanhPhai", 1f);
        AnimatorController ctrl = EnsureController();
        EnsurePrefab(long_, mL, mR, ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[Phong Lôi Sí] Đã dựng xong FBX, material, controller và prefab.");
    }

    private static void SetupTextures()
    {
        string[] sRgb = { "XuongCanhTrai_Albedo", "XuongCanhPhai_Albedo" };
        string[] normals = { "XuongCanhTrai_Normal", "XuongCanhPhai_Normal" };
        string[] data = { "XuongCanhTrai_MetalSmooth", "XuongCanhPhai_MetalSmooth", "Long_Alpha" };
        foreach (string n in sRgb) Configure(Tex + n + ".png", TextureImporterType.Default, true);
        foreach (string n in normals) Configure(Tex + n + ".png", TextureImporterType.NormalMap, false);
        foreach (string n in data) Configure(Tex + n + ".png", TextureImporterType.Default, false);
    }

    private static void Configure(string path, TextureImporterType type, bool srgb)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("Thiếu texture " + path); return; }
        ti.textureType = type;
        ti.sRGBTexture = srgb;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = path.Contains("Long_Alpha");
        ti.mipmapEnabled = true;
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.SaveAndReimport();
    }

    private static void SetupFbx()
    {
        ModelImporter imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) { Debug.LogError("Thiếu " + Fbx); return; }
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.importBlendShapes = false;
        imp.importCameras = false;
        imp.importLights = false;
        imp.bakeAxisConversion = true;            // đưa phép xoay -90 và tỉ lệ 100 của FBX Blender vào dữ liệu, node gốc sạch
        imp.optimizeGameObjects = false;
        imp.SaveAndReimport();

        // Chia lại clip: chỉ giữ 4 clip "PhongLoiSi_Rig|X" đổi tên thành X
        ModelImporterClipAnimation[] defs = imp.defaultClipAnimations;
        var list = defs.Where(d => d.name.StartsWith("PhongLoiSi_Rig|")).ToList();
        for (int i = 0; i < list.Count; i++)
        {
            ModelImporterClipAnimation c = list[i];
            c.name = c.name.Substring("PhongLoiSi_Rig|".Length);
            bool loop = c.name == "Flap" || c.name == "Glide";
            c.loopTime = loop;
            c.loopPose = loop;
            list[i] = c;
        }
        imp.clipAnimations = list.ToArray();
        imp.SaveAndReimport();
    }

    private static Material EnsureFeatherMaterial()
    {
        string path = MatDir + "PhongLoiSi_Long.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        System.IO.Directory.CreateDirectory(MatDir);
        Shader sh = Shader.Find("PhamKiemLuc/LongVuLit");
        if (sh == null) { Debug.LogError("Thiếu shader PhamKiemLuc/LongVuLit"); return null; }
        m = new Material(sh) { name = "PhongLoiSi_Long" };
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "Long_Alpha.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetColor("_EdgeEmission", new Color(0.25f, 1.6f, 0.7f, 1f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Material EnsureArmMaterial(string key, float metalScale)
    {
        string path = MatDir + "PhongLoiSi_" + key + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(sh) { name = "PhongLoiSi_" + key };
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + key + "_Albedo.png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + key + "_Normal.png"));
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + key + "_MetalSmooth.png"));
        m.EnableKeyword("_NORMALMAP");
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_BumpScale", 1f);
        m.SetFloat("_SmoothnessTextureChannel", 0f);   // smoothness lấy từ alpha của MetallicGlossMap
        m.SetFloat("_Smoothness", 1f);
        m.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static AnimatorController EnsureController()
    {
        AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (c != null) return c;
        c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        foreach (string n in new[] { "Open", "Flap", "Glide", "Close" })
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().FirstOrDefault(x => x.name == n);
            AnimatorState st = sm.AddState(n);
            st.motion = clip;
            if (n == "Glide") sm.defaultState = st;
        }
        return c;
    }

    private static void EnsurePrefab(Material long_, Material mL, Material mR, AnimatorController ctrl)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        GameObject root = new GameObject("PhongLoiSi");
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
        inst.name = "Mo_Hinh";
        foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = smr.name.StartsWith("Long_") ? long_ : (smr.name.EndsWith("Trai") ? mL : mR);
            smr.renderingLayerMask = 3;                                   // sáng bởi mặt trời (lớp 0 và 1) như nhân vật
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.updateWhenOffscreen = true;
        }
        Animator anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }
}
#endif
