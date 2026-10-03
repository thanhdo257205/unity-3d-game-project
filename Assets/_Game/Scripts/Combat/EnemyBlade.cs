using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phi kiếm đỏ của tà tu: bay quanh chủ nhân theo vòng chậm; khi được lệnh thì lao vào người chơi
/// (kiếm bay thẳng với tốc độ cố định) rồi quay về. Chỉ bị chặn được khi đang lao đi (xem <see cref="IEnemyProjectile"/>):
/// phi kiếm người chơi chặn theo đúng luật Quạt/Vòng. Bị chặn thì lập tức quay về.
/// </summary>
public class EnemyBlade : MonoBehaviour, IEnemyProjectile
{
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

    [Tooltip("Lao đi quá điểm ngắm bao xa thì thu về (mét).")]
    [SerializeField, Min(0f)] private float overshoot = 3f;

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
    private Vector3 strikeEnd;
    private float strikeSpeed;
    private float damage;
    private PlayerStats target;
    private CharacterController targetBody;
    private Vector3 velocityDir = Vector3.forward;

    /// <summary>Đang chờ ở vòng quanh chủ, sẵn sàng được phóng.</summary>
    public bool IsAvailable => phase == Phase.Orbit;

    // ----- IEnemyProjectile -----
    public Vector3 Position => transform.position;
    public float Radius => radius;
    public bool IsDone => phase != Phase.Strike;
    public bool HasGuard { get; set; }
    public Vector3 Direction => velocityDir;
    public bool IsApproaching(Vector3 point) => phase == Phase.Strike && Vector3.Dot(strikeDir, point - transform.position) > 0f;

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
        slotAngle = 360f * index / Mathf.Max(1, count);
        target = playerTarget;
        targetBody = playerTarget != null ? playerTarget.GetComponent<CharacterController>() : null;
        phase = Phase.Orbit;
        transform.position = SlotPosition();
    }

    /// <summary>Đặt lại về vòng quanh chủ (hồi sinh, bị loạng choạng...).</summary>
    public void ResetToOrbit()
    {
        phase = Phase.Orbit;
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
        strikeEnd = aimPoint + strikeDir * overshoot;
        velocityDir = strikeDir;
        HasGuard = false;
        phase = Phase.Strike;
        transform.rotation = Quaternion.LookRotation(strikeDir);
    }

    private Vector3 SlotPosition()
    {
        if (owner == null) return transform.position;
        float a = (slotAngle + Time.time * orbitSpeed) * Mathf.Deg2Rad;
        return owner.position + Vector3.up * orbitHeight + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * orbitRadius;
    }

    private void BeginReturn()
    {
        phase = Phase.Return;
        HasGuard = false;
    }

    private void Update()
    {
        if (owner == null) return;
        float dt = Time.deltaTime;

        switch (phase)
        {
            case Phase.Orbit:
            {
                Vector3 slot = SlotPosition();
                Vector3 old = transform.position;
                transform.position = Vector3.Lerp(old, slot, 1f - Mathf.Exp(-orbitFollow * dt));
                Vector3 move = transform.position - old;
                // Mũi kiếm hướng theo chiều quay quanh chủ
                Vector3 tangent = Vector3.Cross(Vector3.up, slot - (owner.position + Vector3.up * orbitHeight));
                if (tangent.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(tangent.normalized), 1f - Mathf.Exp(-12f * dt));
                if (move.sqrMagnitude > 0.000001f) velocityDir = move.normalized;
                break;
            }
            case Phase.Strike:
            {
                Vector3 step = strikeDir * (strikeSpeed * dt);
                transform.position += step;
                if (EnemyProjectiles.HitsPlayer(transform.position, radius, target, targetBody))
                {
                    if (target != null) target.TakeDamage(damage);
                    if (hitBurst != null) hitBurst.Emit(8);
                    BeginReturn();
                }
                else if (Vector3.Dot(strikeEnd - transform.position, strikeDir) <= 0f) BeginReturn();
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

    /// <summary>Cho mọi phi kiếm đỏ về lại vòng quanh chủ (người chơi hồi sinh).</summary>
    public static void RecallAll()
    {
        for (int i = 0; i < Blades.Count; i++)
            if (Blades[i] != null) Blades[i].ResetToOrbit();
    }
}
