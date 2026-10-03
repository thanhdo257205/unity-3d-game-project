using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Các đội hình có thể chọn bằng phím 1/2/3.</summary>
public enum FormationType
{
    /// <summary>Phím 1: Thủ Kiếm - Quạt.</summary>
    Fan = 0,
    /// <summary>Phím 2: Hồi Kiếm - Vòng.</summary>
    Ring = 1,
    /// <summary>Phím 3: Phá Kiếm - Mũi tên.</summary>
    Arrow = 2
}

/// <summary>
/// Đàn phi kiếm của người chơi: sinh ra các thanh kiếm, xếp chúng theo đội hình, và điều khiển việc phóng kiếm.
/// Phím 1/2/3 đổi đội hình Quạt/Vòng/Mũi tên. Chuột trái phóng một kiếm về tâm ngắm (giữ = bắn liên tiếp).
/// Phím 3 lao cả đàn vào mục tiêu gần tâm ngắm nhất rồi thu về. Phím R gọi toàn bộ kiếm về.
/// Đổi "Số kiếm" trong Inspector ngay lúc đang Play để thấy đội hình tự giãn ra.
/// </summary>
public class SwordSwarm : MonoBehaviour
{
    private const int MaxSwords = 60;

    [Header("Tham chiếu")]
    [Tooltip("Prefab phi kiếm (ThanhTrucKiem). Phải có script FlyingSword ở gốc.")]
    [SerializeField] private GameObject swordPrefab;

    [Tooltip("Nhân vật mà đàn kiếm bám theo (kéo đối tượng NhanVat vào).")]
    [SerializeField] private Transform owner;

    [Tooltip("Script điều khiển nhân vật, để quay mặt theo hướng camera lúc bắn. Có thể để trống.")]
    [SerializeField] private PlayerController player;

    [Tooltip("Camera dùng để ngắm. Để trống thì dùng camera chính.")]
    [SerializeField] private Camera aimCamera;

    [Tooltip("Dừng hình và rung camera khi kiếm trúng mục tiêu. Có thể để trống thì không có hiệu ứng này.")]
    [SerializeField] private HitFeedback hitFeedback;

    [Header("Số kiếm")]
    [Tooltip("Số phi kiếm đang dùng. Đổi lúc đang Play (ví dụ 3 → 9 → 24) để xem đội hình tự giãn ra. Sau này sẽ bị giới hạn bởi Thần thức.")]
    [SerializeField, Range(0, MaxSwords)] private int swordCount = 9;

    [Header("Đội hình")]
    [Tooltip("Đội hình lúc bắt đầu.")]
    [SerializeField] private FormationType startFormation = FormationType.Fan;

    [Tooltip("Độ cao điểm neo của đội hình so với chân nhân vật (mét). 1.1 là khoảng ngực.")]
    [SerializeField] private float anchorHeight = 1.1f;

    [Tooltip("Thời gian kiếm bay tới chỗ mới khi đổi đội hình (giây). 0.4 cho cảm giác nhanh nhưng vẫn nhìn thấy kiếm bay.")]
    [SerializeField, Min(0.05f)] private float formationSettleTime = 0.4f;

    [Tooltip("Độ 'nảy' của lò xo. 1 = vào vị trí êm không nảy, nhỏ hơn 1 (ví dụ 0.6) = nảy nhẹ trước khi yên vị.")]
    [SerializeField, Range(0.3f, 1.5f)] private float dampingRatio = 0.8f;

    [Tooltip("Tốc độ tối đa của kiếm khi bay về đội hình (mét/giây). Chặn để kiếm không bay quá nhanh khi nhân vật chạy xa.")]
    [SerializeField, Min(1f)] private float maxSwordSpeed = 70f;

    [SerializeField] private FanFormation fan = new FanFormation();
    [SerializeField] private RingFormation ring = new RingFormation();
    [SerializeField] private ArrowFormation arrow = new ArrowFormation();

    [Header("Bắn lẻ (chuột trái)")]
    [Tooltip("Tốc độ kiếm lao đi khi bắn lẻ (mét/giây). Số lớn = vút nhanh hơn.")]
    [SerializeField, Min(1f)] private float shotSpeed = 80f;

    [Tooltip("Thời gian giữa hai phát khi giữ chuột trái (giây).")]
    [SerializeField, Min(0.02f)] private float fireInterval = 0.12f;

    [Tooltip("Sát thương mỗi nhát chém khi bắn lẻ.")]
    [SerializeField, Min(0f)] private float strikeDamage = 14f;

    [Tooltip("Kiếm bay thêm bao xa sau điểm ngắm rồi mới quay về (mét).")]
    [SerializeField, Min(0f)] private float shotOvershoot = 0.5f;

