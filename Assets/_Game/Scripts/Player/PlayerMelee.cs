using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Đánh cận chiến bằng chuột trái, hai kiểu tùy ô đang chọn trên thanh ô đồ:
/// - Tay không (ô trống): quyền cước Dam1 → Dam2 → Da.
/// - Cầm kiếm (ô có Thanh Trúc Kiếm, kiếm đã tới tay): chém Chem1 → Chem2 → Chem3.
/// Bấm tiếp ở nửa sau mỗi đòn thì nối đòn sau; không bấm thì về đứng yên.
/// Khi đánh: nhân vật quay mặt theo camera, bước nhẹ tới trước, không đi bằng WASD (nhưng nhảy thì hủy được đòn).
/// Đòn trúng thì gây sát thương cho mọi mục tiêu trong hình quạt phía trước; đòn cuối còn đẩy lùi mục tiêu.
/// Mọi con số chỉnh trong Inspector. Không tốn pháp lực.
/// </summary>
public class PlayerMelee : MonoBehaviour
{
    /// <summary>Dữ liệu một đòn trong chuỗi combo.</summary>
    [Serializable]
    public class Step
    {
        [Tooltip("Tên trạng thái trong Animator (Dam1, Dam2, Da, Chem1, Chem2, Chem3).")]
        public string stateName = "Dam1";

        [Tooltip("Sát thương của đòn này.")]
        [Min(0f)] public float damage = 10f;

        [Header("Mốc trong clip (chỉ phụ thuộc file anim — đổi clip thì chỉ sửa ba số này)")]
        [Tooltip("Giây trong clip mà đòn bắt đầu phát (bỏ đoạn dạo đầu của clip cho ra đòn nhanh).")]
        [Min(0f)] public float clipStart = 0.1f;

        [Tooltip("Giây trong clip tại khoảnh khắc đòn trúng (nắm đấm/chân/lưỡi kiếm quét qua phía trước).")]
        [Min(0f)] public float clipHit = 0.3f;

        [Tooltip("Giây trong clip mà đòn kết thúc (hết đà đánh, bấm tiếp là nối được đòn sau).")]
        [Min(0.05f)] public float clipEnd = 0.7f;

        [Header("Thời gian thật (giây tính từ lúc bấm — giữ nguyên khi đổi clip)")]
        [Tooltip("Tổng thời gian thật của đòn, từ lúc bấm tới lúc nối được đòn sau (giây). Đặt 0 = kiểu cũ: tính từ 'Tốc độ phát anim' bên dưới (dùng cho quyền cước).")]
        [Min(0f)] public float duration = 0f;

        [Tooltip("Thời gian thật từ lúc bấm tới khoảnh khắc trúng đòn (giây). Chỉ dùng khi 'Tổng thời gian thật' > 0. Anim tự được tăng/giảm tốc để mốc 'trúng đòn' của clip rơi đúng giây này.")]
        [Min(0f)] public float hitTime = 0.25f;

        [Tooltip("Tốc độ phát anim (1 = bình thường, 1.5 = nhanh gấp rưỡi). Chỉ dùng khi 'Tổng thời gian thật' = 0 (kiểu cũ: thời gian thật = (kết thúc - bắt đầu) / tốc độ).")]
        [Min(0.1f)] public float speed = 1.4f;

        [Tooltip("Quãng bước tới trước khi ra đòn (mét).")]
        [Min(0f)] public float lungeDistance = 0.3f;

        [Tooltip("Đẩy lùi mục tiêu trúng đòn (mét). 0 = không đẩy.")]
        [Min(0f)] public float knockback = 0f;

        [Header("Cầm kiếm")]
        [Tooltip("Vệt chém bắt đầu hiện trước khoảnh khắc trúng đòn bao nhiêu giây thật.")]
        [Min(0f)] public float trailBeforeHit = 0.12f;

