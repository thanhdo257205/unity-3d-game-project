using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Nền chung cho kẻ địch: máu (Damageable), cảnh giới, di chuyển bằng NavMesh, máy trạng thái
/// Tuần tra → Phát hiện → Tấn công → (Bỏ chạy) → Chết, loạng choạng khi bị quyền cước, rơi túi trữ vật khi chết.
/// Thanh máu kèm tên và cảnh giới do HUD vẽ. Rung camera, dừng hình, số sát thương dùng chung với đòn đánh của người chơi.
/// Lớp con (Thanh Lang, Tà tu...) viết phần hành vi ở các hàm OnPatrol, OnAttack, OnFlee...
/// </summary>
[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyBase : MonoBehaviour
{
    /// <summary>Các trạng thái chung của kẻ địch.</summary>
    public enum EnemyState { Patrol, Detect, Attack, Flee, Staggered, Dead }

    [Header("Thông tin")]
    [Tooltip("Tên hiển thị trên thanh máu.")]
    [SerializeField] private string displayName = "Kẻ địch";

    [Tooltip("Cảnh giới của kẻ địch (kéo một file RealmData vào). Quyết định hệ số chênh lệch cảnh giới với người chơi (Phần 5.3).")]
    [SerializeField] private RealmData realm;

    [Tooltip("Độ cao thanh máu so với chân (mét).")]
    [SerializeField] private float barHeight = 2.2f;

    [Header("Phát hiện")]
    [Tooltip("Thấy người chơi trong khoảng này (mét) thì chuyển sang Phát hiện.")]
    [SerializeField, Min(1f)] protected float detectRange = 15f;

    [Tooltip("Mất dấu người chơi bao nhiêu giây thì quay về tuần tra.")]
    [SerializeField, Min(0.5f)] protected float loseSightTime = 6f;

    [Tooltip("Độ cao mắt so với chân (mét), dùng để kiểm tra có bị vật cản che không.")]
    [SerializeField] private float eyeHeight = 1.4f;

    [Tooltip("Bật: phải nhìn thấy người chơi (không bị hộp/tường che) mới phát hiện được.")]
    [SerializeField] private bool requireLineOfSight = true;

    [Tooltip("Thời gian khựng lại nhìn người chơi trước khi lao vào tấn công (giây).")]
    [SerializeField, Min(0f)] protected float detectPause = 0.6f;

    [Header("Tuần tra")]
    [Tooltip("Bán kính khu vực tuần tra quanh chỗ đứng ban đầu (mét).")]
    [SerializeField, Min(1f)] protected float patrolRadius = 10f;

    [Tooltip("Tốc độ đi tuần (mét/giây).")]
    [SerializeField, Min(0.1f)] protected float patrolSpeed = 2f;

    [Tooltip("Đứng nghỉ ít nhất bấy nhiêu giây ở mỗi điểm tuần tra.")]
    [SerializeField, Min(0f)] protected float patrolWaitMin = 1.5f;

    [Tooltip("Đứng nghỉ nhiều nhất bấy nhiêu giây ở mỗi điểm tuần tra.")]
    [SerializeField, Min(0f)] protected float patrolWaitMax = 3.5f;

    [Header("Bị đánh")]
    [Tooltip("Thời gian loạng choạng khi bị quyền cước đánh trúng (giây). Đòn đang ra bị ngắt.")]
    [SerializeField, Min(0f)] protected float staggerTime = 0.8f;

    [Tooltip("Thời gian trượt lùi khi bị đẩy (giây).")]
    [SerializeField, Min(0.02f)] private float knockDuration = 0.15f;

    [Tooltip("Màu nháy khi bị đánh.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.1f);

    [Tooltip("Thời gian nháy (giây).")]
    [SerializeField, Min(0.01f)] private float flashTime = 0.15f;

    [Header("Chiến lợi phẩm")]
    [Tooltip("Prefab túi trữ vật rơi ra khi chết (có script LootPouch).")]
    [SerializeField] private LootPouch lootPrefab;

    [Tooltip("Số linh thạch ít nhất trong túi.")]
    [SerializeField, Min(0)] private int stonesMin = 3;

    [Tooltip("Số linh thạch nhiều nhất trong túi.")]
    [SerializeField, Min(0)] private int stonesMax = 6;

    [Tooltip("Túi trữ vật lơ lửng cao bao nhiêu so với mặt đất (mét).")]
    [SerializeField] private float lootHeight = 0.7f;

    /// <summary>Mọi kẻ địch trong scene.</summary>
    public static readonly List<EnemyBase> All = new List<EnemyBase>();

    /// <summary>Tắt thì mọi kẻ địch đứng yên (F7 để chụp ảnh).</summary>
    public static bool AIEnabled = true;

    protected Damageable damageable;
    protected NavMeshAgent agent;
    protected PlayerStats player;
    protected Transform playerT;
    protected Vector3 home;
    protected float lastSeenTime = -100f;
    protected float stateStartTime;

    private EnemyState state = EnemyState.Patrol;
    private EnemyState stateBeforeStagger = EnemyState.Attack;
    private float staggerEnd;
    private float detectEnd;
    private float nextPatrolTime;
    private Vector3 knockRemaining;
    private float knockTimer;
    private Collider[] colliders;
    private Renderer[] flashRenderers;
    private Color[] flashBase;
    private MaterialPropertyBlock block;
    private float flashTimer;
    private bool flashing;
    private bool wasAIEnabled = true;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // ----- Thông tin cho HUD và lớp con -----
    public string DisplayName => displayName;
    public string RealmName => realm != null ? realm.DisplayName : "";
    public RealmData Realm => realm;
    public int MajorRealm => realm != null ? realm.MajorRealm : 0;
    public EnemyState State => state;
    public bool IsAlive => damageable != null && damageable.IsAlive;
    public float HealthRatio => damageable.MaxHealth > 0f ? damageable.Health / damageable.MaxHealth : 0f;
    public Vector3 BarPosition => transform.position + Vector3.up * barHeight;
    public Vector3 Eye => transform.position + Vector3.up * eyeHeight;
    public float DistanceToPlayer => playerT != null ? Vector3.Distance(transform.position, playerT.position) : float.MaxValue;
    public Vector3 PlayerChest => playerT != null ? playerT.position + Vector3.up * 1.1f : transform.position + transform.forward;

    protected virtual void Awake()
    {
        damageable = GetComponent<Damageable>();
        agent = GetComponent<NavMeshAgent>();
        colliders = GetComponentsInChildren<Collider>();
        block = new MaterialPropertyBlock();
        CollectFlashRenderers();
    }

    protected virtual void OnEnable()
    {
        All.Add(this);
        damageable.Damaged += HandleDamaged;
        damageable.Died += HandleDied;
        damageable.Revived += HandleRevived;
        damageable.KnockedBack += HandleKnockedBack;
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
        damageable.Damaged -= HandleDamaged;
        damageable.Died -= HandleDied;
        damageable.Revived -= HandleRevived;
        damageable.KnockedBack -= HandleKnockedBack;
    }

    protected virtual void Start()
    {
        home = transform.position;
        player = PlayerStats.Instance != null ? PlayerStats.Instance : FindAnyObjectByType<PlayerStats>();
        playerT = player != null ? player.transform : null;
        PlaceOnNavMesh();
    }

    /// <summary>Đặt tác tử (agent) lên NavMesh tại chỗ đứng hiện tại.</summary>
    protected void PlaceOnNavMesh()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
        {
            agent.enabled = false;
            transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
        }
    }

    // ================= Cập nhật chung =================

    protected virtual void Update()
    {
        if (state == EnemyState.Dead) { OnDeadUpdate(); return; }
        if (playerT == null) return;

        // Hệ số chênh lệch cảnh giới: người chơi đánh kẻ địch này
        damageable.DamageTakenMultiplier = RealmRules.DamageMultiplier(player.MajorRealm, MajorRealm);
        UpdateKnock();
        UpdateFlash();

        if (!AIEnabled)
        {
            if (wasAIEnabled && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; }
            wasAIEnabled = false;
            return;
        }
        wasAIEnabled = true;

        if (!player.IsAlive && state != EnemyState.Patrol) SetState(EnemyState.Patrol);

        bool sees = player.IsAlive && CanSeePlayer();
        if (sees) lastSeenTime = Time.time;

        switch (state)
        {
            case EnemyState.Patrol:
                if (sees) { SetState(EnemyState.Detect); break; }
                UpdatePatrol();
                break;

            case EnemyState.Detect:
                OnDetect();
                if (Time.time >= detectEnd) SetState(EnemyState.Attack);
                break;

            case EnemyState.Attack:
                if (Time.time - lastSeenTime > loseSightTime) { SetState(EnemyState.Patrol); break; }
                OnAttack();
                break;

            case EnemyState.Flee:
                OnFlee();
                break;

            case EnemyState.Staggered:
                if (Time.time >= staggerEnd) SetState(stateBeforeStagger == EnemyState.Staggered ? EnemyState.Attack : stateBeforeStagger);
                break;
        }
    }

    /// <summary>Chuyển trạng thái, gọi OnEnterState để lớp con xử lý.</summary>
    protected void SetState(EnemyState next)
    {
        if (state == next) return;
        EnemyState prev = state;
        state = next;
        stateStartTime = Time.time;
        if (next == EnemyState.Detect) detectEnd = Time.time + detectPause;
        if (next == EnemyState.Patrol) nextPatrolTime = Time.time + 0.5f;
        if (agent.enabled && agent.isOnNavMesh && (next == EnemyState.Staggered || next == EnemyState.Dead)) { agent.ResetPath(); }
        OnEnterState(prev, next);
    }

    /// <summary>Báo cho kẻ địch biết người chơi đang ở đâu (đồng bọn gọi nhau).</summary>
    public void Alert()
    {
        if (state == EnemyState.Dead) return;
        lastSeenTime = Time.time;
        if (state == EnemyState.Patrol) SetState(EnemyState.Detect);
    }

    /// <summary>Có thấy người chơi không: trong tầm detectRange và (nếu bật) không bị vật cản che.</summary>
    protected bool CanSeePlayer()
    {
        if (playerT == null) return false;
        Vector3 flat = playerT.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > detectRange * detectRange) return false;
        if (!requireLineOfSight) return true;

        Vector3 from = Eye;
        Vector3 to = playerT.position + Vector3.up * 1.1f;
        RaycastHit hit;
        if (!Physics.Linecast(from, to, out hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        Transform t = hit.collider.transform;
        if (t.IsChildOf(playerT) || t.IsChildOf(transform)) return true;
        if (Damageable.TryGet(hit.collider, out _)) return true;   // đồng bọn/hình nộm không che tầm nhìn
        return false;
    }

    // ================= Tuần tra mặc định =================

    /// <summary>Tuần tra mặc định: đi ngẫu nhiên quanh chỗ ban đầu, nghỉ giữa các điểm. Lớp con có thể ghi đè OnPatrol.</summary>
    private void UpdatePatrol()
    {
        OnPatrol();
    }

    /// <summary>Đi tuần ngẫu nhiên. Có thể ghi đè (đàn sói đi theo con đầu đàn).</summary>
    protected virtual void OnPatrol()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
        if (Time.time < nextPatrolTime) return;
        if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.3f) return;

        Vector3 p;
        if (RandomPointAround(home, patrolRadius, out p)) agent.SetDestination(p);
        nextPatrolTime = Time.time + Random.Range(patrolWaitMin, patrolWaitMax);
    }

    /// <summary>Điểm ngẫu nhiên trên NavMesh quanh center.</summary>
    protected static bool RandomPointAround(Vector3 center, float r, out Vector3 point)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 c = Random.insideUnitCircle * r;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(center + new Vector3(c.x, 0f, c.y), out hit, 3f, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }
        point = center;
        return false;
    }

    // ================= Hành vi do lớp con viết =================

    /// <summary>Gọi mỗi khung hình ở trạng thái Phát hiện (thường là quay mặt về người chơi).</summary>
    protected virtual void OnDetect() { FacePlayer(540f); }

    /// <summary>Gọi mỗi khung hình ở trạng thái Tấn công.</summary>
    protected abstract void OnAttack();

    /// <summary>Gọi mỗi khung hình ở trạng thái Bỏ chạy (chỉ lớp con nào có Bỏ chạy mới cần).</summary>
    protected virtual void OnFlee() { }

    /// <summary>Gọi khi đổi trạng thái.</summary>
    protected virtual void OnEnterState(EnemyState from, EnemyState to) { }

    /// <summary>Hủy mọi đòn đang chuẩn bị (khi loạng choạng hoặc chết).</summary>
    protected virtual void CancelActions() { }

    /// <summary>Lớp con xử lý thêm khi bị đánh trúng.</summary>
    protected virtual void OnDamagedExtra(DamageInfo info) { }

    /// <summary>Lớp con xử lý khi chết (anim ngã...).</summary>
    protected virtual void OnDiedExtra() { }

    /// <summary>Gọi mỗi khung hình khi đã chết.</summary>
    protected virtual void OnDeadUpdate() { }

    /// <summary>Lớp con xử lý khi hồi sinh.</summary>
    protected virtual void OnRevivedExtra() { }

    // ================= Tiện ích cho lớp con =================

    /// <summary>Quay mặt về phía người chơi (chỉ quay quanh trục dọc).</summary>
    protected void FacePlayer(float degPerSec)
    {
        if (playerT == null) return;
        Vector3 d = playerT.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degPerSec * Time.deltaTime);
    }

    /// <summary>Đi tới pos với tốc độ speed (nếu pos không nằm trên NavMesh thì lấy điểm gần nhất).</summary>
    protected bool MoveTo(Vector3 pos, float speed)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return false;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, 4f, NavMesh.AllAreas)) pos = hit.position;
        agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(pos);
        return true;
    }

    /// <summary>Đứng lại.</summary>
    protected void StopMoving()
    {
        if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; }
    }

    /// <summary>Gây sát thương lên người chơi, đã nhân hệ số chênh lệch cảnh giới.</summary>
    protected void DealDamageToPlayer(float amount)
    {
        if (player == null) return;
        player.TakeDamage(amount * ScaledDamage());
    }

    /// <summary>Hệ số sát thương kẻ địch này gây ra cho người chơi (chênh lệch cảnh giới).</summary>
    protected float ScaledDamage()
    {
        return player != null ? RealmRules.DamageMultiplier(MajorRealm, player.MajorRealm) : 1f;
    }

    /// <summary>Làm choáng seconds giây (sét của Phá Không): giống loạng choạng nhưng thời gian do bên gọi quyết định.</summary>
    public void Stun(float seconds)
    {
        if (state == EnemyState.Dead || seconds <= 0f) return;
        if (state != EnemyState.Staggered) stateBeforeStagger = state == EnemyState.Patrol || state == EnemyState.Detect ? EnemyState.Attack : state;
        staggerEnd = Mathf.Max(staggerEnd, Time.time + seconds);
        CancelActions();
        SetState(EnemyState.Staggered);
    }

    /// <summary>Vào trạng thái loạng choạng trong staggerTime giây.</summary>
    protected void Stagger()
    {
        if (state == EnemyState.Dead || state == EnemyState.Staggered || staggerTime <= 0f) return;
        stateBeforeStagger = state == EnemyState.Patrol || state == EnemyState.Detect ? EnemyState.Attack : state;
        staggerEnd = Time.time + staggerTime;
        CancelActions();
        SetState(EnemyState.Staggered);
    }

    // ================= Sự kiện của Damageable =================

    private void HandleDamaged(DamageInfo info)
    {
        flashTimer = flashTime;
        lastSeenTime = Time.time;   // bị đánh thì biết người chơi ở đâu
        if (state == EnemyState.Patrol || state == EnemyState.Detect) SetState(EnemyState.Attack);
        if (info.melee && damageable.IsAlive) Stagger();
        OnDamagedExtra(info);
    }

    private void HandleDied(Damageable d)
    {
        CancelActions();
        SetState(EnemyState.Dead);
        if (agent.enabled) agent.enabled = false;
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = false;
        DropLoot();
        OnDiedExtra();
    }

    private void HandleRevived(Damageable d)
    {
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = true;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        agent.enabled = false;
        transform.position = home;
        agent.enabled = true;
        PlaceOnNavMesh();
        state = EnemyState.Patrol;
        stateStartTime = Time.time;
        lastSeenTime = -100f;
        flashTimer = 0f;
        OnRevivedExtra();
    }

    private void HandleKnockedBack(Vector3 delta)
    {
        knockRemaining = delta;
        knockTimer = knockDuration;
    }

    private void UpdateKnock()
    {
        if (knockTimer <= 0f) return;
        float step = Mathf.Min(Time.deltaTime, knockTimer);
        if (agent.enabled && agent.isOnNavMesh) agent.Move(knockRemaining * (step / knockDuration));
        else transform.position += knockRemaining * (step / knockDuration);
        knockTimer -= step;
    }

    private void DropLoot()
    {
        if (lootPrefab == null) return;
        Vector3 pos = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;
        LootPouch pouch = Instantiate(lootPrefab, pos + Vector3.up * lootHeight, Quaternion.identity);
        pouch.Amount = Random.Range(stonesMin, Mathf.Max(stonesMin, stonesMax) + 1);
    }

    // ================= Nháy đỏ khi bị đánh =================

    private void CollectFlashRenderers()
    {
        List<Renderer> list = new List<Renderer>();
        List<Color> colors = new List<Color>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.name.StartsWith("Mat")) continue;   // mắt phát sáng không nháy
            Material m = r.sharedMaterial;
            if (m == null || !m.HasProperty(BaseColorId)) continue;
            list.Add(r);
            colors.Add(m.GetColor(BaseColorId));
        }
        flashRenderers = list.ToArray();
        flashBase = colors.ToArray();
    }

    private void UpdateFlash()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / flashTime);
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] == null) continue;
                block.SetColor(BaseColorId, Color.Lerp(flashBase[i], flashColor, t));
                flashRenderers[i].SetPropertyBlock(block);
            }
            flashing = true;
        }
        else if (flashing)
        {
            for (int i = 0; i < flashRenderers.Length; i++)
                if (flashRenderers[i] != null) flashRenderers[i].SetPropertyBlock(null);
            flashing = false;
        }
    }

    // ================= Phím thử nghiệm =================

    /// <summary>Hồi sinh toàn bộ kẻ địch về chỗ ban đầu, đầy máu (F6).</summary>
    public static void ReviveAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] != null) All[i].damageable.Revive();
    }
}
