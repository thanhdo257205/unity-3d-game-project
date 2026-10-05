#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Dựng sấm sét cho Phong Lôi Sí (xem WingLightning):
/// - "Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Dựng bản thử trong TestVFX": đặt một cánh (instance của prefab) vào scene TestVFX để xem trước.
/// - "Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Gắn vào prefab PhongLoiSi": dựng thẳng vào Assets/_Game/Prefabs/PhongLoiSi.prefab.
/// Việc dựng: (1) vẽ lại các ảnh tia sét / dải lửa (ảnh do code sinh) và tạo các vật liệu; (2) tạo 8 điểm sét (Transform rỗng "Set_Diem_*") làm con của các
/// xương cánh Canh_T1..3 / Canh_P1..3, rải từ gốc tới đầu cánh theo đường xương; (3) thêm WingLightning vào đối tượng con "HieuUngSet" và gán vật liệu.
/// Các tia, vệt, quầng, mảng tối, đèn do WingLightning tự tạo lúc chạy. Mỗi lần dựng sẽ xóa bản "HieuUngSet" cũ rồi dựng lại. Không đụng tới mô hình.
/// </summary>
public static class WingLightningBuilder
{
    private const string PrefabPath = "Assets/_Game/Prefabs/PhongLoiSi.prefab";
    private const string ScenePath = "Assets/_Game/Scenes/TestVFX.unity";
    private const string BenchName = "Test_PhongLoiSi_Set";
    private const string FolderName = "HieuUngSet";
    private const string MatBase = "Assets/_Game/Materials/VFX_VetKiem.mat";
    private const string TexDir = "Assets/_Game/VFX/Textures/";
    private const string MatDir = "Assets/_Game/Materials/";

    /// <summary>Độ sáng lớp ngoài so với lõi trong ảnh tia (lõi = 1). Lõi HDR ~9 thì lớp ngoài ~1.1: còn đủ màu vàng/xanh, không bị cắt thành trắng.</summary>
    public static float HaloLevel = 0.05f;

    /// <summary>Độ chụm của lớp ngoài: số mũ càng cao càng chỉ sáng sát lõi.</summary>
    public static float HaloPower = 2.2f;