        [Tooltip("Vệt chém ngừng vẽ thêm sau khoảnh khắc trúng đòn bao nhiêu giây thật.")]
        [Min(0f)] public float trailAfterHit = 0.08f;

        [Tooltip("Chỉ dùng khi cầm kiếm: bật thì nhát này cầm kiếm theo tư thế riêng bên dưới thay vì tư thế 'Cầm chém' chung (dùng cho nhát đâm, nơi hai tay xa nhau).")]
        public bool useGripOverride = false;

        [Tooltip("Vị trí tâm chuôi kiếm so với xương bàn tay phải (mét) cho nhát này.")]
        public Vector3 gripPosition = new Vector3(0f, 0.08f, 0.03f);

        [Tooltip("Góc xoay của kiếm so với xương bàn tay phải (độ) cho nhát này.")]
        public Vector3 gripEuler = Vector3.zero;
    }

    [Header("Tham chiếu")]
    [Tooltip("Script điều khiển nhân vật (đối tượng NhanVat).")]
    [SerializeField] private PlayerController player;

    [Tooltip("Tài nguyên người chơi (để biết còn sống không).")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Animator của mô hình nhân vật. Để trống thì tự tìm trong các đối tượng con.")]
    [SerializeField] private Animator animator;

    [Tooltip("Hiệu ứng rung camera và dừng hình khi trúng đòn (đối tượng BayKiem). Có thể để trống.")]
    [SerializeField] private HitFeedback hitFeedback;

    [Tooltip("Thanh ô đồ: ô đang chọn quyết định đánh tay không hay chém kiếm. Để trống thì tự tìm.")]
    [SerializeField] private Hotbar hotbar;

    [Tooltip("Vũ khí cầm tay: biết kiếm đã tới tay chưa, và bật/tắt vệt chém. Để trống thì tự tìm.")]
    [SerializeField] private HeldWeapon weapon;

    [Tooltip("Phong Lôi Sí: đang bay thì vẫn chém được, động tác tay phát ở lớp thân trên (trạng thái '<tên đòn>_Bay') để thân dưới giữ dáng bay. Để trống thì tự tìm.")]
    [SerializeField] private PhongLoiSi wings;

    [Tooltip("Tên lớp Animator chỉ điều khiển thân trên (chém khi đang bay).")]
    [SerializeField] private string upperLayerName = "UpperBody";

    [Tooltip("Tên trạng thái rỗng của lớp thân trên (để trả quyền lại cho dáng bay sau khi chém).")]
    [SerializeField] private string upperEmptyState = "Trong";

    [Header("Tay không (ô trống)")]
    [Tooltip("Ba đòn theo thứ tự: Dam1 (10 sát thương), Dam2 (12), Da (20, đẩy lùi 1.5m).")]
    [FormerlySerializedAs("steps")]
    [SerializeField] private Step[] unarmedSteps =
    {
        // Số giây clipStart/clipHit/clipEnd đo từ chuyển động tay chân thật của từng clip Mixamo
        new Step { stateName = "Dam1", damage = 10f, clipStart = 0.30f, clipHit = 0.45f, clipEnd = 0.72f, speed = 1.2f, lungeDistance = 0.3f },
        new Step { stateName = "Dam2", damage = 12f, clipStart = 0.65f, clipHit = 1.00f, clipEnd = 1.30f, speed = 1.3f, lungeDistance = 0.3f },
        new Step { stateName = "Da",   damage = 20f, clipStart = 0.20f, clipHit = 0.60f, clipEnd = 1.05f, speed = 1.2f, lungeDistance = 0.4f, knockback = 1.5f },
    };

    [Tooltip("Tầm với của đòn đánh tay không phía trước mặt (mét).")]
    [SerializeField, Min(0.1f)] private float range = 1.6f;

    [Tooltip("Góc hình quạt trước mặt khi đánh tay không (độ). 90 = lệch trái/phải mỗi bên 45 độ.")]
    [SerializeField, Range(10f, 360f)] private float arc = 90f;

    [Header("Cầm kiếm")]
    [Tooltip("Ba nhát chém: Chem1 (18 sát thương), Chem2 (20), Chem3 (32, đẩy lùi 2m). Thời gian thật mỗi nhát 0.5 / 0.6 / 0.9 giây, trúng đòn sau 0.25 / 0.38 / 0.54 giây. Đổi clip chém: chỉ sửa 3 'Mốc trong clip' của từng nhát, không đụng sát thương và thời gian thật.")]
    [SerializeField] private Step[] swordSteps =
    {
        // Đo từ chuyển động tay phải thật của 3 clip Great Sword Slash / Thrust Slash (Mixamo)
        new Step { stateName = "Chem1", damage = 18f, clipStart = 0.30f, clipHit = 0.62f, clipEnd = 0.92f, duration = 0.5f, hitTime = 0.25f, lungeDistance = 0.3f },
        new Step { stateName = "Chem2", damage = 20f, clipStart = 0.30f, clipHit = 0.73f, clipEnd = 1.00f, duration = 0.6f, hitTime = 0.38f, lungeDistance = 0.3f },
        new Step { stateName = "Chem3", damage = 32f, clipStart = 0.20f, clipHit = 0.76f, clipEnd = 1.15f, duration = 0.9f, hitTime = 0.54f, lungeDistance = 0.55f, knockback = 2f, useGripOverride = true, gripPosition = new Vector3(0f, 0.08f, 0.03f), gripEuler = new Vector3(2.86f, 59.33f, 0f) },
    };

    [Tooltip("Tầm chém của kiếm phía trước mặt (mét).")]
    [SerializeField, Min(0.1f)] private float swordRange = 2.2f;

    [Tooltip("Góc hình quạt trước mặt khi chém kiếm (độ). 120 = lệch trái/phải mỗi bên 60 độ.")]
    [SerializeField, Range(10f, 360f)] private float swordArc = 120f;

    [Header("Nối đòn")]
    [Tooltip("Từ lúc nào (tỉ lệ 0-1 của đòn hiện tại) thì bấm tiếp mới được tính là nối đòn. 0.5 = nửa sau của đòn.")]
    [SerializeField, Range(0f, 1f)] private float chainWindowStart = 0.5f;

    [Tooltip("Thời gian chuyển mượt giữa hai đòn (giây).")]
    [SerializeField, Min(0f)] private float blendTime = 0.06f;

    [Tooltip("Thời gian chuyển mượt về đứng yên sau đòn cuối (giây).")]
    [SerializeField, Min(0f)] private float exitBlendTime = 0.15f;

    [Tooltip("Tên trạng thái Animator để quay về khi hết combo.")]
    [SerializeField] private string idleState = "Locomotion";

    [Tooltip("Thời gian ân hạn (giây) coi như vẫn đang đứng trên đất, tránh hủy đòn oan khi bước xuống bậc thang.")]
    [SerializeField, Min(0f)] private float groundGrace = 0.15f;

    private static readonly int AtkSpeedId = Animator.StringToHash("AtkSpeed");
    private float lastGroundedTime;

    private Step[] activeSteps;        // chuỗi đòn đang dùng (tay không hoặc kiếm)
    private bool usingSword;
    private bool trailOn;
    private bool queuedPress;          // lệnh đánh gửi từ code (kiểm thử)
    private int stepIndex = -1;        // -1 = không đánh
    private float stepElapsed;         // thời gian thật đã trôi qua trong đòn hiện tại
    private float stepDuration;        // thời gian thật của đòn hiện tại
    private float hitDelay;            // thời gian thật từ lúc bắt đầu đòn tới khi trúng
    private float speedAfterHit;       // tốc độ phát anim sau khoảnh khắc trúng (khi dùng thời gian thật)
    private bool hitDone;
    private bool chainQueued;
    private float cooldownUntil;

    /// <summary>Đang trong chuỗi đòn (quyền cước hoặc chém kiếm) không.</summary>
    public bool IsAttacking => stepIndex >= 0;

    /// <summary>Đòn đang ra được bắt đầu lúc đang bay (phát ở lớp thân trên, không khóa đi, không bước tới).</summary>
    public bool AttackInFlight => IsAttacking && attackInFlight;

    private bool attackInFlight;

    /// <summary>Đòn đang ra là chém kiếm (false = tay không).</summary>
    public bool IsSwordAttack => IsAttacking && usingSword;

    /// <summary>Chỉ số đòn đang ra (0-2), -1 nếu không đánh.</summary>
    public int StepIndex => stepIndex;

    /// <summary>Báo mỗi lần bắt đầu một đòn (tham số: chỉ số đòn 0-2).</summary>
    public event Action<int> StepStarted;

    /// <summary>Báo đúng khoảnh khắc đòn trúng (lúc gây sát thương). Dùng cho kiểm thử và hiệu ứng.</summary>
    public event Action<int> HitMoment;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (hotbar == null) hotbar = GetComponent<Hotbar>();
        if (weapon == null) weapon = GetComponent<HeldWeapon>();
        if (wings == null) wings = GetComponent<PhongLoiSi>();
    }

