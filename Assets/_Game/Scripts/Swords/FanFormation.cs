using System;
using UnityEngine;

/// <summary>
/// Thủ Kiếm - Quạt: các kiếm xòe hình quạt dựng đứng phía sau lưng, mũi chĩa ra ngoài.
/// Nhiều hơn một lớp thì xếp thêm lớp ngoài, so le với lớp trong.
/// </summary>
[Serializable]
public class FanFormation : IFormation
{
    [Tooltip("Góc mở của quạt (độ). 180 là đúng nửa vòng tròn. Thiết kế gốc là 160, đã nới rộng hơn.")]
    [Range(10f, 360f)] public float arcAngle = 180f;

    [Tooltip("Bán kính quạt: khoảng cách từ tâm quạt tới chuôi kiếm (mét). Kiếm đã thu còn 0.7 lần nên 0.7m cho mũi kiếm xa nhất cách tâm khoảng 1.3m.")]
    [Min(0.2f)] public float radius = 0.7f;

    [Tooltip("Số kiếm tối đa trong một lớp. Nhiều hơn thì xếp thêm lớp ngoài.")]
    [Min(1)] public int layerCapacity = 12;

    [Tooltip("Khoảng cách giữa các lớp (mét).")]
    [Min(0f)] public float layerSpacing = 0.2f;

    [Tooltip("Độ ngả của quạt ra sau lưng (độ). 0 là quạt đứng thẳng, số dương là ngả ra sau như đôi cánh.")]
    [Range(-30f, 60f)] public float tiltBack = 12f;

    [Tooltip("Tâm quạt cách điểm neo (giữa thân) bao xa về phía sau (mét). 0.58 tức là cách mặt lưng khoảng 0.35m.")]
    public float behindDistance = 0.58f;

    [Tooltip("Độ cao tâm quạt so với điểm neo (điểm neo ngang vai nên 0 là tâm quạt ngang vai). Số âm là thấp xuống.")]
    public float heightOffset = 0f;

    public string DisplayName => "Quạt";

    public bool ContactDamage => false;

    public SwordSlot GetSlot(int index, int count, in FormationContext ctx)
    {
        int layer = index / layerCapacity;
        int indexInLayer = index % layerCapacity;
        int layerCount = Mathf.Min(layerCapacity, count - layer * layerCapacity);

        // Lớp chẵn: chia đều không chạm hai đầu mút. Lớp lẻ: trải từ mút này tới mút kia, nên so le với lớp trong
        float u;
        if (layer % 2 == 0) u = (indexInLayer + 0.5f) / Mathf.Max(layerCount, 1);
        else u = layerCount <= 1 ? 0.5f : indexInLayer / (float)(layerCount - 1);
        float angle = (u - 0.5f) * arcAngle * Mathf.Deg2Rad;

        Vector3 forward = ctx.body * Vector3.forward;
        Vector3 right = ctx.body * Vector3.right;

        // Hướng từ tâm quạt ra mũi kiếm, nằm trong mặt phẳng đứng (phải - lên), rồi ngả ra sau
        Quaternion tilt = Quaternion.AngleAxis(-tiltBack, right);
        Vector3 dir = tilt * (Mathf.Sin(angle) * right + Mathf.Cos(angle) * Vector3.up);
        Vector3 normal = tilt * forward; // pháp tuyến của mặt phẳng quạt: mặt phẳng lưỡi kiếm quay về phía này

        Vector3 center = ctx.origin - forward * behindDistance + Vector3.up * heightOffset;
        SwordSlot slot;
        slot.position = center + dir * (radius + layer * layerSpacing);
        slot.rotation = Quaternion.LookRotation(dir, normal);
        return slot;
    }
}
