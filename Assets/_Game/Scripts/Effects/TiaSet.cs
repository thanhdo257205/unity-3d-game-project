using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bộ sinh tia sét DÙNG CHUNG cho sét cánh Phong Lôi Sí (WingLightning) và cổng không gian (CongKhongGian): chỉnh hình dạng tia ở một chỗ này.
/// - MakeLine: tạo LineRenderer tia (cộng sáng, tọa độ thế giới nên bề rộng tính bằng mét, KHÔNG bị nhân theo scale của đối tượng cha).
///   Ảnh tia có LÕI gần trắng rộng 1/4 bề ngang dải + lớp ngoài màu (CoreFraction), nên bề ngang dải = bề ngang lõi / CoreFraction = 4 lần lõi.
/// - BuildPath: từ một đường gốc (thẳng hay cong) dựng đường gãy khúc răng cưa: mỗi đỉnh lệch xen kẽ hai bên vuông góc với đường gốc,
///   lệch lớn nhất ở giữa; có thể thêm vài nhánh con đi ra rồi lùi lại đúng đường cũ (nên chỉ thấy một nhánh liền, không thành tam giác rỗng).
/// </summary>
public static class TiaSet
{
    /// <summary>Lõi chiếm bấy nhiêu bề ngang dải trong ảnh tia (khớp WingLightningBuilder.MakeBoltTexture).</summary>
    public const float CoreFraction = 0.25f;

    private static readonly List<Vector3> mainBuf = new List<Vector3>(32);
    private static readonly Vector3[][] branchBuf = new Vector3[32][];
    private static readonly bool[] hasBuf = new bool[32];

    /// <summary>Tạo một LineRenderer tia. coreWidth = bề ngang LÕI (mét). taper = nhỏ dần về cuối (tia tỏa).</summary>
    public static LineRenderer MakeLine(Transform parent, string name, Material mat, float coreWidth, bool taper)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 1;     // góc gãy giữ gần như nhọn (tia sét), chỉ bo nhẹ cho khỏi lủng
        if (taper) lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f));
        lr.sharedMaterial = mat;
        lr.widthMultiplier = coreWidth / CoreFraction;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 0;
        return lr;
    }

    /// <summary>
    /// Dựng đường gãy khúc vào result. basePts: đường gốc (>= 2 điểm). amplitude: độ lệch lớn nhất ở giữa (mét). refAxis: trục tham chiếu để chọn hướng lệch
    /// (lệch theo cross(tiếp tuyến, refAxis) và hướng còn lại). alongJitter: xô lệch dọc đường, tính theo phần của độ dài một đoạn. branches: số nhánh con (0-3).
    /// branchLength: độ dài tham chiếu của nhánh (mét). keepEnds: giữ nguyên hai đầu đường gốc (tia chạy dọc vòng).
    /// </summary>
    public static void BuildPath(List<Vector3> result, IList<Vector3> basePts, float amplitude, Vector3 refAxis, float alongJitter, int branches, float branchLength, bool keepEnds)
    {
        result.Clear();
        int n = basePts.Count - 1;
        if (n < 1) return;
        mainBuf.Clear();
        float sign = Random.value < 0.5f ? 1f : -1f;
        for (int i = 0; i <= n; i++)
        {
            Vector3 p = basePts[i];
            bool end = i == 0 || (keepEnds && i == n);
            if (!end)
            {
                Vector3 tan = (basePts[Mathf.Min(n, i + 1)] - basePts[Mathf.Max(0, i - 1)]);
                tan = tan.sqrMagnitude > 1e-8f ? tan.normalized : Vector3.forward;
                Vector3 perpA = Vector3.Cross(tan, refAxis);
                perpA = perpA.sqrMagnitude > 1e-8f ? perpA.normalized : Vector3.Cross(tan, Vector3.up).normalized;
                Vector3 perpB = Vector3.Cross(tan, perpA);
                float seg = (basePts[Mathf.Min(n, i + 1)] - basePts[Mathf.Max(0, i - 1)]).magnitude * 0.5f;
                if (i < n) p += tan * (Random.Range(-alongJitter, alongJitter) * seg);
                sign = -sign;
                float t = i / (float)n;
                float mag = amplitude * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f)) + 0.01f;
                p += (perpA * sign * Random.Range(0.45f, 1f) + perpB * Random.Range(-0.6f, 0.6f)) * mag;
            }
            mainBuf.Add(p);
        }

        System.Array.Clear(hasBuf, 0, hasBuf.Length);
        int maxIdx = Mathf.Min(n, hasBuf.Length - 1);
        for (int b = 0; b < branches; b++)
        {
            if (maxIdx < 3) break;
            int at = Random.Range(2, maxIdx);
            if (hasBuf[at]) continue;
            hasBuf[at] = true;
            Vector3 d = (basePts[Mathf.Min(n, at + 1)] - basePts[at - 1]).normalized;
            Vector3 axis = Random.onUnitSphere;
            Vector3 rot = Vector3.Cross(d, axis);
            rot = rot.sqrMagnitude > 1e-8f ? rot.normalized : Vector3.up;
            Vector3 bd = Quaternion.AngleAxis(Random.Range(25f, 60f) * (Random.value < 0.5f ? 1f : -1f), rot) * d;
            float bl = branchLength * Random.Range(0.22f, 0.45f);
            Vector3[] bp = branchBuf[at] != null ? branchBuf[at] : (branchBuf[at] = new Vector3[3]);
            Vector3 cur = mainBuf[at];
            for (int i = 0; i < 3; i++)
            {
                cur += bd * (bl / 3f) + Random.onUnitSphere * (bl * 0.16f);
                bp[i] = cur;
            }
        }
        for (int i = 0; i <= n; i++)
        {
            result.Add(mainBuf[i]);
            if (hasBuf[i])
            {
                Vector3[] bp = branchBuf[i];
                result.Add(bp[0]); result.Add(bp[1]); result.Add(bp[2]);
                result.Add(bp[1]); result.Add(bp[0]); result.Add(mainBuf[i]);
            }
        }
    }
}
