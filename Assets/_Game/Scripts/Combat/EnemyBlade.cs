using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phi kiếm tà tu: xếp thành kiếm luân sau lưng, truy kích người chơi bằng nhiều lượt bay cong,
/// hoặc rời trận ra đón kiếm đối thủ. Bị chặn/va kiếm/gặp vật cản thì quay về.
/// </summary>
public class EnemyBlade : MonoBehaviour, IEnemyProjectile
{
    [Header("Truy kích và phòng thủ")]
    [SerializeField, Min(1f)] private float pursuitDuration = 12f;
    [SerializeField, Min(30f)] private float turnSpeed = 540f;
    [SerializeField, Min(0f)] private float pursuitArc = 1.5f;
    [SerializeField, Min(0.2f)] private float hitInterval = 1.2f;
    [SerializeField, Min(1f)] private float maxPursuitRange = 65f;
    [SerializeField, Min(0.03f)] private float guardResponse = 0.07f;
    [SerializeField] private bool verticalFormation = true;
    private Damageable ownerLife;
    private float pursuitUntil;
    private float nextPassAt;
    private Vector3 escapePoint;
    private bool guarding;
    private Vector3 guardPosition;
    private Quaternion guardRotation;
    private float guardUntil;
    private float guardCooldownUntil;
    public int SuccessfulPasses { get; private set; }
    public Vector3 PreviousPosition { get; private set; }
    public bool IsGuarding => phase == Phase.Orbit && guarding && Time.time < guardUntil;
    public bool CanGuard => phase == Phase.Orbit && Time.time >= guardCooldownUntil;
    public Transform Owner => owner;

    private enum Phase { Orbit, Strike, Return }

    [Header("Bay quanh chủ")]
    [Tooltip("Bán kính vòng bay quanh chủ nhân (mét).")]
    [SerializeField, Min(0.2f)] private float orbitRadius = 1.1f;

    [Tooltip("Độ cao vòng bay so với chân chủ nhân (mét).")]
    [SerializeField] private float orbitHeight = 1.2f;

    [Tooltip("Tốc độ quay quanh chủ nhân (độ/giây). Chậm cho cảm giác lơ lửng.")]
    [SerializeField] private float orbitSpeed = 70f;

    [Tooltip("Độ nhạy khi bay về chỗ trong vòng (số lớn = bám sát hơn).")]
    [SerializeField, Min(0.1f)] private float orbitFollow = 10f;

    [Header("Lao vào người chơi")]
    [Tooltip("Bán kính va chạm: để chạm người chơi và để bị phi kiếm người chơi chặn (mét).")]
    [SerializeField, Min(0.05f)] private float radius = 0.35f;
    [SerializeField] private float bladeTipOffset = 0.87f;
    [SerializeField] private float bladeTailOffset = 0.13f;

    [Tooltip("Tốc độ bay về chỗ (mét/giây).")]
    [SerializeField, Min(1f)] private float returnSpeed = 18f;

    [Tooltip("Hệ hạt phát một lần khi bị chặn hoặc trúng (có thể để trống).")]
    [SerializeField] private ParticleSystem hitBurst;

    /// <summary>Mọi phi kiếm đỏ đang tồn tại (để xóa khi người chơi hồi sinh).</summary>
    private static readonly List<EnemyBlade> Blades = new List<EnemyBlade>();

    private Transform owner;
    private Phase phase = Phase.Orbit;
    private float slotAngle;
    private Vector3 strikeDir;
    private float strikeSpeed;
    private float damage;
    private PlayerStats target;
    private CharacterController targetBody;
    private Vector3 velocityDir = Vector3.forward;

    /// <summary>Đang chờ ở vòng quanh chủ, sẵn sàng được phóng.</summary>
    public bool IsAvailable => phase == Phase.Orbit && !IsGuarding && Time.time >= guardCooldownUntil;

    // ----- IEnemyProjectile -----
    public Vector3 Position => transform.position;
    public float Radius => radius;
    public bool IsDone => phase != Phase.Strike;
    public bool HasGuard { get; set; }
    public Vector3 Direction => velocityDir;
    public bool IsApproaching(Vector3 point) => phase == Phase.Strike && Vector3.Dot(velocityDir, point - transform.position) > 0f;

    /// <summary>Block: bị chặn thì quay về, không biến mất.</summary>
    public void Block()
    {
        if (hitBurst != null) hitBurst.Emit(8);
        BeginReturn();
    }

    private void OnEnable()
    {
        EnemyProjectiles.All.Add(this);
        Blades.Add(this);
    }

    private void OnDisable()
    {
        EnemyProjectiles.All.Remove(this);
        Blades.Remove(this);
    }

