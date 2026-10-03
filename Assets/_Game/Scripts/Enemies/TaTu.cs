using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tà tu Luyện Khí: dùng lại mô hình nhân vật (áo đỏ sẫm), 2 phi kiếm đỏ bay quanh người.
/// Giữ khoảng cách 8-12m với người chơi, đi ngang né tránh, mỗi 2 giây phóng một phi kiếm,
/// thỉnh thoảng ném Hỏa Cầu. Còn dưới 20% máu thì bỏ chạy bằng tốc độ chạy, 8 giây sau quay lại.
/// Bị quyền cước đánh trúng thì loạng choạng (anim Bị Đánh) và ngắt đòn đang ra.
/// </summary>
public class TaTu : EnemyBase
{
    [Header("Tham chiếu")]
    [Tooltip("Animator của mô hình (đối tượng con NhanVat_Model). Để trống thì tự tìm.")]
    [SerializeField] private Animator animator;

    [Tooltip("Prefab phi kiếm đỏ (có script EnemyBlade).")]
    [SerializeField] private EnemyBlade bladePrefab;

    [Tooltip("Số phi kiếm đỏ bay quanh người.")]
    [SerializeField, Min(1)] private int bladeCount = 2;

    [Tooltip("Prefab Hỏa Cầu (quả cầu lửa, có script EnemyOrb).")]
    [SerializeField] private EnemyOrb fireballPrefab;

    [Header("Giữ khoảng cách")]
    [Tooltip("Gần hơn mức này (mét) thì lùi ra xa người chơi.")]
    [SerializeField, Min(1f)] private float minDistance = 8f;

    [Tooltip("Xa hơn mức này (mét) thì tiến lại gần.")]
    [SerializeField, Min(1f)] private float maxDistance = 12f;

    [Tooltip("Tốc độ đi ngang né tránh và chỉnh khoảng cách (mét/giây).")]
    [SerializeField, Min(0.1f)] private float strafeSpeed = 3f;

    [Tooltip("Đổi chiều đi ngang sau khoảng thời gian ngẫu nhiên từ ... giây.")]
    [SerializeField, Min(0.5f)] private float strafeSwitchMin = 1.5f;

    [Tooltip("... đến ... giây.")]
    [SerializeField, Min(0.5f)] private float strafeSwitchMax = 3.5f;

    [Header("Phi kiếm")]
    [Tooltip("Chỉ phóng kiếm và ném Hỏa Cầu khi người chơi trong khoảng này (mét).")]
    [SerializeField, Min(1f)] private float attackRange = 16f;

    [Tooltip("Cứ bao nhiêu giây thì phóng một phi kiếm.")]
    [SerializeField, Min(0.3f)] private float swordInterval = 2f;

    [Tooltip("Sát thương mỗi phi kiếm (đã nhân hệ số chênh lệch cảnh giới).")]
    [SerializeField, Min(0f)] private float swordDamage = 12f;

    [Tooltip("Tốc độ bay của phi kiếm (mét/giây).")]
    [SerializeField, Min(1f)] private float swordSpeed = 18f;

    [Header("Hỏa Cầu")]
    [Tooltip("Thời gian chờ tối thiểu giữa hai lần ném Hỏa Cầu (giây).")]
    [SerializeField, Min(1f)] private float fireballCooldownMin = 6f;

    [Tooltip("Thời gian chờ tối đa giữa hai lần ném Hỏa Cầu (giây).")]
    [SerializeField, Min(1f)] private float fireballCooldownMax = 10f;

    [Tooltip("Sát thương Hỏa Cầu (đã nhân hệ số chênh lệch cảnh giới).")]
    [SerializeField, Min(0f)] private float fireballDamage = 15f;

    [Tooltip("Tốc độ bay của Hỏa Cầu (mét/giây).")]
    [SerializeField, Min(1f)] private float fireballSpeed = 10f;

    [Tooltip("Thời gian niệm trước khi Hỏa Cầu bay ra (giây).")]
    [SerializeField, Min(0.1f)] private float fireballWindup = 0.6f;

