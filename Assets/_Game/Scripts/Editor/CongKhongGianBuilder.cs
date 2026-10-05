#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dựng cổng không gian của Phá Không (lượt 3A, chỉ phần nhìn):
/// - "Phàm Kiếm Lục/Cổng không gian/Dựng prefab CongKhongGian": vẽ ảnh khói, tạo vật liệu, dựng Assets/_Game/VFX/CongKhongGian.prefab
///   (tấm xoáy, quầng sáng, 3 hệ hạt, đèn điểm, script CongKhongGian). Dựng lại sẽ ghi đè prefab này.
/// - "Phàm Kiếm Lục/Cổng không gian/Dựng scene TestPhaKhong": tạo scene Assets/_Game/Scenes/TestPhaKhong.unity từ SanTap (cùng trời chiều, mặt đất, khối hộp),
///   bỏ hết phần chơi (nhân vật điều khiển, UI, kẻ địch), đặt hai cổng, một bản NhanVat_Model đứng cạnh để so kích thước. Không đụng PlayerController.
/// </summary>
public static class CongKhongGianBuilder
{
    private const string PrefabPath = "Assets/_Game/VFX/CongKhongGian.prefab";
    private const string ScenePath = "Assets/_Game/Scenes/TestPhaKhong.unity";
    private const string SourceScene = "Assets/_Game/Scenes/SanTap.unity";
    private const string TexDir = "Assets/_Game/VFX/Textures/";
    private const string MatDir = "Assets/_Game/Materials/";

    [MenuItem("Phàm Kiếm Lục/Cổng không gian/Dựng prefab CongKhongGian")]
    public static void BuildPrefab()
    {
        Texture2D smoke = WingLightningBuilder.MakeFibreTexture("KhoiCong_Soi", false);
        Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "QuangTron.png");
        Shader sh = Shader.Find("PhamKiemLuc/CongKhongGian");
        if (sh == null) { Debug.LogError("[Cổng] Không thấy shader PhamKiemLuc/CongKhongGian."); return; }

