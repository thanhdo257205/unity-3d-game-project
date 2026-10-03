using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vật bay của kẻ địch mà phi kiếm người chơi có thể chặn (quả cầu, phi kiếm đỏ của tà tu).
/// SwordSwarm duyệt danh sách <see cref="EnemyProjectiles.All"/> để cử kiếm ra đón và kiểm tra va chạm.
/// </summary>
public interface IEnemyProjectile
{
    /// <summary>Vị trí tâm.</summary>
    Vector3 Position { get; }
    /// <summary>Bán kính va chạm với phi kiếm người chơi.</summary>
    float Radius { get; }
    /// <summary>Đã tan hoặc không còn đang bay tấn công (khi đó không bị chặn nữa).</summary>
    bool IsDone { get; }
    /// <summary>Đã có kiếm được cử ra đón vật này chưa.</summary>
    bool HasGuard { get; set; }
    /// <summary>Hướng bay hiện tại (vector đơn vị).</summary>
    Vector3 Direction { get; }
    /// <summary>Có đang bay lại gần điểm point không.</summary>
    bool IsApproaching(Vector3 point);
    /// <summary>Bị phi kiếm chặn.</summary>
    void Block();
}

/// <summary>Danh sách mọi vật bay của kẻ địch đang hoạt động.</summary>
public static class EnemyProjectiles
{
    public static readonly List<IEnemyProjectile> All = new List<IEnemyProjectile>();

    /// <summary>Quả cầu hoặc phi kiếm có chạm viên nang của người chơi không.</summary>
    public static bool HitsPlayer(Vector3 center, float radius, PlayerStats target, CharacterController body)
    {
        if (target == null || !target.IsAlive) return false;
        if (body == null) return (target.transform.position + Vector3.up - center).sqrMagnitude <= 0.5f * 0.5f;

        Vector3 basePos = body.transform.TransformPoint(body.center);
        float half = Mathf.Max(0f, body.height * 0.5f - body.radius);
        Vector3 a = basePos - Vector3.up * half;
        Vector3 b = basePos + Vector3.up * half;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(center - a, ab) / ab.sqrMagnitude) : 0f;
        Vector3 closest = a + ab * t;
        float r = radius + body.radius;
        return (center - closest).sqrMagnitude <= r * r;
    }

    /// <summary>Xóa mọi vật bay của kẻ địch (khi người chơi hồi sinh).</summary>
    public static void ClearAll()
    {
        EnemyOrb.DestroyAll();
        EnemyBlade.RecallAll();
    }
}
