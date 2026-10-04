using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kiếm Luân (thay đội hình Quạt): vầng kiếm tròn đầy dựng đứng sau lưng, vuông góc với hướng nhìn của nhân vật, giống vầng hào quang tỏa đều.
/// Kiếm xếp đều đủ 360 độ, mũi chĩa ra ngoài, chuôi chụm về tâm; ở giữa chỉ chừa một lỗ nhỏ.
/// Hai lớp so le (một nửa số kiếm mỗi lớp): lớp ngoài lệch nửa bước góc và đứng xa tâm hơn một chút nên mũi kiếm so le dài - ngắn.
/// Cả hai lớp xoay CÙNG chiều cùng tốc độ (đổi "Tỉ lệ xoay lớp ngoài" để cho xoay ngược) nên khoảng cách góc giữa các kiếm luôn đều, không bao giờ đứng thành cặp.
/// Mỗi kiếm dao động nhẹ ra vào cho khỏi cứng đơ. Chỗ đứng tính theo tổng số kiếm, nên kiếm phóng đi thì chỗ của nó để trống tới khi kiếm về.
/// </summary>
[Serializable]
public class WheelFormation : IFormation
{
    [Tooltip("Tâm vầng kiếm cách điểm neo (ngang bả vai) bao xa về phía sau (mét). 0.58 tức là tâm nằm giữa hai bả vai, cách mặt lưng khoảng 0.35m.")]
    public float behindDistance = 0.58f;

    [Tooltip("Độ cao tâm vầng kiếm so với điểm neo (điểm neo ngang bả vai nên 0 là tâm ngang bả vai). Số âm là thấp xuống.")]
    public float heightOffset = 0f;

    [Tooltip("Khoảng cách từ tâm tới tâm chuôi kiếm lớp trong (mét). 0.28 cho vầng kiếm chụm sát tâm, chỉ chừa lỗ nhỏ ở giữa.")]
    [Min(0.05f)] public float innerRadius = 0.28f;

    [Tooltip("Lớp ngoài đứng xa tâm hơn lớp trong bao nhiêu (mét). Với kiếm thu 0.8 lần, 0.17 cho mũi kiếm lớp ngoài xa tâm khoảng 1.15m (cả vầng cao ~2.3m).")]
    [Min(0f)] public float outerExtraRadius = 0.17f;

    [Tooltip("Lớp ngoài lùi ra sau thêm bao nhiêu so với lớp trong (mét), để hai lớp không đâm vào nhau.")]
    [Min(0f)] public float outerExtraBack = 0.03f;

    [Tooltip("Tốc độ xoay của vầng kiếm (độ/giây), lớp trong.")]
    public float spinSpeed = 12f;

    [Tooltip("Tốc độ xoay lớp ngoài so với lớp trong. 1 = cùng chiều cùng tốc độ (kiếm luôn cách đều, không thành cặp). -1 = ngược chiều (thỉnh thoảng hai lớp trùng góc và kiếm đứng thành cặp).")]
    public float outerSpinRatio = 1f;

    [Tooltip("Mỗi kiếm dao động ra vào quanh chỗ đứng bao nhiêu (mét).")]
    [Min(0f)] public float wobbleAmplitude = 0.02f;

    [Tooltip("Số lần dao động mỗi giây.")]
    [Min(0f)] public float wobbleFrequency = 0.7f;

    /// <summary>Khoảng đẩy thêm ra sau lưng (mét), do SwordSwarm đặt khi cánh Phong Lôi Sí mở để Kiếm Luân không cắm vào cánh.</summary>
    [NonSerialized] public float extraBack;

    public string DisplayName => "Kiếm Luân";

    public bool ContactDamage => false;

    /// <summary>Tâm vầng kiếm trong thế giới (không tính xoay): sau lưng, giữa hai bả vai.</summary>
    public Vector3 GetCenter(in FormationContext ctx)
    {
        return ctx.origin - (ctx.body * Vector3.forward) * (behindDistance + extraBack) + Vector3.up * heightOffset;
    }

    /// <summary>Khoảng cách xa nhất từ tâm tới mũi kiếm (mét) với thanh kiếm có mũi cách pivot tipOffset, thu nhỏ theo swordScale.</summary>
    public float OuterRadius(float tipOffset, float swordScale)
    {
        return innerRadius + outerExtraRadius + tipOffset * swordScale;
    }

    /// <summary>Chỗ đứng chẵn (0, 2, 4...) thuộc lớp trong, chỗ lẻ thuộc lớp ngoài; mỗi lớp một nửa số kiếm.</summary>
    private static void LayerInfo(int index, int count, out int layer, out int indexInLayer, out int layerCount)
    {
        layer = index % 2;
        indexInLayer = index / 2;
        layerCount = layer == 0 ? (count + 1) / 2 : count / 2;
        if (layerCount < 1) layerCount = 1;
    }

    /// <summary>Góc gốc (độ, 0 = thẳng lên, dương = sang phải) của chỗ đứng index khi vầng kiếm chưa xoay. Lớp ngoài lệch nửa bước.</summary>
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

        float spin = spinSpeed * (layer == 0 ? 1f : outerSpinRatio);
        float angle = (BaseAngle(index, count) + spin * ctx.time) * Mathf.Deg2Rad;

        Vector3 forward = ctx.body * Vector3.forward;
        Vector3 right = ctx.body * Vector3.right;
        Vector3 dir = Mathf.Cos(angle) * Vector3.up + Mathf.Sin(angle) * right;   // hướng từ tâm ra mũi kiếm, nằm trong mặt phẳng đứng (phải - lên)

        float wobble = wobbleAmplitude * Mathf.Sin(ctx.time * wobbleFrequency * Mathf.PI * 2f + index * 1.7f);
        float radius = innerRadius + (layer == 1 ? outerExtraRadius : 0f) + wobble;
        Vector3 center = GetCenter(ctx) - forward * (layer == 1 ? outerExtraBack : 0f);

        SwordSlot slot;
        slot.position = center + dir * radius;
        slot.rotation = Quaternion.LookRotation(dir, forward);   // mặt phẳng lưỡi kiếm quay về phía trước (pháp tuyến = hướng nhìn)
        return slot;
    }

    /// <summary>
    /// Thứ tự lấp các chỗ khi n kiếm lần lượt bay ra: bắt đầu từ đỉnh vầng kiếm rồi luân phiên bên phải - bên trái,
    /// mỗi lần xa đỉnh thêm một bước, nên vầng kiếm mở ra đối xứng như một vầng sáng.
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