    /// <summary>Vẽ lại hai ảnh tia và ảnh dải lửa (không đụng prefab). Dùng khi chỉnh độ sáng lớp ngoài.</summary>
    public static void RegenerateTextures()
    {
        MakeBoltTexture("Set_Vang", new Color(1f, 0.72f, 0.2f));
        MakeBoltTexture("Set_Xanh", new Color(0.15f, 0.85f, 1f));
        MakeFireTexture();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Dựng bản thử trong TestVFX")]
    public static void BuildBench()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject bench = GameObject.Find(BenchName);
        if (bench != null) Object.DestroyImmediate(bench);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bench = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        bench.name = BenchName;
        bench.transform.position = new Vector3(0f, 1.3f, 0f);
        bench.transform.rotation = Quaternion.identity;
        if (bench.transform.Find(FolderName) == null) Build(bench);   // prefab đã có sấm sét thì dùng luôn
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Sấm sét] Đã dựng bản thử trong TestVFX (đối tượng " + BenchName + "). Bấm Play, chỉnh 'cuongDoSet' trong WingLightning (HieuUngSet).");
    }

    [MenuItem("Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Gắn vào prefab PhongLoiSi")]
    public static void BuildPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Build(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Sấm sét] Đã gắn sấm sét vào " + PrefabPath);
    }

    // ================= Ảnh và vật liệu =================

    /// <summary>Smoothstep kiểu shader (0 dưới e0, 1 trên e1). Lưu ý: Mathf.SmoothStep của Unity KHÔNG phải hàm này (nó nội suy giữa hai số).</summary>
    private static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    private static void SaveTexture(string path, Texture2D t, bool srgb, TextureWrapMode wrap, bool mips)
    {
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.sRGBTexture = srgb;                                              // tắt sRGB để số trong ảnh là số thật nhân vào màu
        imp.textureCompression = TextureImporterCompression.Uncompressed;    // không nén: nén làm mép còn giá trị lớn hơn 0
        imp.mipmapEnabled = mips;
        imp.wrapMode = wrap;
        imp.alphaIsTransparency = false;
        imp.SaveAndReimport();
    }

    /// <summary>
    /// Mặt cắt ngang tia (trục v, giữa dải ở v = 0.5): LÕI gần trắng rộng 1/4 bề ngang dải, nét, viền lõi gắt (chuyển trong 0.20-0.28 của nửa bề ngang);
    /// bao quanh là LỚP NGOÀI màu vàng kim / xanh lơ, sáng bằng ~0.22 lõi, giảm dần về ĐÚNG 0 trước mép ảnh. Cộng sáng: đen = trong suốt, alpha luôn 1.
    /// Tỉ lệ lõi / bề ngang phải khớp WingLightning.CoreFraction. Luôn vẽ lại (ảnh do code sinh).
    /// </summary>
    public static Texture2D MakeBoltTexture(string name, Color halo, float haloLevel = -1f)
    {
        string path = TexDir + name + ".png";
        const int h = 128;
        Texture2D t = new Texture2D(4, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float p = Mathf.Abs((y + 0.5f) / h * 2f - 1f);             // 0 giữa, 1 mép
            float core = 1f - Smooth(0.20f, 0.28f, p);
            float q = Mathf.Clamp01((p - 0.25f) / 0.75f);
            float ring = Mathf.Pow(1f - q, HaloPower) * (haloLevel >= 0f ? haloLevel : HaloLevel);
            if (p > 0.97f) ring = 0f;                                  // chắc chắn đen ở mép
            Color c = Color.Lerp(halo * ring, new Color(1f, 1f, 0.96f), core);
            c.a = 1f;
            for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
        }
        t.Apply();
        SaveTexture(path, t, false, TextureWrapMode.Clamp, false);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Nhiễu giá trị tuần hoàn theo trục x (chu kỳ periodX ô) để ảnh lát được dọc theo vệt
    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    private static float PeriodicNoise(float x, float y, int periodX, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int xa = ((x0 % periodX) + periodX) % periodX, xb = (xa + 1) % periodX;
        float a = Mathf.Lerp(Hash(xa, y0, seed), Hash(xb, y0, seed), fx);
        float b = Mathf.Lerp(Hash(xa, y0 + 1, seed), Hash(xb, y0 + 1, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>
    /// Ảnh dải lửa dạng SỢI: trục u (dọc theo vệt) đổi chậm, trục v (ngang vệt) đổi nhanh nên hoa văn là các sợi dài kéo dọc vệt (không phải tổ ong).
    /// Lát được theo u. Độ đậm nhân thêm hình cắt ngang mượt về ĐÚNG 0 ở hai mép nên dải không có cạnh thẳng. Trắng + alpha; cộng sáng nên alpha = độ sáng.
    /// </summary>
    private static Texture2D MakeFireTexture() { return MakeFibreTexture("DaiLua_Soi", true); }

    /// <summary>Ảnh sợi dài (xem MakeFireTexture). gold = giữa dải gần trắng, ra mép vàng kim; ngược lại trắng hoàn toàn (nhuộm màu bằng vật liệu). Dùng chung cho vệt cánh và khói cổng không gian.</summary>
    public static Texture2D MakeFibreTexture(string fileName, bool gold)
    {
        string path = TexDir + fileName + ".png";
        const int w = 256, h = 128;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float edge = 1f - Mathf.Abs(v * 2f - 1f);                  // 0 ở mép, 1 giữa
            float profile = Mathf.SmoothStep(0f, 1f, Mathf.Pow(edge, 0.9f));
            if (y == 0 || y == h - 1) profile = 0f;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                // Ba tầng sợi: u đổi chậm (chu kỳ 4, 8, 16 ô dọc), v đổi nhanh (10, 22, 46 ô ngang)
                float n = PeriodicNoise(u * 4f, v * 10f, 4, 1) * 0.5f + PeriodicNoise(u * 8f, v * 22f, 8, 2) * 0.32f + PeriodicNoise(u * 16f, v * 46f, 16, 3) * 0.18f;
                // Sợi rõ, khe giữa các sợi trong suốt hẳn (nhìn xuyên qua được)
                float fibre = Mathf.Clamp01((n - 0.40f) / 0.28f);
                fibre = fibre * fibre * (3f - 2f * fibre);
                float a = Mathf.Clamp01(fibre * 1.15f * profile);
                // Giữa dải gần trắng, ra mép chuyển vàng kim
                Color c = gold ? Color.Lerp(new Color(1f, 0.72f, 0.3f), Color.white, Mathf.Pow(edge, 1.4f)) : Color.white;
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        }
        t.Apply();
        SaveTexture(path, t, false, TextureWrapMode.Repeat, true);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Vật liệu hạt cộng sáng (sao chép từ VFX_VetKiem). Mọi tia, dải lửa, tia lửa dùng loại này.</summary>
    public static Material MakeMat(string name, Texture tex, Color color, bool additive = true)
    {
        string path = MatDir + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(MatBase);
        if (baseMat == null) { Debug.LogError("[Sấm sét] Thiếu " + MatBase); return null; }
        if (m == null)
        {
            m = new Material(baseMat) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Vật liệu quầng tròn (shader PhamKiemLuc/QuangTron): additive = cộng sáng (quầng sáng), ngược lại trộn alpha (mảng tối).</summary>
    public static Material MakeRoundMat(string name, bool additive, Color inner, Color outer, float alpha, float power, int queue)
    {
        string path = MatDir + name + ".mat";
        Shader sh = Shader.Find("PhamKiemLuc/QuangTron");
        if (sh == null) { Debug.LogError("[Sấm sét] Không thấy shader PhamKiemLuc/QuangTron."); return null; }
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetColor("_ColorInner", inner);
        m.SetColor("_ColorOuter", outer);
        m.SetFloat("_Alpha", alpha);
        m.SetFloat("_Power", power);
        m.SetFloat("_Additive", additive ? 1f : 0f);
        m.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.renderQueue = queue;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================= Dựng =================

    public static void Build(GameObject root)
    {
        Texture2D vang = MakeBoltTexture("Set_Vang", new Color(1f, 0.72f, 0.2f));
        Texture2D xanh = MakeBoltTexture("Set_Xanh", new Color(0.15f, 0.85f, 1f));
        Texture2D soi = MakeFireTexture();
        Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "QuangTron.png");
        Material mGold = MakeMat("VFX_SetVang", vang, Color.white);
        Material mBlue = MakeMat("VFX_SetXanh", xanh, Color.white);
        // quầng sáng sau lưng: tâm vàng, rìa xanh, cộng sáng; mảng tối sau cánh: xanh đen, trộn alpha, vẽ trước (dưới) mọi tia
        Material mGlow = MakeRoundMat("VFX_QuangLungSet", true, new Color(1.6f, 1.25f, 0.5f), new Color(0.2f, 0.9f, 1.8f), 0.3f, 1.6f, 2995);
        Material mDark = MakeRoundMat("VFX_MangToiSet", false, new Color(0.02f, 0.05f, 0.12f), new Color(0.02f, 0.05f, 0.12f), 0.3f, 0.6f, 2990);
        // dải lửa: sợi dài, cam HDR; dải xanh mảnh dùng ảnh tia xanh
        Material mFire = MakeMat("VFX_DaiLua", soi, new Color(1.7f, 1.45f, 1.05f, 1f), false);
        Material mRib = MakeMat("VFX_DaiXanh", xanh, new Color(0.6f, 1.5f, 2.1f, 1f));
        Material mSpark = MakeMat("VFX_TiaLuaCanh", dot, new Color(1f, 1f, 1f, 1f));
        AssetDatabase.SaveAssets();

        Transform old = root.transform.Find(FolderName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        List<GameObject> stale = new List<GameObject>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Set_Diem_")) stale.Add(t.gameObject);
        foreach (GameObject g in stale) if (g != null) Object.DestroyImmediate(g);

        Transform[][] bones = { FindBones(root.transform, "Canh_T"), FindBones(root.transform, "Canh_P") };
        if (bones[0][0] == null || bones[1][0] == null) { Debug.LogError("[Sấm sét] Không thấy xương cánh Canh_T1..3 / Canh_P1..3."); return; }

        GameObject folder = new GameObject(FolderName);
        folder.transform.SetParent(root.transform, false);

        Transform[][] points = new Transform[2][];
        for (int s = 0; s < 2; s++) points[s] = MakePoints(bones[s], s == 0 ? "T" : "P");
        // Mảng [0] = cánh nằm ở -X (trái), [1] = cánh ở +X (phải)
        if (points[0][3].position.x > points[1][3].position.x) { Transform[] tmp = points[0]; points[0] = points[1]; points[1] = tmp; }

        VisualEffect dust = MakeVfx("BuiDapDat", folder.transform, AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/_Game/VFX/BuiDapDat.vfx"));

        WingLightning wl = folder.AddComponent<WingLightning>();
        SerializedObject so = new SerializedObject(wl);
        SetArray(so.FindProperty("pointsLeft"), points[0]);
        SetArray(so.FindProperty("pointsRight"), points[1]);
        so.FindProperty("matGold").objectReferenceValue = mGold;
        so.FindProperty("matBlue").objectReferenceValue = mBlue;
        so.FindProperty("matGlow").objectReferenceValue = mGlow;
        so.FindProperty("matDark").objectReferenceValue = mDark;
        so.FindProperty("matFire").objectReferenceValue = mFire;
        so.FindProperty("matRibbon").objectReferenceValue = mRib;
        so.FindProperty("matSpark").objectReferenceValue = mSpark;
        so.FindProperty("dustVfx").objectReferenceValue = dust;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
    }

    private static void SetArray<T>(SerializedProperty p, T[] items) where T : Object
    {
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static Transform[] FindBones(Transform root, string prefix)
    {
        Transform[] b = new Transform[3];
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            for (int i = 0; i < 3; i++) if (t.name == prefix + (i + 1)) b[i] = t;
        return b;
    }

    /// <summary>8 điểm rải đều theo đường xương: gốc -> khuỷu -> cổ tay -> đầu cánh (cổ tay kéo dài thêm một đoạn). Mỗi điểm là con của xương đoạn đó.</summary>
    private static Transform[] MakePoints(Transform[] bone, string side)
    {
        Vector3 root = bone[0].position, elbow = bone[1].position, wrist = bone[2].position;
        Vector3 tip = wrist + (wrist - elbow);
        Transform[] pts = new Transform[8];
        for (int i = 0; i < 8; i++)
        {
            float u = i / 7f;
            Vector3 p;
            Transform parent;
            if (u < 0.34f) { p = Vector3.Lerp(root, elbow, u / 0.34f); parent = bone[0]; }
            else if (u < 0.67f) { p = Vector3.Lerp(elbow, wrist, (u - 0.34f) / 0.33f); parent = bone[1]; }
            else { p = Vector3.Lerp(wrist, tip, (u - 0.67f) / 0.33f); parent = bone[2]; }
            GameObject g = new GameObject("Set_Diem_" + side + i);
            g.transform.SetParent(parent, true);
            g.transform.position = p;
            pts[i] = g.transform;
        }
        return pts;
    }

    private static VisualEffect MakeVfx(string name, Transform parent, VisualEffectAsset asset)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        VisualEffect v = g.AddComponent<VisualEffect>();
        v.visualEffectAsset = asset;
        v.initialEventName = "KhongPhat";    // không tự phát khi bật; WingLightning gửi sự kiện OnPlay khi cần
        return v;
    }
}
#endif
