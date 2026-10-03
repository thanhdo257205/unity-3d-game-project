using UnityEngine;

/// <summary>
/// Thanh Lang: yêu thú bầy đàn, tạm dùng hình khối (không có anim xương).
/// Trong đàn (ThanhLangPack): đi tuần theo đàn, phát hiện người chơi thì cả đàn vào vây, tản ra các hướng khác nhau,
/// rồi lần lượt từng con lao vào cắn (chồm tới), cắn xong lùi ra.
/// Nhún thân khi chạy, chồm tới khi cắn đều bằng code (di chuyển phần "Thân").
/// </summary>
public class ThanhLang : EnemyBase
{
    private enum WolfAction { Circling, Lunging, Biting, Retreating }

    [Header("Chiến đấu")]
    [Tooltip("Tốc độ chạy (mét/giây).")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 7f;

    [Tooltip("Sát thương mỗi cú cắn (đã nhân hệ số chênh lệch cảnh giới).")]
    [SerializeField, Min(0f)] private float biteDamage = 8f;

    [Tooltip("Tới gần người chơi trong khoảng này (mét) thì chồm cắn.")]
    [SerializeField, Min(0.3f)] private float biteRange = 1.8f;

    [Tooltip("Thời gian cú chồm cắn (giây).")]
    [SerializeField, Min(0.1f)] private float biteDuration = 0.45f;

    [Tooltip("Cú cắn gây sát thương sau bao nhiêu giây kể từ lúc bắt đầu chồm.")]
    [SerializeField, Min(0f)] private float biteHitTime = 0.18f;

    [Tooltip("Lao mãi không tới thì bỏ cuộc sau bấy nhiêu giây.")]
    [SerializeField, Min(0.5f)] private float lungeTimeout = 3f;

    [Tooltip("Lùi ra cách người chơi bao xa (mét) thì coi là xong và quay lại vòng vây.")]
    [SerializeField, Min(1f)] private float retreatDistance = 4.5f;

    [Tooltip("Lùi tối đa bấy nhiêu giây.")]
    [SerializeField, Min(0.2f)] private float retreatTimeout = 1.6f;

    [Tooltip("Phải ở gần điểm vây trong khoảng này (mét) mới được lao vào.")]
    [SerializeField, Min(0.5f)] private float slotTolerance = 2.2f;

    [Header("Nhún thân (hình ảnh)")]
    [Tooltip("Phần thân hình khối sẽ nhún và chồm (đối tượng con 'Than').")]
    [SerializeField] private Transform body;

    [Tooltip("Độ nhún lên xuống khi chạy (mét).")]
    [SerializeField, Min(0f)] private float bobHeight = 0.07f;

    [Tooltip("Nhịp nhún theo tốc độ chạy (số nhịp mỗi mét).")]
    [SerializeField, Min(0f)] private float bobPerMeter = 2.2f;

    [Tooltip("Độ cao cú chồm (mét).")]
    [SerializeField, Min(0f)] private float pounceHeight = 0.4f;

    [Tooltip("Cú chồm lao tới trước bao xa (mét).")]
    [SerializeField, Min(0f)] private float pounceForward = 0.7f;

    [Tooltip("Độ ngã khi chết (độ).")]
    [SerializeField] private float deathRoll = 85f;

    private ThanhLangPack pack;
    private int packIndex;
    private WolfAction action = WolfAction.Circling;
    private float actionStart;
    private float biteTime;
    private bool biteDealt;
    private float nextPathTime;
    private Vector3 bodyBasePos;
    private float bobPhase;
    private float deathT;

    /// <summary>Đang lao hoặc cắn (đàn chỉ cho một con lao mỗi lần).</summary>
    public bool IsLunging => action == WolfAction.Lunging || action == WolfAction.Biting;

    /// <summary>Sẵn sàng được chọn để lao: đang vây, đã vào vị trí.</summary>
    public bool CanLunge
    {
        get
        {
            if (!IsAlive || State != EnemyState.Attack || action != WolfAction.Circling || playerT == null) return false;
            Vector3 slot = pack != null ? pack.SlotPositionFor(this, playerT.position) : playerT.position;
            Vector3 d = slot - transform.position; d.y = 0f;
            return d.magnitude <= slotTolerance;
        }
    }

    /// <summary>Đàn gọi để gắn con sói vào đàn.</summary>
    public void JoinPack(ThanhLangPack p, int index)
    {
        pack = p;
        packIndex = index;
    }

    protected override void Awake()
    {
        base.Awake();
        if (body == null && transform.childCount > 0) body = transform.GetChild(0);
        if (body != null) bodyBasePos = body.localPosition;
        agent.acceleration = 40f;
        agent.angularSpeed = 540f;
    }

    // ================= Hành vi =================

    protected override void OnPatrol()
    {
        if (pack == null) { base.OnPatrol(); return; }
        // Đi theo điểm tuần tra chung của đàn
        Vector3 p = pack.PatrolPositionFor(packIndex);
        if (Time.time >= nextPathTime)
        {
            MoveTo(p, patrolSpeed);
            nextPathTime = Time.time + 0.7f;
        }
    }

    protected override void OnEnterState(EnemyState from, EnemyState to)
    {
        if (to == EnemyState.Detect || to == EnemyState.Attack)
        {
            if (pack != null) pack.AlertAll(this);
        }
        if (to == EnemyState.Attack) SetAction(WolfAction.Circling);
        if (to == EnemyState.Patrol) { action = WolfAction.Circling; ResetBody(); }
    }

    protected override void OnAttack()
    {
        float speed = chaseSpeed * (pack != null ? pack.SpeedMultiplier : 1f);
        switch (action)
        {
            case WolfAction.Circling:
            {
                Vector3 slot = pack != null ? pack.SlotPositionFor(this, playerT.position) : playerT.position;
                if (Time.time >= nextPathTime) { MoveTo(slot, speed); nextPathTime = Time.time + 0.2f; }
                // Tới chỗ rồi thì quay mặt về người chơi
                if (!agent.pathPending && agent.remainingDistance < 1f) FacePlayer(540f);
                break;
            }
            case WolfAction.Lunging:
            {
                if (Time.time >= nextPathTime) { MoveTo(playerT.position, speed * 1.15f); nextPathTime = Time.time + 0.1f; }
                Vector3 d = playerT.position - transform.position; d.y = 0f;
                if (d.magnitude <= biteRange) { StartBite(); break; }
                if (Time.time - actionStart > lungeTimeout) EndLunge();
                break;
            }
            case WolfAction.Biting:
            {
                StopMoving();
                FacePlayer(720f);
                float t = Time.time - actionStart;
                if (!biteDealt && t >= biteHitTime)
                {
                    biteDealt = true;
                    Vector3 d = playerT.position - transform.position; d.y = 0f;
                    if (d.magnitude <= biteRange + 0.8f) DealDamageToPlayer(biteDamage);
                }
                if (t >= biteDuration) SetAction(WolfAction.Retreating);
                break;
            }
            case WolfAction.Retreating:
            {
                Vector3 away = transform.position - playerT.position; away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                if (Time.time >= nextPathTime) { MoveTo(playerT.position + away.normalized * (retreatDistance + 1f), speed); nextPathTime = Time.time + 0.2f; }
                if (away.magnitude >= retreatDistance || Time.time - actionStart > retreatTimeout) EndLunge();
                break;
            }
        }
    }

    /// <summary>Đàn gọi: con này được phép lao vào cắn.</summary>
    public void BeginLunge()
    {
        if (State != EnemyState.Attack) return;
        SetAction(WolfAction.Lunging);
    }

    private void StartBite()
    {
        biteDealt = false;
        SetAction(WolfAction.Biting);
    }

    /// <summary>Lao/cắn xong: quay về vòng vây, báo đàn để tính nhịp.</summary>
    private void EndLunge()
    {
        SetAction(WolfAction.Circling);
        if (pack != null) pack.OnLungeFinished();
    }

    private void SetAction(WolfAction a)
    {
        action = a;
        actionStart = Time.time;
        nextPathTime = 0f;
    }

    protected override void CancelActions()
    {
        bool wasLunging = IsLunging;
        action = WolfAction.Circling;
        ResetBody();
        if (wasLunging && pack != null) pack.OnLungeFinished();
    }

    protected override void OnDiedExtra()
    {
        if (pack != null) pack.OnWolfDied();
        deathT = 0f;
    }

    protected override void OnRevivedExtra()
    {
        action = WolfAction.Circling;
        deathT = 0f;
        ResetBody();
    }

    // ================= Hình ảnh =================

    private void ResetBody()
    {
        if (body != null) { body.localPosition = bodyBasePos; body.localRotation = Quaternion.identity; }
    }

    private void LateUpdate()
    {
        if (body == null) return;

        if (State == EnemyState.Dead)
        {
            // Ngã nghiêng sang một bên
            deathT = Mathf.MoveTowards(deathT, 1f, Time.deltaTime * 3f);
            body.localRotation = Quaternion.Euler(0f, 0f, deathRoll * deathT);
            body.localPosition = bodyBasePos + Vector3.down * (0.12f * deathT);
            return;
        }

        Vector3 pos = bodyBasePos;
        Quaternion rot = Quaternion.identity;

        if (action == WolfAction.Biting)
        {
            // Chồm tới: nhảy lên và lao về trước, đầu chúi xuống
            float u = Mathf.Clamp01((Time.time - actionStart) / biteDuration);
            float arc = Mathf.Sin(u * Mathf.PI);
            pos += Vector3.up * (pounceHeight * arc) + Vector3.forward * (pounceForward * Mathf.Sin(Mathf.Min(1f, u * 1.4f) * Mathf.PI * 0.5f));
            rot = Quaternion.Euler(-25f * arc, 0f, 0f);
        }
        else if (agent.enabled)
        {
            float speed = agent.velocity.magnitude;
            if (speed > 0.3f)
            {
                bobPhase += speed * bobPerMeter * Time.deltaTime * Mathf.PI * 2f;
                pos += Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight);
                rot = Quaternion.Euler(Mathf.Sin(bobPhase) * 4f, 0f, 0f);
            }
        }
        body.localPosition = pos;
        body.localRotation = rot;
    }
}
