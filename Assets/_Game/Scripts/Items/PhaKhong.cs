using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Phá Không: kỹ năng xuyên không gian của Phong Lôi Sí (phím V). Gắn ở đối tượng NhanVat, cạnh PhongLoiSi.
/// Điều kiện: đang chọn ô 4 (ô chứa Phong Lôi Sí) và cánh đã mở xong. Dùng được cả trên đất lẫn khi bay.
/// - NGẮM: giữ V thì hiện vòng tròn mờ ở điểm đến (theo hướng camera, xa tối đa theo cảnh giới: RealmData.PhaKhongDistance);
///   nhả V thì thi triển; chuột phải khi đang giữ thì hủy. Bấm nhanh V = đi hết tầm theo hướng camera.
/// - ĐIỂM ĐẾN: nếu đường thẳng vướng vật cản thì dừng trước vật cản "Dừng trước vật cản" mét; không đáp vào trong vật thể hay dưới đất; không vượt trần bay.
///   Gần mặt đất thì đáp xuống đất. Gần hơn "Tầm tối thiểu" thì không thi triển được.
/// - CHI PHÍ: 50 pháp lực, hồi 6 giây. Thiếu pháp lực hoặc đang hồi thì không làm gì, ô hồi chiêu trên HUD nháy đỏ nhẹ.
/// - DIỄN BIẾN (giây từ lúc nhả V): 0.00 cổng vào mở trước mặt 2.5m, cánh khép, sét bùng mức 1 | 0.25 lao vào tâm cổng trong 0.15s kèm vệt vàng xanh
///   (bắt đầu bất tử, mất điều khiển) | 0.40 nhân vật biến mất, cổng vào đóng, cổng ra mở tại điểm đến, camera lướt tới trong 0.3s |
///   0.70 nhân vật lao ra khỏi cổng ra, cánh bung, sóng xung kích bán kính 5m nở trong 0.25s (25 sát thương lôi, choáng 0.6s), chớp sáng | 0.90 cổng ra đóng, trả điều khiển.
///   Ra giữa trời thì tiếp tục bay với vận tốc bay thường theo hướng cũ; ra trên đất thì đứng yên.
/// Mọi con số chỉnh trong Inspector. Phím thử (chỉ Editor): F8 hồi đầy pháp lực và xóa hồi chiêu.
/// </summary>
public class PhaKhong : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Pháp bảo Phong Lôi Sí (cùng đối tượng NhanVat). Để trống thì tự tìm.")]
    [SerializeField] private PhongLoiSi wings;

    [Tooltip("Prefab cổng không gian (Assets/_Game/VFX/CongKhongGian.prefab). Hai cổng (vào và ra) được tạo từ prefab này lúc bắt đầu.")]
    [SerializeField] private CongKhongGian congPrefab;

    [Tooltip("Rung camera / hiệu ứng trúng đòn. Để trống thì tự tìm.")]
    [SerializeField] private HitFeedback feedback;

    [Tooltip("Vật liệu vòng ngắm ở điểm đến (ảnh tia xanh lơ).")]
    [SerializeField] private Material matVongNham;

    [Tooltip("Vật liệu sóng xung kích (shader PhamKiemLuc/SongXungKich).")]
    [SerializeField] private Material matSongXungKich;

    [Tooltip("Vật liệu chớp sáng lúc ra khỏi cổng (quầng tròn cộng sáng, shader PhamKiemLuc/QuangTron).")]
    [SerializeField] private Material matChopSang;

    [Header("Chi phí")]
    [Tooltip("Pháp lực tốn mỗi lần thi triển.")]
    [SerializeField, Min(0f)] private float cost = 50f;

    [Tooltip("Thời gian hồi chiêu (giây), tính từ lúc thi triển.")]
    [SerializeField, Min(0f)] private float cooldown = 6f;

    [Header("Điểm đến")]
    [Tooltip("Lớp vật cản và mặt đất (tường, bậc thang, nền). Mặc định lớp MoiTruong.")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Tooltip("Vướng vật cản thì dừng trước vật cản bấy nhiêu mét.")]
    [SerializeField, Min(0f)] private float stopBeforeObstacle = 1f;

    [Tooltip("Tầm tối thiểu (mét): điểm đến gần hơn mức này thì không thi triển được (cổng vào và cổng ra cần chỗ).")]
    [SerializeField, Min(5f)] private float minDistance = 6f;

    [Tooltip("Trần bay: điểm đến không cao hơn mặt đất bên dưới quá bấy nhiêu mét.")]
    [SerializeField, Min(1f)] private float ceiling = 60f;

    [Tooltip("Độ cao tâm thân người so với chân (mét). Điểm đến tính theo tâm thân.")]
    [SerializeField, Min(0.5f)] private float bodyCenterHeight = 1f;

    [Tooltip("Bán kính khoảng trống cần có quanh người ở điểm đến (mét), để không đáp vào trong vật thể.")]
    [SerializeField, Min(0.1f)] private float clearanceRadius = 0.35f;

    [Tooltip("Bán kính vòng ngắm ở điểm đến (mét). Bằng bán kính cổng.")]
    [SerializeField, Min(0.3f)] private float aimRingRadius = 1.6f;

    [Header("Diễn biến (giây từ lúc nhả V)")]
    [Tooltip("Cổng vào mở cách trước mặt bấy nhiêu mét.")]
    [SerializeField, Min(1f)] private float entranceDistance = 2.5f;

    [Tooltip("Cổng ra mở lùi sau điểm đến bấy nhiêu mét (nhân vật lao ra khỏi cổng rồi tới đúng điểm đến).")]
    [SerializeField, Min(0.5f)] private float exitBehind = 2.5f;

    [Tooltip("Mốc nhân vật bắt đầu lao vào cổng vào (cũng là lúc bắt đầu bất tử và mất điều khiển).")]
    [SerializeField, Min(0.05f)] private float timeDash = 0.25f;

    [Tooltip("Mốc nhân vật biến mất: cổng vào đóng, cổng ra mở, camera lướt tới điểm đến. Thời gian lao vào cổng = mốc này trừ mốc lao.")]
    [SerializeField, Min(0.1f)] private float timeVanish = 0.4f;

    [Tooltip("Mốc nhân vật lao ra khỏi cổng ra: cánh bung, sóng xung kích, chớp sáng. Hết bất tử.")]
    [SerializeField, Min(0.2f)] private float timeReappear = 0.7f;

    [Tooltip("Mốc kết thúc: cổng ra đóng, trả điều khiển.")]
    [SerializeField, Min(0.3f)] private float timeEnd = 0.9f;

    [Tooltip("Thời gian camera lướt tới điểm đến (giây), bắt đầu từ lúc nhân vật biến mất.")]
    [SerializeField, Min(0.05f)] private float cameraGlideTime = 0.3f;

    [Tooltip("Mức khép cánh lúc lao vào cổng (0 = giữ nguyên, 1 = khép hẳn).")]
    [SerializeField, Range(0f, 1f)] private float wingFoldAmount = 0.9f;

    [Tooltip("Mở rộng góc nhìn thêm bấy nhiêu độ trong lúc lao (từ mốc lao tới mốc ra).")]
    [SerializeField, Range(0f, 30f)] private float fovBoost = 10f;

    [Header("Sóng xung kích")]
    [Tooltip("Bán kính sóng (mét).")]
    [SerializeField, Min(0.5f)] private float shockRadius = 5f;

    [Tooltip("Thời gian sóng nở ra tới hết bán kính (giây).")]
    [SerializeField, Min(0.05f)] private float shockTime = 0.25f;

    [Tooltip("Sát thương lôi lên mỗi kẻ địch trong vòng (chưa nhân hệ số chênh lệch cảnh giới của kẻ địch).")]
    [SerializeField, Min(0f)] private float shockDamage = 25f;

    [Tooltip("Thời gian làm choáng kẻ địch trong vòng (giây).")]
    [SerializeField, Min(0f)] private float shockStun = 0.6f;

    [Tooltip("Rung camera lúc sóng nổ (mét). 0 = không rung.")]
    [SerializeField, Min(0f)] private float shockShake = 0.08f;

    [Tooltip("Độ sáng đèn chớp lúc ra khỏi cổng (chỉ chiếu mặt đất / môi trường).")]
    [SerializeField, Min(0f)] private float flashLight = 10f;

    [Header("Phím thử (chỉ Editor)")]
    [Tooltip("Bật phím F8: hồi đầy pháp lực và xóa hồi chiêu.")]
    [SerializeField] private bool debugKeys = true;

    // ----- Tham chiếu tự tìm -----
    private PlayerStats stats;
    private PlayerController player;
    private Hotbar hotbar;
    private CameraOrbit camOrbit;
    private Animator animator;
    private CongKhongGian portalIn, portalOut;

    // ----- Trạng thái -----
    private bool aiming, casting;
    private float cooldownEnd = -10f;
    private float rejectedAt = -10f;
    private float castStart;
    private Vector3 castDir, p0, portalInPos, destCenter, exitCenter;
    private bool destGrounded, wasFlying;
    private bool stDash, stVanish, stReappear, stEnd;
    private LineRenderer aimRing;
    private Material aimMat;
    private Renderer[] hideList;
    private bool[] hideWas;

    // Sóng xung kích
    private Transform shockQuad, flashQuad;
    private Material shockMat, flashMat;
    private Light flashLightObj;
    private float shockStart = -10f;
    private Vector3 shockCenter;
    private readonly HashSet<Damageable> shockHit = new HashSet<Damageable>();
    private static Mesh quadMesh;
    private static readonly int TId = Shader.PropertyToID("_T");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // ----- Giá trị cho script khác (HUD, kiểm thử) -----

    /// <summary>Xóa hồi chiêu (phím F8 / kiểm thử).</summary>
    public void ResetCooldown() { cooldownEnd = -10f; }

    /// <summary>Thời gian hồi chiêu đầy đủ (giây).</summary>
    public float Cooldown => cooldown;

    /// <summary>Số giây còn phải chờ (0 = dùng được).</summary>
    public float CooldownRemaining => AdminPanel.KhongHoiChieu ? 0f : Mathf.Max(0f, cooldownEnd - Time.time);

    /// <summary>Pháp lực mỗi lần thi triển.</summary>
    public float Cost => cost;

    /// <summary>Đang giữ V để ngắm.</summary>
    public bool IsAiming => aiming;

    /// <summary>Đang trong diễn biến Phá Không (từ lúc nhả V tới 0.9s).</summary>
    public bool IsCasting => casting;

    /// <summary>Số giây từ lúc thi triển (âm nếu chưa thi triển).</summary>
    public float CastElapsed => casting ? Time.time - castStart : -1f;

    /// <summary>Thời điểm (Time.unscaledTime) lần bấm V gần nhất bị từ chối (thiếu pháp lực / đang hồi / điểm đến không hợp lệ). HUD nháy đỏ nhẹ.</summary>
    public float RejectedAtUnscaled => rejectedAt;

    /// <summary>Điểm đến (tâm thân) của lần thi triển gần nhất.</summary>
    public Vector3 LastDestination => destCenter;

    /// <summary>Bán kính sóng xung kích (mét).</summary>
    public float ShockRadius => shockRadius;

    /// <summary>Sát thương sóng.</summary>
    public float ShockDamage => shockDamage;

    /// <summary>Thời gian choáng của sóng.</summary>
    public float ShockStun => shockStun;

    /// <summary>Báo khi có lần bấm V bị từ chối.</summary>
    public event Action Rejected;

    /// <summary>Báo khi diễn biến kết thúc.</summary>
    public event Action CastFinished;

    // ================= Khởi tạo =================

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        player = GetComponent<PlayerController>();
        hotbar = GetComponent<Hotbar>();
        if (wings == null) wings = GetComponent<PhongLoiSi>();
        if (feedback == null) feedback = FindAnyObjectByType<HitFeedback>();
        camOrbit = FindAnyObjectByType<CameraOrbit>();
        animator = GetComponentInChildren<Animator>();
        if (stats != null) stats.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.Died -= OnDied;
    }

    private void OnDied() { Cancel(); }

    private void EnsureObjects()
    {
        if (portalIn == null && congPrefab != null)
        {
            portalIn = Instantiate(congPrefab);
            portalIn.name = "CongPhaKhong_Vao";
            portalOut = Instantiate(congPrefab);
            portalOut.name = "CongPhaKhong_Ra";
        }
        if (aimRing == null)
        {
            aimMat = matVongNham != null ? new Material(matVongNham) : null;
            if (aimMat != null) aimMat.SetColor(BaseColorId, new Color(1.6f, 1.6f, 1.6f, 1f));
            GameObject go = new GameObject("VongNham_PhaKhong");
            aimRing = TiaSet.MakeLine(go.transform, "Vong", aimMat, 0.02f, false);
            aimRing.loop = true;
            aimRing.positionCount = 0;
            aimRing.enabled = false;
        }
        if (shockQuad == null)
        {
            shockMat = matSongXungKich != null ? new Material(matSongXungKich) : null;
            flashMat = matChopSang != null ? new Material(matChopSang) : null;
            shockQuad = MakeQuad("SongXungKich_PhaKhong", shockMat);
            flashQuad = MakeQuad("ChopSang_PhaKhong", flashMat);
            shockQuad.gameObject.SetActive(false);
            flashQuad.gameObject.SetActive(false);
            GameObject lg = new GameObject("DenChop_PhaKhong");
            flashLightObj = lg.AddComponent<Light>();
            flashLightObj.type = LightType.Point;
            flashLightObj.color = new Color(0.8f, 0.9f, 1f);
            flashLightObj.range = 12f;
            flashLightObj.intensity = 0f;
            flashLightObj.shadows = LightShadows.None;
            flashLightObj.enabled = false;
            lg.AddComponent<UniversalAdditionalLightData>().renderingLayers = 1u;   // chỉ chiếu môi trường, không nhuộm nhân vật
        }
    }

    private static Transform MakeQuad(string name, Material mat)
    {
        if (quadMesh == null)
        {
            quadMesh = new Mesh { name = "PhaKhong_Quad" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quadMesh.RecalculateBounds();
        }
        GameObject g = new GameObject(name);
        g.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return g.transform;
    }

    // ================= Điều khiển =================

    private Vector3 BodyCenter() { return transform.position + Vector3.up * bodyCenterHeight; }

    private bool Usable()
    {
        if (wings == null || hotbar == null || stats == null || player == null) return false;
        if (hotbar.GetItem(hotbar.SelectedIndex) != wings.WingItem || wings.WingItem == null) return false;
        return wings.ReadyForRift && !wings.FrozenForRift && !player.ControlLocked && stats.IsAlive;
    }

    private void Update()
    {
        if (stats == null || !stats.IsAlive)
        {
            if (aiming || casting) Cancel();
            return;
        }
        Keyboard kb = Keyboard.current;
#if UNITY_EDITOR
        if (debugKeys && kb != null && kb.f8Key.wasPressedThisFrame)
        {
            stats.RestoreMana(stats.MaxMana);
            cooldownEnd = -10f;
        }
#endif
        if (casting) { UpdateCast(); UpdateShock(); return; }
        UpdateShock();
        if (kb == null && !testActive) return;
        Mouse ms = Mouse.current;

        // Phím V (hoặc giả lập để kiểm thử qua code)
        bool vDown, vUp, vHeld;
        if (testActive) { vDown = testDown; vUp = testUp; vHeld = testV; testDown = false; testUp = false; }
        else { vDown = kb.vKey.wasPressedThisFrame; vUp = kb.vKey.wasReleasedThisFrame; vHeld = kb.vKey.isPressed; }

        if (!aiming)
        {
            if (vDown && Usable())
            {
                if (CooldownRemaining > 0f) { Reject(false); return; }
                if (!stats.HasMana(cost)) { Reject(true); return; }
                EnsureObjects();
                aiming = true;
            }
            return;
        }

        // Đang giữ V để ngắm
        bool cancel = (ms != null && ms.rightButton.wasPressedThisFrame) || testCancel || !Usable();
        testCancel = false;
        if (cancel) { CancelAim(); return; }
        Vector3 dir = AimDirection();
        bool ok = ComputeDestination(dir, out Vector3 dest, out _, out _);
        UpdateAimRing(ok, dest, dir);
        if (vUp || !vHeld)
        {
            CancelAim();
            Cast(dir);
        }
    }

    // ----- Kiểm thử qua code (thay phím V và chuột phải) -----
    private bool testActive, testDown, testUp, testV, testCancel;

    /// <summary>Kiểm thử: giả lập bấm V xuống (bắt đầu ngắm nếu đủ điều kiện).</summary>
    public void TestPressV() { testActive = true; testDown = true; testV = true; }

    /// <summary>Kiểm thử: giả lập nhả V (thi triển).</summary>
    public void TestReleaseV() { testActive = true; testUp = true; testV = false; }

    /// <summary>Kiểm thử: giả lập bấm chuột phải khi đang ngắm (hủy).</summary>
    public void TestRightClick() { testActive = true; testCancel = true; }

    /// <summary>Kiểm thử: trả đầu vào về bàn phím thật.</summary>
    public void TestClear() { testActive = false; testDown = testUp = testV = testCancel = false; }

    private static Vector3 AimDirection()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.forward : Vector3.forward;
    }

    private void Reject(bool manaShort)
    {
        rejectedAt = Time.unscaledTime;
        if (manaShort && stats != null) stats.ReportManaShortage();
        Rejected?.Invoke();
    }

    private void CancelAim()
    {
        aiming = false;
        if (aimRing != null) aimRing.enabled = false;
    }

    /// <summary>Hủy mọi thứ (chết, đổi cảnh): thôi ngắm, dừng diễn biến, trả lại điều khiển và hiện nhân vật.</summary>
    public void Cancel()
    {
        CancelAim();
        if (casting) Finish(true);
    }

    // ================= Điểm đến =================

    /// <summary>
    /// Tính điểm đến (tâm thân) khi thi triển theo hướng dir. Trả về false nếu không hợp lệ (gần hơn tầm tối thiểu).
    /// distance = quãng thật sẽ đi; grounded = điểm đến sát mặt đất (đáp xuống đất).
    /// </summary>
    public bool ComputeDestination(Vector3 dir, out Vector3 dest, out float distance, out bool grounded)
    {
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : transform.forward;
        Vector3 origin = BodyCenter();
        float maxD = stats != null && stats.Realm != null ? stats.Realm.PhaKhongDistance : 15f;
        float d = maxD;
        RaycastHit hit;
        if (Physics.SphereCast(origin, 0.3f, dir, out hit, maxD, obstacleMask, QueryTriggerInteraction.Ignore))
            d = Mathf.Max(0f, hit.distance + 0.3f - stopBeforeObstacle);   // dừng trước bề mặt vật cản stopBeforeObstacle mét

        grounded = false;
        dest = origin + dir * d;
        // Không vượt trần bay và không nằm trong vật thể / dưới đất: lùi dần dọc đường đi
        for (int i = 0; i < 80 && d >= minDistance; i++)
        {
            dest = origin + dir * d;
            grounded = false;
            // Sát mặt đất thì đáp xuống đất
            RaycastHit g;
            if (Physics.Raycast(dest + Vector3.up * 0.3f, Vector3.down, out g, bodyCenterHeight + 0.3f + 0.25f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                dest.y = g.point.y + bodyCenterHeight;
                grounded = true;
            }
            bool tooHigh = false;
            RaycastHit gh;
            if (Physics.Raycast(dest + Vector3.up * 0.5f, Vector3.down, out gh, 1000f, obstacleMask, QueryTriggerInteraction.Ignore))
                tooHigh = (dest.y - bodyCenterHeight) - gh.point.y > ceiling;
            else tooHigh = false;
            Vector3 feet = dest - Vector3.up * bodyCenterHeight;
            bool blocked = LandingBlocked(feet);
            if (!tooHigh && !blocked) { distance = Vector3.Distance(origin, dest); return distance >= minDistance - 0.01f; }
            d -= tooHigh ? 2f : 0.5f;
        }
        distance = Mathf.Max(0f, d);
        return false;
    }

    private readonly Collider[] overlapBuf = new Collider[16];

    /// <summary>
    /// Chỗ đáp (chân ở feet) có vướng vật thể nào không: tường, mặt đất, bậc thang, hình nộm, kẻ địch... Bỏ qua chính nhân vật, phi kiếm và vùng kích hoạt (trigger).
    /// </summary>
    private bool LandingBlocked(Vector3 feet)
    {
        Vector3 p1 = feet + Vector3.up * (clearanceRadius + 0.05f);
        Vector3 p2 = feet + Vector3.up * (bodyCenterHeight * 1.8f - clearanceRadius);
        int n = Physics.OverlapCapsuleNonAlloc(p1, p2, clearanceRadius, overlapBuf, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = overlapBuf[i];
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<FlyingSword>() != null) continue;
            return true;
        }
        return false;
    }

    private void UpdateAimRing(bool ok, Vector3 dest, Vector3 dir)
    {
        if (aimRing == null) return;
        if (!ok) { aimRing.enabled = false; return; }
        aimRing.enabled = true;
        Vector3 right = Vector3.Cross(Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up, dir).normalized;
        Vector3 up = Vector3.Cross(dir, right);
        const int n = 48;
        aimRing.positionCount = n;
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            aimRing.SetPosition(i, dest + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * (aimRingRadius * pulse));
        }
        aimRing.startColor = aimRing.endColor = new Color(1f, 1f, 1f, 0.45f);
    }

    // ================= Thi triển =================

    /// <summary>
    /// Thi triển Phá Không theo hướng dir: kiểm tra điều kiện, trừ pháp lực, bắt đầu hồi chiêu và diễn biến.
    /// Trả về false (không làm gì, HUD nháy đỏ) nếu thiếu pháp lực, đang hồi hoặc điểm đến không hợp lệ.
    /// </summary>
    public bool Cast(Vector3 dir)
    {
        if (casting || !Usable()) return false;
        if (CooldownRemaining > 0f) { Reject(false); return false; }
        if (!ComputeDestination(dir, out Vector3 dest, out float dist, out bool grounded)) { Reject(false); return false; }
        if (!stats.TrySpendMana(cost)) { Reject(false); return false; }   // TrySpendMana đã báo ManaInsufficient
        EnsureObjects();
        cooldownEnd = Time.time + cooldown;

        castDir = (dest - BodyCenter()).normalized;
        p0 = BodyCenter();
        destCenter = dest;
        destGrounded = grounded;
        portalInPos = p0 + castDir * entranceDistance;
        exitCenter = dest - castDir * exitBehind;
        casting = true;
        castStart = Time.time;
        stDash = stVanish = stReappear = stEnd = false;

        wings.FrozenForRift = true;
        wasFlying = wings.IsFlying;
        if (!wasFlying) wings.BeginFlight(Vector3.zero);   // dáng lao (nằm ngang) cả khi xuất phát từ mặt đất
        player.SetControllerEnabled(false);
        wings.RiftLevel = 1f;
        wings.RiftFold = wingFoldAmount;
        if (wings.Lightning != null) wings.Lightning.Spike(1f);

        PlacePortal(portalIn, portalInPos);
        portalIn.Mo();
        return true;
    }

    private void PlacePortal(CongKhongGian p, Vector3 center)
    {
        if (p == null) return;
        p.transform.SetPositionAndRotation(center, Quaternion.LookRotation(castDir, Vector3.up));
    }

    private void SetBodyCenter(Vector3 c) { transform.position = c - Vector3.up * bodyCenterHeight; }

    private void FaceCast(float dt)
    {
        Vector3 flat = new Vector3(castDir.x, 0f, castDir.z);
        if (flat.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), 1800f * dt);
    }

    private void UpdateCast()
    {
        float t = Time.time - castStart;
        FaceCast(Time.deltaTime);
        Vector3 vel = Vector3.zero;

        // 0.25s: bắt đầu lao vào cổng vào, bất tử, mất điều khiển
        if (!stDash && t >= timeDash)
        {
            stDash = true;
            stats.GrantInvulnerability(Mathf.Max(0.05f, timeReappear - timeDash));
            player.ControlLocked = true;
            wings.RiftFovBoost = fovBoost;
        }
        if (stDash && !stVanish)
        {
            float dur = Mathf.Max(0.02f, timeVanish - timeDash);
            float u = Mathf.Clamp01((t - timeDash) / dur);
            SetBodyCenter(Vector3.Lerp(p0, portalInPos, u * u));          // tăng tốc vào tâm cổng
            vel = castDir * (Vector3.Distance(p0, portalInPos) / dur * 2f * Mathf.Max(u, 0.2f));
        }
        // 0.40s: biến mất, cổng vào đóng, cổng ra mở, camera lướt
        if (!stVanish && t >= timeVanish)
        {
            stVanish = true;
            SetHidden(true);
            if (portalIn != null) portalIn.Dong();
            if (camOrbit != null) camOrbit.Glide(camOrbit.transform.position, cameraGlideTime);
            SetBodyCenter(exitCenter);
            PlacePortal(portalOut, exitCenter);
            portalOut.Mo();
        }
        if (stVanish && !stReappear) { SetBodyCenter(exitCenter); }
        // 0.70s: lao ra khỏi cổng ra, cánh bung, sóng xung kích
        if (!stReappear && t >= timeReappear)
        {
            stReappear = true;
            SetHidden(false);
            wings.RiftFold = 0f;
            if (wings.Lightning != null) { wings.Lightning.PlayBurst(0.3f); wings.Lightning.Spike(1f); }
            StartShock(exitCenter);
            if (feedback != null && shockShake > 0f) feedback.Shake(shockShake);
        }
        if (stReappear && !stEnd)
        {
            float dur = Mathf.Max(0.02f, timeEnd - timeReappear);
            float u = Mathf.Clamp01((t - timeReappear) / dur);
            float e = 1f - (1f - u) * (1f - u);                            // giảm tốc về điểm đến
            SetBodyCenter(Vector3.Lerp(exitCenter, destCenter, e));
            vel = castDir * (Vector3.Distance(exitCenter, destCenter) / dur * 2f * Mathf.Max(1f - u, 0.2f));
        }
        if (stDash) wings.SetFlightVelocity(vel);
        // 0.90s: kết thúc
        if (!stEnd && t >= timeEnd) { stEnd = true; Finish(false); }
    }

    private void LateUpdate()
    {
        // Vệt vàng xanh sau đầu cánh trong lúc lao (PhongLoiSi tắt vệt khi đứng yên chờ Phá Không, nên bật lại sau nó)
        if (casting && stDash && wings != null && wings.Lightning != null && !stVanish)
            wings.Lightning.SetStreaks(true);
        else if (casting && stReappear && wings != null && wings.Lightning != null)
            wings.Lightning.SetStreaks(true);
    }

    private void SetHidden(bool hidden)
    {
        if (hidden)
        {
            if (hideList == null && animator != null)
            {
                List<Renderer> list = new List<Renderer>();
                foreach (Renderer r in animator.GetComponentsInChildren<Renderer>(true))
                {
                    if (wings != null && wings.Visual != null && r.transform.IsChildOf(wings.Visual.transform)) continue;
                    list.Add(r);
                }
                hideList = list.ToArray();
                hideWas = new bool[hideList.Length];
            }
            if (hideList != null)
                for (int i = 0; i < hideList.Length; i++) { if (hideList[i] == null) continue; hideWas[i] = hideList[i].enabled; hideList[i].enabled = false; }
        }
        else if (hideList != null)
        {
            for (int i = 0; i < hideList.Length; i++) if (hideList[i] != null) hideList[i].enabled = hideWas[i];
        }
        if (wings != null)
        {
            if (wings.Visual != null) wings.Visual.Hidden = hidden;
            if (wings.Lightning != null) wings.Lightning.Suppressed = hidden;
        }
    }

    /// <summary>Kết thúc diễn biến: trả điều khiển. aborted = bị hủy giữa chừng (chết...).</summary>
    private void Finish(bool aborted)
    {
        casting = false;
        SetHidden(false);
        if (wings != null)
        {
            wings.RiftFold = 0f;
            wings.RiftLevel = 0f;
            wings.RiftFovBoost = 0f;
            wings.FrozenForRift = false;
        }
        if (portalIn != null) portalIn.Dong();
        if (portalOut != null) portalOut.Dong();
        if (player != null && stats != null && stats.IsAlive)
        {
            if (stDash) player.ControlLocked = false;
            player.SetControllerEnabled(true);
        }
        if (aborted) { if (wings != null && !wasFlying && wings.IsFlying) wings.EndFlight(false); CastFinished?.Invoke(); return; }

        // Đã tới điểm đến
        SetBodyCenter(destCenter);
        if (wings != null)
        {
            wings.ResetFallTracking();
            if (destGrounded)
            {
                // Ra trên đất: đứng yên
                if (wings.IsFlying) wings.EndFlight(true);
                else { player.SetVerticalVelocity(-2f); player.SetHorizontalVelocity(Vector3.zero); }
            }
            else
            {
                // Ra giữa trời: tiếp tục bay với vận tốc bay thường theo hướng cũ
                Vector3 v = castDir * wings.CruiseSpeed;
                if (wings.IsFlying) wings.SetFlightVelocity(v);
                else wings.BeginFlight(v);
            }
        }
        CastFinished?.Invoke();
    }

    // ================= Sóng xung kích =================

    private void StartShock(Vector3 center)
    {
        shockStart = Time.time;
        shockCenter = center;
        shockHit.Clear();
        shockQuad.gameObject.SetActive(true);
        flashQuad.gameObject.SetActive(true);
        flashLightObj.enabled = true;
        shockQuad.position = center;
        shockQuad.localScale = Vector3.one * (shockRadius * 2f / 0.92f);
        flashQuad.position = center;
        flashQuad.localScale = Vector3.one * 7f;
    }

    private void UpdateShock()
    {
        if (shockQuad == null || !shockQuad.gameObject.activeSelf) return;
        float el = Time.time - shockStart;
        float p = el / Mathf.Max(0.01f, shockTime);
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 away = shockCenter - cam.transform.position;
            if (away.sqrMagnitude > 0.01f)
            {
                Quaternion face = Quaternion.LookRotation(away, Vector3.up);
                shockQuad.rotation = face;
                flashQuad.rotation = face;
            }
        }
        if (shockMat != null) { shockMat.SetFloat(TId, Mathf.Clamp01(p)); shockMat.SetFloat(AlphaId, 1f); }
        // Chớp sáng ngắn (0.12s)
        float f = Mathf.Clamp01(1f - el / 0.12f);
        if (flashMat != null) flashMat.SetFloat(AlphaId, f * 0.55f);
        flashLightObj.intensity = flashLight * f;
        flashLightObj.transform.position = shockCenter;
        if (flashQuad.gameObject.activeSelf && f <= 0f) { flashQuad.gameObject.SetActive(false); flashLightObj.enabled = false; }

        // Sát thương và choáng: mỗi kẻ địch trúng một lần khi mép sóng quét tới
        float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(p), 2f);
        float reach = shockRadius * eased;
        if (p <= 1.05f)
        {
            Damageable[] all = Damageable.All.ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                Damageable d = all[i];
                if (d == null || !d.IsAlive || shockHit.Contains(d)) continue;
                Vector3 pos = d.AimPoint;
                Vector3 off = pos - shockCenter;
                if (off.magnitude > reach) continue;
                shockHit.Add(d);
                DamageInfo info = new DamageInfo { amount = shockDamage, point = pos, direction = off.sqrMagnitude > 1e-4f ? off.normalized : castDir, melee = false };
                d.TakeDamage(info);
                EnemyBase eb = d.GetComponentInParent<EnemyBase>();
                if (eb != null) eb.Stun(shockStun);
            }
        }
        if (p >= 1.15f) shockQuad.gameObject.SetActive(false);
    }

    /// <summary>Số kẻ địch đã trúng sóng ở lần gần nhất (kiểm thử).</summary>
    public int LastShockHitCount => shockHit.Count;

    /// <summary>Danh sách kẻ địch đã trúng sóng ở lần gần nhất (kiểm thử).</summary>
    public IEnumerable<Damageable> LastShockHits => shockHit;
}