    [Tooltip("Thời gian kiếm bay về đội hình sau khi bắn (giây).")]
    [SerializeField, Min(0.1f)] private float shotReturnTime = 0.6f;

    [Tooltip("Tầm ngắm tối đa (mét). Nhìn ra xa hơn thì kiếm bay tới điểm ở khoảng cách này.")]
    [SerializeField, Min(1f)] private float aimRange = 60f;

    [Tooltip("Góc hỗ trợ ngắm (độ): hình nộm nằm trong góc này quanh tâm ngắm thì kiếm tự nhắm vào giữa người nó. Tâm ngắm sẽ đỏ.")]
    [SerializeField, Range(0f, 15f)] private float aimAssistAngle = 6f;

    [Tooltip("Sau lần bắn cuối, nhân vật còn quay mặt theo camera thêm bao lâu (giây).")]
    [SerializeField, Min(0f)] private float aimHoldTime = 0.8f;

    [Header("Phá Kiếm - Mũi tên (phím 3)")]
    [Tooltip("Bấm 3 khi chưa ở đội hình Mũi tên: kiếm xếp hàng xong thì lao luôn. Tắt đi thì phải bấm 3 lần nữa mới lao.")]
    [SerializeField] private bool volleyOnKey3 = true;

    [Tooltip("Tốc độ lao của loạt kiếm (mét/giây). Thiết kế gốc là 30, đã tăng lên cho cảm giác nhanh hơn.")]
    [SerializeField, Min(1f)] private float volleySpeed = 55f;

    [Tooltip("Khoảng cách thời gian giữa hai kiếm liên tiếp xuất phát (giây).")]
    [SerializeField, Min(0f)] private float volleyInterval = 0.08f;

    [Tooltip("Thời gian thu kiếm về sau loạt lao (giây). Trong lúc này lưng nhân vật trống.")]
    [SerializeField, Min(0.1f)] private float volleyReturnTime = 1.5f;

    [Tooltip("Sát thương mỗi kiếm trong loạt lao.")]
    [SerializeField, Min(0f)] private float volleyDamage = 14f;

    [Tooltip("Góc tìm mục tiêu quanh tâm ngắm (độ). Mục tiêu gần tâm ngắm nhất trong góc này sẽ bị nhắm.")]
    [SerializeField, Range(1f, 60f)] private float volleyLockAngle = 25f;

    [Tooltip("Tầm tìm mục tiêu cho loạt lao (mét).")]
    [SerializeField, Min(1f)] private float volleyRange = 50f;

    [Tooltip("Kiếm xuyên qua mục tiêu thêm bao xa rồi mới quay về (mét).")]
    [SerializeField, Min(0f)] private float volleyOvershoot = 2.5f;

    [Header("Va chạm và thu kiếm")]
    [Tooltip("Sát thương mỗi lần kiếm trong đội hình Vòng chạm vào địch.")]
    [SerializeField, Min(0f)] private float ringContactDamage = 6f;

    [Tooltip("Mỗi thanh kiếm Vòng chỉ chém lại cùng một mục tiêu sau ít nhất bấy nhiêu giây.")]
    [SerializeField, Min(0.05f)] private float contactHitInterval = 0.35f;

    [Tooltip("Thời gian kiếm bay về khi bấm R gọi kiếm về (giây).")]
    [SerializeField, Min(0.1f)] private float recallTime = 0.5f;

    [Tooltip("Phần vận tốc giữ lại khi kiếm bắt đầu quay về (0 = dừng khựng rồi bay về, 1 = giữ nguyên đà). Số nhỏ cho kiếm quay đầu gọn.")]
    [SerializeField, Range(0f, 1f)] private float returnVelocityKeep = 0.15f;

    private readonly List<FlyingSword> swords = new List<FlyingSword>();
    private readonly List<FlyingSword> temp = new List<FlyingSword>();
    private readonly RaycastHit[] rayHits = new RaycastHit[32];

    private FormationType current;
    private float nextFireTime;
    private float aimUntil;
    private float pendingVolleyTime = -1f;
    private int nextShooter;

    // ----- Giá trị cho FlyingSword đọc -----
    public float FormationSettleTime => formationSettleTime;
    public float DampingRatio => dampingRatio;
    public float MaxSwordSpeed => maxSwordSpeed;
    public float ReturnVelocityKeep => returnVelocityKeep;
    public float RingContactDamage => ringContactDamage;
    public float ContactHitInterval => contactHitInterval;

    /// <summary>Đội hình đang dùng.</summary>
    public FormationType CurrentFormation => current;

    /// <summary>Đội hình đang dùng ở dạng giao diện IFormation.</summary>
    public IFormation ActiveFormation => GetFormation(current);

