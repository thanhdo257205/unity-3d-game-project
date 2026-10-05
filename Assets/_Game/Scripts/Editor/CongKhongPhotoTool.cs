#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ kiểm thử (chỉ Editor) cho cổng không gian: trong scene TestPhaKhong đang Play (Unity cần có tiêu điểm), chụp các ảnh 1920x1080 bằng camera chính
/// ở nhiều độ mở / góc nhìn (Assets/Screenshots/{tiền tố}_*.png) và đo thời gian khung hình khi hai cổng cùng mở so với không có cổng.
/// Gọi CongKhongPhotoTool.Run("tiền tố") hoặc CongKhongPhotoTool.Measure().
/// </summary>
public static class CongKhongPhotoTool
{
    private class Shot { public string name; public Vector3 cam; public Vector3 look; public float doMo; public float wait = 1.4f; }

    private static List<Shot> shots;
    private static int index;
    private static float stepStart;
    private static string prefix;
    private static CongKhongGian a, b;
    private static Camera cam;
    private static bool shotDone;

    private static void Find()
    {
        a = GameObject.Find("CongKhongGian_A").GetComponent<CongKhongGian>();
        b = GameObject.Find("CongKhongGian_B").GetComponent<CongKhongGian>();
        cam = Camera.main;
    }

    public static void Run(string tag)
    {
        if (!Application.isPlaying) { Debug.LogError("[Cổng] Cần đang Play."); return; }
        prefix = tag;
        Find();
        Vector3 c = new Vector3(0f, 1.8f, 0f);
        shots = new List<Shot>
        {
            new Shot { name = "xa_01", cam = new Vector3(0f, 1.9f, -4.8f), look = c, doMo = 0.1f },
            new Shot { name = "xa_05", cam = new Vector3(0f, 1.9f, -4.8f), look = c, doMo = 0.5f },
            new Shot { name = "xa_10", cam = new Vector3(0f, 1.9f, -4.8f), look = c, doMo = 1f },
            new Shot { name = "can_thang", cam = new Vector3(0f, 1.85f, -3.4f), look = c, doMo = 1f },
            new Shot { name = "can_cheo45", cam = c + new Vector3(Mathf.Sin(45f * Mathf.Deg2Rad), 0.05f, -Mathf.Cos(45f * Mathf.Deg2Rad)) * 3.6f, look = c, doMo = 1f },
            new Shot { name = "can_matsau", cam = new Vector3(0.3f, 1.85f, 3.4f), look = c, doMo = 1f },
            new Shot { name = "can_nendat", cam = new Vector3(0f, 4.2f, -3.6f), look = new Vector3(0f, 1.2f, 0f), doMo = 1f },
            new Shot { name = "nhanvat", cam = new Vector3(1.5f, 2.0f, -6.8f), look = new Vector3(1.5f, 1.55f, 0f), doMo = 1f },
        };
        index = -1; stepStart = 0f;
        EditorApplication.update += Tick;
        Debug.Log("[Cổng] Bắt đầu chụp.");
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        if (index < 0 || Time.realtimeSinceStartup - stepStart >= shots[index].wait)
        {
            if (index >= 0) Capture(shots[index].name);
            index++;
            if (index >= shots.Count)
            {
                EditorApplication.update -= Tick;
                a.DoMo = 1f;
                Debug.Log("[Cổng] Chụp xong.");
                return;
            }
            stepStart = Time.realtimeSinceStartup;
            Shot s = shots[index];
            a.DoMo = s.doMo;
            cam.transform.position = s.cam;
            cam.transform.LookAt(s.look);
        }
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void Capture(string name)
    {
        const int W = 1920, H = 1080;
        RenderTexture rt = new RenderTexture(W, H, 24);
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        t.Apply();
        RenderTexture.active = null;
        cam.targetTexture = prev;
        File.WriteAllBytes("Assets/Screenshots/" + prefix + "_" + name + ".png", t.EncodeToPNG());
        Object.DestroyImmediate(t);
        rt.Release(); Object.DestroyImmediate(rt);
        Debug.Log("[Cổng] " + name + " doMo=" + a.DoMo.ToString("F2"));
    }

    // ----- Đo thời gian khung hình -----

    private static double sum; private static int n; private static float maxDt;
    private static int phase; private static float phaseStart;
    private static readonly double[] avg = new double[3];

    public static void Measure()
    {
        if (!Application.isPlaying) { Debug.LogError("[Cổng] Cần đang Play."); return; }
        Find();
        cam.transform.position = new Vector3(-3f, 2.0f, -6.5f);
        cam.transform.LookAt(new Vector3(-3.2f, 1.8f, 2f));
        a.DoMo = 0f; b.DoMo = 0f;
        phase = 0; phaseStart = Time.realtimeSinceStartup; sum = 0; n = 0; maxDt = 0;
        EditorApplication.update += MeasureTick;
    }

    private static void MeasureTick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= MeasureTick; return; }
        float t = Time.realtimeSinceStartup - phaseStart;
        if (t > 1.5f) { float dt = Time.unscaledDeltaTime; sum += dt; n++; if (dt > maxDt) maxDt = dt; }
        if (t > 5.5f)
        {
            avg[phase] = sum / Mathf.Max(1, n);
            Debug.Log("[Cổng] đo pha " + phase + (phase == 1 ? " (hai cổng mở)" : " (không cổng)") + ": " + (avg[phase] * 1000).ToString("F2") + " ms, " + n + " khung, max " + (maxDt * 1000).ToString("F1"));
            phase++;
            sum = 0; n = 0; maxDt = 0; phaseStart = Time.realtimeSinceStartup;
            if (phase == 1) { a.DoMo = 1f; b.DoMo = 1f; }
            if (phase == 2) { a.DoMo = 0f; b.DoMo = 0f; }
            if (phase >= 3) { EditorApplication.update -= MeasureTick; Debug.Log("[Cổng] Đo xong."); return; }
        }
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
#endif
