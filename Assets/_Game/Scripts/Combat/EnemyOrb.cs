using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quả cầu năng lượng của địch (hình nộm bắn trả): bay thẳng với tốc độ cố định về phía người chơi.
/// Trúng người chơi thì trừ máu rồi biến mất. Bị phi kiếm chặn (SwordSwarm gọi <see cref="Block"/>) thì cũng biến mất.
/// Đụng vật cản (tường, hộp) thì tự tan. Bay quá lâu cũng tự tan.
/// </summary>
public class EnemyOrb : MonoBehaviour, IEnemyProjectile
{
    [Header("Thông số")]
    [Tooltip("Bán kính va chạm của quả cầu (mét). Dùng để kiểm tra trúng người chơi và trúng phi kiếm.")]
    [SerializeField, Min(0.01f)] private float radius = 0.2f;

    [Tooltip("Thời gian sống tối đa (giây). Quá thời gian này mà chưa trúng gì thì tự tan.")]
    [SerializeField, Min(0.5f)] private float maxLifetime = 6f;

    [Tooltip("Có tan khi đụng vật cản (mặt đất, tường, hộp) không.")]
    [SerializeField] private bool stopAtObstacles = true;

    [Header("Hiệu ứng")]
    [Tooltip("Hệ hạt phát một lần khi quả cầu tan (có thể để trống).")]
    [SerializeField] private ParticleSystem burst;

    [Tooltip("Số hạt phát ra khi tan.")]
    [SerializeField, Min(0)] private int burstCount = 12;

    /// <summary>Mọi quả cầu đang bay trong scene.</summary>
    public static readonly List<EnemyOrb> All = new List<EnemyOrb>();

    private Vector3 velocity;
    private float damage;
    private float age;
    private PlayerStats targetStats;
    private CharacterController targetBody;
    private bool done;

    /// <summary>Vị trí tâm quả cầu.</summary>
    public Vector3 Position => transform.position;
    /// <summary>Bán kính va chạm.</summary>
    public float Radius => radius;
    /// <summary>Hướng bay hiện tại (vector đơn vị).</summary>
    public Vector3 Direction => velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector3.forward;
    /// <summary>Đã trúng/bị chặn/tan rồi.</summary>
    public bool IsDone => done;
    /// <summary>Đã có một thanh kiếm được cử ra đón quả cầu này chưa (SwordSwarm dùng).</summary>
    public bool HasGuard { get; set; }

    private void OnEnable() { All.Add(this); EnemyProjectiles.All.Add(this); }
    private void OnDisable() { All.Remove(this); EnemyProjectiles.All.Remove(this); }

    /// <summary>Phóng quả cầu từ origin về phía aimPoint với tốc độ speed (m/s) và sát thương damage.</summary>
    public void Launch(Vector3 origin, Vector3 aimPoint, float speed, float damage, PlayerStats target)
    {
        transform.position = origin;
        Vector3 dir = aimPoint - origin;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        velocity = dir.normalized * speed;
        this.damage = damage;
        targetStats = target;
        targetBody = target != null ? target.GetComponent<CharacterController>() : null;
        age = 0f;
        done = false;
        HasGuard = false;
    }

    /// <summary>Có đang bay lại gần điểm point không (dùng để biết cầu nào cần đón).</summary>
    public bool IsApproaching(Vector3 point)
    {
        return Vector3.Dot(velocity, point - transform.position) > 0f;
    }

    /// <summary>Bị phi kiếm chặn: tan ngay.</summary>
    public void Block()
    {
        Dissolve();
    }

    private void Update()
    {
        if (done) return;
        age += Time.deltaTime;
        if (age >= maxLifetime) { Dissolve(); return; }

        Vector3 from = transform.position;
        Vector3 to = from + velocity * Time.deltaTime;

        if (ThanThongCombat.TryBlockProjectile(this, from, to)) return;
        SwordSwarm swarm = SwordSwarm.Instance;
        if (swarm != null && swarm.TryBlockIncoming(this, from, to)) return;

        if (stopAtObstacles && HitObstacle(from, to)) { transform.position = to; Dissolve(); return; }
        transform.position = to;

        if (HitsPlayer(to))
        {
            if (targetStats != null) targetStats.TakeDamage(damage);
            Dissolve();
        }
    }

    /// <summary>Quét đường đi trong khung hình này xem có đụng vật cản (không tính người chơi, kiếm, hình nộm) không.</summary>
    private bool HitObstacle(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.0001f) return false;
        RaycastHit hit;
        if (!Physics.SphereCast(from, radius * 0.5f, d / len, out hit, len, ~0, QueryTriggerInteraction.Ignore)) return false;
        Collider c = hit.collider;
        if (targetStats != null && c.transform.IsChildOf(targetStats.transform)) return false;
        Damageable dm;
        if (Damageable.TryGet(c, out dm)) return false;
        if (c.GetComponentInParent<FlyingSword>() != null) return false;
        return true;
    }

    /// <summary>Quả cầu có chạm vào thân người chơi (viên nang của CharacterController) không.</summary>
    private bool HitsPlayer(Vector3 center)
    {
        if (targetStats == null || !targetStats.IsAlive) return false;
        if (targetBody == null)
            return (targetStats.transform.position + Vector3.up - center).sqrMagnitude <= 0.5f * 0.5f;

        // Đoạn trục viên nang từ chân tới đầu
        Vector3 basePos = targetBody.transform.TransformPoint(targetBody.center);
        float half = Mathf.Max(0f, targetBody.height * 0.5f - targetBody.radius);
        Vector3 a = basePos - Vector3.up * half;
        Vector3 b = basePos + Vector3.up * half;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(center - a, ab) / ab.sqrMagnitude) : 0f;
        Vector3 closest = a + ab * t;
        float r = radius + targetBody.radius;
        return (center - closest).sqrMagnitude <= r * r;
    }

    private void Dissolve()
    {
        if (done) return;
        done = true;
        if (burst != null && burstCount > 0)
        {
            burst.transform.SetParent(null, true);
            burst.Emit(burstCount);
            Destroy(burst.gameObject, 1.5f);
        }
        Destroy(gameObject);
    }

    /// <summary>Xóa mọi quả cầu đang bay (khi người chơi hồi sinh).</summary>
    public static void DestroyAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] != null) Destroy(All[i].gameObject);
        All.Clear();
    }
}
