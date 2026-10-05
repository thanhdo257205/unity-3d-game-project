#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Kiểm thử Phá Không (chỉ Editor), chạy trong scene SanTap đang Play (Unity cần có tiêu điểm). Kết quả in vào Console, dòng bắt đầu bằng "[PK]".
/// - PhaKhongTestTool.RunTests(): dịch chuyển trên đất, khi bay, hướng vào tường, hướng xuống đất, thiếu pháp lực, đang hồi chiêu, sóng xung kích (sát thương + choáng).
/// - PhaKhongTestTool.RunPhotos(tag): ảnh ngắm + chuỗi 5 ảnh ở các mốc 0.00 / 0.25 / 0.40 / 0.70 / 0.90 giây bằng camera chơi thật (làm chậm thời gian để bắt đúng mốc).
/// </summary>
public static class PhaKhongTestTool
{
    private class Wait { public float until; public Wait(float s) { until = Time.realtimeSinceStartup + s; } }

    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static object current;
    private static PlayerStats stats;
    private static PhaKhong pk;
    private static PhongLoiSi pl;
    private static PlayerController pc;
    private static Hotbar hb;
    private static string prefix;

    /// <summary>Tốc độ thời gian khi chụp chuỗi ảnh (1 = thật; nhỏ hơn để bắt đúng mốc).</summary>
    public static float SlowMo = 1f;

    /// <summary>Chỉ chạy ca sóng xung kích (T7) khi gọi RunTests.</summary>
    public static bool OnlyShock;

    private static void Init()
    {
        stats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        pk = UnityEngine.Object.FindAnyObjectByType<PhaKhong>();
        pl = UnityEngine.Object.FindAnyObjectByType<PhongLoiSi>();
        pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        hb = UnityEngine.Object.FindAnyObjectByType<Hotbar>();
    }

    private static void Start(IEnumerator s)
    {
        stack.Clear();
        stack.Push(s);
        current = null;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        if (current is Wait w && Time.realtimeSinceStartup < w.until) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        current = null;
        // Chạy tiếp coroutine trên cùng ngăn xếp; yield return IEnumerator = chạy coroutine con rồi quay lại
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            object c = top.Current;
            if (c is IEnumerator child) { stack.Push(child); continue; }
            current = c;
            break;
        }
        if (stack.Count == 0) { EditorApplication.update -= Tick; Time.timeScale = 1f; Debug.Log("[PK] Xong."); return; }
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void Log(string s) { Debug.Log("[PK] " + s); }

    // ----- Việc chung -----

    private static IEnumerator OpenWings()
    {
        hb.Select(3);
        if (!pl.IsOpen) hb.TryUseSelected();
        yield return new Wait(1.3f);
    }

    private static void Place(Vector3 feet, float yaw)
    {
        if (pl.IsFlying) pl.EndFlight(true);
        pc.TeleportTo(feet, Quaternion.Euler(0f, yaw, 0f));
        pl.ResetFallTracking();
    }

    private static void Refill() { stats.RestoreMana(stats.MaxMana); pk.ResetCooldown(); }

    private static IEnumerator WaitCastDone(float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (pk.IsCasting && Time.realtimeSinceStartup - t0 < timeout) yield return null;
        yield return new Wait(0.25f);
    }

    private static IEnumerator Cast(string name, Vector3 feet, float yaw, bool fly, float flyHeight, Vector3 dir, float manaLeft)
    {
        Place(feet, yaw);
        yield return new Wait(0.4f);
        Refill();
        if (fly)
        {
            pl.TestSetInput(Vector2.zero, 1f, false);
            if (!pl.IsFlying) pl.BeginFlight(new Vector3(0f, 6f, 0f));
            float t0 = Time.realtimeSinceStartup;
            while (pl.transform.position.y < feet.y + flyHeight && Time.realtimeSinceStartup - t0 < 6f) yield return null;
            pl.TestSetInput(Vector2.zero, 0f, false);
            yield return new Wait(1.0f);
        }
        if (manaLeft >= 0f) stats.DrainMana(stats.Mana - manaLeft);
        Vector3 before = pl.transform.position + Vector3.up;
        float mana0 = stats.Mana;
        pk.ComputeDestination(dir, out Vector3 predicted, out float predDist, out bool predGround);
        bool ok = pk.Cast(dir);
        Log(name + ": Cast=" + ok + " dựĐoán=" + predicted.ToString("F1") + " (" + predDist.ToString("F1") + "m, đất=" + predGround + ") manaTrước=" + mana0.ToString("F0") + " manaSau=" + stats.Mana.ToString("F0") + " hồiChiêuCòn=" + pk.CooldownRemaining.ToString("F1"));
        if (!ok) { yield break; }
        yield return WaitCastDone(3f);
        Vector3 after = pl.transform.position + Vector3.up;
        Log(name + ": xong. vịTríSau=" + after.ToString("F1") + " đã đi " + Vector3.Distance(before, after).ToString("F1") + "m, tới điểm đến cách " + Vector3.Distance(after, predicted).ToString("F2")
            + "m, đangBay=" + pl.IsFlying + " vậnTốc=" + pl.FlightVelocity.magnitude.ToString("F1") + " chạmĐất=" + pc.IsGrounded + " độCaoChân=" + pl.transform.position.y.ToString("F2") + " máu=" + stats.Health.ToString("F0"));
        yield return new Wait(1.0f);
        Log(name + ": sau 1s: đangBay=" + pl.IsFlying + " vậnTốc=" + pl.FlightVelocity.magnitude.ToString("F1") + " chạmĐất=" + pc.IsGrounded + " vịTrí=" + pl.transform.position.ToString("F1"));
    }

