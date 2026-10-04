#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ kiểm thử (chỉ Editor): chụp ảnh dáng bay Phong Lôi Sí bằng camera chơi thật trong chế độ Play.
/// Gọi <c>FlightPhotoTool.Run("tiền tố")</c> khi đang Play (cần Unity có tiêu điểm để game chạy khung hình): tự mở cánh, cất cánh rồi lần lượt
/// chụp lơ lửng, bay thường, tăng tốc (kèm ảnh nhìn ngang), rẽ trái; ghi số đo đối xứng cánh vào Console. Ảnh lưu ở Assets/Screenshots/.
/// </summary>
public static class FlightPhotoTool
{
    private class Step
    {
        public string name;
        public float duration;
        public Action begin;
        public Action end;
    }

    private static List<Step> steps;
    private static int index;
    private static float stepStart;
    private static string prefix;
    private static PhongLoiSi pl;
    private static PlayerController pc;
    private static Vector3 startPos;

    public static void Run(string tag)
    {
        if (!Application.isPlaying) { Debug.LogError("[Ảnh bay] Cần đang Play."); return; }
        prefix = tag;
        pl = UnityEngine.Object.FindAnyObjectByType<PhongLoiSi>();
        pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        PlayerStats stats = pc.GetComponent<PlayerStats>();
        startPos = pc.transform.position;
        stats.GrantInvulnerability(1e6f);
        stats.RestoreMana(1000f);
        Hotbar hb = pc.GetComponent<Hotbar>();
        hb.Select(3);
        hb.TryUseSelected();

        steps = new List<Step>();
        steps.Add(new Step { name = "mo", duration = 1.2f });
        steps.Add(new Step { name = "len", duration = 1.6f, begin = () => pl.TestSetInput(Vector2.zero, 1f, false) });
        steps.Add(new Step { name = "lolung", duration = 2.0f, begin = () => pl.TestSetInput(Vector2.zero, 0f, false), end = () => Shoot("lolung", true) });
        steps.Add(new Step { name = "thuong", duration = 2.4f, begin = () => pl.TestSetInput(new Vector2(0f, 1f), 0f, false), end = () => Shoot("thuong", true) });
        steps.Add(new Step { name = "tangtoc", duration = 2.6f, begin = () => pl.TestSetInput(new Vector2(0f, 1f), 0f, true), end = () => { Shoot("tangtoc", true); Shoot("tangtoc_ngang", false); } });
        steps.Add(new Step { name = "re", duration = 2.6f, begin = () => pl.TestSetInput(new Vector2(-1f, 1f), 0f, true), end = () => Shoot("retrai", true) });
        steps.Add(new Step { name = "xong", duration = 0.1f, begin = () => { pl.TestClearInput(); Report(); } });
        index = -1;
        stepStart = 0f;
        EditorApplication.update += Tick;
        Debug.Log("[Ảnh bay] Bắt đầu.");
    }

    /// <summary>Chụp nhiều góc (sau-chếch-trên, bên, sau) khi bay thường hoặc tăng tốc, để so sánh các thông số dáng. Cần Play; cánh đã mở và đang bay hoặc tự mở/cất cánh.</summary>
    public static void Angles(string tag, int mode)
    {
        if (!Application.isPlaying) return;
        prefix = tag;
        pl = UnityEngine.Object.FindAnyObjectByType<PhongLoiSi>();
        pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        PlayerStats stats = pc.GetComponent<PlayerStats>();
        stats.GrantInvulnerability(1e6f);
        stats.RestoreMana(1000f);
        steps = new List<Step>();
        if (!pl.IsFlying)
        {
            Hotbar hb = pc.GetComponent<Hotbar>();
            if (!pl.IsOpen) { hb.Select(3); hb.TryUseSelected(); }
            steps.Add(new Step { name = "mo", duration = 1.2f });
            steps.Add(new Step { name = "len", duration = 1.6f, begin = () => pl.TestSetInput(Vector2.zero, 1f, false) });
        }
        steps.Add(new Step { name = "bay", duration = 2.6f, begin = () => pl.TestSetInput(mode == 0 ? Vector2.zero : new Vector2(0f, 1f), 0f, mode == 2),
            end = () => { Shoot3("sauTren", new Vector3(2.2f, 2.6f, -4.5f)); Shoot3("ben", new Vector3(6f, 0.3f, 0f)); Shoot3("truocTren", new Vector3(-2.5f, 2.2f, 4.5f)); Shoot3("sau", new Vector3(0f, 0.4f, -5.5f)); } });
        steps.Add(new Step { name = "xong", duration = 0.1f, begin = () => { pl.TestClearInput(); } });
        index = -1;
        stepStart = 0f;
        EditorApplication.update += Tick;
    }