    /// <summary>Số kiếm mong muốn (có thể gán lúc đang chạy).</summary>
    public int SwordCount
    {
        get => swordCount;
        set => swordCount = Mathf.Clamp(value, 0, MaxSwords);
    }

    /// <summary>Số kiếm đang tồn tại thực tế.</summary>
    public int ActualSwordCount => swords.Count;

    /// <summary>Danh sách kiếm (chỉ đọc).</summary>
    public IReadOnlyList<FlyingSword> Swords => swords;

    /// <summary>Điểm đang ngắm trong thế giới (nơi tia từ giữa màn hình chạm vào).</summary>
    public Vector3 AimPoint { get; private set; }

    /// <summary>Mục tiêu đang được hỗ trợ ngắm (null nếu không có). Tâm ngắm UI dùng để đổi màu.</summary>
    public Damageable AimTarget { get; private set; }

    private void Reset()
    {
        owner = transform;
    }

    private void OnValidate()
    {
        swordCount = Mathf.Clamp(swordCount, 0, MaxSwords);
    }

    private void Awake()
    {
        current = startFormation;
        if (owner == null) owner = transform;
    }

    private void Update()
    {
        HandleInput();
        UpdateAim();

        if (pendingVolleyTime > 0f && Time.time >= pendingVolleyTime)
        {
            pendingVolleyTime = -1f;
            if (current == FormationType.Arrow) FireVolley();
        }

        if (player != null) player.FaceCamera = Time.time < aimUntil;
    }

    private void LateUpdate()
    {
        SyncSwordCount();
        if (swords.Count == 0 || owner == null) return;

        FormationContext ctx;
        ctx.origin = owner.position + Vector3.up * anchorHeight;
        ctx.body = Quaternion.Euler(0f, owner.eulerAngles.y, 0f);
        ctx.time = Time.time;

        IFormation formation = ActiveFormation;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        int count = swords.Count;
        for (int i = 0; i < count; i++)
        {
            SwordSlot slot = formation.GetSlot(i, count, ctx);
            swords[i].Tick(slot, dt);
        }
    }

    // ================= Đầu vào =================

