using System;
using UnityEngine;

/// <summary>
/// Phá Kiếm - Mũi tên: các kiếm xếp thành hàng dọc phía sau lưng, mũi hướng về phía trước, sẵn sàng lao vào mục tiêu.
/// Hàng quá dài thì tách thành nhiều hàng cạnh nhau. Việc lao đi do SwordSwarm.FireVolley lo.
/// </summary>
[Serializable]
public class ArrowFormation : IFormation
{
    [Tooltip("Số kiếm tối đa trong một hàng dọc. Nhiều hơn thì thêm hàng bên cạnh.")]
    [Min(1)] public int columnCapacity = 12;

    [Tooltip("Khoảng cách giữa hai kiếm liền nhau theo chiều dọc (mét).")]
    [Min(0.05f)] public float verticalSpacing = 0.14f;

    [Tooltip("Khoảng cách giữa các hàng cạnh nhau (mét).")]
    [Min(0.05f)] public float columnSpacing = 0.25f;

    [Tooltip("Hàng kiếm cách lưng nhân vật bao xa về phía sau (mét). Kiếm dài ~0.87m nên để từ 0.6 trở lên để mũi không xuyên qua người.")]
    [Min(0f)] public float behindDistance = 0.45f;

    [Tooltip("Lệch sang ngang so với giữa lưng (mét). Số âm = bên trái, số dương = bên phải. Camera đứng sau vai phải nên để bên trái cho khỏi che tâm ngắm.")]
    public float sideOffset = -0.6f;

    [Tooltip("Độ cao tâm hàng kiếm so với điểm neo ngang ngực (mét).")]
    public float heightOffset = 0f;

    public string DisplayName => "Mũi tên";

    public bool ContactDamage => false;

    public SwordSlot GetSlot(int index, int count, in FormationContext ctx)
    {
        int column = index / columnCapacity;
        int row = index % columnCapacity;
        int columns = Mathf.CeilToInt(count / (float)columnCapacity);
        int rowCount = Mathf.Min(columnCapacity, count - column * columnCapacity);

        float lateral = sideOffset + (column - (columns - 1) * 0.5f) * columnSpacing;
        float vertical = (row - (rowCount - 1) * 0.5f) * verticalSpacing;

        Vector3 forward = ctx.body * Vector3.forward;
        Vector3 right = ctx.body * Vector3.right;

        SwordSlot slot;
        slot.position = ctx.origin + Vector3.up * (heightOffset + vertical) - forward * behindDistance + right * lateral;
        slot.rotation = Quaternion.LookRotation(forward, Vector3.up);
        return slot;
    }
}
