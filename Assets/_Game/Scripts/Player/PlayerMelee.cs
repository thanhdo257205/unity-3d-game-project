using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quyền cước 3 đòn: Dam1 → Dam2 → Da (chuột phải hoặc F). Không tốn pháp lực.
/// Bấm tiếp khi đòn trước gần xong thì nối đòn sau; không bấm thì về đứng yên.
/// Khi đánh: nhân vật quay mặt theo camera, bước nhẹ tới trước, không đi bằng WASD (nhưng nhảy thì hủy được đòn).
/// Đòn trúng thì gây sát thương cho mọi mục tiêu trong hình quạt phía trước; đòn cuối còn đẩy lùi mục tiêu.
/// Mọi con số chỉnh trong Inspector.
/// </summary>
public class PlayerMelee : MonoBehaviour
{
    /// <summary>Dữ liệu một đòn trong chuỗi combo.</summary>
    [Serializable]
    public class Step
    {
        [Tooltip("Tên trạng thái trong Animator (Dam1, Dam2, Da).")]
        public string stateName = "Dam1";

        [Tooltip("Sát thương của đòn này.")]
        [Min(0f)] public float damage = 10f;

        [Tooltip("Giây trong clip mà đòn bắt đầu phát (bỏ đoạn dạo đầu của clip cho ra đòn nhanh).")]
        [Min(0f)] public float clipStart = 0.1f;

        [Tooltip("Giây trong clip tại khoảnh khắc đòn trúng (nắm đấm/chân chạm tới).")]
        [Min(0f)] public float clipHit = 0.3f;

        [Tooltip("Giây trong clip mà đòn kết thúc (hết đà đánh).")]
        [Min(0.05f)] public float clipEnd = 0.7f;

        [Tooltip("Tốc độ phát anim (1 = bình thường, 1.5 = nhanh gấp rưỡi).")]
        [Min(0.1f)] public float speed = 1.4f;

        [Tooltip("Quãng bước tới trước khi ra đòn (mét).")]
        [Min(0f)] public float lungeDistance = 0.3f;

        [Tooltip("Đẩy lùi mục tiêu trúng đòn (mét). 0 = không đẩy.")]
        [Min(0f)] public float knockback = 0f;
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

    [Header("Chuỗi đòn")]
    [Tooltip("Ba đòn theo thứ tự: Dam1 (10 sát thương), Dam2 (12), Da (20, đẩy lùi 1.5m).")]
    [SerializeField] private Step[] steps =
    {
        // Số giây clipStart/clipHit/clipEnd đo từ chuyển động tay chân thật của từng clip Mixamo
        new Step { stateName = "Dam1", damage = 10f, clipStart = 0.30f, clipHit = 0.45f, clipEnd = 0.72f, speed = 1.2f, lungeDistance = 0.3f },
        new Step { stateName = "Dam2", damage = 12f, clipStart = 0.65f, clipHit = 1.00f, clipEnd = 1.30f, speed = 1.3f, lungeDistance = 0.3f },
        new Step { stateName = "Da",   damage = 20f, clipStart = 0.20f, clipHit = 0.60f, clipEnd = 1.05f, speed = 1.2f, lungeDistance = 0.4f, knockback = 1.5f },
    };

    [Header("Vùng đánh")]
    [Tooltip("Tầm với của đòn đánh phía trước mặt (mét).")]
    [SerializeField, Min(0.1f)] private float range = 1.6f;

    [Tooltip("Góc hình quạt trước mặt (độ). 90 = lệch trái/phải mỗi bên 45 độ.")]
    [SerializeField, Range(10f, 360f)] private float arc = 90f;

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

    private int stepIndex = -1;        // -1 = không đánh
    private float stepElapsed;         // thời gian thật đã trôi qua trong đòn hiện tại
    private float stepDuration;        // thời gian thật của đòn hiện tại
    private float hitDelay;            // thời gian thật từ lúc bắt đầu đòn tới khi trúng
    private bool hitDone;
    private bool chainQueued;
    private float cooldownUntil;

    /// <summary>Đang trong chuỗi quyền cước không.</summary>
    public bool IsAttacking => stepIndex >= 0;

    /// <summary>Báo mỗi lần bắt đầu một đòn (tham số: chỉ số đòn 0-2).</summary>
    public event Action<int> StepStarted;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (player != null) player.Jumped += OnJumped;
        if (stats != null) stats.Died += OnDied;
    }

    private void OnDisable()
    {
        if (player != null) player.Jumped -= OnJumped;
        if (stats != null) stats.Died -= OnDied;
        EndAttack(false);
    }

