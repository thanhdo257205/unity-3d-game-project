using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Đàn Thanh Lang: điều phối cả đàn.
/// - Đi tuần: cả đàn theo một điểm tuần tra chung, mỗi con đứng lệch một chút.
/// - Bao vây: mỗi con được chia một hướng riêng quanh người chơi, các hướng chia đều và xoay chậm.
/// - Lao vào cắn: mỗi lần chỉ MỘT con được lao; con kế tiếp phải đợi hết khoảng nghỉ.
/// - Một con chết thì các con còn lại hung hăng hơn một lúc (nhanh hơn, lao dồn dập hơn).
/// Gắn vào đối tượng cha chứa các con sói (các con tự được tìm trong đối tượng con).
/// </summary>
public class ThanhLangPack : MonoBehaviour
{
    [Header("Bao vây")]
    [Tooltip("Bán kính vòng vây quanh người chơi (mét).")]
    [SerializeField, Min(1f)] private float circleRadius = 5f;

    [Tooltip("Tốc độ cả vòng vây xoay quanh người chơi (độ/giây).")]
    [SerializeField] private float circleSpin = 20f;

    [Header("Nhịp lao vào cắn")]
    [Tooltip("Khoảng nghỉ giữa hai lần có sói lao vào (giây). Chỉ một con lao mỗi lần.")]
    [SerializeField, Min(0.1f)] private float attackInterval = 1.4f;

    [Tooltip("Ngẫu nhiên thêm tối đa bấy nhiêu giây vào khoảng nghỉ để nhịp không đều.")]
    [SerializeField, Min(0f)] private float attackIntervalJitter = 0.6f;

    [Header("Hung hăng khi đồng bọn chết")]
    [Tooltip("Thời gian cả đàn hung hăng sau khi một con chết (giây).")]
    [SerializeField, Min(0f)] private float aggressionTime = 3f;

    [Tooltip("Hệ số tốc độ chạy khi hung hăng.")]
    [SerializeField, Min(1f)] private float aggressionSpeedMultiplier = 1.3f;

    [Tooltip("Hệ số rút ngắn khoảng nghỉ giữa các lần lao khi hung hăng (0.5 = nhanh gấp đôi).")]
    [SerializeField, Range(0.1f, 1f)] private float aggressionIntervalMultiplier = 0.5f;

    [Header("Đi tuần")]
    [Tooltip("Bán kính khu vực tuần tra của cả đàn quanh chỗ đứng ban đầu (mét).")]
    [SerializeField, Min(1f)] private float patrolRadius = 10f;

    [Tooltip("Cứ bao nhiêu giây thì cả đàn đổi điểm tuần tra.")]
    [SerializeField, Min(1f)] private float patrolPeriod = 8f;

    [Tooltip("Mỗi con đứng cách điểm tuần tra chung bao xa (mét).")]
    [SerializeField, Min(0f)] private float patrolSpread = 1.8f;

    private readonly List<ThanhLang> wolves = new List<ThanhLang>();
    private Vector3 home;
    private Vector3 patrolCenter;
    private float nextPatrolChange;
    private float nextLungeTime;
    private float aggressionUntil;
    private float baseAngle;

    /// <summary>Đàn đang hung hăng (vừa có đồng bọn chết).</summary>
    public bool IsAggressive => Time.time < aggressionUntil;

    /// <summary>Hệ số tốc độ hiện tại của cả đàn.</summary>
    public float SpeedMultiplier => IsAggressive ? aggressionSpeedMultiplier : 1f;

    /// <summary>Bán kính vòng vây.</summary>
    public float CircleRadius => circleRadius;

    /// <summary>Bán kính tuần tra của đàn.</summary>
    public float PatrolRadius => patrolRadius;

    private void Awake()
    {
        GetComponentsInChildren(true, wolves);
        for (int i = 0; i < wolves.Count; i++) wolves[i].JoinPack(this, i);
    }

    private void Start()
    {
        home = transform.position;
        if (wolves.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < wolves.Count; i++) sum += wolves[i].transform.position;
            home = sum / wolves.Count;
        }
        patrolCenter = home;
    }

    private void Update()
    {
        UpdatePatrolCenter();
        baseAngle += circleSpin * Time.deltaTime;
        if (!EnemyBase.AIEnabled) return;
        UpdateLunge();
    }

    private void UpdatePatrolCenter()
    {
        if (Time.time < nextPatrolChange) return;
        nextPatrolChange = Time.time + patrolPeriod * Random.Range(0.8f, 1.2f);
        for (int i = 0; i < 6; i++)
        {
            Vector2 c = Random.insideUnitCircle * patrolRadius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(home + new Vector3(c.x, 0f, c.y), out hit, 3f, NavMesh.AllAreas)) { patrolCenter = hit.position; return; }
        }
    }

    /// <summary>Điểm tuần tra của con thứ index: quanh điểm chung của đàn.</summary>
    public Vector3 PatrolPositionFor(int index)
    {
        float a = (index * (360f / Mathf.Max(1, wolves.Count))) * Mathf.Deg2Rad;
        return patrolCenter + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * patrolSpread;
    }

    /// <summary>Điểm vây của một con quanh người chơi: các con còn sống chia đều các hướng khác nhau.</summary>
    public Vector3 SlotPositionFor(ThanhLang wolf, Vector3 playerPos)
    {
        int alive = 0, rank = 0;
        for (int i = 0; i < wolves.Count; i++)
        {
            if (wolves[i] == null || !wolves[i].IsAlive) continue;
            if (wolves[i] == wolf) rank = alive;
            alive++;
        }
        alive = Mathf.Max(1, alive);
        float a = (baseAngle + 360f * rank / alive) * Mathf.Deg2Rad;
        return playerPos + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * circleRadius;
    }

    /// <summary>Một con báo động thì cả đàn biết.</summary>
    public void AlertAll(ThanhLang except)
    {
        for (int i = 0; i < wolves.Count; i++)
            if (wolves[i] != null && wolves[i] != except && wolves[i].IsAlive) wolves[i].Alert();
    }

    /// <summary>Một con chết: các con còn lại hung hăng lên.</summary>
    public void OnWolfDied()
    {
        aggressionUntil = Time.time + aggressionTime;
        nextLungeTime = Mathf.Min(nextLungeTime, Time.time + 0.3f);
    }

    /// <summary>Con sói lao xong (cắn hoặc hụt): tính lại khoảng nghỉ cho con kế tiếp.</summary>
    public void OnLungeFinished()
    {
        float interval = attackInterval + Random.Range(0f, attackIntervalJitter);
        if (IsAggressive) interval *= aggressionIntervalMultiplier;
        nextLungeTime = Time.time + interval;
    }

    private void UpdateLunge()
    {
        if (Time.time < nextLungeTime) return;
        // Đang có con lao thì chờ
        for (int i = 0; i < wolves.Count; i++)
            if (wolves[i] != null && wolves[i].IsLunging) return;

        // Chọn ngẫu nhiên trong các con đang vây và đã vào vị trí
        ThanhLang pick = null;
        int count = 0;
        for (int i = 0; i < wolves.Count; i++)
        {
            ThanhLang w = wolves[i];
            if (w == null || !w.CanLunge) continue;
            count++;
            if (Random.value < 1f / count) pick = w;
        }
        if (pick != null) pick.BeginLunge();
    }
}
