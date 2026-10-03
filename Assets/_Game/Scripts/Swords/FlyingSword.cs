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
/// - Mũi kiếm luôn quay theo hướng bay; khi đã yên vị thì theo hướng của đội hình (Quạt: chĩa ra ngoài).
/// - Khi tấn công: lao thẳng tới điểm đích, chém trúng thì gây sát thương rồi bay về.
/// Quy ước mô hình: pivot ở giữa chuôi, mũi kiếm là trục +Z, mặt phẳng lưỡi vuông góc trục Y.
/// </summary>
[DisallowMultipleComponent]
public class FlyingSword : MonoBehaviour
{
    [Header("Thân kiếm")]
    [Tooltip("Khoảng cách từ pivot (giữa chuôi) tới mũi kiếm, mét. Thanh Trúc Kiếm dài 1m nên là 0.87.")]
    [SerializeField] private float tipOffset = 0.87f;

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
        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        if (trail != null)
        {
            baseTrailWidth = trail.widthMultiplier;
            baseTrailTime = trail.time;
            trail.Clear();
        }
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
    public void ScheduleStrike(float launchTime, Vector3 aimPoint, float overshoot, float speed, float damage, float returnTime, float intentGain = 0f)
    {
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
        scheduled = false;
        guarding = false;
        Mode = SwordMode.Strike;
        strikeEnd = end;
        strikeSpeed = speed;
        strikeDamage = damage;
        strikeIntent = intentGain;
        returnSettleTime = returnTime;
        hitTimes.Clear();
    }

    /// <summary>Đang tạm rời đội hình để đón một quả cầu địch.</summary>
    public bool IsGuarding => guarding && Time.time < guardUntil;

    /// <summary>Bán kính vùng chạm quanh thân kiếm (mét).</summary>
    public float HitRadius => hitRadius;

    /// <summary>Cho kiếm bay ra vị trí pos, mũi hướng theo rot, chờ tối đa hold giây để chắn cầu.</summary>
    public void SetGuard(Vector3 pos, Quaternion rot, float hold, float settleTime)
    {
        guarding = true;
        guardPosition = pos;
        guardRotation = rot;
        guardUntil = Time.time + hold;
        guardSettleTime = settleTime;
    }

    /// <summary>Thôi đón cầu, về lại chỗ trong đội hình.</summary>
    public void ClearGuard()
    {
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
        Mode = SwordMode.Return;
        returnSettleTime = settleTime;
        Velocity *= Swarm != null ? Swarm.ReturnVelocityKeep : 0.15f;
    }

    /// <summary>Cập nhật một khung hình. Do SwordSwarm gọi.</summary>
    public void Tick(in SwordSlot slot, float dt)
    {
        Vector3 slotVelocity = Vector3.zero;
        if (hasLastSlot && dt > 0f)
            slotVelocity = Vector3.ClampMagnitude((slot.position - lastSlotPosition) / dt, 40f);
        lastSlotPosition = slot.position;
        hasLastSlot = true;

        UpdateSpeedFeel(dt);

        switch (Mode)
        {
            case SwordMode.Formation:
                if (scheduled && Time.time >= scheduledTime)
                {
                    BeginScheduledStrike();
                    StepStrike(dt);
                    break;
                }
                if (IsGuarding)
                {
                    // Đang đón cầu: bay thật nhanh ra điểm chắn, mũi hướng về phía cầu
                    SpringTo(guardPosition, Vector3.zero, guardSettleTime, dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation, guardRotation, 1f - Mathf.Exp(-30f * dt));
                    break;
                }
                SpringTo(slot.position, slotVelocity, Swarm.FormationSettleTime, dt);
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
            trail.widthMultiplier = Mathf.Lerp(baseTrailWidth * bodyScale, strikeTrailWidth, speedFeel);
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
        Vector3 to = scheduledAim - transform.position;
        float len = to.magnitude;
        Vector3 dir = len > 0.01f ? to / len : transform.forward;
        LaunchStrike(scheduledAim + dir * scheduledOvershoot, scheduledSpeed, scheduledDamage, scheduledReturnTime, scheduledIntent);
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
            transform.position = arrived ? strikeEnd : transform.position + dir * step;
            remainingStep -= step;

            CheckHits(strikeDamage, true, 0f);

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

            float last;
            bool hitBefore = hitTimes.TryGetValue(d, out last);
            if (hitBefore && (oncePerFlight || Time.time - last < interval)) continue;
            hitTimes[d] = Time.time;

            Vector3 dir = Velocity.sqrMagnitude > 0.01f ? Velocity.normalized : transform.forward;
            DamageInfo info;
            info.amount = damage;
            info.point = OverlapBuffer[i].ClosestPoint(b);
            info.direction = dir;
            d.TakeDamage(info);

            // Nhát chém tấn công (không phải chạm nhẹ của đội hình Vòng) thì có dừng hình và rung camera
            if (oncePerFlight) Swarm.NotifyStrikeHit(d, strikeIntent);
        }
    }
}
