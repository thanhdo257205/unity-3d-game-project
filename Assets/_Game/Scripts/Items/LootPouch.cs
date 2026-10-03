using UnityEngine;

/// <summary>
/// Túi trữ vật do kẻ địch rơi ra: khối nhỏ phát sáng vàng, lơ lửng nhấp nhô và xoay chậm.
/// Người chơi đi tới gần thì nhặt, cộng linh thạch (PlayerStats) và HUD hiện "+N linh thạch".
/// </summary>
public class LootPouch : MonoBehaviour
{
    [Header("Phần thưởng")]
    [Tooltip("Số linh thạch trong túi (EnemyBase gán lúc rơi ra).")]
    [SerializeField, Min(0)] private int amount = 5;

    [Header("Nhặt")]
    [Tooltip("Người chơi tới gần túi trong khoảng này (mét) thì tự nhặt.")]
    [SerializeField, Min(0.1f)] private float pickupRadius = 1.6f;

    [Tooltip("Sau khi rơi, đợi bao nhiêu giây mới nhặt được (để thấy túi rơi ra).")]
    [SerializeField, Min(0f)] private float pickupDelay = 0.4f;

    [Header("Hiệu ứng lơ lửng")]
    [Tooltip("Độ nhấp nhô lên xuống (mét).")]
    [SerializeField, Min(0f)] private float bobHeight = 0.12f;

    [Tooltip("Tốc độ nhấp nhô (chu kỳ mỗi giây).")]
    [SerializeField, Min(0f)] private float bobSpeed = 1.2f;

    [Tooltip("Tốc độ xoay chậm (độ/giây).")]
    [SerializeField] private float spinSpeed = 50f;

    private Vector3 basePosition;
    private float spawnTime;
    private float phase;

    /// <summary>Số linh thạch trong túi.</summary>
    public int Amount { get => amount; set => amount = Mathf.Max(0, value); }

    private void Start()
    {
        basePosition = transform.position;
        spawnTime = Time.time;
        phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + phase) * bobHeight);
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        if (Time.time - spawnTime < pickupDelay) return;
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null || !stats.IsAlive) return;

        Vector3 d = stats.transform.position + Vector3.up * 0.9f - basePosition;
        if (d.sqrMagnitude <= pickupRadius * pickupRadius)
        {
            stats.AddSpiritStones(amount);
            Destroy(gameObject);
        }
    }
}
