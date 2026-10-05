#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

/// <summary>
/// Dựng toàn bộ sấm sét cho Phong Lôi Sí (xem WingLightning):
/// - "Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Dựng bản thử trong TestVFX": đặt một cánh (instance của prefab) vào scene TestVFX và dựng sét lên đó để xem trước.
/// - "Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Gắn vào prefab PhongLoiSi": dựng thẳng vào Assets/_Game/Prefabs/PhongLoiSi.prefab.
/// Mỗi lần dựng sẽ xóa bản sấm sét cũ (đối tượng "HieuUngSet") rồi dựng lại. Không đụng tới file .vfx hay mô hình.
/// 8 điểm sét mỗi cánh là Transform rỗng "Set_Diem_*" gắn làm con của các xương cánh (Canh_T1..3 / Canh_P1..3), rải từ gốc tới đầu cánh theo đường xương.
/// </summary>
public static class WingLightningBuilder
{
    private const string PrefabPath = "Assets/_Game/Prefabs/PhongLoiSi.prefab";
    private const string ScenePath = "Assets/_Game/Scenes/TestVFX.unity";
    private const string BenchName = "Test_PhongLoiSi_Set";
    private const string FolderName = "HieuUngSet";
    private const string MatSecondary = "Assets/_Game/Materials/VFX_SetPhu.mat";
    private const string MatStreak = "Assets/_Game/Materials/VFX_VetCanh.mat";
    private const string MatBase = "Assets/_Game/Materials/VFX_VetKiem.mat";
    private const string TexLighting = "Assets/_Game/VFX/Textures/TX_Lighting.tga";

