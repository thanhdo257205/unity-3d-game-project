using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dựng các tài nguyên hình ảnh cho Kiếm Luân (chỉ chạy trong Editor, menu "Phàm Kiếm Lục"):
/// - Texture quầng tròn mờ dần ra mép (QuangTron.png) và vật liệu cộng sáng VFX_QuangKiemLuan (sao từ vật liệu vệt kiếm).
/// - Vật liệu trong suốt ThanhTrucKiem_Mo (bản sao ThanhTrucKiem_Mat, alpha blend) rồi gán vào ô "Fade Material" của prefab phi kiếm.
/// Không ghi đè asset đã có (muốn dựng lại thì xóa asset cũ rồi chạy lại).
/// </summary>
public static class WheelVisualBuilder
{
    private const string TexturePath = "Assets/_Game/VFX/Textures/QuangTron.png";
    private const string HaloMatPath = "Assets/_Game/Materials/VFX_QuangKiemLuan.mat";
    private const string SolidMatPath = "Assets/_Game/Materials/ThanhTrucKiem_Mat.mat";
    private const string FadeMatPath = "Assets/_Game/Materials/ThanhTrucKiem_Mo.mat";
    private const string TrailMatPath = "Assets/_Game/Materials/VFX_VetKiem.mat";
    private const string SwordPrefabPath = "Assets/_Game/Prefabs/ThanhTrucKiem.prefab";

    [MenuItem("Phàm Kiếm Lục/Dựng vật liệu Kiếm Luân (quầng sáng, kiếm mờ)")]
    public static void Build()
    {
        Texture2D tex = BuildHaloTexture();
        BuildHaloMaterial(tex);
        Material fade = BuildFadeMaterial();
        AssignFadeMaterial(fade);
        AssetDatabase.SaveAssets();
        Debug.Log("[Kiếm Luân] Đã dựng quầng sáng và vật liệu kiếm mờ.");
    }

    private static Texture2D BuildHaloTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null) return existing;

        const int size = 256;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);                 // 0 ở tâm, 1 ở mép
                float a = 1f - Mathf.SmoothStep(0.15f, 1f, r);       // đậm ở giữa, mờ dần tới mép
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        t.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
        File.WriteAllBytes(TexturePath, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(TexturePath);

        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    private static void BuildHaloMaterial(Texture2D tex)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(HaloMatPath) != null) return;
        Material trail = AssetDatabase.LoadAssetAtPath<Material>(TrailMatPath);
        if (trail == null) { Debug.LogError("Thiếu vật liệu " + TrailMatPath); return; }

        Material m = new Material(trail) { name = "VFX_QuangKiemLuan" };   // sao cấu hình cộng sáng (additive) của vệt kiếm
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", new Color(0.3f, 1f, 0.65f, 1f));
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);              // hai mặt
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        AssetDatabase.CreateAsset(m, HaloMatPath);
    }

    private static Material BuildFadeMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(FadeMatPath);
        if (existing != null) return existing;
        Material solid = AssetDatabase.LoadAssetAtPath<Material>(SolidMatPath);
        if (solid == null) { Debug.LogError("Thiếu vật liệu " + SolidMatPath); return null; }

        Material m = new Material(solid) { name = "ThanhTrucKiem_Mo" };
        // Chuyển URP Lit sang trong suốt (alpha blend), giống nút Surface Type = Transparent trong Inspector
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetShaderPassEnabled("ShadowCaster", false);   // kiếm đang mờ không đổ bóng đậm lên nhân vật
        AssetDatabase.CreateAsset(m, FadeMatPath);
        return m;
    }

    private static void AssignFadeMaterial(Material fade)
    {
        if (fade == null) return;
        GameObject root = PrefabUtility.LoadPrefabContents(SwordPrefabPath);
        try
        {
            FlyingSword sword = root.GetComponent<FlyingSword>();
            if (sword == null) { Debug.LogError("Prefab phi kiếm thiếu FlyingSword."); return; }
            SerializedObject so = new SerializedObject(sword);
            SerializedProperty p = so.FindProperty("fadeMaterial");
            p.objectReferenceValue = fade;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SwordPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
