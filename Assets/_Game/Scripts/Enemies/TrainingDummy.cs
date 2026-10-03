using UnityEngine;

/// <summary>
/// Hình nộm tập bắn: bị đánh thì nháy đỏ và văng tia lửa, có thanh máu nổi phía trên.
/// Hết máu thì ngã xuống, sau <see cref="reviveDelay"/> giây thì đứng dậy với đầy máu.
/// Cần có <see cref="Damageable"/> cùng đối tượng.
/// </summary>
[RequireComponent(typeof(Damageable))]
public class TrainingDummy : MonoBehaviour
{
    [Header("Nháy đỏ khi bị đánh")]
    [Tooltip("Phần thân (Renderer) sẽ đổi màu khi bị đánh.")]
    [SerializeField] private Renderer bodyRenderer;

    [Tooltip("Màu nháy khi bị đánh.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.12f, 0.08f);

    [Tooltip("Độ sáng phát quang (emission) lúc nháy. Số lớn hơn 1 thì Bloom làm chói hơn.")]
    [SerializeField, Min(0f)] private float flashEmission = 2.5f;

    [Tooltip("Thời gian nháy đỏ (giây).")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.18f;

    [Header("Tia lửa")]
    [Tooltip("Hệ hạt tia lửa (Particle System) sẽ bắn ra tại điểm bị đánh.")]
    [SerializeField] private ParticleSystem sparks;

    [Tooltip("Số tia lửa mỗi lần bị đánh.")]
    [SerializeField, Min(0)] private int sparkCount = 14;

    [Header("Thanh máu")]
    [Tooltip("Đối tượng chứa thanh máu (luôn xoay mặt về phía camera).")]
    [SerializeField] private Transform healthBar;

    [Tooltip("Phần thanh máu màu (co lại khi mất máu). Pivot ở giữa, script tự dịch cho co về bên trái.")]
    [SerializeField] private Transform healthFill;

    [Tooltip("Chiều rộng thanh máu khi đầy (mét).")]
    [SerializeField, Min(0.1f)] private float barWidth = 1.2f;

    [Tooltip("Độ cao thanh máu so với chân hình nộm (mét).")]
    [SerializeField] private float barHeight = 2.6f;

    [Header("Hết máu và hồi lại")]
    [Tooltip("Sau bao nhiêu giây kể từ lúc ngã thì hồi sinh với đầy máu.")]
    [SerializeField, Min(0.1f)] private float reviveDelay = 3f;

    [Tooltip("Góc ngã khi hết máu (độ).")]
    [SerializeField] private float fallAngle = 80f;

    [Tooltip("Tốc độ ngã/đứng dậy (độ/giây).")]
    [SerializeField, Min(1f)] private float fallSpeed = 240f;

    [Header("Bị đẩy lùi")]
    [Tooltip("Thời gian trượt lùi khi bị đẩy (giây).")]
    [SerializeField, Min(0.02f)] private float knockDuration = 0.15f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Damageable damageable;
    private MaterialPropertyBlock block;
    private Color baseColor = Color.white;
    private float flashTimer;
    private float reviveTimer;
    private float tilt;
    private Quaternion baseRotation;
    private bool flashing;
    private Vector3 knockRemaining;
    private float knockTimer;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        block = new MaterialPropertyBlock();
        baseRotation = transform.rotation;
        if (sparks != null) sparks.Play(); // đã tắt phát liên tục, chỉ phát khi gọi Emit lúc bị đánh
        if (bodyRenderer != null && bodyRenderer.sharedMaterial != null && bodyRenderer.sharedMaterial.HasProperty(BaseColorId))
            baseColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
    }

    private void OnEnable()
    {
        damageable.Damaged += OnDamaged;
        damageable.Died += OnDied;
        damageable.KnockedBack += OnKnockedBack;
    }

    private void OnDisable()
    {
        damageable.Damaged -= OnDamaged;
        damageable.Died -= OnDied;
        damageable.KnockedBack -= OnKnockedBack;
    }

    private void OnKnockedBack(Vector3 delta)
    {
        knockRemaining = delta;
        knockTimer = knockDuration;
    }

    private void OnDamaged(DamageInfo info)
    {
        flashTimer = flashDuration;
        if (sparks != null && sparkCount > 0)
        {
            Transform t = sparks.transform;
            t.position = info.point;
            Vector3 back = -info.direction;
            if (back.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(back);
            sparks.Emit(sparkCount);
        }
    }

    private void OnDied(Damageable d)
    {
        reviveTimer = reviveDelay;
    }

    private void Update()
    {
        UpdateFlash();
        UpdateFall();
        UpdateKnock();
    }

    /// <summary>Trượt lùi đều trong knockDuration giây.</summary>
    private void UpdateKnock()
    {
        if (knockTimer <= 0f) return;
        float step = Mathf.Min(Time.deltaTime, knockTimer);
        transform.position += knockRemaining * (step / knockDuration);
        knockTimer -= step;
    }

    private void LateUpdate()
    {
        UpdateHealthBar();
    }

    /// <summary>Đổi màu thân từ đỏ về màu gốc trong thời gian nháy.</summary>
    private void UpdateFlash()
    {
        if (bodyRenderer == null) return;
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / flashDuration);
            block.SetColor(BaseColorId, Color.Lerp(baseColor, flashColor, t));
            block.SetColor(EmissionColorId, flashColor * (flashEmission * t));
            bodyRenderer.SetPropertyBlock(block);
            flashing = true;
        }
        else if (flashing)
        {
            bodyRenderer.SetPropertyBlock(null);
            flashing = false;
        }
    }

    /// <summary>Ngã khi chết, đếm ngược rồi hồi sinh và đứng dậy.</summary>
    private void UpdateFall()
    {
        if (!damageable.IsAlive)
        {
            reviveTimer -= Time.deltaTime;
            if (reviveTimer <= 0f) damageable.Revive();
        }
        float target = damageable.IsAlive ? 0f : fallAngle;
        tilt = Mathf.MoveTowards(tilt, target, fallSpeed * Time.deltaTime);
        // Ngã ra sau theo trục ngang của chính hình nộm, pivot ở chân nên không bị lún xuống đất
        transform.rotation = baseRotation * Quaternion.AngleAxis(tilt, Vector3.right);
    }

    /// <summary>Đặt thanh máu phía trên đầu, quay mặt về camera và co theo lượng máu.</summary>
    private void UpdateHealthBar()
    {
        if (healthBar == null) return;
        Camera cam = Camera.main;
        healthBar.position = transform.position + Vector3.up * barHeight;
        if (cam != null)
            healthBar.rotation = Quaternion.LookRotation(healthBar.position - cam.transform.position);

        float ratio = damageable.MaxHealth > 0f ? damageable.Health / damageable.MaxHealth : 0f;
        if (healthFill != null)
        {
            float w = barWidth * ratio;
            Vector3 s = healthFill.localScale;
            s.x = Mathf.Max(0.0001f, w);
            healthFill.localScale = s;
            Vector3 p = healthFill.localPosition;
            p.x = -(barWidth - w) * 0.5f;
            healthFill.localPosition = p;
        }
        healthBar.gameObject.SetActive(damageable.IsAlive);
    }
}