    [MenuItem("Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Dựng bản thử trong TestVFX")]
    public static void BuildBench()
    {
        Scene();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject bench = GameObject.Find(BenchName);
        if (bench != null) Object.DestroyImmediate(bench);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bench = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        bench.name = BenchName;
        bench.transform.position = new Vector3(0f, 1.3f, 0f);
        bench.transform.rotation = Quaternion.identity;
        if (bench.transform.Find(FolderName) == null) Build(bench);   // prefab đã có sấm sét thì dùng luôn, chưa có thì dựng thử lên bản này
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Sấm sét] Đã dựng bản thử trong TestVFX (đối tượng " + BenchName + "). Bấm Play, chỉnh 'cuongDoSet' trong WingLightning (HieuUngSet).");
    }

    [MenuItem("Phàm Kiếm Lục/Sấm sét Phong Lôi Sí/Gắn vào prefab PhongLoiSi")]
    public static void BuildPrefab()
    {
        Scene();
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

    private static void Scene() { BuildMaterials(); }

    // ================= Vật liệu =================

    private static void BuildMaterials()
    {
        Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(MatBase);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexLighting);
        if (baseMat == null || tex == null) { Debug.LogError("[Sấm sét] Thiếu " + MatBase + " hoặc " + TexLighting); return; }
        if (AssetDatabase.LoadAssetAtPath<Material>(MatSecondary) == null)
        {
            Material m = new Material(baseMat) { name = "VFX_SetPhu" };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1.3f, 1.1f, 2.2f, 1f));
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            AssetDatabase.CreateAsset(m, MatSecondary);
        }
        if (AssetDatabase.LoadAssetAtPath<Material>(MatStreak) == null)
        {
            Material m = new Material(baseMat) { name = "VFX_VetCanh" };
            m.SetColor("_BaseColor", new Color(0.8f, 1.2f, 2.4f, 1f));
            m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            AssetDatabase.CreateAsset(m, MatStreak);
        }
        AssetDatabase.SaveAssets();
    }

    // ================= Dựng =================

    public static void Build(GameObject root)
    {
        Transform old = root.transform.Find(FolderName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        // Xóa các điểm sét cũ trong xương
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name.StartsWith("Set_Diem_")) { /* sẽ xóa sau */ }
        List<GameObject> stale = new List<GameObject>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Set_Diem_")) stale.Add(t.gameObject);
        foreach (GameObject g in stale) if (g != null) Object.DestroyImmediate(g);

        Transform[][] bones = { FindBones(root.transform, "Canh_T"), FindBones(root.transform, "Canh_P") };   // [0] cánh trái (-X), [1] cánh phải (+X)
        if (bones[0][0] == null || bones[1][0] == null) { Debug.LogError("[Sấm sét] Không thấy xương cánh Canh_T1..3 / Canh_P1..3."); return; }

        GameObject folder = new GameObject(FolderName);
        folder.transform.SetParent(root.transform, false);

        Transform[][] points = new Transform[2][];
        Vector3[] wingCenter = new Vector3[2];
        for (int s = 0; s < 2; s++)
        {
            points[s] = MakePoints(bones[s], s == 0 ? "T" : "P");
            wingCenter[s] = (points[s][0].position + points[s][7].position) * 0.5f;
        }
        // Cánh bên nào nằm ở -X thì là cánh trái
        if (wingCenter[0].x > wingCenter[1].x) Debug.LogWarning("[Sấm sét] Canh_T đang nằm ở +X: kiểm tra lại quy ước trái/phải.");

        VisualEffectAsset coreAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/_Game/VFX/SetCanh.vfx");
        VisualEffectAsset haloAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/_Game/VFX/SetCanhQuang.vfx");
        VisualEffectAsset sparkAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/_Game/VFX/TiaLuaCanh.vfx");
        VisualEffectAsset dustAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/_Game/VFX/BuiDapDat.vfx");
        Material secMat = AssetDatabase.LoadAssetAtPath<Material>(MatSecondary);
        Material streakMat = AssetDatabase.LoadAssetAtPath<Material>(MatStreak);

        VisualEffect[] core = new VisualEffect[2], halo = new VisualEffect[2], sparks = new VisualEffect[2];
        ParticleSystem[] secondary = new ParticleSystem[2];
        TrailRenderer[] streaks = new TrailRenderer[2];
        SkinnedMeshRenderer[] feathers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int s = 0; s < 2; s++)
        {
            string side = s == 0 ? "Trai" : "Phai";
            // Sét chính: lõi + quầng đặt cố định giữa cánh (giới hạn vẽ 8m quanh nó), vị trí sét lấy từ 8 điểm gắn xương
            GameObject set = new GameObject("Set_" + side);
            set.transform.SetParent(folder.transform, false);
            set.transform.position = wingCenter[s];
            core[s] = MakeVfx("Loi", set.transform, coreAsset);
            halo[s] = MakeVfx("Quang", set.transform, haloAsset);

            // Tia lửa ở đầu cánh
            sparks[s] = MakeVfx("TiaLua_" + side, points[s][7], sparkAsset);

            // Sét phụ trên bề mặt lông cánh: chọn lưới lông nằm cùng phía
            SkinnedMeshRenderer smr = PickFeather(feathers, root.transform, wingCenter[s].x);
            GameObject sec = new GameObject("SetPhu_" + side);
            sec.transform.SetParent(folder.transform, false);
            secondary[s] = MakeSecondary(sec, smr, secMat);

            // Vệt sáng khi tăng tốc: gắn đầu cánh
            GameObject tr = new GameObject("VetSang_" + side);
            tr.transform.SetParent(points[s][7], false);
            streaks[s] = MakeTrail(tr, streakMat);
        }

        // Đèn giữa lưng: ở gốc của prefab cánh (tâm hai gốc cánh), hơi nâng lên
        GameObject lightGo = new GameObject("Den_Lung");
        lightGo.transform.SetParent(folder.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.72f, 0.55f, 1f, 1f);
        light.range = 4f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        UniversalAdditionalLightData ld = lightGo.AddComponent<UniversalAdditionalLightData>();
        ld.renderingLayers = uint.MaxValue;    // sáng cả nhân vật (lớp ánh sáng riêng) lẫn mặt đất

        // Bụi đáp đất
        VisualEffect dust = MakeVfx("BuiDapDat", folder.transform, dustAsset);

        WingLightning wl = folder.AddComponent<WingLightning>();
        SerializedObject so = new SerializedObject(wl);
        SetArray(so.FindProperty("pointsLeft"), points[0]);
        SetArray(so.FindProperty("pointsRight"), points[1]);
        SetArray(so.FindProperty("coreVfx"), core);
        SetArray(so.FindProperty("haloVfx"), halo);
        SetArray(so.FindProperty("secondary"), secondary);
        SetArray(so.FindProperty("sparksVfx"), sparks);
        SetArray(so.FindProperty("streaks"), streaks);
        so.FindProperty("backLight").objectReferenceValue = light;
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

    private static SkinnedMeshRenderer PickFeather(SkinnedMeshRenderer[] all, Transform root, float sideX)
    {
        SkinnedMeshRenderer best = null;
        float bestDist = float.MaxValue;
        foreach (SkinnedMeshRenderer r in all)
        {
            if (!r.name.StartsWith("Long_")) continue;
            float cx = root.InverseTransformPoint(r.bounds.center).x;
            float d = Mathf.Abs(cx - sideX);
            if (d < bestDist) { bestDist = d; best = r; }
        }
        return best;
    }

    private static ParticleSystem MakeSecondary(GameObject go, SkinnedMeshRenderer smr, Material mat)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        shape.skinnedMeshRenderer = smr;
        shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
        shape.normalOffset = 0.02f;
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 4;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
        sheet.cycleCount = 1;
        var colorLife = ps.colorOverLifetime;
        colorLife.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colorLife.color = g;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    private static TrailRenderer MakeTrail(GameObject go, Material mat)
    {
        TrailRenderer t = go.AddComponent<TrailRenderer>();
        t.time = 0.16f;
        t.minVertexDistance = 0.05f;
        t.widthCurve = new AnimationCurve(new Keyframe(0f, 0.025f), new Keyframe(1f, 0f));
        t.alignment = LineAlignment.View;
        t.sharedMaterial = mat;
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.receiveShadows = false;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.9f, 1f, 1f), 0f), new GradientColorKey(new Color(0.55f, 0.75f, 1f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        t.colorGradient = g;
        t.emitting = false;
        return t;
    }
}
#endif