    /// <summary>Gắn vào chủ nhân. index/count để chia đều vị trí trên vòng.</summary>
    public void Setup(Transform ownerTransform, int index, int count, PlayerStats playerTarget)
    {
        owner = ownerTransform;
        ownerLife = owner != null ? owner.GetComponent<Damageable>() : null;
        slotAngle = 360f * index / Mathf.Max(1, count);
        target = playerTarget;
        targetBody = playerTarget != null ? playerTarget.GetComponent<CharacterController>() : null;
        phase = Phase.Orbit;
        transform.position = SlotPosition();
        PreviousPosition = transform.position;
    }

    /// <summary>Đặt lại về vòng quanh chủ (hồi sinh, bị loạng choạng...).</summary>
    public void ResetToOrbit()
    {
        phase = Phase.Orbit;
        guarding = false;
        guardCooldownUntil = 0f;
        HasGuard = false;
        if (owner != null) transform.position = SlotPosition();
    }

    /// <summary>Phóng về phía aimPoint với tốc độ speed và sát thương damage (đã nhân hệ số chênh lệch cảnh giới).</summary>
    public void Launch(Vector3 aimPoint, float speed, float damageAmount)
    {
        Vector3 to = aimPoint - transform.position;
        if (to.sqrMagnitude < 0.01f) to = owner != null ? owner.forward : Vector3.forward;
        strikeDir = to.normalized;
        strikeSpeed = speed;
        damage = damageAmount;
        pursuitUntil = Time.time + pursuitDuration;
        nextPassAt = Time.time;
        SuccessfulPasses = 0;
        guarding = false;
        velocityDir = strikeDir;
        HasGuard = false;
        phase = Phase.Strike;
        transform.rotation = Quaternion.LookRotation(strikeDir);
    }

    private Vector3 SlotPosition()
    {
        if (owner == null) return transform.position;
        float a = (slotAngle + Time.time * orbitSpeed) * Mathf.Deg2Rad;
        if (verticalFormation)
            return owner.position + Vector3.up * orbitHeight - owner.forward * 0.65f
                + (owner.right * Mathf.Sin(a) + Vector3.up * Mathf.Cos(a)) * orbitRadius;
        return owner.position + Vector3.up * orbitHeight + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * orbitRadius;
    }

    private void BeginReturn()
    {
        phase = Phase.Return;
        HasGuard = false;
        guarding = false;
    }

    public void SetGuard(Vector3 position, Quaternion rotation, float hold)
    {
        if (!CanGuard) return;
        guarding = true;
        guardPosition = position;
        guardRotation = rotation;
        guardUntil = Time.time + hold;
    }

    public void ClearGuard() { guarding = false; }

    public bool TryBlock(FlyingSword incoming, Vector3 from, Vector3 to)
    {
        if (!CanGuard || !IsGuarding) return false;
        Vector3 contact;
        if (!SwordFlightMath.HitsBlade(from, to, incoming.HitRadius,
            transform.position - transform.forward * bladeTailOffset,
            transform.position + transform.forward * bladeTipOffset, radius * 0.55f, out contact)) return false;
        incoming.Swarm.EmitBlockSparks(contact, -incoming.transform.forward);
        incoming.Deflect(0.22f);
        guarding = false;
        guardCooldownUntil = Time.time + 0.4f;
        return true;
    }

    /// <summary>Opposing attack swords can collide in mid-air; both are deflected and recalled.</summary>
    public static bool TryClash(FlyingSword incoming, Vector3 from, Vector3 to)
    {
        if (!incoming.IsAttacking) return false;
        for (int i = 0; i < Blades.Count; i++)
        {
            EnemyBlade enemy = Blades[i];
            if (enemy == null || enemy.IsDone || !enemy.isActiveAndEnabled) continue;
            Vector3 contact;
            bool hits = SwordFlightMath.HitsBlade(from, to, incoming.HitRadius,
                enemy.transform.position - enemy.transform.forward * enemy.bladeTailOffset,
                enemy.transform.position + enemy.transform.forward * enemy.bladeTipOffset, enemy.radius * 0.55f, out contact);
            if (!hits) hits = SwordFlightMath.HitsBlade(from, to, incoming.HitRadius,
                enemy.PreviousPosition, enemy.Position, enemy.radius * 0.55f, out contact);
            if (!hits) continue;
            incoming.Swarm.EmitBlockSparks(contact, -incoming.transform.forward);
            incoming.Deflect(0.25f);
            enemy.Block();
            return true;
        }
        return false;
    }