    private void OnEnable()
    {
        if (player != null) player.Jumped += OnJumped;
        if (stats != null) stats.Died += OnDied;
        if (hotbar != null) hotbar.SelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        if (player != null) player.Jumped -= OnJumped;
        if (stats != null) stats.Died -= OnDied;
        if (hotbar != null) hotbar.SelectionChanged -= OnSelectionChanged;
        EndAttack(false);
    }

    private void OnJumped() { if (IsAttacking) EndAttack(false); }

    private void OnDied() { if (IsAttacking) EndAttack(false); }

    // Đổi ô giữa chừng thì hủy đòn đang ra
    private void OnSelectionChanged(int index) { if (IsAttacking) EndAttack(false); }

    /// <summary>Hủy chuỗi đòn đang ra (cất cánh giữa chừng một đòn dưới đất...).</summary>
    public void CancelAttack() { if (IsAttacking) EndAttack(false); }

    /// <summary>Ra lệnh đánh như vừa bấm chuột trái (dùng để kiểm thử qua code).</summary>
    public void PressAttack() { queuedPress = true; }

    private void Update()
    {
        bool pressed = PressedThisFrame() || queuedPress;
        queuedPress = false;

        if (stats != null && !stats.IsAlive) return;
        if (player != null && player.ControlLocked) return;

        if (player.IsGrounded) lastGroundedTime = Time.time;
        bool onGround = Time.time - lastGroundedTime <= groundGrace;   // chịu được vài khung hình "hụt đất" khi đi xuống bậc
        if (wings != null && wings.IsFlying) onGround = true;           // đang bay thì vẫn chém được

        if (!IsAttacking)
        {
            bool sword;
            if (pressed && Time.time >= cooldownUntil && onGround && CanAttack(out sword)) BeginStep(0, false, sword);
            return;
        }

        // Rời khỏi mặt đất giữa chừng (rơi xuống vực...) thì hủy đòn
        if (!onGround) { EndAttack(false); return; }

        stepElapsed += Time.deltaTime;
        Step current = activeSteps[stepIndex];

        if (usingSword) UpdateSlashTrail(current);

        if (!hitDone && stepElapsed >= hitDelay)
        {
            hitDone = true;
            if (animator != null) animator.SetFloat(AtkSpeedId, speedAfterHit);
            DoHit(current);
        }

        // Chỉ nhận lệnh nối đòn ở nửa sau của đòn
        if (pressed && stepIndex < activeSteps.Length - 1 && stepElapsed >= stepDuration * chainWindowStart)
            chainQueued = true;

        if (stepElapsed >= stepDuration)
        {
            if (chainQueued && stepIndex < activeSteps.Length - 1) BeginStep(stepIndex + 1, true, usingSword);
            else EndAttack(true);
        }
    }

