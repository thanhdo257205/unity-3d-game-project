#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ kiểm thử (chỉ Editor): chụp ảnh dáng bay Phong Lôi Sí bằng camera chơi thật trong chế độ Play, và đo khoảng hở giữa lông cánh với đầu/thân.
/// Gọi <c>FlightPhotoTool.Run("tiền tố")</c> khi đang Play (cần Unity có tiêu điểm để game chạy khung hình): tự mở cánh, cất cánh rồi lần lượt
/// lơ lửng, bay thường, tăng tốc (kèm ảnh nhìn ngang bằng camera tạm cùng ống kính với camera chơi), rẽ trái. Mỗi ảnh render 3840x2160 rồi cắt quanh
/// nhân vật (kể cả cánh) thành 1920x1080 sao cho nhân vật chiếm khoảng 40-60% chiều cao khung. Ảnh lưu ở Assets/Screenshots/, số đo in vào Console
/// (dòng "[Ảnh bay]": độ lệch đối xứng cánh, khoảng hở nhỏ nhất của mọi đỉnh lông/khung cánh tới đầu và thân trong suốt bước đó, âm = xuyên).
/// </summary>
public static class FlightPhotoTool
{
    private class Step
    {
        public string name;
        public float duration;
        public bool sample;
        public Action begin;
        public Action end;
    }

    private static List<Step> steps;
    private static int index;
    private static float stepStart;
    private static string prefix;
    private static PhongLoiSi pl;
    private static PlayerController pc;
    private static Animator anim;
    private static float minHead, minTorso;
    private static int torsoHits, headHits;

    private const float HeadRadius = 0.12f;
    private const float TorsoRadius = 0.2f;

    public static void Run(string tag)
    {
        if (!Application.isPlaying) { Debug.LogError("[Ảnh bay] Cần đang Play."); return; }
        prefix = tag;
        Init();
        Hotbar hb = pc.GetComponent<Hotbar>();
        if (!pl.IsOpen) { hb.Select(3); hb.TryUseSelected(); }

        steps = new List<Step>();
        steps.Add(new Step { name = "mo", duration = 1.2f });
        steps.Add(new Step { name = "len", duration = 1.6f, begin = () => pl.TestSetInput(Vector2.zero, 1f, false) });
        steps.Add(new Step { name = "lolung", duration = 2.2f, sample = true, begin = () => { pl.TestSetInput(Vector2.zero, 0f, false); ResetSample(); }, end = () => Shoot("lolung", false) });
        steps.Add(new Step { name = "thuong", duration = 2.6f, sample = true, begin = () => { pl.TestSetInput(new Vector2(0f, 1f), 0f, false); ResetSample(); }, end = () => Shoot("thuong", false) });
        steps.Add(new Step { name = "tangtoc", duration = 2.8f, sample = true, begin = () => { pl.TestSetInput(new Vector2(0f, 1f), 0f, true); ResetSample(); }, end = () => { Shoot("tangtoc", false); Shoot("tangtoc_ngang", true); } });
        steps.Add(new Step { name = "re", duration = 2.6f, begin = () => pl.TestSetInput(new Vector2(-1f, 1f), 0f, true), end = () => Shoot("retrai", false) });
        steps.Add(new Step { name = "xong", duration = 0.1f, begin = () => pl.TestClearInput() });
        index = -1;
        stepStart = 0f;
        EditorApplication.update += Tick;
        Debug.Log("[Ảnh bay] Bắt đầu.");
    }

    private static void Init()
    {
        pl = UnityEngine.Object.FindAnyObjectByType<PhongLoiSi>();
        pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        anim = pc.GetComponentInChildren<Animator>();
        PlayerStats stats = pc.GetComponent<PlayerStats>();
        stats.GrantInvulnerability(1e6f);
        stats.RestoreMana(1000f);
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        if (index < 0 || Time.realtimeSinceStartup - stepStart >= steps[index].duration)
        {
            if (index >= 0 && steps[index].end != null)
            {
                if (steps[index].sample) Debug.Log("[Ảnh bay] " + steps[index].name + " khoảng hở nhỏ nhất: đầu=" + minHead.ToString("F2") + "m (xuyên " + headHits + " đỉnh), thân=" + minTorso.ToString("F2") + "m (xuyên " + torsoHits + " đỉnh)");
                steps[index].end();
            }
            index++;
            if (index >= steps.Count) { EditorApplication.update -= Tick; Debug.Log("[Ảnh bay] Xong."); return; }
            stepStart = Time.realtimeSinceStartup;
            if (steps[index].begin != null) steps[index].begin();
        }
        if (steps[index].sample && Time.realtimeSinceStartup - stepStart > 0.8f) Sample();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    // ----- Đo khoảng hở lông cánh - đầu/thân -----

    private static void ResetSample() { minHead = 99f; minTorso = 99f; torsoHits = 0; headHits = 0; }

    private static void Sample()
    {
        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        Transform neck = anim.GetBoneTransform(HumanBodyBones.Neck);
        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        Vector3 a = hips.position, b = neck.position;
        Vector3 headCenter = head.position + (head.position - neck.position).normalized * 0.08f;
        Vector3 ab = b - a;
        float abLen2 = ab.sqrMagnitude;
        foreach (SkinnedMeshRenderer smr in pl.Visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!smr.enabled) continue;
            Mesh m = new Mesh();
            smr.BakeMesh(m, true);
            Vector3[] vs = m.vertices;
            Transform t = smr.transform;
            for (int i = 0; i < vs.Length; i++)
            {
                Vector3 w = t.position + t.rotation * vs[i];
                float dh = Vector3.Distance(w, headCenter) - HeadRadius;
                float k = Mathf.Clamp01(Vector3.Dot(w - a, ab) / abLen2);
                float dt = Vector3.Distance(w, a + ab * k) - TorsoRadius;
                if (dh < minHead) minHead = dh;
                if (dt < minTorso) minTorso = dt;
                if (dh < 0f) headHits++;
                if (dt < 0f) torsoHits++;
            }
            UnityEngine.Object.DestroyImmediate(m);
        }
    }

