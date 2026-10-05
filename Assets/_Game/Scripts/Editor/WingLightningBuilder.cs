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
/// Việc dựng: (1) vẽ các ảnh tia sét/quầng (nếu chưa có) và các vật liệu cộng sáng; (2) tạo 8 điểm sét (Transform rỗng "Set_Diem_*") làm con của các
/// xương cánh Canh_T1..3 / Canh_P1..3, rải từ gốc tới đầu cánh theo đường xương; (3) thêm WingLightning vào đối tượng con "HieuUngSet" và gán vật liệu.
/// Các tia, vệt, quầng, đèn do WingLightning tự tạo lúc chạy. Mỗi lần dựng sẽ xóa bản "HieuUngSet" cũ rồi dựng lại. Không đụng tới mô hình.
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

    private static Texture2D MakeBoltTexture(string name, Color inner, Color outer)
    {
        string path = TexDir + name + ".png";
        if (!File.Exists(path))
        {
            // Mặt cắt ngang tia (trục v): giữa gần trắng -> màu -> màu cam/xanh đậm -> đen ở mép (cộng sáng nên đen = trong suốt)
            const int h = 64;
            Texture2D t = new Texture2D(4, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float p = Mathf.Abs((y + 0.5f) / h * 2f - 1f);              // 0 giữa, 1 mép
                Color c = Color.Lerp(new Color(1f, 1f, 0.9f), inner, Mathf.SmoothStep(0.03f, 0.14f, p));
                c = Color.Lerp(c, outer, Mathf.SmoothStep(0.3f, 0.8f, p));
                float fade = 1f - Mathf.SmoothStep(0.55f, 1f, p);   // đậm đặc tới giữa dải rồi mới mờ ra mép
                c = new Color(c.r, c.g, c.b, fade);   // alpha = hình tia, màu giữ nguyên (trộn alpha để màu hiện rõ cả trên nền sáng)
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Ảnh nhiễu cho dải lửa: lấy độ sáng của RidgedPerlin.png làm alpha, màu trắng (màu cam do vật liệu nhuộm), nền dày hơn để dải lửa đặc.</summary>
    private static Texture2D MakeFireTexture()
    {
        string path = TexDir + "DaiLua_Nhieu.png";
        if (!File.Exists(path))
        {
            Texture2D src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(TexDir + "RidgedPerlin.png"));
            Color[] px = src.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                float g = px[i].grayscale;
                float a = Mathf.Clamp01(0.05f + Mathf.Pow(g, 1.2f) * 1.25f);   // tương phản cao để lửa có vệt cuộn rõ
                px[i] = new Color(1f, 1f, 1f, a);
            }
            Texture2D t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D MakeGlowTexture()
    {
        string path = TexDir + "QuangLungSet.png";
        if (!File.Exists(path))
        {
            const int n = 128;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color gold = new Color(1f, 0.78f, 0.3f), blue = new Color(0.12f, 0.6f, 1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c = Color.Lerp(gold, blue, Mathf.SmoothStep(0.1f, 0.75f, r));    // vàng ở tâm, xanh ở rìa
                    float f = 1f - Mathf.SmoothStep(0f, 1f, r);
                    f *= f;
                    t.SetPixel(x, y, new Color(c.r * f, c.g * f, c.b * f, 1f));
                }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material MakeMat(string name, Texture tex, Color color, bool alphaBlend = false)
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
        if (alphaBlend)
        {
            // Trộn alpha thường (không cộng sáng) để màu vàng/xanh hiện rõ cả trên nền trời sáng; độ chói HDR vẫn cho Bloom bắt
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================= Dựng =================

    public static void Build(GameObject root)
    {
        Texture2D vang = MakeBoltTexture("Set_Vang", new Color(1f, 0.55f, 0f), new Color(1f, 0.22f, 0f));
        Texture2D xanh = MakeBoltTexture("Set_Xanh", new Color(0f, 0.65f, 1f), new Color(0f, 0.22f, 1f));
        Texture2D glow = MakeGlowTexture();
        Texture2D ridged = MakeFireTexture();
        Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "QuangTron.png");
        Material mGold = MakeMat("VFX_SetVang", vang, Color.white * 1.1f, true);
        Material mBlue = MakeMat("VFX_SetXanh", xanh, Color.white * 1.1f, true);
        Material mGlow = MakeMat("VFX_QuangLungSet", glow, new Color(1.6f, 1.6f, 1.6f, 0.1f));
        // màu cam HDR; ảnh nhiễu trắng + alpha
        Material mFire = MakeMat("VFX_DaiLua", ridged, new Color(2.4f, 1.0f, 0.12f, 1.2f), true);
        Material mRib = MakeMat("VFX_DaiXanh", xanh, new Color(1.6f, 1.6f, 1.6f, 1f), true);
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