    /// <summary>
    /// Có được bắt đầu một chuỗi đòn không, và là kiểu nào. Ô trống: tay không. Ô vũ khí: chỉ khi kiếm đã tới tay.
    /// Ô đan dược/pháp bảo/phù: không đánh (chuột trái dùng vật phẩm).
    /// </summary>
    private bool CanAttack(out bool sword)
    {
        sword = false;
        ItemData item = hotbar != null ? hotbar.SelectedItem : null;
        if (item == null) return true;
        if (item.type != ItemType.VuKhi) return false;
        if (weapon == null || !weapon.IsReady) return false;
        sword = true;
        return true;
    }

    private static bool PressedThisFrame()
    {
        Mouse mouse = Mouse.current;
        // Chỉ nhận khi con trỏ đã khóa vào cửa sổ Game (bấm chuột trái để khóa lại thì chưa tính là đánh)
        return mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked;
    }

    /// <summary>Bắt đầu đòn thứ index: bật cờ khóa đi, quay mặt theo camera, bước tới và phát anim.</summary>
    private void BeginStep(int index, bool chained, bool sword)
    {
        usingSword = sword;
        activeSteps = sword ? swordSteps : unarmedSteps;
        Step s = activeSteps[index];
        stepIndex = index;
        stepElapsed = 0f;
        float speed;
        if (s.duration > 0.05f)
        {
            // Thời gian thật cố định: tăng/giảm tốc anim để mốc "trúng đòn" của clip rơi đúng hitTime và mốc "kết thúc" rơi đúng duration
            stepDuration = s.duration;
            hitDelay = Mathf.Clamp(s.hitTime, 0.02f, stepDuration - 0.02f);
            speed = Mathf.Max(0.1f, (s.clipHit - s.clipStart) / hitDelay);
            speedAfterHit = Mathf.Max(0.1f, (s.clipEnd - s.clipHit) / Mathf.Max(0.02f, stepDuration - hitDelay));
        }
        else
        {
            speed = Mathf.Max(0.1f, s.speed);
            stepDuration = Mathf.Max(0.05f, (s.clipEnd - s.clipStart) / speed);
            hitDelay = Mathf.Clamp((s.clipHit - s.clipStart) / speed, 0f, stepDuration);
            speedAfterHit = speed;
        }
        hitDone = false;
        chainQueued = false;

        attackInFlight = wings != null && wings.IsFlying;
        if (!attackInFlight)
        {
            player.MoveLocked = true;
            player.FaceCameraForAttack = true;
        }
        if (weapon != null)
            weapon.SetSlashPose(sword, sword && s.useGripOverride, new HeldWeapon.GripPose { position = s.gripPosition, euler = s.gripEuler });
        SetTrail(false);

        if (animator != null)
        {
            animator.SetFloat(AtkSpeedId, speed);
            if (attackInFlight)
            {
                // Đang bay: chỉ tay chém (lớp thân trên), thân dưới giữ dáng bay
                int layer = animator.GetLayerIndex(upperLayerName);
                animator.CrossFadeInFixedTime(s.stateName + "_Bay", chained ? blendTime : 0.08f, layer, s.clipStart);
            }
            else animator.CrossFadeInFixedTime(s.stateName, chained ? blendTime : 0.08f, 0, s.clipStart);
        }

        // Bước nhẹ tới trước theo hướng camera đang nhìn (đang bay thì không)
        if (!attackInFlight)
        {
            Vector3 dir = AimDirection();
            player.Lunge(dir, s.lungeDistance, Mathf.Max(0.05f, hitDelay));
        }
        StepStarted?.Invoke(index);
    }