    private void OnJumped() { if (IsAttacking) EndAttack(false); }

    private void OnDied() { if (IsAttacking) EndAttack(false); }

    private void Update()
    {
        if (stats != null && !stats.IsAlive) return;
        if (player != null && player.ControlLocked) return;

        bool pressed = PressedThisFrame();
        if (player.IsGrounded) lastGroundedTime = Time.time;
        bool onGround = Time.time - lastGroundedTime <= groundGrace;   // chịu được vài khung hình "hụt đất" khi đi xuống bậc

        if (!IsAttacking)
        {
            if (pressed && Time.time >= cooldownUntil && onGround) BeginStep(0, false);
            return;
        }

        // Rời khỏi mặt đất giữa chừng (rơi xuống vực...) thì hủy đòn
        if (!onGround) { EndAttack(false); return; }

        stepElapsed += Time.deltaTime;

        if (!hitDone && stepElapsed >= hitDelay)
        {
            hitDone = true;
            DoHit(steps[stepIndex]);
        }

        // Chỉ nhận lệnh nối đòn ở nửa sau của đòn
        if (pressed && stepIndex < steps.Length - 1 && stepElapsed >= stepDuration * chainWindowStart)
            chainQueued = true;

        if (stepElapsed >= stepDuration)
        {
            if (chainQueued && stepIndex < steps.Length - 1) BeginStep(stepIndex + 1, true);
            else EndAttack(true);
        }
    }

    private static bool PressedThisFrame()
    {
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        return (mouse != null && mouse.rightButton.wasPressedThisFrame) || (kb != null && kb.fKey.wasPressedThisFrame);
    }

    /// <summary>Bắt đầu đòn thứ index: bật cờ khóa đi, quay mặt theo camera, bước tới và phát anim.</summary>
    private void BeginStep(int index, bool chained)
    {
        Step s = steps[index];
        stepIndex = index;
        stepElapsed = 0f;
        float speed = Mathf.Max(0.1f, s.speed);
        stepDuration = Mathf.Max(0.05f, (s.clipEnd - s.clipStart) / speed);
        hitDelay = Mathf.Clamp((s.clipHit - s.clipStart) / speed, 0f, stepDuration);
        hitDone = false;
        chainQueued = false;

        player.MoveLocked = true;
        player.FaceCameraForAttack = true;

        if (animator != null)
        {
            animator.SetFloat(AtkSpeedId, speed);
            animator.CrossFadeInFixedTime(s.stateName, chained ? blendTime : 0.08f, 0, s.clipStart);
        }

        // Bước nhẹ tới trước theo hướng camera đang nhìn
        Vector3 dir = AimDirection();
        player.Lunge(dir, s.lungeDistance, Mathf.Max(0.05f, hitDelay));
        StepStarted?.Invoke(index);
    }

    /// <summary>Kết thúc chuỗi đòn. smoothExit = chuyển mượt về đứng yên.</summary>
    private void EndAttack(bool smoothExit)
    {
        bool was = IsAttacking;
        stepIndex = -1;
        chainQueued = false;
        cooldownUntil = Time.time + 0.1f;
        if (player != null)
        {
            player.MoveLocked = false;
            player.FaceCameraForAttack = false;
        }
        if (was && animator != null && stats != null && stats.IsAlive)
            animator.CrossFadeInFixedTime(idleState, smoothExit ? exitBlendTime : 0.1f, 0);
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
        Vector3 forward = AimDirection();
        Vector3 origin = transform.position + Vector3.up * 1f;
        float halfArc = arc * 0.5f;

        // Duyệt ngược vì danh sách có thể thay đổi khi có mục tiêu chết
        for (int i = Damageable.All.Count - 1; i >= 0; i--)
        {
            Damageable d = Damageable.All[i];
            if (d == null || !d.IsAlive) continue;

            Vector3 point = ClosestPointOn(d, origin);
            Vector3 to = point - origin;
            if (to.sqrMagnitude > range * range) continue;

            Vector3 flat = to;
            flat.y = 0f;
            // Sát người (gần như trùng) thì tính là trúng, còn lại kiểm tra góc quạt
            if (flat.sqrMagnitude > 0.04f && Vector3.Angle(forward, flat) > halfArc) continue;

            DamageInfo info = new DamageInfo { amount = s.damage, point = point, direction = forward };
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
        // Vẽ hình quạt vùng đánh để dễ chỉnh tầm và góc
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        Vector3 origin = transform.position + Vector3.up;
        Vector3 f = transform.forward;
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, -arc * 0.5f, 0f) * f * range);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, arc * 0.5f, 0f) * f * range);
    }
#endif
}