    private void HandleInput()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) SetFormation(FormationType.Fan);
            if (kb.digit2Key.wasPressedThisFrame) SetFormation(FormationType.Ring);
            if (kb.digit3Key.wasPressedThisFrame) OnArrowKey();
            if (kb.rKey.wasPressedThisFrame) RecallAll();
        }

        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        if (cursorLocked && mouse != null && mouse.leftButton.isPressed)
        {
            aimUntil = Mathf.Max(aimUntil, Time.time + aimHoldTime);
            if (Time.time >= nextFireTime && FireSingle())
                nextFireTime = Time.time + fireInterval;
        }
    }

    /// <summary>Phím 3: vào đội hình Mũi tên; nếu đã ở đó (hoặc bật "lao luôn") thì lao cả đàn.</summary>
    private void OnArrowKey()
    {
        if (current != FormationType.Arrow)
        {
            SetFormation(FormationType.Arrow);
            if (volleyOnKey3) pendingVolleyTime = Time.time + formationSettleTime * 0.75f;
        }
        else
        {
            FireVolley();
        }
    }

    // ================= Công khai (UI, kiểm thử) =================

    /// <summary>Đổi đội hình. Kiếm bay sang chỗ mới trong khoảng "Thời gian đổi đội hình".</summary>
    public void SetFormation(FormationType type)
    {
        pendingVolleyTime = -1f;
        if (type == current) return;
        current = type;
        for (int i = 0; i < swords.Count; i++) swords[i].OnFormationChanged();
    }

    /// <summary>FlyingSword gọi khi một nhát chém (bắn lẻ hoặc loạt Mũi tên) trúng mục tiêu: dừng hình và rung camera.</summary>
    public void NotifyStrikeHit(Damageable target)
    {
        if (hitFeedback != null) hitFeedback.OnHit(!target.IsAlive);
    }

    /// <summary>Bấm R: gọi toàn bộ kiếm về đội hình, hủy mọi đòn đang hẹn giờ.</summary>
    public void RecallAll()
    {
        pendingVolleyTime = -1f;
        for (int i = 0; i < swords.Count; i++)
        {
            FlyingSword s = swords[i];
            if (s.Mode == SwordMode.Formation && !s.HasScheduledStrike) continue;
            s.CancelAndBeginReturn(recallTime);
        }
    }

    /// <summary>Phóng một thanh kiếm về tâm ngắm rồi tự quay về. Trả về false nếu không có kiếm rảnh.</summary>
    public bool FireSingle()
    {
        FlyingSword s = PickShooter();
        if (s == null) return false;

        Vector3 end = AimTarget != null ? AimTarget.AimPoint : AimPoint;
        Vector3 to = end - s.transform.position;
        Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : s.transform.forward;
        s.LaunchStrike(end + dir * shotOvershoot, shotSpeed, strikeDamage, shotReturnTime);

        aimUntil = Mathf.Max(aimUntil, Time.time + aimHoldTime);
        return true;
    }

    /// <summary>Lao cả đàn (các kiếm đang rảnh) vào mục tiêu gần tâm ngắm nhất, mỗi kiếm cách nhau một chút. Sau đó thu về.</summary>
    public bool FireVolley()
    {
        temp.Clear();
        for (int i = 0; i < swords.Count; i++)
        {
            FlyingSword s = swords[i];
            if (s.Mode == SwordMode.Formation && !s.HasScheduledStrike) temp.Add(s);
        }
        if (temp.Count == 0) return false;

        Damageable target = FindTarget(volleyLockAngle, volleyRange);
        Vector3 aim = target != null ? target.AimPoint : AimPoint;

        for (int k = 0; k < temp.Count; k++)
            temp[k].ScheduleStrike(Time.time + k * volleyInterval, aim, volleyOvershoot, volleySpeed, volleyDamage, volleyReturnTime);

        aimUntil = Mathf.Max(aimUntil, Time.time + temp.Count * volleyInterval + aimHoldTime);
        return true;
    }

    // ================= Nội bộ =================

    private IFormation GetFormation(FormationType type)
    {
        switch (type)
        {
            case FormationType.Ring: return ring;
            case FormationType.Arrow: return arrow;
            default: return fan;
        }
    }

    /// <summary>Chọn thanh kiếm rảnh tiếp theo để bắn, luân phiên đều các kiếm.</summary>
    private FlyingSword PickShooter()
    {
        int n = swords.Count;
        for (int k = 0; k < n; k++)
        {
            int idx = (nextShooter + k) % n;
            FlyingSword s = swords[idx];
            if (s.Mode == SwordMode.Formation && !s.HasScheduledStrike)
            {
                nextShooter = (idx + 1) % n;
                return s;
            }
        }
        return null;
    }

    private Camera GetCamera()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        return aimCamera;
    }

    /// <summary>Bắn tia từ giữa màn hình để biết điểm ngắm, và tìm mục tiêu được hỗ trợ ngắm.</summary>
    private void UpdateAim()
    {
        Camera cam = GetCamera();
        if (cam == null) return;

        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        float best = aimRange;
        Vector3 point = origin + dir * aimRange;

        int n = Physics.RaycastNonAlloc(origin, dir, rayHits, aimRange, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayHits[i];
            if (owner != null && h.collider.transform.IsChildOf(owner)) continue; // bỏ qua chính nhân vật
            if (h.distance < best)
            {
                best = h.distance;
                point = h.point;
            }
        }

        AimPoint = point;
        AimTarget = FindTarget(aimAssistAngle, aimRange);
    }

    /// <summary>Tìm Damageable còn sống nằm gần tâm ngắm nhất, trong góc và tầm cho phép.</summary>
    private Damageable FindTarget(float maxAngle, float range)
    {
        Camera cam = GetCamera();
        if (cam == null) return null;

        Vector3 camPos = cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        Damageable best = null;
        float bestAngle = maxAngle;

        List<Damageable> all = Damageable.All;
        for (int i = 0; i < all.Count; i++)
        {
            Damageable d = all[i];
            if (!d.IsAlive) continue;
            Vector3 v = d.AimPoint - camPos;
            float dist = v.magnitude;
            if (dist > range || dist < 0.01f) continue;
            float angle = Vector3.Angle(fwd, v);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                best = d;
            }
        }
        return best;
    }

    /// <summary>Thêm/bớt kiếm cho khớp "Số kiếm". Kiếm mới sinh ra ở người nhân vật rồi bay ra chỗ của nó.</summary>
    private void SyncSwordCount()
    {
        if (swordPrefab == null || owner == null) return;

        while (swords.Count < swordCount)
        {
            Vector3 spawn = owner.position + Vector3.up * anchorHeight;
            GameObject go = Instantiate(swordPrefab, spawn, Quaternion.identity, transform);
            go.name = "Kiem_" + swords.Count;
            FlyingSword s = go.GetComponent<FlyingSword>();
            if (s == null)
            {
                Debug.LogError("Prefab phi kiếm thiếu script FlyingSword ở gốc.", swordPrefab);
                Destroy(go);
                swordCount = swords.Count;
                return;
            }
            s.Init(this, swords.Count);
            swords.Add(s);
        }

        while (swords.Count > swordCount)
        {
            FlyingSword last = swords[swords.Count - 1];
            swords.RemoveAt(swords.Count - 1);
            if (last != null) Destroy(last.gameObject);
        }
        if (nextShooter >= swords.Count) nextShooter = 0;
    }
}