    [Header("Bỏ chạy")]
    [Tooltip("Máu dưới tỉ lệ này (0-1) thì bỏ chạy. 0.2 = dưới 20%.")]
    [SerializeField, Range(0f, 1f)] private float fleeHealthRatio = 0.2f;

    [Tooltip("Tốc độ chạy khi bỏ chạy (mét/giây).")]
    [SerializeField, Min(0.1f)] private float fleeSpeed = 6.5f;

    [Tooltip("Chạy bao nhiêu giây thì quay lại chiến đấu (nếu chưa bị hạ).")]
    [SerializeField, Min(1f)] private float fleeDuration = 8f;

    [Tooltip("Sau khi hết bỏ chạy, được thêm bấy nhiêu giây để chạy về tầm nhìn người chơi trước khi bị coi là mất dấu (giây).")]
    [SerializeField, Min(0f)] private float fleeReturnGrace = 8f;

    [Tooltip("Chỉ bỏ chạy một lần: quay lại rồi thì chiến đấu tới cùng.")]
    [SerializeField] private bool fleeOnlyOnce = true;

    [Header("Anim niệm / phóng")]
    [Tooltip("Từ lúc giơ tay tới lúc phi kiếm lao đi (giây).")]
    [SerializeField, Min(0f)] private float castDelay = 0.12f;

    [Tooltip("Giữ tay giơ bao lâu rồi thu về (giây).")]
    [SerializeField, Min(0.1f)] private float castHold = 0.5f;

    [Tooltip("Giây trong clip PhongKiem2 của khung tay giơ lên (giống người chơi).")]
    [SerializeField, Min(0f)] private float castKeyTime = 0.9f;

    [Tooltip("Tốc độ phát phần thu tay về.")]
    [SerializeField, Min(0.1f)] private float castRetractSpeed = 2.5f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int DieId = Animator.StringToHash("Die");
    private static readonly int HoldTimeId = Animator.StringToHash("ShootHoldTime");
    private static readonly int RetractSpeedId = Animator.StringToHash("ShootRetractSpeed");
    private static readonly int HoldStateId = Animator.StringToHash("TayGiu");
    private static readonly int RetractStateId = Animator.StringToHash("TayThu");
    private static readonly int EmptyUpperId = Animator.StringToHash("Trong");

    private EnemyBlade[] blades;
    private int upperLayer = -1;
    private float clipLength = 3.6f;
    private float nextSwordTime;
    private float nextFireballTime;
    private float strafeDir = 1f;
    private float nextStrafeSwitch;
    private float nextPathTime;
    private float smoothSpeed;

    // Đòn đang chuẩn bị
    private float pendingSwordAt = -1f;
    private float pendingFireballAt = -1f;
    private float retractAt = -1f;
    private float retractDoneAt = -1f;

    private float fleeEnd;
    private bool hasFled;
    private float nextFleePathTime;

    protected override void Awake()
    {
        base.Awake();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        agent.updateRotation = false;
        agent.acceleration = 30f;
        if (animator != null)
        {
            upperLayer = animator.GetLayerIndex("UpperBody");
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                if (c != null && c.name == "PhongKiem2") clipLength = c.length;
        }
    }

    protected override void Start()
    {
        base.Start();
        blades = new EnemyBlade[bladeCount];
        for (int i = 0; i < bladeCount; i++)
        {
            blades[i] = Instantiate(bladePrefab, transform.position + Vector3.up, Quaternion.identity);
            blades[i].Setup(transform, i, bladeCount, player);
        }
        nextFireballTime = Time.time + Random.Range(fireballCooldownMin, fireballCooldownMax);
        nextStrafeSwitch = Time.time + Random.Range(strafeSwitchMin, strafeSwitchMax);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (blades == null) return;
        for (int i = 0; i < blades.Length; i++) if (blades[i] != null) Destroy(blades[i].gameObject);
    }

    // ================= Cập nhật =================

    protected override void Update()
    {
        base.Update();
        if (State == EnemyState.Dead) return;
        UpdateCast();
        UpdateAnimator();
    }

