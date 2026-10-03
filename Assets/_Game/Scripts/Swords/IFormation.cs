using UnityEngine;

/// <summary>Vị trí và hướng mà một thanh kiếm phải đạt tới trong đội hình.</summary>
public struct SwordSlot
{
    /// <summary>Vị trí (pivot giữa chuôi) trong thế giới.</summary>
    public Vector3 position;
    /// <summary>Hướng kiếm: trục Z của kiếm là mũi kiếm, trục Y là mặt phẳng lưỡi.</summary>
    public Quaternion rotation;
}

/// <summary>Thông tin chung để tính vị trí các kiếm trong đội hình.</summary>
public struct FormationContext
{
    /// <summary>Điểm neo của đội hình (khoảng ngang ngực nhân vật).</summary>
    public Vector3 origin;
    /// <summary>Hướng thân người, chỉ xoay quanh trục đứng.</summary>
    public Quaternion body;
    /// <summary>Thời gian hiện tại (giây), dùng cho đội hình chuyển động như Vòng.</summary>
    public float time;
}

/// <summary>
/// Một đội hình kiếm: cho biết vị trí và hướng của thanh kiếm thứ <c>index</c> trong <c>count</c> thanh.
/// Mỗi đội hình là một class riêng, các thông số hiển thị trong Inspector của SwordSwarm.
/// </summary>
public interface IFormation
{
    /// <summary>Tên hiển thị tiếng Việt.</summary>
    string DisplayName { get; }

    /// <summary>Kiếm đứng trong đội hình này có gây sát thương khi chạm địch không (ví dụ Vòng thì có).</summary>
    bool ContactDamage { get; }

    /// <summary>Tính chỗ đứng của thanh kiếm thứ index.</summary>
    SwordSlot GetSlot(int index, int count, in FormationContext ctx);
}