    private static void Shoot3(string name, Vector3 localOffset)
    {
        GameObject tmp = new GameObject("TmpCam");
        Camera cam = tmp.AddComponent<Camera>();
        cam.fieldOfView = 35f;
        Vector3 c = pc.transform.position + Vector3.up * 1.2f;
        cam.transform.position = c + pc.transform.rotation * localOffset;
        cam.transform.LookAt(c);
        RenderTexture rt = new RenderTexture(2560, 1440, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(2560, 1440, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 2560, 1440), 0, 0);
        tex.Apply();
        File.WriteAllBytes("Assets/Screenshots/" + prefix + "_" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tmp);
        Debug.Log("[Ảnh bay] " + name + " v=" + pl.FlightVelocity.magnitude.ToString("F1") + " " + Describe());
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        if (index < 0 || Time.realtimeSinceStartup - stepStart >= steps[index].duration)
        {
            if (index >= 0 && steps[index].end != null) steps[index].end();
            index++;
            if (index >= steps.Count) { EditorApplication.update -= Tick; Debug.Log("[Ảnh bay] Xong."); return; }
            stepStart = Time.realtimeSinceStartup;
            if (steps[index].begin != null) steps[index].begin();
        }
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void Shoot(string name, bool gameCam)
    {
        Camera cam = Camera.main;
        GameObject tmp = null;
        if (!gameCam)
        {
            tmp = new GameObject("TmpCam");
            cam = tmp.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            Vector3 c = pc.transform.position + Vector3.up * 1.2f;
            cam.transform.position = c + pc.transform.right * 6f;
            cam.transform.LookAt(c);
        }
        RenderTexture rt = new RenderTexture(2560, 1440, 24);
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(2560, 1440, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 2560, 1440), 0, 0);
        tex.Apply();
        File.WriteAllBytes("Assets/Screenshots/" + prefix + "_" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = prev;
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        if (tmp != null) UnityEngine.Object.DestroyImmediate(tmp);
        Debug.Log("[Ảnh bay] " + name + " v=" + pl.FlightVelocity.magnitude.ToString("F1") + " " + Describe());
    }

    /// <summary>Số đo: góc lệch của trục nối hai gốc cánh so với trục phải nhân vật, độ nghiêng thân và độ cao hai đầu cánh.</summary>
    private static string Describe()
    {
        WingVisual v = pl.Visual;
        Transform tr = v.transform;
        Transform model = tr.GetComponentInParent<PlayerController>().GetComponentInChildren<Animator>().transform;
        Animator anim = model.GetComponent<Animator>();
        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        Vector3 up = (head.position - hips.position).normalized;
        float tilt = Vector3.Angle(Vector3.up, up);
        float wingTilt = Vector3.Angle(Vector3.up, tr.up);
        float wingRoll = Vector3.SignedAngle(pc.transform.right, Vector3.ProjectOnPlane(tr.right, pc.transform.forward), pc.transform.forward);
        Vector3 shoulders = anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position - anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
        Vector3 hipLine = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
        Transform lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot), rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        Vector3 legDir = (lf.position - anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position).normalized;
        float legAngle = Vector3.Angle(legDir, -up);
        Vector3 axisFwd = Vector3.Cross(tr.right, tr.up);
        string sign = " vaiRoll=" + Vector3.SignedAngle(tr.right, Vector3.ProjectOnPlane(shoulders, tr.forward), tr.forward).ToString("F0")
            + " vaiYaw=" + Vector3.SignedAngle(tr.right, Vector3.ProjectOnPlane(shoulders, tr.up), tr.up).ToString("F0")
            + " hongRoll=" + Vector3.SignedAngle(tr.right, Vector3.ProjectOnPlane(hipLine, tr.forward), tr.forward).ToString("F0")
            + " hongYaw=" + Vector3.SignedAngle(tr.right, Vector3.ProjectOnPlane(hipLine, tr.up), tr.up).ToString("F0");
        return sign + " vaiLech=" + Vector3.Angle(shoulders, tr.right).ToString("F0") + " hongLech=" + Vector3.Angle(hipLine, tr.right).ToString("F0")
            + " chanLechTruc=" + legAngle.ToString("F0") + " chanCachNhau=" + Vector3.Distance(lf.position, rf.position).ToString("F2")
            + " thanNghieng=" + tilt.ToString("F0") + " canhUp=" + wingTilt.ToString("F0") + " canhRight-lech=" + Vector3.Angle(pc.transform.right, tr.right).ToString("F0") + " roll=" + wingRoll.ToString("F0");
    }

    private static void Report()
    {
        Debug.Log("[Ảnh bay] " + Describe());
    }
}
#endif
