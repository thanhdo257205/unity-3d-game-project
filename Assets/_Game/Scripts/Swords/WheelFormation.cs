using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kiếm Luân (thay đội hình Quạt): bánh xe kiếm tròn đầy dựng đứng sau lưng, vuông góc với hướng nhìn của nhân vật.
/// Kiếm xếp đều đủ 360 độ, mũi chĩa ra ngoài, chuôi hướng về tâm; vành rỗng ở giữa để thấy nhân vật qua lỗ.
/// Hai lớp so le (một nửa số kiếm mỗi lớp): lớp ngoài lệch nửa bước góc, lùi ra sau thêm một chút, xoay ngược chiều lớp trong.
/// Cả bánh xe xoay chậm, mỗi kiếm dao động nhẹ ra vào cho khỏi cứng đơ.
/// Chỗ đứng của mỗi kiếm tính theo tổng số kiếm, nên kiếm phóng đi thì chỗ của nó để trống tới khi kiếm về.
/// </summary>
[Serializable]
public class WheelFormation : IFormation
{
    [Tooltip("Tâm bánh xe cách điểm neo (ngang bả vai) bao xa về phía sau (mét). 0.63 tức là cách mặt lưng khoảng 0.4m.")]
    public float behindDistance = 0.63f;

    [Tooltip("Độ cao tâm bánh xe so với điểm neo (điểm neo ngang bả vai nên 0 là tâm ngang bả vai). Số âm là thấp xuống.")]
    public float heightOffset = 0f;

    [Tooltip("Khoảng cách từ tâm tới tâm chuôi kiếm lớp trong (mét). Kiếm trong đội hình thu còn 0.7 lần nên 0.78 cho chuôi cách tâm khoảng 0.7m, để lại lỗ tròn ở giữa.")]
    [Min(0.2f)] public float innerRadius = 0.78f;

    [Tooltip("Lớp ngoài đứng xa tâm hơn lớp trong bao nhiêu (mét). Với kiếm thu 0.7 lần, 0.21 cho mũi kiếm lớp ngoài chạm bán kính khoảng 1.6m.")]
    [Min(0f)] public float outerExtraRadius = 0.21f;

    [Tooltip("Lớp ngoài lùi ra sau thêm bao nhiêu so với lớp trong (mét), để hai lớp không đâm vào nhau.")]
    [Min(0f)] public float outerExtraBack = 0.08f;

    [Tooltip("Tốc độ xoay của bánh xe (độ/giây). Lớp trong xoay thuận, lớp ngoài xoay ngược chiều.")]
    public float spinSpeed = 12f;

    [Tooltip("Mỗi kiếm dao động ra vào quanh chỗ đứng bao nhiêu (mét).")]
    [Min(0f)] public float wobbleAmplitude = 0.04f;

    [Tooltip("Số lần dao động mỗi giây.")]
    [Min(0f)] public float wobbleFrequency = 0.7f;

    public string DisplayName => "Kiếm Luân";

    public bool ContactDamage => false;

    /// <summary>Chỗ đứng chẵn (0, 2, 4...) thuộc lớp trong, chỗ lẻ thuộc lớp ngoài; mỗi lớp một nửa số kiếm.</summary>
    private static void LayerInfo(int index, int count, out int layer, out int indexInLayer, out int layerCount)
    {
        layer = index % 2;
        indexInLayer = index / 2;
        layerCount = layer == 0 ? (count + 1) / 2 : count / 2;
        if (layerCount < 1) layerCount = 1;
    }

    /// <summary>Góc gốc (độ, 0 = thẳng lên, dương = sang phải) của chỗ đứng index khi bánh xe chưa xoay. Lớp ngoài lệch nửa bước.</summary>
    private static float BaseAngle(int index, int count)
    {
        int layer, j, m;
        LayerInfo(index, count, out layer, out j, out m);
        return 360f * j / m + (layer == 1 ? 180f / m : 0f);
    }

    public SwordSlot GetSlot(int index, int count, in FormationContext ctx)
    {
        int layer, j, m;
        LayerInfo(index, count, out layer, out j, out m);

        float sign = layer == 0 ? 1f : -1f;
        float angle = (BaseAngle(index, count) + sign * spinSpeed * ctx.time) * Mathf.Deg2Rad;

        Vector3 forward = ctx.body * Vector3.forward;
        Vector3 right = ctx.body * Vector3.right;
        Vector3 dir = Mathf.Cos(angle) * Vector3.up + Mathf.Sin(angle) * right;   // hướng từ tâm ra mũi kiếm, nằm trong mặt phẳng đứng (phải - lên)

        float wobble = wobbleAmplitude * Mathf.Sin(ctx.time * wobbleFrequency * Mathf.PI * 2f + index * 1.7f);
        float radius = innerRadius + (layer == 1 ? outerExtraRadius : 0f) + wobble;
        Vector3 center = ctx.origin - forward * (behindDistance + (layer == 1 ? outerExtraBack : 0f)) + Vector3.up * heightOffset;

        SwordSlot slot;
        slot.position = center + dir * radius;
        slot.rotation = Quaternion.LookRotation(dir, forward);   // mặt phẳng lưỡi kiếm quay về phía trước (pháp tuyến = hướng nhìn)
        return slot;
    }

    /// <summary>
    /// Thứ tự lấp các chỗ khi n kiếm lần lượt bay ra: bắt đầu từ đỉnh bánh xe rồi luân phiên bên phải - bên trái,
    /// mỗi lần xa đỉnh thêm một bước, nên bánh xe mở ra đối xứng như một vầng sáng.
    /// </summary>
    public static int[] DeployOrder(int n)
    {
        List<int> order = new List<int>(n);
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            float aa = Signed(BaseAngle(a, n)), ab = Signed(BaseAngle(b, n));
            float da = Mathf.Abs(aa), db = Mathf.Abs(ab);
            if (Mathf.Abs(da - db) < 0.001f) return ab.CompareTo(aa);   // cùng khoảng cách: bên phải (dương) ra trước
            return da.CompareTo(db);
        });
        return order.ToArray();
    }

    // Đưa góc về khoảng (-180, 180]
    private static float Signed(float deg)
    {
        deg = Mathf.Repeat(deg, 360f);
        return deg > 180f ? deg - 360f : deg;
    }
}