    private void Update()
    {
        if (owner == null) return;
        PreviousPosition = transform.position;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (ownerLife != null && !ownerLife.IsAlive) return;

        switch (phase)
        {
            case Phase.Orbit:
            {
                Vector3 slot = IsGuarding ? guardPosition : SlotPosition();
                Vector3 old = transform.position;
                transform.position = Vector3.Lerp(old, slot, 1f - Mathf.Exp(-(IsGuarding ? 4f / guardResponse : orbitFollow) * dt));
                Vector3 move = transform.position - old;
                // Mũi kiếm hướng theo chiều quay quanh chủ
                Vector3 tangent = verticalFormation ? slot - (owner.position + Vector3.up * orbitHeight - owner.forward * 0.65f)
                    : Vector3.Cross(Vector3.up, slot - (owner.position + Vector3.up * orbitHeight));
                if (IsGuarding) transform.rotation = Quaternion.Slerp(transform.rotation, guardRotation, 1f - Mathf.Exp(-35f * dt));
                else if (tangent.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(tangent.normalized), 1f - Mathf.Exp(-12f * dt));
                if (move.sqrMagnitude > 0.000001f) velocityDir = move.normalized;
                break;
            }
            case Phase.Strike:
            {
                StepPursuit(dt);
                break;
            }
            case Phase.Return:
            {
                Vector3 slot = SlotPosition();
                Vector3 to = slot - transform.position;
                float dist = to.magnitude;
                if (dist <= returnSpeed * dt + 0.3f) { phase = Phase.Orbit; break; }
                velocityDir = to / dist;
                transform.position += velocityDir * (returnSpeed * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocityDir), 1f - Mathf.Exp(-20f * dt));
                break;
            }
        }
    }

    private void StepPursuit(float dt)
    {
        if (target == null || !target.IsAlive || Time.time >= pursuitUntil
            || (target.transform.position - owner.position).sqrMagnitude > maxPursuitRange * maxPursuitRange)
        { BeginReturn(); return; }
        int steps = Mathf.Max(1, Mathf.CeilToInt(strikeSpeed * dt / 0.3f));
        float h = dt / steps;
        for (int i = 0; i < steps && phase == Phase.Strike; i++)
        {
            bool recovering = Time.time < nextPassAt;
            Vector3 center = targetBody != null ? targetBody.transform.TransformPoint(targetBody.center)
                : target.transform.position + Vector3.up * 1.1f;
            Vector3 aim = recovering ? escapePoint : center;
            velocityDir = SwordFlightMath.Steer(velocityDir, transform.position, aim,
                slotAngle * Mathf.Deg2Rad, Time.time, turnSpeed, recovering ? 0f : pursuitArc, h);
            float moveSpeed = strikeSpeed * Mathf.Lerp(0.25f, 1f,
                Mathf.Clamp01((aim - transform.position).magnitude / 4f));
            Vector3 from = transform.position, to = from + velocityDir * moveSpeed * h;
            transform.rotation = Quaternion.LookRotation(velocityDir, Vector3.up);
            if (ThanThongCombat.TryBlockProjectile(this, from, to)) return;
            SwordSwarm swarm = SwordSwarm.Instance;
            if (swarm != null && swarm.TryBlockIncoming(this, from, to)) return;
            for (int k = 0; k < FlyingSword.All.Count; k++)
            {
                FlyingSword other = FlyingSword.All[k];
                if (!other.IsAttacking) continue;
                Vector3 contact;
                if (!other.SweptOverlaps(from, to, radius * 0.55f, out contact)) continue;
                other.Swarm.EmitBlockSparks(contact, -velocityDir);
                other.Deflect(0.25f);
                Block();
                return;
            }
            if (SwordFlightMath.HitsEnvironment(from, to, radius * 0.5f, owner))
            { BeginReturn(); return; }
            transform.position = to;
            if (!recovering && SwordFlightMath.HitsPlayer(from, to, radius, target, targetBody))
            {
                if (target.TakeDamage(damage)) SuccessfulPasses++;
                if (hitBurst != null) hitBurst.Emit(8);
                nextPassAt = Time.time + hitInterval;
                Vector3 side = Vector3.Cross(Vector3.up, velocityDir).normalized;
                escapePoint = center + velocityDir * 4f + side * 2.5f + Vector3.up * 1.2f;
            }
        }
    }

    /// <summary>Cho mọi phi kiếm đỏ về lại vòng quanh chủ (người chơi hồi sinh).</summary>
    public static void RecallAll()
    {
        for (int i = 0; i < Blades.Count; i++)
            if (Blades[i] != null) Blades[i].ResetToOrbit();
    }
}
