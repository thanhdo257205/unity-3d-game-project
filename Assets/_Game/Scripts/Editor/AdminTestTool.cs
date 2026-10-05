#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm thử bảng Admin (chỉ Editor), chạy trong SanTap đang Play (Unity cần có tiêu điểm). Kết quả in vào Console, dòng bắt đầu bằng "[AD]".
/// AdminTestTool.Run(): từng công tắc, nút bấm, đổi cảnh giới tới Nguyên Anh, thanh trượt tốc độ; chụp ảnh bảng đang mở (Assets/Screenshots/admin_bang.png, admin_nhan.png).
/// </summary>
public static class AdminTestTool
{
    private class Wait { public float until; public Wait(float s) { until = Time.realtimeSinceStartup + s; } }
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static object current;
    private static PlayerStats stats; private static PhaKhong pk; private static PhongLoiSi pl; private static PlayerController pc; private static Hotbar hb;
    private static SwordSwarm swarm; private static AdminPanel panel;
    private static int damagedEvents;

    private static void Log(string s) { Debug.Log("[AD] " + s); }

    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("[AD] Cần đang Play trong SanTap."); return; }
        stats = Object.FindAnyObjectByType<PlayerStats>(); pk = Object.FindAnyObjectByType<PhaKhong>(); pl = Object.FindAnyObjectByType<PhongLoiSi>();
        pc = Object.FindAnyObjectByType<PlayerController>(); hb = Object.FindAnyObjectByType<Hotbar>(); swarm = Object.FindAnyObjectByType<SwordSwarm>();
        panel = Object.FindAnyObjectByType<AdminPanel>();
        stack.Clear(); stack.Push(Tests()); current = null;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        if (current is Wait w && Time.realtimeSinceStartup < w.until) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        current = null;
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            object c = top.Current;
            if (c is IEnumerator child) { stack.Push(child); continue; }
            current = c; break;
        }
        if (stack.Count == 0) { EditorApplication.update -= Tick; Time.timeScale = 1f; Log("Xong."); return; }
        EditorApplication.QueuePlayerLoopUpdate();
    }

    /// <summary>Chụp bảng đang mở với vài công tắc bật (để xem giao diện).</summary>
    public static void Shot()
    {
        if (!Application.isPlaying) return;
        panel = Object.FindAnyObjectByType<AdminPanel>();
        stack.Clear(); stack.Push(ShotSeq()); current = null;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    private static IEnumerator ShotSeq()
    {
        AdminPanel.MauVoHan = true; AdminPanel.KeDichDungYen = true;
        panel.SetOpen(true);
        yield return new Wait(0.6f);
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/admin_bang.png");
        yield return new Wait(0.6f);
        panel.SetOpen(false);
        AdminPanel.MauVoHan = false; AdminPanel.KeDichDungYen = false;
    }

    private static IEnumerator Tests()
    {
        if (panel == null) { Log("Không thấy AdminPanel trong scene."); yield break; }
        Log("Lúc mới Play: MauVoHan=" + AdminPanel.MauVoHan + " PhapLucVoHan=" + AdminPanel.PhapLucVoHan + " KhongHoiChieu=" + AdminPanel.KhongHoiChieu + " KeDichDungYen=" + AdminPanel.KeDichDungYen + " MotDonHaGuc=" + AdminPanel.MotDonHaGuc + " tốcĐộ=" + AdminPanel.TocDoGame + " (mong đợi toàn False, 1)");

        // --- Mở bảng ---
        bool lockedBefore = Cursor.lockState == CursorLockMode.Locked;
        panel.SetOpen(true);
        yield return new Wait(0.4f);
        Log("Mở bảng: DangMo=" + AdminPanel.DangMo + " conTrỏKhóa=" + (Cursor.lockState == CursorLockMode.Locked) + " (trước đó khóa=" + lockedBefore + ") conTrỏHiện=" + Cursor.visible + " cameraNhậnChuột=" + Object.FindAnyObjectByType<CameraOrbit>().InputEnabled);
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/admin_bang.png");
        yield return new Wait(0.5f);
        panel.SetOpen(false);
        yield return new Wait(0.2f);
        Log("Đóng bảng: DangMo=" + AdminPanel.DangMo + " cameraNhậnChuột=" + Object.FindAnyObjectByType<CameraOrbit>().InputEnabled);

        // --- Chuẩn bị: mở cánh, đứng ở chỗ trống ---
        hb.Select(3);
        if (!pl.IsOpen) hb.TryUseSelected();
        yield return new Wait(1.3f);
        pc.TeleportTo(new Vector3(30f, 0.1f, -45f), Quaternion.identity);
        yield return new Wait(0.5f);
        stats.RestoreAll();
        stats.Damaged += OnDamaged;

        // --- Máu vô hạn ---
        float hp0 = stats.Health; damagedEvents = 0;
        stats.TakeDamage(30f);
        Log("Máu vô hạn TẮT: máu " + hp0 + " → " + stats.Health + " (mong đợi giảm 30), sựKiệnDamaged=" + damagedEvents);
        stats.RestoreAll();
        yield return new Wait(0.7f);
        AdminPanel.MauVoHan = true; damagedEvents = 0;
        hp0 = stats.Health;
        stats.TakeDamage(30f);
        yield return new Wait(0.7f);
        stats.TakeDamage(30f);
        Log("Máu vô hạn BẬT: máu " + hp0 + " → " + stats.Health + " sau 2 đòn 30 (mong đợi không đổi), sựKiệnDamaged=" + damagedEvents + " (mong đợi 2)");
        AdminPanel.MauVoHan = false;
        stats.Damaged -= OnDamaged;

        // --- Pháp lực vô hạn ---
        stats.RestoreAll();
        stats.DrainMana(stats.Mana - 5f);
        float m0 = stats.Mana;
        bool okOff = stats.TrySpendMana(50f);
        Log("Pháp lực vô hạn TẮT: mana còn " + m0.ToString("F0") + ", TrySpendMana(50)=" + okOff + " (mong đợi False)");
        AdminPanel.PhapLucVoHan = true;
        bool okOn = stats.TrySpendMana(50f);
        yield return new Wait(0.3f);
        Log("Pháp lực vô hạn BẬT: TrySpendMana(50)=" + okOn + ", DrainMana=" + stats.DrainMana(500f) + ", mana=" + stats.Mana.ToString("F0") + "/" + stats.MaxMana.ToString("F0") + " (mong đợi True, True, đầy)");
        // Phá Không với pháp lực vô hạn: cast 2 lần liên tiếp, mana vẫn đầy
        AdminPanel.KhongHoiChieu = true;
        pc.TeleportTo(new Vector3(30f, 0.1f, -45f), Quaternion.identity);
        yield return new Wait(0.5f);
        bool c1 = pk.Cast(Vector3.forward);
        yield return new Wait(1.2f);
        bool c2 = pk.Cast(Vector3.back);
        yield return new Wait(1.2f);
        Log("Không hồi chiêu + pháp lực vô hạn: Phá Không lần 1=" + c1 + ", lần 2 ngay sau=" + c2 + " (mong đợi True, True), hồiChiêuCòn=" + pk.CooldownRemaining.ToString("F1") + ", mana=" + stats.Mana.ToString("F0"));
        AdminPanel.PhapLucVoHan = false;
        // Không hồi chiêu TẮT: lần 2 phải bị từ chối
        AdminPanel.KhongHoiChieu = false;
        stats.RestoreAll(); pk.ResetCooldown();
        pc.TeleportTo(new Vector3(30f, 0.1f, -45f), Quaternion.identity);
        yield return new Wait(0.5f);
        bool d1 = pk.Cast(Vector3.forward);
        yield return new Wait(1.2f);
        bool d2 = pk.Cast(Vector3.back);
        Log("Không hồi chiêu TẮT: lần 1=" + d1 + ", lần 2 ngay sau=" + d2 + " (mong đợi True, False), hồiChiêuCòn=" + pk.CooldownRemaining.ToString("F1") + "s");
        pk.ResetCooldown();

        // --- Nút bấm: sinh kẻ địch ---
        panel.XoaKeDich();
        yield return new Wait(0.3f);
        pc.TeleportTo(new Vector3(30f, 0.1f, -45f), Quaternion.identity);
        yield return new Wait(0.5f);
        GameObject tl = null, tt = null;
        tl = panel.SinhKeDich(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/ThanhLang.prefab"));
        tt = panel.SinhKeDich(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TaTu.prefab"));
        yield return new Wait(0.5f);
        Log("Sinh kẻ địch: ThanhLang cách " + Vector3.Distance(Flat(tl.transform.position), Flat(pc.transform.position)).ToString("F1") + "m, TàTu cách " + Vector3.Distance(Flat(tt.transform.position), Flat(pc.transform.position)).ToString("F1") + "m (mong đợi 8), tổng EnemyBase=" + EnemyBase.All.Count);

        // --- Kẻ địch đứng yên ---
        EnemyBase eTL = tl.GetComponent<EnemyBase>();
        Vector3 pa = tl.transform.position, pb = tt.transform.position;
        float hpP = stats.Health;
        yield return new Wait(3f);
        float moved = Vector3.Distance(pa, tl.transform.position) + Vector3.Distance(pb, tt.transform.position);
        Log("Kẻ địch đứng yên TẮT: 3s sau hai con đã di chuyển tổng " + moved.ToString("F1") + "m (mong đợi > 0), máu người chơi " + hpP.ToString("F0") + " → " + stats.Health.ToString("F0"));
        AdminPanel.KeDichDungYen = true;
        yield return new Wait(0.5f);
        pa = tl.transform.position; pb = tt.transform.position;
        hpP = stats.Health;
        pc.TeleportTo(tl.transform.position + new Vector3(2f, 0f, 0f), Quaternion.identity);   // đứng sát để địch muốn đánh
        yield return new Wait(3f);
        moved = Vector3.Distance(pa, tl.transform.position) + Vector3.Distance(pb, tt.transform.position);
        Log("Kẻ địch đứng yên BẬT (người chơi đứng sát ThanhLang): 3s sau di chuyển " + moved.ToString("F2") + "m (mong đợi ~0), máu người chơi " + hpP.ToString("F0") + " → " + stats.Health.ToString("F0") + " (mong đợi không đổi), EnemyBase.AIEnabled=" + EnemyBase.AIEnabled);
        ScreenCapture.CaptureScreenshot("Assets/Screenshots/admin_nhan.png");
        yield return new Wait(0.5f);

        // --- Một đòn hạ gục ---
        Damageable dTT = tt.GetComponent<Damageable>();
        float h0 = dTT.Health;
        dTT.TakeDamage(new DamageInfo { amount = 1f, point = dTT.AimPoint, direction = Vector3.forward });
        Log("Một đòn hạ gục TẮT: TàTu máu " + h0.ToString("F0") + " → " + dTT.Health.ToString("F0") + " sau đòn 1 sát thương (mong đợi còn sống)");
        AdminPanel.MotDonHaGuc = true;
        dTT.TakeDamage(new DamageInfo { amount = 1f, point = dTT.AimPoint, direction = Vector3.forward });
        Log("Một đòn hạ gục BẬT: TàTu máu " + dTT.Health.ToString("F0") + " còn sống=" + dTT.IsAlive + " (mong đợi chết sau đòn 1 sát thương)");
        AdminPanel.MotDonHaGuc = false;
        AdminPanel.KeDichDungYen = false;

        // --- Xóa hết kẻ địch ---
        int before = EnemyBase.All.Count;
        panel.XoaKeDich();
        yield return new Wait(0.4f);
        Log("Xóa hết kẻ địch: " + before + " → " + EnemyBase.All.Count);

        // --- Về điểm xuất phát, hồi đầy ---
        panel.VeXuatPhat();
        yield return new Wait(0.5f);
        Log("Về điểm xuất phát: vịTrí=" + pc.transform.position.ToString("F1") + " (mong đợi gần 0, 0.1, 0)");
        stats.DrainMana(stats.Mana - 3f); stats.TakeDamage(10f);
        panel.HoiDay();
        Log("Hồi đầy: máu " + stats.Health.ToString("F0") + "/" + stats.MaxHealth.ToString("F0") + " mana " + stats.Mana.ToString("F0") + "/" + stats.MaxMana.ToString("F0"));

        // --- Thanh trượt tốc độ ---
        panel.SetOpen(true);
        yield return new Wait(0.3f);
        var slider = panel.GetComponentInChildren<UnityEngine.UI.Slider>(true);
        slider.value = 0.3f;
        yield return new Wait(0.3f);
        Log("Thanh trượt 0.3: TocDoGame=" + AdminPanel.TocDoGame.ToString("F2") + " Time.timeScale=" + Time.timeScale.ToString("F2") + " CoCongTacBat=" + AdminPanel.CoCongTacBat);
        slider.value = 1f;
        yield return new Wait(0.3f);
        Log("Đặt lại 1: TocDoGame=" + AdminPanel.TocDoGame.ToString("F2") + " Time.timeScale=" + Time.timeScale.ToString("F2") + " CoCongTacBat=" + AdminPanel.CoCongTacBat);
        panel.SetOpen(false);

        // --- Đổi cảnh giới tới Nguyên Anh ---
        stats.SetRealmIndex(2);
        yield return new Wait(0.3f);
        Log("Trước: " + stats.Realm.DisplayName + " manaMax=" + stats.MaxMana.ToString("F0") + " kiếmMax=" + stats.MaxSwords + " số kiếm hiện tại=" + swarm.SwordCount + " tầmPháKhông=" + stats.Realm.PhaKhongDistance);
        stats.DrainMana(stats.Mana - 5f); stats.TakeDamage(15f);
        panel.SetRealm(stats.RealmCount - 1);
        yield return new Wait(0.5f);
        Log("Sau (Nguyên Anh): " + stats.Realm.DisplayName + " manaMax=" + stats.MaxMana.ToString("F0") + " mana=" + stats.Mana.ToString("F0") + " máu=" + stats.Health.ToString("F0") + "/" + stats.MaxHealth.ToString("F0") + " kiếmMax=" + stats.MaxSwords + " số kiếm hiện tại=" + swarm.SwordCount + " tầmPháKhông=" + stats.Realm.PhaKhongDistance);
        // Tầm Phá Không thật: bay giữa trời, hướng ra ngoài mặt đất
        pc.TeleportTo(new Vector3(0f, 60f, 0f), Quaternion.Euler(0f, 90f, 0f));
        pl.TestSetInput(Vector2.zero, 0f, false);
        pk.ComputeDestination(Vector3.right, out Vector3 dest, out float dist, out bool grd);
        Log("Phá Không thực tế ở Nguyên Anh (hướng +X từ y=61, trống): quãng " + dist.ToString("F1") + "m (mong đợi 400)");
        stats.SetRealmIndex(2);
        pk.ComputeDestination(Vector3.right, out dest, out dist, out grd);
        Log("Phá Không thực tế ở Luyện Khí 7: quãng " + dist.ToString("F1") + "m (mong đợi 40)");
        pl.TestClearInput();
        yield return new Wait(0.2f);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    private static void OnDamaged(float a) { damagedEvents++; }
}
#endif