    /// <summary>Vệt chém chỉ hiện quanh khoảnh khắc trúng đòn (theo thời gian thật, không phụ thuộc clip).</summary>
    private void UpdateSlashTrail(Step s)
    {
        SetTrail(stepElapsed >= hitDelay - s.trailBeforeHit && stepElapsed <= hitDelay + s.trailAfterHit);
    }

    private void SetTrail(bool on)
    {
        if (trailOn == on) return;
        trailOn = on;
        if (weapon != null) weapon.SetSlashTrail(on);
    }

    /// <summary>Kết thúc chuỗi đòn. smoothExit = chuyển mượt về đứng yên.</summary>
    private void EndAttack(bool smoothExit)
    {
        bool was = IsAttacking;
        stepIndex = -1;
        chainQueued = false;
        cooldownUntil = Time.time + 0.1f;
        SetTrail(false);
        if (weapon != null) weapon.SetSlashPose(false);
        if (player != null)
        {
            player.MoveLocked = false;
            player.FaceCameraForAttack = false;
        }
        if (was && animator != null && stats != null && stats.IsAlive)
        {
            if (attackInFlight)
            {
                // Chém khi bay: trả tay về dáng bay (lớp thân trên về trạng thái rỗng), không đụng lớp nền
                int layer = animator.GetLayerIndex(upperLayerName);
                if (layer >= 0) animator.CrossFadeInFixedTime(upperEmptyState, smoothExit ? exitBlendTime : 0.1f, layer);
            }
            else animator.CrossFadeInFixedTime(idleState, smoothExit ? exitBlendTime : 0.1f, 0);
        }
        attackInFlight = false;
    }

