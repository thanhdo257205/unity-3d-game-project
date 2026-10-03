using UnityEngine;

/// <summary>
/// Hình nộm biết đánh trả: cứ mỗi vài giây bắn một quả cầu đỏ chậm về phía người chơi (khi người chơi trong tầm).
/// Gắn cùng đối tượng với <see cref="Damageable"/> (hình nộm). Hình nộm ngã xuống thì không bắn.
/// </summary>
[RequireComponent(typeof(Damageable))]
public class ShooterDummy : MonoBehaviour
{
    [Header("Quả cầu")]
    [Tooltip("Prefab quả cầu địch (có script EnemyOrb).")]
    [SerializeField] private EnemyOrb orbPrefab;

    [Tooltip("Tốc độ bay của quả cầu (mét/giây).")]
    [SerializeField, Min(0.1f)] private float orbSpeed = 8f;

    [Tooltip("Sát thương của quả cầu khi trúng người chơi.")]
    [SerializeField, Min(0f)] private float orbDamage = 8f;

    [Header("Nhịp bắn")]
    [Tooltip("Cứ bao nhiêu giây thì bắn một quả.")]
    [SerializeField, Min(0.1f)] private float fireInterval = 2.5f;

    [Tooltip("Chỉ bắn khi người chơi ở trong khoảng cách này (mét).")]
    [SerializeField, Min(1f)] private float range = 25f;

    [Tooltip("Độ cao nơi quả cầu xuất hiện so với chân hình nộm (mét).")]
    [SerializeField] private float muzzleHeight = 1.5f;

    [Tooltip("Xoay mặt hình nộm về phía người chơi khi bắn (chỉ xoay quanh trục dọc).")]
    [SerializeField] private bool faceTarget = false;

    [Header("Màu báo hiệu")]
    [Tooltip("Phần thân sẽ sáng lên một thoáng trước khi bắn để người chơi biết sắp có đạn. Để trống nếu không cần.")]
    [SerializeField] private Renderer chargeRenderer;

    [Tooltip("Màu sáng lên trước khi bắn.")]
    [SerializeField] private Color chargeColor = new Color(1f, 0.15f, 0.1f);

    [Tooltip("Báo hiệu sớm bao nhiêu giây trước khi bắn.")]
    [SerializeField, Min(0f)] private float chargeTime = 0.4f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Damageable damageable;
    private PlayerStats target;
    private MaterialPropertyBlock block;
    private float nextFireTime;
    private bool charging;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        block = new MaterialPropertyBlock();
    }

    private void Start()
    {
        target = FindAnyObjectByType<PlayerStats>();
        // Lệch nhịp ngẫu nhiên để hai hình nộm không bắn cùng lúc
        nextFireTime = Time.time + Random.Range(0.5f, fireInterval);
    }

    private void Update()
    {
        if (target == null || orbPrefab == null) return;

        bool canShoot = damageable.IsAlive && target.IsAlive && InRange();
        if (!canShoot)
        {
            SetCharge(0f);
            if (Time.time >= nextFireTime) nextFireTime = Time.time + 0.3f;   // đợi người chơi vào tầm, không dồn đạn
            return;
        }

        float timeLeft = nextFireTime - Time.time;
        if (timeLeft <= chargeTime && chargeTime > 0f) SetCharge(1f - Mathf.Clamp01(timeLeft / chargeTime));
        else SetCharge(0f);

        if (faceTarget) FaceTarget();
        if (timeLeft <= 0f)
        {
            Fire();
            nextFireTime = Time.time + fireInterval;
            SetCharge(0f);
        }
    }

    private bool InRange()
    {
        Vector3 d = target.transform.position - transform.position;
        return d.sqrMagnitude <= range * range;
    }

    private void FaceTarget()
    {
        Vector3 d = target.transform.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
    }

    /// <summary>Bắn một quả cầu thẳng vào ngực người chơi (tại thời điểm bắn).</summary>
    private void Fire()
    {
        Vector3 origin = transform.position + Vector3.up * muzzleHeight;
        Vector3 aim = target.transform.position + Vector3.up * 1.1f;
        EnemyOrb orb = Instantiate(orbPrefab, origin, Quaternion.identity);
        orb.Launch(origin, aim, orbSpeed, orbDamage, target);
    }

    private void SetCharge(float amount)
    {
        if (chargeRenderer == null) return;
        if (amount > 0f)
        {
            block.SetColor(EmissionColorId, chargeColor * (amount * 3f));
            chargeRenderer.SetPropertyBlock(block);
            charging = true;
        }
        else if (charging)
        {
            chargeRenderer.SetPropertyBlock(null);
            charging = false;
        }
    }
}