        string portalMatPath = MatDir + "VFX_CongKhongGian.mat";
        Material mPortal = AssetDatabase.LoadAssetAtPath<Material>(portalMatPath);
        if (mPortal == null) { mPortal = new Material(sh) { name = "VFX_CongKhongGian" }; AssetDatabase.CreateAsset(mPortal, portalMatPath); }
        mPortal.shader = sh;
        mPortal.SetTexture("_NoiseA", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "RidgedPerlin.png"));
        mPortal.SetTexture("_NoiseB", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "CellularNoise01.png"));
        mPortal.renderQueue = 2998;
        EditorUtility.SetDirty(mPortal);

        Material mGlow = WingLightningBuilder.MakeRoundMat("VFX_QuangCong", true, new Color(1.6f, 1.15f, 0.45f), new Color(0.2f, 0.8f, 1.8f), 0.3f, 1.4f, 2996);
        Material mSmoke = WingLightningBuilder.MakeMat("VFX_KhoiCong", smoke, new Color(1.0f, 1.5f, 2.1f, 1f));
        Material mSpark = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "VFX_TiaLuaCanh.mat");
        Material mGold = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "VFX_SetVang.mat");
        Material mBlue = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "VFX_SetXanh.mat");
        AssetDatabase.SaveAssets();

        GameObject root = new GameObject("CongKhongGian");
        CongKhongGian cg = root.AddComponent<CongKhongGian>();

        Renderer tam = MakeQuad("Tam_Xoay", root.transform, mPortal, Vector3.zero);
        Renderer quang = MakeQuad("Tam_Quang", root.transform, mGlow, new Vector3(0f, 0f, 0.05f));

        ParticleSystem khoi = MakeTrailSystem("Khoi_Xoay", root.transform, mSmoke, 1.5f, 0.85f, 1.2f, 0.5f, 2.6f, -0.35f, 0.14f);
        ParticleSystem vang = MakeTrailSystem("Khoi_Vang", root.transform, mSmoke, 1.4f, 0.15f, 1.0f, 0.7f, 2.0f, 1.3f, 0.1f);
        ParticleSystem dom = MakeSparkSystem("Dom_Hut", root.transform, mSpark);

        GameObject lg = new GameObject("Den_Cong");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = new Vector3(0f, 0f, -0.9f);
        Light light = lg.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.75f, 0.85f, 1f);
        light.range = 6f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        lg.AddComponent<UniversalAdditionalLightData>().renderingLayers = 1u;

        SerializedObject so = new SerializedObject(cg);
        so.FindProperty("tamXoay").objectReferenceValue = tam;
        so.FindProperty("tamQuang").objectReferenceValue = quang;
        so.FindProperty("khoiXoay").objectReferenceValue = khoi;
        so.FindProperty("khoiVang").objectReferenceValue = vang;
        so.FindProperty("domHut").objectReferenceValue = dom;
        so.FindProperty("den").objectReferenceValue = light;
        so.FindProperty("matSetVang").objectReferenceValue = mGold;
        so.FindProperty("matSetXanh").objectReferenceValue = mBlue;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cổng] Đã dựng " + PrefabPath);
    }

    private static Renderer MakeQuad(string name, Transform parent, Material mat, Vector3 localPos)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        MeshRenderer mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    private static ParticleSystem BaseSystem(string name, Transform parent, float radius, float thickness, float life, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = life;
        main.startSpeed = 0f;
        main.startSize = size;
        main.maxParticles = 400;
        var em = ps.emission; em.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;   // đĩa nằm trong mặt phẳng XY của cổng
        shape.radius = radius;
        shape.radiusThickness = thickness;
        shape.arc = 360f;
        return ps;
    }

    private static void Orbit(ParticleSystem ps, float orbitalZ, float radial)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = 0f; vel.y = 0f; vel.z = 0f;
        vel.orbitalX = 0f; vel.orbitalY = 0f;
        vel.orbitalZ = orbitalZ;       // cuốn theo chiều xoáy
        vel.radial = radial;           // âm = hút vào tâm, dương = văng ra ngoài
    }

    /// <summary>Dải khói: hạt không vẽ, chỉ vẽ VỆT của hạt (trails) bay xoắn quanh tâm. Vệt thuôn nhọn, mờ dần.</summary>
    private static ParticleSystem MakeTrailSystem(string name, Transform parent, Material mat, float radius, float thickness, float life, float trailLife, float orbital, float radial, float size)
    {
        ParticleSystem ps = BaseSystem(name, parent, radius, thickness, life, size);
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(0.55f, 0.9f, 1f, 0.9f));
        Orbit(ps, orbital, radial);
        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = trailLife;
        trails.minVertexDistance = 0.04f;
        trails.worldSpace = false;
        trails.dieWithParticles = false;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.sizeAffectsWidth = true;
        trails.inheritParticleColor = true;
        // Đầu vệt vào mượt (hẹp + trong suốt rồi đậm dần), đuôi thuôn nhọn: không có đầu cụt vuông
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.5f, 0.55f), new GradientAlphaKey(0f, 1f) });
        trails.colorOverTrail = g;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g2 = new Gradient();
        g2.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g2;
        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.None;
        pr.trailMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    /// <summary>Đốm sáng nhỏ từ ngoài bị hút xoắn vào tâm.</summary>
    private static ParticleSystem MakeSparkSystem(string name, Transform parent, Material mat)
    {
        ParticleSystem ps = BaseSystem(name, parent, 2.1f, 0.15f, 1.0f, 0.05f);
        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        Gradient cg = new Gradient();
        cg.SetKeys(new[] { new GradientColorKey(new Color(2.4f, 1.5f, 0.35f), 0f), new GradientColorKey(new Color(1.6f, 2.2f, 2.8f), 0.5f), new GradientColorKey(new Color(0.5f, 1.8f, 2.6f), 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(cg) { mode = ParticleSystemGradientMode.RandomColor };
        Orbit(ps, 4.5f, -1.7f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    // ================= Scene =================

    [MenuItem("Phàm Kiếm Lục/Cổng không gian/Dựng scene TestPhaKhong")]
    public static void BuildScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[Cổng] Chưa có prefab, hãy chạy 'Dựng prefab CongKhongGian' trước."); return; }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) AssetDatabase.CopyAsset(SourceScene, ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Bỏ phần chơi: nhân vật điều khiển, camera Cinemachine, UI, kẻ địch, hình nộp, bầy kiếm, NavMesh
        foreach (string n in new[] { "NhanVat", "CameraTarget", "CM_SauVaiPhai", "BayKiem", "HinhNop_Nhom", "UI_TamNgam", "HUD", "NavMesh_SanTap", "KeDich", "KeDich_PhimThu", "EventSystem" })
        {
            GameObject g = GameObject.Find(n);
            if (g != null) Object.DestroyImmediate(g);
        }
        foreach (GameObject g in scene.GetRootGameObjects()) { if (g.name.StartsWith("Test_") || g.name.StartsWith("CongKhongGian") || g.name == "NhanVat_Dung") Object.DestroyImmediate(g); }

        Camera cam = Camera.main;
        if (cam != null)
        {
            var brain = cam.GetComponent("CinemachineBrain");
            if (brain != null) Object.DestroyImmediate(brain);
            cam.transform.position = new Vector3(0f, 1.9f, -5.5f);
            cam.transform.LookAt(new Vector3(0f, 1.8f, 0f));
        }

        GameObject a = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        a.name = "CongKhongGian_A";
        a.transform.position = new Vector3(0f, 1.8f, 0f);
        GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        b.name = "CongKhongGian_B";
        b.transform.position = new Vector3(-6.5f, 1.8f, 4f);
        b.transform.rotation = Quaternion.Euler(0f, 40f, 0f);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/NhanVat_Model.prefab");
        if (model != null)
        {
            GameObject nv = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            nv.name = "NhanVat_Dung";
            nv.transform.position = new Vector3(3.2f, 0.1f, 0f);
            nv.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Cổng] Đã dựng scene " + ScenePath + ". Chọn CongKhongGian_A, kéo 'doMo' hoặc bấm Play rồi chuột phải vào script → Mở cổng / Đóng cổng.");
    }
}
#endif