    protected override void OnPatrol()
    {
        base.OnPatrol();
        FaceVelocity();
    }

    protected override void OnDetect()
    {
        StopMoving();
        FacePlayer(540f);
    }

    protected override void OnEnterState(EnemyState from, EnemyState to)
    {
        if (to == EnemyState.Attack)
        {
            nextSwordTime = Mathf.Max(nextSwordTime, Time.time + 0.8f);   // chưa bắn ngay lúc vừa phát hiện
        }
        if (to == EnemyState.Flee) { fleeEnd = Time.time + fleeDuration; nextFleePathTime = 0f; hasFled = true; }
    }

    protected override void OnAttack()
    {
        // Máu thấp thì bỏ chạy
        if (HealthRatio < fleeHealthRatio && !(fleeOnlyOnce && hasFled)) { SetState(EnemyState.Flee); return; }

        float dist = DistanceToPlayer;
        Vector3 toPlayer = playerT.position - transform.position; toPlayer.y = 0f;
        Vector3 dir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dir) * strafeDir;

        if (Time.time >= nextStrafeSwitch)
        {
            strafeDir = -strafeDir;
            nextStrafeSwitch = Time.time + Random.Range(strafeSwitchMin, strafeSwitchMax);
        }

        if (Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + 0.25f;
            Vector3 target;
            if (dist < minDistance) target = transform.position - dir * 4f + side * 2f;          // lùi ra, vừa lùi vừa né
            else if (dist > maxDistance) target = transform.position + dir * 4f + side * 1.5f;   // tiến lại gần
            else target = transform.position + side * 3f;                                         // đi ngang
            // Ở rất xa (vừa bỏ chạy về) thì chạy nhanh để quay lại
            MoveTo(target, dist > maxDistance * 1.6f ? fleeSpeed : strafeSpeed);
        }
        FacePlayer(360f);

