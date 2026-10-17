using System.Collections.Generic;
using UnityEngine;

/// <summary>Trạng thái của một thanh phi kiếm.</summary>
public enum SwordMode
{
    /// <summary>Đang ở trong đội hình (hoặc đang bay tới chỗ của mình trong đội hình).</summary>
    Formation,
    /// <summary>Đang lao đi tấn công theo đường thẳng.</summary>
    Strike,
    /// <summary>Vừa tấn công xong, đang bay về chỗ trong đội hình.</summary>
    Return
}

/// <summary>
/// Một thanh phi kiếm. Gắn ở gốc prefab (cùng với Trail Renderer).
/// Kiếm không tự cập nhật: SwordSwarm gọi <see cref="Tick"/> mỗi khung hình để mọi kiếm chạy đúng thứ tự.
/// - Trong đội hình: bay tới vị trí đích bằng lò xo (mượt, hơi nảy), không dịch chuyển tức thời.
/// - Mũi kiếm luôn quay theo hướng bay; khi đã yên vị thì theo hướng của đội hình (Kiếm Luân: chĩa ra ngoài).
/// - Khi có mục tiêu: cả đàn dùng chung đường lao thẳng và vòng lượn, bám theo mục tiêu.
/// - Bị chặn/va kiếm thì ngắt sát thương ngắn rồi nối đàn; gặp vật cản không xuyên qua.
/// - Không có mục tiêu thì phóng theo điểm ngắm.
/// Quy ước mô hình: pivot ở giữa chuôi, mũi kiếm là trục +Z, mặt phẳng lưỡi vuông góc trục Y.
/// </summary>
[DisallowMultipleComponent]
public class FlyingSword : MonoBehaviour
{
    public static readonly List<FlyingSword> All = new List<FlyingSword>();
    public Damageable PursuitTarget => pursuitTarget;
    public bool HasPursuitCommand => pursuing;
    public int SuccessfulPasses { get; private set; }
    public int Deflections { get; private set; }
    public bool IsAttacking => Mode == SwordMode.Strike && !retiring
        && Swarm != null && Swarm.isActiveAndEnabled && Swarm.IsActive;
    public IEnemyProjectile GuardedProjectile { get; private set; }

