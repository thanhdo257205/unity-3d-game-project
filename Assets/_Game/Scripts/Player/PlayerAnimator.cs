using UnityEngine;

/// <summary>
/// Nối PlayerController và SwordSwarm với Animator của mô hình nhân vật.
/// - Đứng/Đi/Chạy: truyền tốc độ thật (mét/giây) vào Blend Tree; chỉnh tốc độ phát anim cho khớp tốc độ mặt đất để chân không trượt.
/// - Nhảy, rơi, tiếp đất: truyền trạng thái chạm đất và vận tốc thẳng đứng.
/// - Phóng kiếm (chuột trái, phím 3): bật anim Phóng Kiếm ở lớp thân trên. Đổi đội hình: bật anim Niệm Quyết.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Animator của mô hình nhân vật. Để trống thì tự tìm trong các đối tượng con.")]
    [SerializeField] private Animator animator;

    [Tooltip("Script điều khiển nhân vật (đối tượng NhanVat).")]
    [SerializeField] private PlayerController player;

    [Tooltip("Đàn kiếm, để biết lúc nào phóng kiếm và đổi đội hình. Có thể để trống.")]
    [SerializeField] private SwordSwarm swarm;

    [Header("Khớp bước chân với tốc độ mặt đất")]
    [Tooltip("Tốc độ tiến thật của anim Đi trong file gốc (mét/giây). Đo từ chuyển động gốc của clip Walk: 1.98.")]
    [SerializeField, Min(0.1f)] private float walkClipSpeed = 1.98f;

    [Tooltip("Tốc độ tiến thật của anim Chạy trong file gốc (mét/giây). Đo từ clip Run: 6.55. Chạy nhanh hơn mức này thì anim được phát nhanh lên để chân không trượt.")]
    [SerializeField, Min(0.1f)] private float runClipSpeed = 6.55f;

    [Tooltip("Anim phát nhanh nhất bao nhiêu lần so với bình thường khi nhân vật chạy nhanh hơn tốc độ gốc của clip. Tránh chân quẫy quá tay.")]
    [SerializeField, Min(1f)] private float maxAnimSpeed = 1.5f;

    [Tooltip("Độ mượt khi đổi tốc độ (giây). Số nhỏ = phản ứng nhanh.")]
    [SerializeField, Min(0.001f)] private float speedSmoothTime = 0.06f;

    [Header("Nhảy và rơi")]
    [Tooltip("Thời gian ân hạn (giây) trước khi coi là 'không chạm đất'. Tránh anim rơi chớp nháy khi bước qua bậc thang hay dốc nhỏ.")]
    [SerializeField, Min(0f)] private float groundedGrace = 0.12f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AnimSpeedId = Animator.StringToHash("AnimSpeed");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int ShootId = Animator.StringToHash("Shoot");
    private static readonly int CastId = Animator.StringToHash("Cast");

    private float smoothSpeed;
    private float speedVelocity;
    private float lastGroundedTime;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (player == null) player = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (player != null) player.Jumped += OnJumped;
        if (swarm != null)
        {
            swarm.StrikeFired += OnStrike;
            swarm.FormationChanged += OnFormationChanged;
        }
    }

    private void OnDisable()
    {
        if (player != null) player.Jumped -= OnJumped;
        if (swarm != null)
        {
            swarm.StrikeFired -= OnStrike;
            swarm.FormationChanged -= OnFormationChanged;
        }
    }

    private void OnJumped() { if (animator != null) animator.SetTrigger(JumpId); }

    private void OnStrike() { if (animator != null) animator.SetTrigger(ShootId); }

    private void OnFormationChanged(FormationType type) { if (animator != null) animator.SetTrigger(CastId); }

    private void Update()
    {
        if (animator == null || player == null) return;

        float speed = player.HorizontalSpeed;
        smoothSpeed = Mathf.SmoothDamp(smoothSpeed, speed, ref speedVelocity, speedSmoothTime);
        animator.SetFloat(SpeedId, smoothSpeed);

        // Phát anim nhanh/chậm cho khớp tốc độ mặt đất: chạy quá nhanh thì tăng tốc anim Chạy; đi rất chậm thì giảm tốc anim Đi
        float animSpeed = 1f;
        if (smoothSpeed > runClipSpeed) animSpeed = Mathf.Min(maxAnimSpeed, smoothSpeed / runClipSpeed);
        else if (smoothSpeed > 0.05f && smoothSpeed < walkClipSpeed) animSpeed = Mathf.Clamp(smoothSpeed / walkClipSpeed, 0.5f, 1f);
        animator.SetFloat(AnimSpeedId, animSpeed);

        if (player.IsGrounded) lastGroundedTime = Time.time;
        animator.SetBool(GroundedId, Time.time - lastGroundedTime <= groundedGrace);
        animator.SetFloat(VerticalSpeedId, player.VerticalVelocity);
    }
}