    // ================= Kiểm thử logic =================

    public static void RunTests()
    {
        if (!Application.isPlaying) { Debug.LogError("[PK] Cần đang Play trong SanTap."); return; }
        Init();
        Start(Tests());
    }

    private static IEnumerator Tests()
    {
        yield return OpenWings();
        Log("Cảnh giới: " + stats.Realm.DisplayName + ", tầm tối đa " + stats.Realm.PhaKhongDistance + "m");

        if (!OnlyShock) {
        yield return Cast("T1 trên đất (+Z, 40m)", new Vector3(0f, 0.1f, -30f), 0f, false, 0f, Vector3.forward, -1f);
        // T6 hồi chiêu: bấm ngay sau T1
        {
            float cd = pk.CooldownRemaining;
            bool ok = pk.Cast(Vector3.forward);
            Log("T6 đang hồi chiêu (còn " + cd.ToString("F1") + "s): Cast=" + ok + " (mong đợi False), nháyĐỏ=" + (Time.unscaledTime - pk.RejectedAtUnscaled < 0.2f));
        }
        yield return Cast("T2 khi bay (hướng +X lên 12°)", new Vector3(-60f, 0.1f, -40f), 90f, true, 8f, (Vector3.right + Vector3.up * 0.2f).normalized, -1f);
        yield return Cast("T3 hướng vào tường (+Z, tường ở z=15.5)", new Vector3(0f, 0.1f, 0f), 0f, false, 0f, Vector3.forward, -1f);
        yield return Cast("T4 hướng xuống đất (bay cao 20m, chúc 45°)", new Vector3(-40f, 0.1f, -40f), 90f, true, 20f, (Vector3.right + Vector3.down).normalized, -1f);
        yield return Cast("T5 thiếu pháp lực (còn 10)", new Vector3(0f, 0.1f, -30f), 0f, false, 0f, Vector3.forward, 10f);
        Log("T5: nháyĐỏ=" + (Time.unscaledTime - pk.RejectedAtUnscaled < 2f));
        }

        // T7 sóng xung kích
        Place(new Vector3(0f, 0.1f, -30f), 0f);
        yield return new Wait(0.4f);
        Refill();
        pk.ComputeDestination(Vector3.forward, out Vector3 dest, out _, out _);
        Vector3 e = dest - Vector3.forward * 2.5f;
        EnemyBase[] all = UnityEngine.Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        EnemyBase tl = null, tt = null, far = null;
        foreach (EnemyBase eb in all) { if (eb is ThanhLang && tl == null) tl = eb; else if (eb is TaTu && tt == null) tt = eb; else if (far == null && eb is ThanhLang) far = eb; }
        WarpEnemy(tl, e + new Vector3(2f, -1f, 0f));
        WarpEnemy(tt, e + new Vector3(-3.5f, -1f, 1f));
        WarpEnemy(far, e + new Vector3(8f, -1f, 0f));
        yield return new Wait(0.3f);
        EnemyBase[] check = { tl, tt, far };
        float[] hp0 = new float[3];
        for (int i = 0; i < 3; i++) hp0[i] = check[i] != null ? check[i].GetComponent<Damageable>().Health : -1f;
        pk.Cast(Vector3.forward);
        float tc = Time.realtimeSinceStartup;
        float[] stunLeft = { -1f, -1f, -1f };
        while (pk.IsCasting && Time.realtimeSinceStartup - tc < 4f)
        {
            if (pk.CastElapsed >= 0.68f)
                for (int i = 0; i < 3; i++) if (check[i] != null && stunLeft[i] <= 0f) { float sl = StunLeft(check[i]); if (sl > 0f) stunLeft[i] = sl; }
            yield return null;
        }
        yield return new Wait(0.3f);
        for (int i = 0; i < 3; i++)
        {
            if (check[i] == null) continue;
            Damageable d = check[i].GetComponent<Damageable>();
            Log("T7 " + (i == 0 ? "ThanhLang trong vòng (2m)" : i == 1 ? "TàTu trong vòng (~3.6m)" : "ThanhLang ngoài vòng (8m)") + ": khoảngCáchTớiTâm=" + Vector3.Distance(d.AimPoint, e).ToString("F1")
                + " máu " + hp0[i].ToString("F1") + " → " + d.Health.ToString("F1") + " (mất " + (hp0[i] - d.Health).ToString("F1") + ", hệSố=" + d.DamageTakenMultiplier.ToString("F2") + "), choángCònLúcMớiChoáng=" + stunLeft[i].ToString("F2") + "s");
        }
    }