    private Damageable pursuitTarget;
    private bool pursuitHadTarget;
    private Vector3 combatCenter;
    private bool pursuing;
    private Damageable scheduledTarget;
    private bool scheduledPursuit;
    private TaTu pursuitDefender;
    private float nextPassAt;
    private Vector3 pursuitDirection;
    private int flockSlot;
    private SwordFlockPath flockPath;
    private bool retiring;

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); ClearGuard(); }

    /// <summary>Khoảng cách pivot - mũi của thanh Trúc Kiếm (mét); SwordSwarm dùng để tính bán kính vầng Kiếm Luân khi chưa có kiếm nào.</summary>
    public const float DefaultTipOffset = 0.87f;

    [Header("Thân kiếm")]
    [Tooltip("Khoảng cách từ pivot (giữa chuôi) tới mũi kiếm, mét. Thanh Trúc Kiếm dài 1m nên là 0.87.")]
    [SerializeField] private float tipOffset = DefaultTipOffset;

    [Header("Độ sáng và mờ dần")]
    [Tooltip("Vật liệu trong suốt (alpha blend) giống hệt vật liệu kiếm, dùng khi kiếm đang mờ dần (camera ở sau lưng). Kiếm đậm 100% thì dùng lại vật liệu đặc gốc. Để trống thì kiếm không mờ được.")]
    [SerializeField] private Material fadeMaterial;

    [Tooltip("Khoảng cách từ pivot tới đuôi chuôi, mét (số âm = phía sau pivot).")]
    [SerializeField] private float tailOffset = -0.13f;

    [Tooltip("Bán kính vùng chém quanh thân kiếm (mét). Lớn hơn thì dễ trúng hơn.")]
    [SerializeField, Min(0.01f)] private float hitRadius = 0.18f;

    [Header("Xoay mũi kiếm")]
    [Tooltip("Độ nhạy khi xoay mũi kiếm về hướng cần tới. Số lớn = quay gắt, số nhỏ = quay uể oải.")]
    [SerializeField, Min(0.1f)] private float turnSharpness = 14f;

    [Tooltip("Tốc độ bay (so với chỗ đứng trong đội hình) từ mức này trở lên thì mũi kiếm bắt đầu hướng theo hướng bay (mét/giây).")]
    [SerializeField, Min(0f)] private float minFlySpeed = 0.6f;

    [Tooltip("Từ tốc độ này trở lên thì mũi kiếm hướng hoàn toàn theo hướng bay (mét/giây).")]
    [SerializeField, Min(0.1f)] private float fullFlySpeed = 3f;

    [Tooltip("Độ nhạy xoay khi đang lao đi tấn công. Số lớn để mũi kiếm quay vào mục tiêu gần như ngay lập tức.")]
    [SerializeField, Min(0.1f)] private float strikeTurnSharpness = 35f;

    [Header("Vệt sáng")]
    [Tooltip("Trail Renderer tạo vệt sáng sau kiếm. Để trống thì tự tìm trên cùng đối tượng.")]
    [SerializeField] private TrailRenderer trail;

    [Header("Cảm giác tốc độ khi lao đi")]
    [Tooltip("Độ rộng vệt sáng khi đang lao đi tấn công (mét). Lớn hơn độ rộng lúc ở trong đội hình thì vệt 'phình ra' cho cảm giác vút.")]
    [SerializeField, Min(0f)] private float strikeTrailWidth = 0.16f;

    [Tooltip("Độ dài vệt sáng khi đang lao đi (giây). Dài hơn lúc ở trong đội hình.")]
    [SerializeField, Min(0f)] private float strikeTrailTime = 0.3f;

    [Tooltip("Thân kiếm được kéo dài theo hướng bay bao nhiêu lần khi đang lao đi (1 = không kéo). Như vệt mờ chuyển động.")]
    [SerializeField, Min(1f)] private float strikeStretch = 1.4f;

    [Tooltip("Độ nhạy khi chuyển giữa dáng 'bay nhanh' và dáng bình thường. Số lớn = chuyển gắt.")]
    [SerializeField, Min(0.1f)] private float feelSharpness = 16f;

    [Tooltip("Phần mô hình kiếm sẽ được kéo dài. Để trống thì lấy đối tượng con đầu tiên.")]
    [SerializeField] private Transform model;

    private static readonly Collider[] OverlapBuffer = new Collider[24];

    private readonly Dictionary<Damageable, float> hitTimes = new Dictionary<Damageable, float>();

    // Thông số của đòn tấn công đang thực hiện
    private Vector3 strikeEnd;
    private float strikeSpeed;
    private float strikeDamage;
    private float returnSettleTime;

    // Đòn tấn công hẹn giờ (dùng cho loạt kiếm Mũi tên, mỗi kiếm cách nhau một chút)
    private bool scheduled;
    private float scheduledTime;
    private Vector3 scheduledAim;
    private float scheduledOvershoot;
    private float scheduledSpeed;
    private float scheduledDamage;
    private float scheduledReturnTime;
    private float scheduledIntent;
    private float strikeIntent;

    // Chế độ đón cầu: kiếm tạm rời chỗ trong đội hình để chắn đường một quả cầu địch
    private bool guarding;
    private Vector3 guardPosition;
    private Quaternion guardRotation;
    private float guardUntil;
    private float guardSettleTime = 0.12f;

    // Rung nhẹ khi chặn được cầu (chỉ rung phần hình ảnh)
    private float shakeAmplitude;
    private float shakeEndTime;
    private float shakeDuration = 0.25f;

    // Để tính vận tốc của chỗ đứng trong đội hình
    private Vector3 lastSlotPosition;
    private bool hasLastSlot;

    // Cảm giác tốc độ: 0 = dáng bình thường, 1 = đang lao hết tốc lực
    private float speedFeel;
    private float baseTrailWidth = 0.07f;
    private float baseTrailTime = 0.22f;
    private float currentStretch = 1f;
    private float bodyScale = 1f;       // kích thước hiện tại của thân kiếm (1 = kích thước thật)
    private bool scaleInitialized;

    // Độ sáng và mờ dần: vật liệu đặc gốc + vật liệu trong suốt, đổi qua lại; màu/emission đặt qua MaterialPropertyBlock
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private Renderer modelRenderer;
    private Material solidMaterial;
    private Color baseEmission = Color.black;
    private Color baseTint = Color.white;
    private MaterialPropertyBlock look;
    private float alpha = 1f;               // độ đậm hiện tại của thân kiếm (1 = đặc)
    private bool usingFadeMaterial;
    private float appliedAlpha = -1f;
    private float appliedBoost = -1f;
    private bool lookReady;

    // Bay ngược về túi trữ vật rồi biến mất (khi tắt Ngự Kiếm Thuật)
    private float despawnDuration = 0.25f;
    private float despawnElapsed;
    private Vector3 despawnStart;
    private float despawnStartScale = 1f;
    private float spawnTime;               // lúc sinh ra: một lúc đầu bay tới chỗ đứng nhanh hơn bình thường

    /// <summary>Giờ (Time.time) kiếm này tới lượt bay về túi khi tắt Ngự Kiếm Thuật; âm = chưa phải về.</summary>
    public float RetireAt = -1f;

    /// <summary>Đàn kiếm sở hữu thanh kiếm này.</summary>
    public SwordSwarm Swarm { get; private set; }

    /// <summary>Số thứ tự trong đàn (quyết định chỗ đứng trong đội hình).</summary>
    public int Index { get; set; }

    /// <summary>Trạng thái hiện tại.</summary>
    public SwordMode Mode { get; private set; } = SwordMode.Formation;

    /// <summary>Vận tốc hiện tại (mét/giây).</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>Đang chờ tới giờ để lao đi.</summary>
    public bool HasScheduledStrike => scheduled;

    /// <summary>Vị trí mũi kiếm trong thế giới (đã tính phần kéo dài khi đang lao).</summary>
    public Vector3 Tip => transform.position + transform.forward * (tipOffset * LengthScale);

    /// <summary>Hệ số độ dài thân kiếm: kích thước hiện tại (nhỏ trong đội hình, to dần khi phóng) nhân với phần kéo dài khi lao.</summary>
    private float LengthScale => currentStretch * bodyScale;

    /// <summary>Gọi một lần khi đàn kiếm sinh ra thanh kiếm này.</summary>
    public void Init(SwordSwarm swarm, int index)
    {
        Swarm = swarm;
        Index = index;
        spawnTime = Time.time;
        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        InitLook();
        if (trail != null)
        {
            baseTrailWidth = trail.widthMultiplier;
            baseTrailTime = trail.time;
            trail.Clear();
        }
    }

    /// <summary>Tìm renderer của thân kiếm và nhớ màu emission gốc để nhân lên khi kiếm đứng trong đội hình.</summary>
    private void InitLook()
    {
        if (lookReady) return;
        lookReady = true;
        modelRenderer = model != null ? model.GetComponentInChildren<Renderer>() : null;
        if (modelRenderer == null) return;
        solidMaterial = modelRenderer.sharedMaterial;
        if (solidMaterial != null && solidMaterial.HasProperty(BaseColorId)) baseTint = solidMaterial.GetColor(BaseColorId);
        if (solidMaterial != null && solidMaterial.HasProperty(EmissionColorId)) baseEmission = solidMaterial.GetColor(EmissionColorId);
        look = new MaterialPropertyBlock();
        alpha = Swarm != null ? Swarm.FormationOpacity : 1f;
    }

    /// <summary>
    /// Độ đậm và độ sáng của thân kiếm. Kiếm đứng trong đội hình thì theo độ đậm của đàn (mờ khi camera ở sau lưng);
    /// kiếm đang phóng đi / bay về / đón cầu / bay về túi thì luôn đậm 100%. Độ sáng (emission) nhân với "formationEmissionBoost".
    /// </summary>
    private void UpdateLook(float dt, bool alwaysSolid)
    {
        if (modelRenderer == null || Swarm == null) return;
        bool inFormation = !alwaysSolid && Mode == SwordMode.Formation && !IsGuarding && !scheduled;
        float target = inFormation ? Swarm.FormationOpacity : 1f;
        float rate = (1f - Swarm.BehindOpacity) / Mathf.Max(0.01f, Swarm.OpacityFadeTime);
        alpha = Mathf.MoveTowards(alpha, target, rate * dt);
        if (fadeMaterial == null) alpha = 1f;

        float boost = Swarm.FormationEmissionBoost;
        if (Mathf.Approximately(alpha, appliedAlpha) && Mathf.Approximately(boost, appliedBoost)) return;
        appliedAlpha = alpha;
        appliedBoost = boost;

        bool transparent = alpha < 0.999f;
        if (transparent != usingFadeMaterial)
        {
            usingFadeMaterial = transparent;
            modelRenderer.sharedMaterial = transparent ? fadeMaterial : solidMaterial;
        }
        look.SetColor(BaseColorId, new Color(baseTint.r, baseTint.g, baseTint.b, alpha));
        look.SetColor(EmissionColorId, baseEmission * boost);
        modelRenderer.SetPropertyBlock(look, 0);
    }

    /// <summary>Báo đội hình vừa đổi: bỏ vận tốc cũ của chỗ đứng để không bị giật.</summary>
    public void OnFormationChanged()
    {
        hasLastSlot = false;
    }

    /// <summary>Đặt kiếm về trạng thái đội hình ngay lập tức (không lao nữa, hủy hẹn giờ).</summary>
    public void CancelAndBeginReturn(float settleTime)
    {
        scheduled = false;
        BeginReturn(settleTime);
    }

    /// <summary>Hẹn giờ lao tới điểm aimPoint (kiếm vẫn đứng trong đội hình cho tới giờ).</summary>
    public void ScheduleStrike(float launchTime, Vector3 aimPoint, float overshoot, float speed, float damage, float returnTime, float intentGain = 0f, Damageable target = null)
    {
        scheduledTarget = target;
        scheduledPursuit = target != null;
        scheduled = true;
        scheduledTime = launchTime;
        scheduledAim = aimPoint;
        scheduledOvershoot = overshoot;
        scheduledSpeed = speed;
        scheduledDamage = damage;
        scheduledReturnTime = returnTime;
        scheduledIntent = intentGain;
    }

    /// <summary>Lao thẳng từ vị trí hiện tại tới điểm end với tốc độ speed. intentGain là Kiếm ý nhận được khi trúng.</summary>
    public void LaunchStrike(Vector3 end, float speed, float damage, float returnTime, float intentGain = 0f)
    {
        pursuitTarget = null;
        pursuitHadTarget = false;
        pursuitDefender = null;
        pursuing = true;
        retiring = false;
        scheduledPursuit = false;
        scheduled = false;
        ClearGuard();
        Mode = SwordMode.Strike;
        combatCenter = end;
        flockPath = null;
        pursuitDirection = (end - transform.position).sqrMagnitude > 0.001f
            ? (end - transform.position).normalized : transform.forward;
        nextPassAt = Time.time;
        strikeEnd = end;
        strikeSpeed = speed;
        strikeDamage = damage;
        strikeIntent = intentGain;
        returnSettleTime = returnTime;
        hitTimes.Clear();
    }

    /// <summary>Keep pursuing through curved attack passes until the player explicitly recalls.</summary>
    public void LaunchPursuit(Damageable target, float speed, float damage, float returnTime, float intentGain = 0f)
    {
        if (target == null || !target.IsAlive) return;
        LaunchStrike(target.AimPoint, speed, damage, returnTime, intentGain);
        JoinFlock(target, target.AimPoint);
        pursuitTarget = target;
        pursuitHadTarget = true;
        pursuing = true;
        pursuitDefender = target.GetComponent<TaTu>();
        nextPassAt = Time.time;
        SuccessfulPasses = 0;
        pursuitDirection = (target.AimPoint - transform.position).normalized;
        Velocity = pursuitDirection * strikeSpeed;
    }

    public bool IsApproaching(Vector3 point) => IsAttacking &&
        Vector3.Dot(Velocity, point - transform.position) > 0f;

    public bool SweptOverlaps(Vector3 from, Vector3 to, float radius, out Vector3 contact)
    {
        return SwordFlightMath.HitsBlade(from, to, radius,
            transform.position + transform.forward * (tailOffset * LengthScale), Tip,
            hitRadius * bodyScale, out contact);
    }

    /// <summary>Đang tạm rời đội hình để đón một quả cầu địch.</summary>
    public bool IsGuarding => guarding && Time.time < guardUntil;

    /// <summary>Bán kính vùng chạm quanh thân kiếm (mét).</summary>
    public float HitRadius => hitRadius;

    /// <summary>Cho kiếm bay ra vị trí pos, mũi hướng theo rot, chờ tối đa hold giây để chắn cầu.</summary>
    public void SetGuard(Vector3 pos, Quaternion rot, float hold, float settleTime, IEnemyProjectile projectile = null)
    {
        if (GuardedProjectile != projectile) ClearGuard();
        GuardedProjectile = projectile;
        guarding = true;
        guardPosition = pos;
        guardRotation = rot;
        guardUntil = Time.time + hold;
        guardSettleTime = settleTime;
    }

    /// <summary>Thôi đón cầu, về lại chỗ trong đội hình.</summary>
    public void ClearGuard()
    {
        if (GuardedProjectile != null) GuardedProjectile.HasGuard = false;
        GuardedProjectile = null;
        guarding = false;
    }

    /// <summary>Làm kiếm rung nhẹ (phần hình ảnh) trong duration giây, biên độ amplitude mét.</summary>
    public void Shake(float amplitude, float duration)
    {
        shakeAmplitude = amplitude;
        shakeDuration = Mathf.Max(0.02f, duration);
        shakeEndTime = Time.time + shakeDuration;
    }

    /// <summary>Thân kiếm (đoạn từ đuôi chuôi tới mũi) có chạm quả cầu tâm center bán kính radius không? closest là điểm gần nhất trên thân kiếm.</summary>
    public bool OverlapsSphere(Vector3 center, float radius, out Vector3 closest)
    {
        Vector3 a = transform.position + transform.forward * (tailOffset * LengthScale);
        Vector3 b = Tip;
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float t = len2 > 0.0001f ? Mathf.Clamp01(Vector3.Dot(center - a, ab) / len2) : 0f;
        closest = a + ab * t;
        float r = hitRadius * bodyScale + radius;
        return (center - closest).sqrMagnitude <= r * r;
    }

    /// <summary>Bay về chỗ trong đội hình, mất khoảng settleTime giây để yên vị.</summary>
    public void BeginReturn(float settleTime)
    {
        pursuing = false;
        pursuitTarget = null;
        pursuitDefender = null;
        scheduledTarget = null;
        scheduled = false;
        ClearGuard();
        Mode = SwordMode.Return;
        returnSettleTime = settleTime;
        Velocity *= Swarm != null ? Swarm.ReturnVelocityKeep : 0.15f;
    }

    /// <summary>A deflection interrupts this pass, but keeps the player's pursuit order alive.</summary>
    public void Deflect(float settleTime)
    {
        Deflections++;
        if (HasPursuitCommand && Swarm != null && Swarm.IsActive)
        {
            // Recoil at the battle, never fly home between attack passes.
            ClearGuard();
            Mode = SwordMode.Strike;
            pursuing = true;
            // A block pauses damage, but the blade stays in its file on the shared loop.
            nextPassAt = Time.time + 0.55f;
            Velocity = pursuitDirection * strikeSpeed;
            return;
        }
        BeginReturn(settleTime);
    }

    /// <summary>Bắt đầu bay về túi trữ vật rồi biến mất trong duration giây. Sau đó SwordSwarm gọi <see cref="TickDespawn"/> mỗi khung hình.</summary>
    public void BeginDespawn(float duration)
    {
        retiring = true;
        pursuing = false;
        pursuitTarget = null;
        pursuitDefender = null;
        scheduledTarget = null;
        scheduled = false;
        ClearGuard();
        despawnDuration = Mathf.Max(0.05f, duration);
        despawnElapsed = 0f;
        despawnStart = transform.position;
        despawnStartScale = bodyScale;
    }

    /// <summary>Cập nhật chuyến bay về túi. Trả về true khi kiếm đã tới nơi (nên xóa đi).</summary>
    public bool TickDespawn(Vector3 target, float dt)
    {
        despawnElapsed += dt;
        UpdateLook(dt, true);   // đang bay về túi: luôn hiện rõ
        float u = Mathf.Clamp01(despawnElapsed / despawnDuration);
        float e = u * u;   // ban đầu chậm rồi vút về túi
        Vector3 pos = Vector3.Lerp(despawnStart, target, e);
        Vector3 to = target - pos;
        if (to.sqrMagnitude > 0.0004f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 1f - Mathf.Exp(-30f * dt));
        transform.position = pos;
        if (model != null)
        {
            float sc = Mathf.Lerp(despawnStartScale, despawnStartScale * 0.2f, e);
            model.localScale = new Vector3(sc, sc, sc);
        }
        return u >= 1f;
    }

    /// <summary>Cập nhật một khung hình. Do SwordSwarm gọi.</summary>
    public void Tick(in SwordSlot slot, float dt)
    {
        if (guarding && (!IsGuarding || (GuardedProjectile != null && GuardedProjectile.IsDone))) ClearGuard();
        Vector3 slotVelocity = Vector3.zero;
        if (hasLastSlot && dt > 0f)
            slotVelocity = Vector3.ClampMagnitude((slot.position - lastSlotPosition) / dt, 40f);
        lastSlotPosition = slot.position;
        hasLastSlot = true;

        UpdateSpeedFeel(dt);
        UpdateLook(dt, false);

        switch (Mode)
        {
            case SwordMode.Formation:
                if (scheduled && Time.time >= scheduledTime)
                {
                    BeginScheduledStrike();
                    if (Mode == SwordMode.Strike) StepStrike(dt);
                    break;
                }
                if (IsGuarding)
                {
                    // Đang đón cầu: bay thật nhanh ra điểm chắn, mũi hướng về phía cầu
                    SpringTo(guardPosition, Vector3.zero, guardSettleTime, dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation, guardRotation, 1f - Mathf.Exp(-30f * dt));
                    break;
                }
                // Mới bay ra khỏi túi: vào chỗ nhanh hơn để bánh xe mở ra như một vầng sáng
                float settle = Time.time - spawnTime < Swarm.DeploySettleTime * 3f ? Swarm.DeploySettleTime : Swarm.FormationSettleTime;
                SpringTo(slot.position, slotVelocity, settle, dt);
                Orient(slot.rotation, slotVelocity, dt);
                if (Swarm.ActiveFormation.ContactDamage)
                    CheckHits(Swarm.RingContactDamage, false, Swarm.ContactHitInterval);
                break;

            case SwordMode.Strike:
                StepStrike(dt);
                break;

            case SwordMode.Return:
                SpringTo(slot.position, slotVelocity, returnSettleTime, dt);
                Orient(slot.rotation, slotVelocity, dt);
                if ((slot.position - transform.position).sqrMagnitude < 0.35f * 0.35f)
                    Mode = SwordMode.Formation;
                break;
        }
    }

    /// <summary>Khi đang lao đi tấn công: vệt sáng rộng và dài hơn, thân kiếm kéo dài ra như vệt mờ chuyển động.</summary>
    private void UpdateSpeedFeel(float dt)
    {
        float target = Mode == SwordMode.Strike ? 1f : 0f;
        speedFeel = Mathf.Lerp(speedFeel, target, 1f - Mathf.Exp(-feelSharpness * dt));
        currentStretch = Mathf.Lerp(1f, strikeStretch, speedFeel);
        UpdateBodyScale(dt);

        if (model != null)
        {
            model.localScale = new Vector3(bodyScale, bodyScale, bodyScale * currentStretch);
            // Rung nhẹ khi chặn cầu: lệch ngẫu nhiên, tắt dần
            if (Time.time < shakeEndTime)
            {
                float fade = (shakeEndTime - Time.time) / shakeDuration;
                model.localPosition = Random.insideUnitSphere * (shakeAmplitude * fade);
            }
            else if (model.localPosition.sqrMagnitude > 0f) model.localPosition = Vector3.zero;
        }
        if (trail != null)
        {
            trail.widthMultiplier = Mathf.Lerp(baseTrailWidth * bodyScale, strikeTrailWidth, speedFeel) * alpha;   // mờ cùng thân kiếm
            trail.time = Mathf.Lerp(baseTrailTime, strikeTrailTime, speedFeel);
        }
    }

    /// <summary>Trong đội hình kiếm nhỏ lại (formationSwordScale); lao đi thì to dần về cỡ thật, thu về thì nhỏ dần lại.</summary>
    private void UpdateBodyScale(float dt)
    {
        float small = Swarm != null ? Swarm.FormationSwordScale : 1f;
        if (!scaleInitialized) { bodyScale = small; scaleInitialized = true; }
        bool big = Mode == SwordMode.Strike;
        float target = big ? 1f : small;
        float time = big ? (Swarm != null ? Swarm.ScaleGrowTime : 0.2f) : (Swarm != null ? Swarm.ScaleShrinkTime : 0.35f);
        float rate = Mathf.Abs(1f - small) / Mathf.Max(0.01f, time);
        bodyScale = Mathf.MoveTowards(bodyScale, target, rate * dt);
    }

    /// <summary>Đến giờ hẹn: tính điểm kết thúc xuyên qua mục tiêu rồi lao đi.</summary>
    private void BeginScheduledStrike()
    {
        Damageable target = scheduledTarget;
        if (scheduledPursuit && (target == null || !target.IsAlive || !target.isActiveAndEnabled))
        {
            scheduled = false;
            scheduledTarget = null;
            scheduledPursuit = false;
            return;
        }
        Vector3 to = scheduledAim - transform.position;
        float len = to.magnitude;
        Vector3 dir = len > 0.01f ? to / len : transform.forward;
        if (target != null && target.IsAlive)
            LaunchPursuit(target, Swarm.PursuitSpeed, scheduledDamage, scheduledReturnTime, scheduledIntent);
        else LaunchStrike(scheduledAim + dir * scheduledOvershoot, scheduledSpeed, scheduledDamage, scheduledReturnTime, scheduledIntent);
        if (Swarm != null) Swarm.NotifySwordLaunched();   // mỗi kiếm trong loạt rời đội hình là một nhịp giật tay
    }

    /// <summary>Lò xo giảm chấn: kéo kiếm tới target, theo kịp chuyển động của chỗ đứng. Độc lập với FPS.</summary>
    private void SpringTo(Vector3 target, Vector3 targetVelocity, float settleTime, float dt)
    {
        float z = Swarm.DampingRatio;
        // Xấp xỉ thời gian ổn định ≈ 4 / (hệ số giảm chấn × tần số)
        float w = 4f / Mathf.Max(0.05f, z * Mathf.Max(0.05f, settleTime));
        int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
        float h = dt / steps;

        Vector3 pos = transform.position;
        Vector3 vel = Velocity;
        for (int i = 0; i < steps; i++)
        {
            Vector3 acc = w * w * (target - pos) + 2f * z * w * (targetVelocity - vel);
            vel += acc * h;
            pos += vel * h;
        }

        float maxSpeed = Swarm.MaxSwordSpeed;
        if (vel.sqrMagnitude > maxSpeed * maxSpeed) vel = vel.normalized * maxSpeed;
        transform.position = pos;
        Velocity = vel;
    }

    /// <summary>
    /// Mũi kiếm quay theo hướng bay (so với chỗ đứng); khi đã yên vị thì theo hướng của đội hình.
    /// </summary>
    private void Orient(Quaternion slotRotation, Vector3 slotVelocity, float dt)
    {
        Vector3 rel = Velocity - slotVelocity;
        float speed = rel.magnitude;
        float t = Mathf.InverseLerp(minFlySpeed, fullFlySpeed, speed);

        Quaternion flightRotation = slotRotation;
        if (speed > 0.01f)
            flightRotation = Quaternion.LookRotation(rel / speed, slotRotation * Vector3.up);

        Quaternion target = Quaternion.Slerp(slotRotation, flightRotation, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSharpness * dt));
    }

    /// <summary>Lao thẳng tới strikeEnd, chia nhỏ bước để không xuyên qua mục tiêu khi tốc độ cao.</summary>
    private void StepStrike(float dt)
    {
        if (pursuing)
        {
            StepPursuit(dt);
            return;
        }
        float remainingStep = strikeSpeed * dt;
        const float maxSubStep = 0.4f;
        Vector3 dir = transform.forward;

        while (remainingStep > 0f)
        {
            Vector3 to = strikeEnd - transform.position;
            float dist = to.magnitude;
            if (dist > 0.0001f) dir = to / dist;

            float step = Mathf.Min(remainingStep, maxSubStep);
            bool arrived = dist <= step;
            Vector3 from = transform.position;
            Vector3 toPosition = arrived ? strikeEnd : from + dir * step;
            if (EnemyBlade.TryClash(this, from, toPosition)) return;
            if (SwordFlightMath.HitsEnvironment(from, toPosition, HitRadius, Swarm.Owner))
            { BeginReturn(returnSettleTime); return; }
            transform.position = toPosition;
            remainingStep -= step;

            CheckHits(strikeDamage, true, 0f);
            if (Mode != SwordMode.Strike) return;

            if (arrived)
            {
                Velocity = dir * strikeSpeed;
                BeginReturn(returnSettleTime);
                return;
            }
        }

        Velocity = dir * strikeSpeed;
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-strikeTurnSharpness * dt));
    }

    private void JoinFlock(Damageable target, Vector3 point)
    {
        flockPath = Swarm.GetFlockPath(target);
        flockSlot = flockPath.Join(Swarm, this, target, point);
    }

    private void StepPursuit(float dt)
    {
        // A finished target ends this flight command, including destroyed/despawned enemies.
        if (pursuitHadTarget && (pursuitTarget == null || !pursuitTarget.IsAlive || !pursuitTarget.isActiveAndEnabled))
        {
            BeginReturn(returnSettleTime);
            return;
        }
        if (Swarm.LockedTarget != null && Swarm.LockedTarget.IsAlive && Swarm.LockedTarget != pursuitTarget)
        {
            JoinFlock(Swarm.LockedTarget, Swarm.LockedTarget.AimPoint);
            pursuitTarget = Swarm.LockedTarget;
            pursuitDefender = pursuitTarget.GetComponent<TaTu>();
        }
        if (pursuitTarget == null || !pursuitTarget.IsAlive || !pursuitTarget.isActiveAndEnabled)
        {
            pursuitTarget = Swarm.FindPursuitTarget(combatCenter, 30f);
            if (pursuitTarget != null) JoinFlock(pursuitTarget, pursuitTarget.AimPoint);
            pursuitDefender = pursuitTarget != null ? pursuitTarget.GetComponent<TaTu>() : null;
        }
        if (pursuitTarget != null) combatCenter = pursuitTarget.AimPoint;
        if (pursuitTarget != null) pursuitHadTarget = true;
        if (flockPath == null) JoinFlock(pursuitTarget, combatCenter);
        Vector3 tangent;
        Vector3 routePoint = flockPath.Sample(pursuitTarget, flockSlot, strikeSpeed, out tangent);
        Vector3 frameStart = transform.position;
        // Catch up smoothly when joining from the defensive wheel; never teleport onto the lane.
        Vector3 frameEnd = Vector3.MoveTowards(frameStart, routePoint, strikeSpeed * 1.65f * dt);
        int substeps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(frameStart, frameEnd) / 0.3f));
        for (int i = 0; i < substeps && Mode == SwordMode.Strike; i++)
        {
            bool recovering = Time.time < nextPassAt;
            Vector3 next = Vector3.Lerp(frameStart, frameEnd, (i + 1f) / substeps);
            Vector3 step = next - transform.position;
            pursuitDirection = step.sqrMagnitude > 0.000001f ? step.normalized : tangent;
            float moveSpeed = dt > 0f ? Vector3.Distance(frameStart, frameEnd) / dt : 0f;
            Vector3 from = transform.position, to = next;
            Velocity = pursuitDirection * moveSpeed;
            transform.rotation = Quaternion.LookRotation(pursuitDirection, Vector3.up);
            if (EnemyBlade.TryClash(this, from, to)) return;
            if (pursuitDefender != null && pursuitDefender.TryBlockSword(this, from, to)) return;
            if (SwordFlightMath.HitsEnvironment(from, to, HitRadius * 0.5f, Swarm.Owner))
            { Deflect(returnSettleTime); return; }
            transform.position = to;
            if (recovering || pursuitTarget == null) continue;
            CheckHits(strikeDamage, false, Swarm.PursuitHitInterval);
            if (Mode != SwordMode.Strike) return;
            if (Time.time < nextPassAt) continue;
            if (pursuitTarget != null && (pursuitTarget.AimPoint - transform.position).sqrMagnitude < 0.5f)
                BeginPassRecovery();
        }
    }

    private void BeginPassRecovery()
    {
        nextPassAt = Time.time + Swarm.PursuitHitInterval;
    }

    /// <summary>Kiểm tra thân kiếm có chạm vật nào có thể bị đánh không và gây sát thương.</summary>
    private void CheckHits(float damage, bool oncePerFlight, float interval)
    {
        if (damage <= 0f) return;
        Vector3 a = transform.position + transform.forward * (tailOffset * LengthScale);
        Vector3 b = Tip;
        int n = Physics.OverlapCapsuleNonAlloc(a, b, hitRadius * bodyScale, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Damageable d;
            if (!Damageable.TryGet(OverlapBuffer[i], out d) || !d.IsAlive) continue;
            if (pursuitTarget != null && d != pursuitTarget) continue;

            TaTu defender = d.GetComponent<TaTu>();
            if (defender != null && defender.TryBlockSword(this, a, b)) return;

            float last;
            bool hitBefore = hitTimes.TryGetValue(d, out last);
            if (hitBefore && (oncePerFlight || Time.time - last < interval)) continue;
            hitTimes[d] = Time.time;

            Vector3 dir = Velocity.sqrMagnitude > 0.01f ? Velocity.normalized : transform.forward;
            DamageInfo info = new DamageInfo { amount = damage, point = OverlapBuffer[i].ClosestPoint(b), direction = dir, melee = false };
            d.TakeDamage(info);

            // Nhát chém tấn công (không phải chạm nhẹ của đội hình Vòng) thì có dừng hình và rung camera
            if (oncePerFlight || pursuitTarget != null) Swarm.NotifyStrikeHit(d, strikeIntent);
            if (pursuitTarget != null)
            {
                SuccessfulPasses++;
                BeginPassRecovery();
                return;
            }
        }
    }
}
