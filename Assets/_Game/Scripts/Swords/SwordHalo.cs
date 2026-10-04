using UnityEngine;

/// <summary>
/// Quầng sáng mờ hình tròn phía sau Kiếm Luân: một tấm quad phát sáng (cộng sáng - additive) đặt ở tâm vầng kiếm, vuông góc hướng nhìn nhân vật,
/// bán kính bằng vầng kiếm, độ đậm thấp để các khe giữa các lưỡi kiếm không trống trơn.
/// Hiện dần khi các kiếm bay ra, tắt dần khi kiếm về túi; chỉ hiện ở đội hình Kiếm Luân.
/// Khi camera ở sau lưng nhân vật thì mờ cùng nhịp với kiếm (xem SwordSwarm.FormationOpacity) để không che nhân vật.
/// Tự dựng tấm quad bằng code (không cần mô hình); chỉ cần vật liệu cộng sáng có texture quầng tròn.
/// </summary>
[DisallowMultipleComponent]
public class SwordHalo : MonoBehaviour
{
    [Tooltip("Đàn kiếm mà quầng sáng đi theo (đối tượng BayKiem).")]
    [SerializeField] private SwordSwarm swarm;

    [Tooltip("Vật liệu cộng sáng (additive) có texture quầng tròn mờ dần ra mép. Mặc định là VFX_QuangKiemLuan.")]
    [SerializeField] private Material haloMaterial;

    [Tooltip("Màu quầng sáng (alpha của màu này bị bỏ qua, độ đậm lấy từ 'Độ đậm tối đa').")]
    [SerializeField] private Color tint = new Color(0.3f, 1f, 0.65f, 1f);

    [Tooltip("Độ đậm tối đa của quầng sáng (0-1) khi camera ở bên cạnh/phía trước. Thấp để chỉ lấp các khe giữa kiếm.")]
    [SerializeField, Range(0f, 1f)] private float peakOpacity = 0.3f;

    [Tooltip("Bán kính quầng sáng so với bán kính vầng kiếm (1 = bằng nhau).")]
    [SerializeField, Min(0.1f)] private float radiusRatio = 1f;

    [Tooltip("Quầng sáng lùi ra sau mặt phẳng kiếm bao nhiêu (mét), để luôn nằm sau lưng các lưỡi kiếm khi nhìn từ phía sau.")]
    [SerializeField] private float behindOffset = 0.05f;

    [Tooltip("Độ 'thở' của quầng sáng: kích thước dao động bao nhiêu phần trăm (0.04 = ±4%).")]
    [SerializeField, Range(0f, 0.3f)] private float breathAmount = 0.04f;

    [Tooltip("Số nhịp 'thở' mỗi giây.")]
    [SerializeField, Min(0f)] private float breathFrequency = 0.5f;

    [Tooltip("Tốc độ hiện/tắt dần khi bật/tắt Ngự Kiếm Thuật hoặc đổi đội hình (đơn vị độ đậm mỗi giây).")]
    [SerializeField, Min(0.1f)] private float presenceSpeed = 4f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform quad;
    private MeshRenderer quadRenderer;
    private MaterialPropertyBlock block;
    private float presence;

    private void Awake()
    {
        if (swarm == null) swarm = GetComponent<SwordSwarm>();
        BuildQuad();
    }

    /// <summary>Tạo tấm quad 1x1 (nằm trong mặt phẳng XY, cạnh trước hướng +Z) làm con của đối tượng này.</summary>
    private void BuildQuad()
    {
        GameObject go = new GameObject("QuangKiemLuan");
        quad = go.transform;
        quad.SetParent(transform, false);

        Mesh mesh = new Mesh { name = "QuangKiemLuan_Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };   // hai mặt, để nhìn từ trước hay sau đều thấy
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        quadRenderer = go.AddComponent<MeshRenderer>();
        quadRenderer.sharedMaterial = haloMaterial;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        quadRenderer.enabled = false;
        block = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (swarm == null || quadRenderer == null || haloMaterial == null) return;

        // Hiện diện: tỉ lệ kiếm đã vào chỗ trong Kiếm Luân (0 khi tắt Ngự Kiếm Thuật hoặc không ở đội hình Kiếm Luân)
        float target = 0f;
        if (swarm.IsActive && swarm.CurrentFormation == FormationType.Wheel)
        {
            int total = Mathf.Max(1, swarm.SwordCount);
            target = Mathf.Clamp01((float)swarm.ReadySwordCount / total);
        }
        presence = Mathf.MoveTowards(presence, target, presenceSpeed * Time.unscaledDeltaTime);

        float opacity = peakOpacity * presence * swarm.FormationOpacity;
        if (opacity <= 0.002f)
        {
            if (quadRenderer.enabled) quadRenderer.enabled = false;
            return;
        }
        if (!quadRenderer.enabled) quadRenderer.enabled = true;

        Vector3 normal = swarm.WheelNormal;
        float breath = 1f + breathAmount * Mathf.Sin(Time.time * breathFrequency * Mathf.PI * 2f);
        float diameter = 2f * swarm.WheelOuterRadius * radiusRatio * breath;

        quad.position = swarm.WheelCenter - normal * behindOffset;
        quad.rotation = Quaternion.LookRotation(normal, Vector3.up);
        quad.localScale = new Vector3(diameter, diameter, 1f);

        Color c = new Color(tint.r, tint.g, tint.b, opacity);
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        quadRenderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        if (quad != null && quad.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null) Destroy(mf.sharedMesh);
    }
}