    private static void WarpEnemy(EnemyBase eb, Vector3 pos)
    {
        if (eb == null) return;
        NavMeshAgent ag = eb.GetComponent<NavMeshAgent>();
        if (ag != null && ag.enabled) ag.Warp(pos); else eb.transform.position = pos;
    }

    private static float StunLeft(EnemyBase eb)
    {
        FieldInfo fe = typeof(EnemyBase).GetField("staggerEnd", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fs = typeof(EnemyBase).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        float end = (float)fe.GetValue(eb);
        object st = fs.GetValue(eb);
        bool stag = st != null && st.ToString() == "Staggered";
        return stag ? end - Time.time : 0f;
    }

    // ================= Chụp ảnh =================

    /// <summary>Chụp màn hình Game có cả giao diện HUD: ô hồi chiêu Phá Không sẵn sàng, đang đếm ngược, và lúc nháy đỏ khi bấm V mà đang hồi.</summary>
    public static void RunHud(string tag)
    {
        if (!Application.isPlaying) { Debug.LogError("[PK] Cần đang Play trong SanTap."); return; }
        Init();
        prefix = tag;
        Start(HudShots());
    }

    private static IEnumerator HudShots()
    {
        yield return OpenWings();
        Place(new Vector3(30f, 0.1f, -45f), 0f);
        yield return new Wait(0.8f);
        Refill();
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/" + prefix + "_hud_san_sang.png");
        yield return new Wait(0.5f);
        pk.Cast(Vector3.forward);
        yield return new Wait(2.4f);
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/" + prefix + "_hud_dem_nguoc.png");
        Log("HUD đếm ngược: còn " + pk.CooldownRemaining.ToString("F1") + "s");
        yield return new Wait(0.6f);
        pk.TestPressV();
        yield return null;
        yield return null;
        pk.TestClear();
        yield return new Wait(0.12f);
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/" + prefix + "_hud_nhay_do.png");
        Log("HUD nháy đỏ (bấm V khi đang hồi): đã chụp");
        yield return new Wait(0.6f);
    }

    public static void RunPhotos(string tag)
    {
        if (!Application.isPlaying) { Debug.LogError("[PK] Cần đang Play trong SanTap."); return; }
        Init();
        prefix = tag;
        Start(Photos());
    }

    private static void Capture(string name)
    {
        Camera cam = Camera.main;
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
        UnityEngine.Object.DestroyImmediate(t);
        rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        Log("ảnh " + name + " tại t=" + pk.CastElapsed.ToString("F3") + "s, vịTríNhânVật=" + pl.transform.position.ToString("F1") + " cam=" + cam.transform.position.ToString("F1"));
    }

    private static IEnumerator Photos()
    {
        yield return OpenWings();
        Place(new Vector3(30f, 0.1f, -45f), 0f);
        yield return new Wait(0.6f);
        Refill();
        // Ảnh ngắm (giữ V)
        pk.TestPressV();
        yield return new Wait(0.5f);
        Capture("ngam");
        pk.TestReleaseV();   // nhả V = thi triển (cần ít nhất một khung hình để PhaKhong đọc)
        yield return null;
        yield return null;
        pk.TestClear();
        Time.timeScale = SlowMo;
        float[] marks = { 0.02f, 0.25f, 0.40f, 0.70f, 0.80f, 0.88f };
        string[] names = { "t000", "t025", "t040", "t070", "t080", "t090" };
        int idx = 0;
        float t0 = Time.realtimeSinceStartup;
        while (idx < marks.Length && Time.realtimeSinceStartup - t0 < 30f)
        {
            if (pk.IsCasting && pk.CastElapsed >= marks[idx]) { Capture(names[idx]); idx++; }
            yield return null;
        }
        Time.timeScale = 1f;
        yield return new Wait(1.5f);
        Capture("sau");
        Log("camera cách nhân vật " + Vector3.Distance(Camera.main.transform.position, pl.transform.position).ToString("F2") + "m sau khi xong (tốc độ thường)");
    }
}
#endif
