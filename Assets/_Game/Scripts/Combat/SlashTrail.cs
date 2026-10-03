using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vệt chém: dải sáng nối từ gốc lưỡi tới mũi kiếm qua các khung hình gần nhất, mờ dần theo thời gian.
/// Gắn trên prefab kiếm cầm tay. PlayerMelee bật <see cref="SetEmitting"/> đúng đoạn lưỡi kiếm đang vung.
/// Mặt dải được dựng trong tọa độ thế giới (đối tượng riêng, không bám theo kiếm) nên vệt đứng lại phía sau khi kiếm đi tiếp.
/// </summary>
public class SlashTrail : MonoBehaviour
{
    [Header("Hai đầu của dải")]
    [Tooltip("Điểm gốc lưỡi (gần đốc kiếm). Là đối tượng con DayLuoi của prefab kiếm.")]
    [SerializeField] private Transform bladeBase;

    [Tooltip("Điểm mũi kiếm. Là đối tượng con MuiKiem của prefab kiếm.")]
    [SerializeField] private Transform bladeTip;

    [Header("Hình dạng và màu")]
    [Tooltip("Vật liệu của vệt (dùng shader URP Particles/Unlit, chế độ cộng sáng, tương tự VFX_VetKiem).")]
    [SerializeField] private Material material;

    [Tooltip("Màu vệt chém ở chỗ mới nhất (alpha là độ đậm). Phía cũ mờ dần về 0.")]
    [SerializeField] private Color color = new Color(0.6f, 1f, 0.8f, 0.4f);

    [Tooltip("Thời gian vệt tồn tại trước khi mờ hết (giây). Dài hơn thì vệt kéo dài hơn sau lưỡi kiếm.")]
    [SerializeField, Min(0.02f)] private float lifetime = 0.12f;

    [Tooltip("Phần đầu đời của vệt (0-1) dùng để mọc lên từ trong suốt, để chỗ mới nhất sát lưỡi kiếm không phủ đặc lên lưỡi. 0.3 = 30% đầu.")]
    [SerializeField, Range(0f, 0.8f)] private float fadeInFraction = 0.3f;

    [Tooltip("Số điểm nội suy giữa hai khung hình liền nhau để vệt cong mượt (1 = không chia).")]
    [SerializeField, Range(1, 8)] private int smoothing = 3;

    [Tooltip("Dải chỉ dài tới mức này của lưỡi kiếm tính từ mũi (0.5 = nửa lưỡi phía mũi, 1 = cả lưỡi).")]
    [SerializeField, Range(0.1f, 1f)] private float bladeFraction = 0.55f;

    private struct Sample
    {
        public Vector3 a;      // điểm gần đốc
        public Vector3 b;      // mũi
        public float time;
    }

    private readonly List<Sample> samples = new List<Sample>();
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<int> triangles = new List<int>();

    private Mesh mesh;
    private GameObject meshObject;
    private bool emitting;

    /// <summary>Đang vẽ thêm vệt mới không.</summary>
    public bool IsEmitting => emitting;

    private void Awake()
    {
        mesh = new Mesh { name = "VetChem" };
        mesh.MarkDynamic();
        meshObject = new GameObject("VetChem", typeof(MeshFilter), typeof(MeshRenderer));
        meshObject.hideFlags = HideFlags.DontSave;
        meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = meshObject.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void OnDestroy()
    {
        if (meshObject != null) Destroy(meshObject);
        if (mesh != null) Destroy(mesh);
    }

    private void OnDisable()
    {
        emitting = false;
        samples.Clear();
        if (mesh != null) mesh.Clear();
    }

    /// <summary>Bật/tắt việc vẽ thêm vệt. Tắt thì vệt đang có mờ dần rồi biến mất.</summary>
    public void SetEmitting(bool on)
    {
        emitting = on;
    }

    private void LateUpdate()
    {
        if (bladeBase == null || bladeTip == null || mesh == null) return;
        float now = Time.time;

        if (emitting)
        {
            Vector3 tip = bladeTip.position;
            Vector3 root = Vector3.Lerp(tip, bladeBase.position, bladeFraction);
            // Bỏ khung hình trùng (dừng hình) để không sinh tam giác suy biến
            if (samples.Count == 0 || (samples[samples.Count - 1].b - tip).sqrMagnitude > 0.000001f || (samples[samples.Count - 1].a - root).sqrMagnitude > 0.000001f)
                samples.Add(new Sample { a = root, b = tip, time = now });
        }

        // Bỏ mẫu quá cũ
        while (samples.Count > 0 && now - samples[0].time > lifetime) samples.RemoveAt(0);

        Rebuild(now);
    }

    /// <summary>Dựng lại dải từ các mẫu còn sống, nội suy Catmull-Rom cho cong mượt.</summary>
    private void Rebuild(float now)
    {
        mesh.Clear();
        int n = samples.Count;
        if (n < 2 || material == null) return;

        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        for (int i = 0; i < n - 1; i++)
        {
            int steps = i == n - 2 ? smoothing + 1 : smoothing;   // đoạn cuối lấy luôn điểm cuối
            for (int s = 0; s < steps; s++)
            {
                float u = (float)s / smoothing;
                Sample p0 = samples[Mathf.Max(i - 1, 0)];
                Sample p1 = samples[i];
                Sample p2 = samples[i + 1];
                Sample p3 = samples[Mathf.Min(i + 2, n - 1)];
                Vector3 a = CatmullRom(p0.a, p1.a, p2.a, p3.a, u);
                Vector3 b = CatmullRom(p0.b, p1.b, p2.b, p3.b, u);
                float t = Mathf.Lerp(p1.time, p2.time, u);
                float age = Mathf.Clamp01((now - t) / lifetime);
                float fadeIn = fadeInFraction > 0.001f ? Mathf.Clamp01(age / fadeInFraction) : 1f;   // mới nhất: trong suốt, rồi đậm dần
                float fadeOut = 1f - age;                                                         // cũ dần: mờ về 0
                Color c = color;
                c.a *= fadeIn * fadeOut * fadeOut;
                vertices.Add(a); colors.Add(new Color(c.r, c.g, c.b, c.a * 0.35f));   // phía đốc mờ hơn, phía mũi sáng nhất
                vertices.Add(b); colors.Add(c);
            }
        }

        int pairs = vertices.Count / 2;
        for (int k = 0; k < pairs - 1; k++)
        {
            int v = k * 2;
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v + 1); triangles.Add(v + 3); triangles.Add(v + 2);
        }

        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.bounds = new Bounds(bladeTip.position, Vector3.one * 20f);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