        // Phi kiếm: mỗi swordInterval giây một thanh
        bool busy = pendingSwordAt > 0f || pendingFireballAt > 0f;
        if (!busy && dist <= attackRange && Time.time >= nextSwordTime && CanSeePlayer())
        {
            EnemyBlade b = FreeBlade();
            if (b != null) BeginSwordCast();
        }
        // Hỏa Cầu thỉnh thoảng
        if (!busy && dist <= attackRange && Time.time >= nextFireballTime && CanSeePlayer() && fireballPrefab != null)
            BeginFireballCast();
    }

    protected override void OnFlee()
    {
        if (Time.time >= fleeEnd)
        {
            // Quay lại chiến đấu: cho thêm thời gian để chạy về tới tầm nhìn trước khi bị coi là mất dấu
            lastSeenTime = Time.time + fleeReturnGrace;
            SetState(EnemyState.Attack);
            return;
        }
        if (Time.time >= nextFleePathTime)
        {
            nextFleePathTime = Time.time + 0.8f;
            Vector3 away = transform.position - playerT.position; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            Vector3 target = transform.position + (away.normalized + Random.insideUnitSphere * 0.3f).normalized * 12f;
            MoveTo(target, fleeSpeed);
        }
        FaceVelocity();
    }

    // ================= Phóng kiếm / Hỏa Cầu =================

    private EnemyBlade FreeBlade()
    {
        if (blades == null) return null;
        for (int i = 0; i < blades.Length; i++)
            if (blades[i] != null && blades[i].IsAvailable) return blades[i];
        return null;
    }

    private void BeginSwordCast()
    {
        pendingSwordAt = Time.time + castDelay;
        nextSwordTime = Time.time + swordInterval;
        PlayCastAnim();
    }

    private void BeginFireballCast()
    {
        pendingFireballAt = Time.time + fireballWindup;
        nextFireballTime = Time.time + Random.Range(fireballCooldownMin, fireballCooldownMax);
        PlayCastAnim();
    }

    private void UpdateCast()
    {
        if (pendingSwordAt > 0f && Time.time >= pendingSwordAt)
        {
            pendingSwordAt = -1f;
            EnemyBlade b = FreeBlade();
            if (b != null && player != null && player.IsAlive) b.Launch(PlayerChest, swordSpeed, swordDamage * ScaledDamage());
        }
        if (pendingFireballAt > 0f && Time.time >= pendingFireballAt)
        {
            pendingFireballAt = -1f;
            if (fireballPrefab != null && player != null && player.IsAlive)
            {
                Vector3 origin = HandPosition();
                EnemyOrb orb = Instantiate(fireballPrefab, origin, Quaternion.identity);
                orb.Launch(origin, PlayerChest, fireballSpeed, fireballDamage * ScaledDamage(), player);
            }
        }
        // Thu tay về sau khi niệm xong
        if (retractAt > 0f && Time.time >= retractAt && animator != null && upperLayer >= 0)
        {
            retractAt = -1f;
            animator.SetFloat(RetractSpeedId, castRetractSpeed);
            animator.CrossFadeInFixedTime(RetractStateId, 0.02f, upperLayer, castKeyTime / castRetractSpeed);
            retractDoneAt = Time.time + Mathf.Max(0.05f, (2.95f - castKeyTime) / castRetractSpeed);
        }
        if (retractDoneAt > 0f && Time.time >= retractDoneAt && animator != null && upperLayer >= 0)
        {
            retractDoneAt = -1f;
            animator.CrossFadeInFixedTime(EmptyUpperId, 0.12f, upperLayer);
        }
    }

    private void PlayCastAnim()
    {
        if (animator == null || upperLayer < 0) return;
        animator.SetFloat(HoldTimeId, castKeyTime / clipLength);
        animator.CrossFadeInFixedTime(HoldStateId, 0.08f, upperLayer);
        retractAt = Time.time + castHold;
        retractDoneAt = -1f;
    }

    private Vector3 HandPosition()
    {
        if (animator != null)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null) return hand.position;
        }
        return transform.position + Vector3.up * 1.4f;
    }

    protected override void CancelActions()
    {
        pendingSwordAt = -1f;
        pendingFireballAt = -1f;
        retractAt = -1f;
        retractDoneAt = -1f;
    }

    // ================= Sự kiện =================

    protected override void OnDamagedExtra(DamageInfo info)
    {
        // Loạng choạng (quyền cước) phát anim Bị Đánh ở thân trên
        if (info.melee && animator != null && damageable.IsAlive) animator.SetTrigger(HitId);
    }

    protected override void OnDiedExtra()
    {
        if (animator != null)
        {
            animator.ResetTrigger(HitId);
            animator.SetTrigger(DieId);
            if (upperLayer >= 0) animator.SetLayerWeight(upperLayer, 0f);
        }
        if (blades != null)
            for (int i = 0; i < blades.Length; i++) if (blades[i] != null) blades[i].gameObject.SetActive(false);
    }

    protected override void OnRevivedExtra()
    {
        CancelActions();
        hasFled = false;
        if (animator != null)
        {
            animator.ResetTrigger(DieId);
            animator.ResetTrigger(HitId);
            animator.CrossFadeInFixedTime("Locomotion", 0.05f, 0);
            if (upperLayer >= 0) { animator.SetLayerWeight(upperLayer, 1f); animator.CrossFadeInFixedTime(EmptyUpperId, 0.05f, upperLayer); }
        }
        if (blades != null)
            for (int i = 0; i < blades.Length; i++)
                if (blades[i] != null) { blades[i].gameObject.SetActive(true); blades[i].ResetToOrbit(); }
        nextSwordTime = Time.time + 1f;
        nextFireballTime = Time.time + Random.Range(fireballCooldownMin, fireballCooldownMax);
    }

    // ================= Hình ảnh =================

    private void FaceVelocity()
    {
        Vector3 v = agent.velocity; v.y = 0f;
        if (v.sqrMagnitude > 0.2f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v), 540f * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        smoothSpeed = Mathf.Lerp(smoothSpeed, agent.enabled ? agent.velocity.magnitude : 0f, 1f - Mathf.Exp(-12f * Time.deltaTime));
        animator.SetFloat(SpeedId, smoothSpeed);
        animator.SetBool(GroundedId, true);
    }
}