    /// <summary>Hướng nhìn ngang của camera (mặt nhân vật sẽ quay theo hướng này).</summary>
    private Vector3 AimDirection()
    {
        Camera cam = Camera.main;
        Vector3 f = cam != null ? cam.transform.forward : transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
    }

    /// <summary>Quét hình quạt phía trước, gây sát thương cho mọi mục tiêu còn sống trong tầm.</summary>
    private void DoHit(Step s)
    {
        HitMoment?.Invoke(stepIndex);
        float reach = usingSword ? swordRange : range;
        float halfArc = (usingSword ? swordArc : arc) * 0.5f;
        Vector3 forward = AimDirection();
        Vector3 origin = transform.position + Vector3.up * 1f;

        // Duyệt ngược vì danh sách có thể thay đổi khi có mục tiêu chết
        for (int i = Damageable.All.Count - 1; i >= 0; i--)
        {
            Damageable d = Damageable.All[i];
            if (d == null || !d.IsAlive) continue;

            Vector3 point = ClosestPointOn(d, origin);
            Vector3 to = point - origin;
            if (to.sqrMagnitude > reach * reach) continue;

            Vector3 flat = to;
            flat.y = 0f;
            // Sát người (gần như trùng) thì tính là trúng, còn lại kiểm tra góc quạt
            if (flat.sqrMagnitude > 0.04f && Vector3.Angle(forward, flat) > halfArc) continue;

            DamageInfo info = new DamageInfo { amount = s.damage, point = point, direction = forward, melee = true };
            d.TakeDamage(info);
            if (hitFeedback != null) hitFeedback.OnHit(!d.IsAlive);
            if (s.knockback > 0f && d.IsAlive) d.Knockback(forward * s.knockback);
        }
    }

    /// <summary>Điểm gần nhất trên vật thể tới origin (qua collider); không có collider thì lấy điểm ngắm.</summary>
    private static Vector3 ClosestPointOn(Damageable d, Vector3 origin)
    {
        Collider c = d.GetComponentInChildren<Collider>();
        if (c != null) return c.ClosestPoint(origin);
        return d.AimPoint;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Vẽ hình quạt vùng đánh (cam: tay không, xanh: cầm kiếm) để dễ chỉnh tầm và góc
        Vector3 origin = transform.position + Vector3.up;
        Vector3 f = transform.forward;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, -arc * 0.5f, 0f) * f * range);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, arc * 0.5f, 0f) * f * range);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, -swordArc * 0.5f, 0f) * f * swordRange);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, swordArc * 0.5f, 0f) * f * swordRange);
    }
#endif
}
