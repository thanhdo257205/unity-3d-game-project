using UnityEngine;

/// <summary>
/// Nối PlayerController và SwordSwarm với Animator của mô hình nhân vật.
/// - Đứng/Đi/Chạy: truyền tốc độ thật (mét/giây) vào Blend Tree; chỉnh tốc độ phát anim cho khớp tốc độ mặt đất để chân không trượt.
/// - Nhảy, rơi, tiếp đất: truyền trạng thái chạm đất và vận tốc thẳng đứng.
/// - Phóng kiếm (chuột phải khi Ngự Kiếm Thuật đang bật): ngay khi bấm, tay chuyển (0.08s) tới tư thế phóng lấy từ clip PhongKiem2 ở lớp thân trên,
///   giữ nguyên tư thế đó khi còn bắn liên tiếp, ngừng bắn 0.35s mới phát phần thu tay về. Kiếm chỉ rời đội hình khi tay tới nơi.
///   Mỗi kiếm phóng đi giật cẳng tay/bàn tay phải một chút, thân trên nghiêng theo tâm ngắm (tối đa 25 độ).
/// - Bật Ngự Kiếm Thuật (phím Q) và đổi đội hình (Z/X/C): bật anim Niệm Quyết.
/// </summary>
[DefaultExecutionOrder(-50)]   // chạy trước SwordSwarm để đàn kiếm bám đúng neo trên xương đã nghiêng
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

    [Header("Tư thế phóng kiếm (clip PhongKiem2)")]
    [Tooltip("Điểm ngắm camera để thân trên ngẩng/cúi theo hướng tâm ngắm (đối tượng CameraTarget). Để trống thì tự tìm.")]
    [SerializeField] private CameraOrbit cameraOrbit;

    [Tooltip("Giây trong clip PhongKiem2 của khung hình 'tay tới điểm phóng' (tay giơ lên và dừng lại). Tư thế giữ lúc bắn chính là khung này.")]
    [SerializeField, Min(0f)] private float shootKeyTime = 0.9f;

    [Tooltip("Thời gian chuyển từ tư thế hiện tại sang tư thế phóng ngay khi bấm bắn (giây). Kiếm rời đội hình đúng sau khoảng này ở phát đầu tiên.")]
    [SerializeField, Min(0.01f)] private float raiseTime = 0.08f;

    [Tooltip("Ngừng bắn bao nhiêu giây thì bắt đầu thu tay về (giây).")]
    [SerializeField, Min(0f)] private float releaseDelay = 0.35f;

    [Tooltip("Tốc độ phát phần thu tay về (1 = tốc độ gốc của clip, 2.5 = nhanh gấp 2.5 lần).")]
    [SerializeField, Min(0.1f)] private float retractSpeed = 2.5f;

    [Tooltip("Giây trong clip mà tay đã về xong tư thế nghỉ. Phát tới đây thì trả quyền lại cho lớp nền.")]
    [SerializeField, Min(0f)] private float retractEndClipTime = 2.95f;

    [Tooltip("Thời gian hòa trộn lúc kết thúc thu tay (giây).")]
    [SerializeField, Min(0f)] private float retractBlendTime = 0.12f;

    [Header("Giật tay mỗi phát bắn")]
    [Tooltip("Góc cẳng tay phải đẩy tới trước mỗi khi một kiếm phóng đi (độ).")]
    [SerializeField, Range(0f, 30f)] private float kickForearmAngle = 8f;

    [Tooltip("Góc bàn tay phải đẩy thêm tới trước (độ).")]
    [SerializeField, Range(0f, 30f)] private float kickHandAngle = 8f;

    [Tooltip("Thời gian cú giật đi và về (giây).")]
    [SerializeField, Min(0.02f)] private float kickTime = 0.1f;

    [Header("Thân trên theo tâm ngắm")]
    [Tooltip("Thân trên ngẩng/cúi theo hướng nhìn của camera khi bắn, tối đa bao nhiêu độ.")]
    [SerializeField, Range(0f, 60f)] private float maxAimPitch = 25f;

    [Tooltip("Tốc độ bật/tắt hiệu ứng nghiêng thân (đơn vị trọng số mỗi giây).")]
    [SerializeField, Min(1f)] private float aimBlendSpeed = 10f;

    [Header("Bị đánh và chết")]
    [Tooltip("Tài nguyên người chơi: phát anim Bị Đánh khi mất máu, anim Chết khi hết máu. Để trống thì tự tìm.")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Quyền cước: khi đang đánh thì tắt lớp thân trên để toàn thân phát anim đòn đánh. Để trống thì tự tìm.")]
    [SerializeField] private PlayerMelee melee;

    [Tooltip("Tên lớp Animator chỉ điều khiển thân trên (phóng kiếm, niệm quyết, bị đánh).")]
    [SerializeField] private string upperLayerName = "UpperBody";

    [Tooltip("Tốc độ bật/tắt lớp thân trên (đơn vị trọng số mỗi giây).")]
    [SerializeField, Min(1f)] private float upperLayerBlendSpeed = 12f;

    [Tooltip("Tên trạng thái Animator để quay về khi hồi sinh.")]
    [SerializeField] private string idleState = "Locomotion";

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int DieId = Animator.StringToHash("Die");
    private static readonly int AnimSpeedId = Animator.StringToHash("AnimSpeed");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int CastId = Animator.StringToHash("Cast");
    private static readonly int RetractSpeedId = Animator.StringToHash("ShootRetractSpeed");
    private static readonly int HoldTimeId = Animator.StringToHash("ShootHoldTime");
    private static readonly int HoldStateId = Animator.StringToHash("TayGiu");
    private static readonly int RetractStateId = Animator.StringToHash("TayThu");
    private static readonly int EmptyUpperStateId = Animator.StringToHash("Trong");

    private enum ShootPhase { None, Raising, Holding, Retracting }

    private ShootPhase phase;
    private float raiseEnd;
    private float retractEnd;
    private float lastShootActivity;
    private float phaseGraceUntil;
    private float kickStart = -10f;
    private float aimWeight;
    private float shootClipLength = 3.6f;

    // Xương bị chỉnh bằng code sau khi Animator chạy (giật tay, nghiêng thân)
    private Transform spineBone, chestBone, upperChestBone, forearmBone, handBone;
    private readonly Quaternion[] lastApplied = new Quaternion[5];
    private readonly Quaternion[] lastBase = new Quaternion[5];
    private readonly bool[] hasApplied = new bool[5];

    private int upperLayer = -1;
    private float upperWeight = 1f;
    private float smoothSpeed;
    private float speedVelocity;
    private float lastGroundedTime;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (player == null) player = GetComponent<PlayerController>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (melee == null) melee = GetComponent<PlayerMelee>();
        if (cameraOrbit == null) cameraOrbit = FindAnyObjectByType<CameraOrbit>();
        if (animator != null)
        {
            upperLayer = animator.GetLayerIndex(upperLayerName);
            // Độ dài clip PhongKiem2 (giây) để đổi shootKeyTime ra thời gian chuẩn hóa
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                if (c != null && c.name == "PhongKiem2") shootClipLength = c.length;
            spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
            chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            forearmBone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            handBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        upperWeight = 1f;
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.Damaged += OnPlayerDamaged;
            stats.Died += OnPlayerDied;
            stats.Respawned += OnPlayerRespawned;
        }
        if (player != null) player.Jumped += OnJumped;
        if (swarm != null)
        {
            swarm.SwordLaunched += OnSwordLaunched;
            swarm.FormationChanged += OnFormationChanged;
            swarm.ActiveChanged += OnSwarmActiveChanged;
            swarm.ShootPoseGate = RequestShootPose;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.Damaged -= OnPlayerDamaged;
            stats.Died -= OnPlayerDied;
            stats.Respawned -= OnPlayerRespawned;
        }
        if (player != null) player.Jumped -= OnJumped;
        if (swarm != null)
        {
            swarm.SwordLaunched -= OnSwordLaunched;
            swarm.FormationChanged -= OnFormationChanged;
            swarm.ActiveChanged -= OnSwarmActiveChanged;
            if (swarm.ShootPoseGate == RequestShootPose) swarm.ShootPoseGate = null;
        }
    }

    // Bị đánh: chỉ phát anim nếu còn sống (nếu chết thì Died lo)
    private void OnPlayerDamaged(float amount)
    {
        if (animator != null && stats != null && stats.IsAlive) animator.SetTrigger(HitId);
    }

    private void OnPlayerDied()
    {
        if (animator == null) return;
        animator.ResetTrigger(HitId);
        animator.ResetTrigger(CastId);
        animator.SetTrigger(DieId);
        phase = ShootPhase.None;
    }

    // Hồi sinh: xóa mọi trigger còn treo và quay về đứng yên
    private void OnPlayerRespawned()
    {
        if (animator == null) return;
        animator.ResetTrigger(DieId);
        animator.ResetTrigger(HitId);
        animator.ResetTrigger(CastId);
        animator.ResetTrigger(JumpId);
        animator.CrossFadeInFixedTime(idleState, 0.1f, 0);
        if (upperLayer >= 0) animator.CrossFadeInFixedTime(EmptyUpperStateId, 0.05f, upperLayer);
        phase = ShootPhase.None;
        smoothSpeed = 0f;
    }

    private void OnJumped() { if (animator != null) animator.SetTrigger(JumpId); }

    /// <summary>Mỗi kiếm rời đội hình: giữ tay ở tư thế phóng và giật tay một nhịp.</summary>
    private void OnSwordLaunched()
    {
        lastShootActivity = Time.time;
        kickStart = Time.time;
    }

    /// <summary>
    /// SwordSwarm gọi ngay khi bấm bắn: đưa tay vào tư thế phóng (khung 'shootKeyTime' của clip PhongKiem2)
    /// và trả về số giây còn lại cho tới khi tay tới nơi. Tay đã ở đó rồi thì trả về 0 (bắn ngay, không lấy đà lại).
    /// </summary>
    private float RequestShootPose()
    {
        lastShootActivity = Time.time;
        if (animator == null || upperLayer < 0) return 0f;
        if (phase == ShootPhase.Holding) return 0f;
        if (phase == ShootPhase.Raising) return Mathf.Max(0f, raiseEnd - Time.time);

        // Đang nghỉ hoặc đang thu tay: chuyển thẳng tới khung tay duỗi, không phát phần lấy đà
        // Trạng thái giữ dùng "Motion Time" (ShootHoldTime) để đứng yên đúng ở khung shootKeyTime
        animator.SetFloat(HoldTimeId, shootKeyTime / shootClipLength);
        animator.CrossFadeInFixedTime(HoldStateId, raiseTime, upperLayer);
        phase = ShootPhase.Raising;
        raiseEnd = Time.time + raiseTime;
        phaseGraceUntil = Time.time + 0.2f;
        return raiseTime;
    }

    private void OnFormationChanged(FormationType type) { if (animator != null) animator.SetTrigger(CastId); }

    // Bật Ngự Kiếm Thuật: nhân vật vào tư thế niệm quyết trong lúc kiếm bay ra khỏi túi
    private void OnSwarmActiveChanged(bool on) { if (on && animator != null) animator.SetTrigger(CastId); }

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

        animator.SetFloat(RetractSpeedId, retractSpeed);
        animator.SetFloat(HoldTimeId, shootKeyTime / shootClipLength);
        UpdateShootPhase();
        UpdateUpperLayer();
    }

    /// <summary>Theo dõi tư thế phóng: tới nơi thì giữ, ngừng bắn đủ lâu thì thu tay về, bị anim khác (bị đánh, niệm quyết) chen vào thì thôi.</summary>
    private void UpdateShootPhase()
    {
        if (phase == ShootPhase.None || upperLayer < 0) return;

        // Anim khác chiếm lớp thân trên (Bị đánh, Niệm quyết...) thì không còn ở tư thế phóng nữa
        if (Time.time > phaseGraceUntil && !IsShootStateActive())
        {
            phase = ShootPhase.None;
            return;
        }

        switch (phase)
        {
            case ShootPhase.Raising:
                if (Time.time >= raiseEnd) phase = ShootPhase.Holding;
                break;
            case ShootPhase.Holding:
                if (Time.time - lastShootActivity >= releaseDelay)
                {
                    // Phát phần thu tay về, bắt đầu đúng ở khung đang giữ nên không bị giật
                    // fixedTimeOffset được nhân với tốc độ trạng thái, nên chia retractSpeed để bắt đầu đúng giây shootKeyTime của clip
                    animator.CrossFadeInFixedTime(RetractStateId, 0.02f, upperLayer, shootKeyTime / retractSpeed);
                    phase = ShootPhase.Retracting;
                    retractEnd = Time.time + Mathf.Max(0.05f, (retractEndClipTime - shootKeyTime) / retractSpeed);
                    phaseGraceUntil = Time.time + 0.2f;
                }
                break;
            case ShootPhase.Retracting:
                if (Time.time >= retractEnd)
                {
                    animator.CrossFadeInFixedTime(EmptyUpperStateId, retractBlendTime, upperLayer);
                    phase = ShootPhase.None;
                }
                break;
        }
    }

    private bool IsShootStateActive()
    {
        AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(upperLayer);
        if (cur.shortNameHash == HoldStateId || cur.shortNameHash == RetractStateId) return true;
        if (animator.IsInTransition(upperLayer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(upperLayer);
            return next.shortNameHash == HoldStateId || next.shortNameHash == RetractStateId;
        }
        return false;
    }

    // ================= Chỉnh xương bằng code (sau khi Animator chạy) =================

    private void LateUpdate()
    {
        if (animator == null || player == null) return;
        // Hit-stop (thời gian dừng): Animator không cập nhật nên xương vẫn mang cú chỉnh của khung trước, đừng cộng dồn
        UpdateAimWeight();
        float kick = KickAmount();
        if (aimWeight <= 0.001f && kick <= 0f)
        {
            for (int i = 0; i < hasApplied.Length; i++) hasApplied[i] = false;
            return;
        }

        Vector3 axis = player.transform.right;     // trục ngang của nhân vật: quay quanh nó là cúi/ngẩng, đẩy tay tới trước
        float pitch = 0f;
        if (cameraOrbit != null) pitch = Mathf.Clamp(cameraOrbit.Pitch, -maxAimPitch, maxAimPitch) * aimWeight;
        float spineShare = pitch / 3f;

        // Thứ tự từ gốc xuống ngọn để xương con thừa hưởng xương cha
        ApplyBone(0, spineBone, axis, spineShare);
        ApplyBone(1, chestBone, axis, spineShare);
        ApplyBone(2, upperChestBone, axis, spineShare);
        ApplyBone(3, forearmBone, axis, kick * kickForearmAngle);
        ApplyBone(4, handBone, axis, kick * kickHandAngle);
    }

    private void UpdateAimWeight()
    {
        bool aiming = phase == ShootPhase.Raising || phase == ShootPhase.Holding;
        if (stats != null && !stats.IsAlive) aiming = false;
        if (melee != null && melee.IsAttacking) aiming = false;
        float dt = Time.deltaTime;
        if (dt > 0f) aimWeight = Mathf.MoveTowards(aimWeight, aiming ? 1f : 0f, aimBlendSpeed * dt);
    }

    /// <summary>Cú giật 0..1..0 trong kickTime giây kể từ lúc kiếm rời đội hình.</summary>
    private float KickAmount()
    {
        float t = (Time.time - kickStart) / kickTime;
        if (t < 0f || t >= 1f) return 0f;
        return Mathf.Sin(t * Mathf.PI);
    }

    /// <summary>Xoay xương quanh trục axis một góc angle (độ) trên nền tư thế do Animator vừa tính.</summary>
    private void ApplyBone(int slot, Transform bone, Vector3 axis, float angle)
    {
        if (bone == null) return;
        // Nếu Animator không viết lại xương (hit-stop) thì lấy lại tư thế gốc đã lưu để khỏi cộng dồn
        Quaternion baseLocal = bone.localRotation;
        if (hasApplied[slot] && Quaternion.Angle(baseLocal, lastApplied[slot]) < 0.0001f) baseLocal = lastBase[slot];
        bone.localRotation = baseLocal;
        if (Mathf.Abs(angle) > 0.0001f) bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        lastBase[slot] = baseLocal;
        lastApplied[slot] = bone.localRotation;
        hasApplied[slot] = true;
    }

    /// <summary>Lớp thân trên chỉ bật khi không đánh quyền cước và còn sống; tắt thì toàn thân phát anim của lớp nền.</summary>
    private void UpdateUpperLayer()
    {
        if (upperLayer < 0) return;
        bool wantUpper = !(melee != null && melee.IsAttacking) && !(stats != null && !stats.IsAlive);
        upperWeight = Mathf.MoveTowards(upperWeight, wantUpper ? 1f : 0f, upperLayerBlendSpeed * Time.deltaTime);
        animator.SetLayerWeight(upperLayer, upperWeight);
    }
}