    // ----- Chụp ảnh, tự cắt quanh nhân vật -----

    private static void Shoot(string name, bool side)
    {
        Camera cam = Camera.main;
        GameObject tmp = null;
        if (side)
        {
            tmp = new GameObject("TmpCam");
            cam = tmp.AddComponent<Camera>();
            cam.fieldOfView = Camera.main.fieldOfView;
            Vector3 c = pc.transform.position + Vector3.up * 1.2f;
            cam.transform.position = c + pc.transform.right * 7f;
            cam.transform.LookAt(c);
        }
        const int W = 3840, H = 2160;
        RenderTexture rt = new RenderTexture(W, H, 24);
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();

        // Khung bao nhân vật + cánh trên màn hình
        Vector2 mn = new Vector2(1e9f, 1e9f), mx = new Vector2(-1e9f, -1e9f);
        foreach (Renderer r in pc.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) continue;
            Bounds bd = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bd.center + Vector3.Scale(bd.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) continue;
                mn = Vector2.Min(mn, sp); mx = Vector2.Max(mx, sp);
            }
        }
        Vector2 center = (mn + mx) * 0.5f;
        float bw = mx.x - mn.x, bh = mx.y - mn.y;
        float cw = Mathf.Max(bw * 1.12f, bh * 1.12f * 16f / 9f);
        cw = Mathf.Min(cw, W);
        float ch = cw * 9f / 16f;
        int cwI = Mathf.RoundToInt(cw), chI = Mathf.RoundToInt(ch);
        int x0 = Mathf.Clamp(Mathf.RoundToInt(center.x - cw * 0.5f), 0, W - cwI);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(center.y - ch * 0.5f), 0, H - chI);

        RenderTexture.active = rt;
        Texture2D full = new Texture2D(W, H, TextureFormat.RGB24, false);
        full.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        full.Apply();
        RenderTexture.active = null;
        cam.targetTexture = prev;

        // Cắt rồi thu về 1920x1080
        Texture2D crop = new Texture2D(cwI, chI, TextureFormat.RGB24, false);
        crop.SetPixels(full.GetPixels(x0, y0, cwI, chI));
        crop.Apply();
        RenderTexture small = new RenderTexture(1920, 1080, 0);
        Graphics.Blit(crop, small);
        RenderTexture.active = small;
        Texture2D outTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        outTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        outTex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Assets/Screenshots/" + prefix + "_" + name + ".png", outTex.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(full);
        UnityEngine.Object.DestroyImmediate(crop);
        UnityEngine.Object.DestroyImmediate(outTex);
        small.Release(); UnityEngine.Object.DestroyImmediate(small);
        rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        if (tmp != null) UnityEngine.Object.DestroyImmediate(tmp);
        Debug.Log("[Ảnh bay] " + name + " v=" + pl.FlightVelocity.magnitude.ToString("F1") + " nhanVatCao=" + (bh / ch * 100f).ToString("F0") + "%khung " + Describe());
    }

    /// <summary>Số đo: độ lệch trục phải của cánh so với nhân vật, độ nghiêng thân và cánh, vai/hông lệch, hai chân cách nhau.</summary>
    private static string Describe()
    {
        Transform tr = pl.Visual.transform;
        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        Vector3 up = (head.position - hips.position).normalized;
        float tilt = Vector3.Angle(Vector3.up, up);
        Vector3 shoulders = anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position - anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
        Vector3 hipLine = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
        Transform lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot), rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        float wingBackDeg = Vector3.SignedAngle(Vector3.up, tr.up, pc.transform.right);
        return "vaiLech=" + Vector3.Angle(shoulders, tr.right).ToString("F0") + " hongLech=" + Vector3.Angle(hipLine, tr.right).ToString("F0")
            + " chanCachNhau=" + Vector3.Distance(lf.position, rf.position).ToString("F2") + " thanNghieng=" + tilt.ToString("F0")
            + " canhNgaSau=" + (-wingBackDeg).ToString("F0") + " canhRight-lech=" + Vector3.Angle(pc.transform.right, tr.right).ToString("F0");
    }
}
#endif
