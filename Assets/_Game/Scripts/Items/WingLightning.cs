using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu ứng sấm sét của Phong Lôi Sí (tất cả tự dựng bằng code, không cần mô hình):
/// - Tia sét vàng trắng chạy dọc cánh từ gốc ra đầu cánh (LineRenderer, đổi hình ngẫu nhiên mỗi 0.05-0.1 giây, có nhánh rẽ).
/// - Quầng sáng vàng nhỏ nhấp nháy không đều ở giữa lưng, kèm một Point Light vàng nhạt chớp theo tia sét.
/// - Tia lửa điện bắn ra từ đầu cánh (nhiều hơn khi tăng tốc).
/// - Thỉnh thoảng (mỗi 2-4 giây) một tia sét dài phóng ra không khí rồi tắt.
/// - Vệt sét trắng dài kéo phía sau đầu cánh và mép lông khi bay tăng tốc (khoảng 1 giây rồi tan).
/// - Hai luồng sét vàng lóe sau vai khi mở cánh, và loạt sét dày khi cánh tan.
/// Cường độ theo trạng thái (nhẹ khi đứng, vừa khi bay, mạnh khi tăng tốc và Phá Không) do PhongLoiSi đặt qua <see cref="SetLevel"/>;
/// thanh trượt "Cường độ sét" là hệ số tổng để giảm nếu thấy chói hoặc rối mắt.
/// </summary>
public class WingLightning : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Cánh mà các tia sét bám vào.")]
    [SerializeField] private WingVisual wings;

    [Tooltip("Vật liệu cộng sáng (additive) cho tia sét và tia lửa. Mặc định VFX_SetPhongLoi.")]
    [SerializeField] private Material boltMaterial;

    [Tooltip("Vật liệu quầng sáng (additive, texture tròn mờ dần). Mặc định VFX_QuangKiemLuan.")]
    [SerializeField] private Material glowMaterial;

    [Header("Cường độ")]
    [Tooltip("Cường độ sét tổng (0 = tắt hẳn, 1 = bình thường, 2 = gấp đôi). Giảm xuống nếu thấy chói hoặc rối mắt.")]
    [SerializeField, Range(0f, 2f)] private float intensity = 1f;

    [Tooltip("Màu tia sét (vàng trắng).")]
    [SerializeField] private Color boltColor = new Color(1f, 0.88f, 0.45f, 1f);

    [Tooltip("Độ sáng HDR của tia sét, để Bloom bắt sáng.")]
    [SerializeField, Min(0.5f)] private float boltHdr = 3.2f;

    [Tooltip("Độ làm mượt khi cường độ đổi theo trạng thái (giây).")]
    [SerializeField, Min(0.01f)] private float levelSmoothTime = 0.25f;

    [Header("Tia sét dọc cánh")]
    [Tooltip("Số tia sét mỗi cánh khi cường độ thấp nhất (đứng yên).")]
    [SerializeField, Range(1, 5)] private int minBoltsPerWing = 3;

    [Tooltip("Số tia sét mỗi cánh khi cường độ cao nhất (tăng tốc, Phá Không).")]
    [SerializeField, Range(1, 5)] private int maxBoltsPerWing = 5;

    [Tooltip("Số điểm gãy khúc của mỗi tia sét.")]
    [SerializeField, Range(5, 24)] private int boltPoints = 12;

    [Tooltip("Độ lệch ngẫu nhiên của tia sét khỏi đường thẳng (mét) khi cường độ cao nhất.")]
    [SerializeField, Min(0f)] private float boltJitter = 0.16f;

    [Tooltip("Độ rộng tia sét (mét).")]
    [SerializeField, Min(0.005f)] private float boltWidth = 0.035f;

    [Tooltip("Thời gian ngắn nhất giữa hai lần tia sét đổi hình (giây).")]
    [SerializeField, Min(0.02f)] private float refreshMin = 0.05f;

    [Tooltip("Thời gian dài nhất giữa hai lần tia sét đổi hình (giây).")]
    [SerializeField, Min(0.02f)] private float refreshMax = 0.1f;

    [Tooltip("Xác suất mỗi tia sét có thêm một nhánh rẽ (0-1).")]
    [SerializeField, Range(0f, 1f)] private float branchChance = 0.6f;

    [Header("Quầng sáng và đèn ở lưng")]
    [Tooltip("Kích thước quầng sáng vàng giữa hai gốc cánh (mét).")]
    [SerializeField, Min(0.1f)] private float glowSize = 1f;

    [Tooltip("Đèn Point Light vàng nhạt ở lưng: độ sáng cao nhất.")]
    [SerializeField, Min(0f)] private float lightIntensity = 3f;

    [Tooltip("Tầm chiếu sáng của đèn ở lưng (mét).")]
    [SerializeField, Min(0.5f)] private float lightRange = 7f;

    [Tooltip("Màu đèn ở lưng.")]
    [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.45f, 1f);

    [Header("Tia lửa điện")]
    [Tooltip("Số hạt lửa mỗi giây ở mỗi đầu cánh khi cường độ thấp nhất.")]
    [SerializeField, Min(0f)] private float sparksMin = 6f;

    [Tooltip("Số hạt lửa mỗi giây ở mỗi đầu cánh khi cường độ cao nhất.")]
    [SerializeField, Min(0f)] private float sparksMax = 70f;

    [Header("Tia sét dài phóng ra không khí")]
    [Tooltip("Khoảng thời gian ngắn nhất giữa hai lần phóng (giây).")]
    [SerializeField, Min(0.5f)] private float arcIntervalMin = 2f;

    [Tooltip("Khoảng thời gian dài nhất giữa hai lần phóng (giây).")]
    [SerializeField, Min(0.5f)] private float arcIntervalMax = 4f;

    [Tooltip("Độ dài tia sét phóng ra (mét): ngắn nhất - dài nhất.")]
    [SerializeField] private Vector2 arcLength = new Vector2(3f, 6f);

    [Header("Vệt sét khi tăng tốc")]
    [Tooltip("Vật liệu vệt sét trắng (additive). Để trống thì dùng vật liệu tia sét.")]
    [SerializeField] private Material streakMaterial;

    [Tooltip("Vệt sét tồn tại bao lâu rồi tan (giây).")]
    [SerializeField, Min(0.1f)] private float streakTime = 1f;

    [Tooltip("Độ rộng đầu vệt sét (mét).")]
    [SerializeField, Min(0.01f)] private float streakWidth = 0.1f;

    [Tooltip("Số vệt sét mỗi cánh (đầu cánh và mép lông). 3 vệt mỗi cánh = 6 vệt.")]
    [SerializeField, Range(1, 4)] private int streaksPerWing = 3;

    // ----- Nội bộ -----
    private class Bolt
    {
        public LineRenderer line;
        public LineRenderer branch;
        public int side;
        public float nextRefresh;
        public int slot;
    }

    private class Flash
    {
        public LineRenderer line;
        public float end;
        public Vector3 a, b;
        public float refresh;
    }

    private readonly List<Bolt> bolts = new List<Bolt>();
    private readonly List<Flash> flashes = new List<Flash>();
    private readonly List<TrailRenderer> streaks = new List<TrailRenderer>();
    private readonly List<int> streakSide = new List<int>();
    private readonly List<int> streakFeather = new List<int>();
    private readonly ParticleSystem[] sparks = new ParticleSystem[2];
    private Vector3[] buffer;
    private Transform glow;
    private MeshRenderer glowRenderer;
    private Light backLight;
    private LineRenderer longArc;
    private float arcEnd, arcRefresh, nextArc;
    private MaterialPropertyBlock block;
    private MaterialPropertyBlock glowBlock;
    private float level;            // cường độ theo trạng thái (đã làm mượt)
    private float levelTarget;
    private float levelVel;
    private float spike;            // chớp thêm (mở cánh, Phá Không...)
    private bool streaming;
    private bool built;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>Cường độ sét tổng (thanh trượt "Cường độ sét").</summary>
    public float Intensity { get => intensity; set => intensity = Mathf.Clamp(value, 0f, 2f); }

    /// <summary>
    /// Đặt cường độ theo trạng thái (0 = tắt, ~0.3 đứng, ~0.6 bay, 1 tăng tốc, 1.4 Phá Không).
    /// Có làm mượt trong "Độ làm mượt".
    /// </summary>
    public void SetLevel(float value) { levelTarget = Mathf.Max(0f, value); }

    /// <summary>Tắt hẳn mọi tia sét/quầng sáng/đèn (Phá Không: nhân vật vô hình).</summary>
    public bool Suppressed { get; set; }

    /// <summary>Có phát vệt sét dài phía sau không (chỉ khi bay tăng tốc).</summary>
    public void SetStreaks(bool on) { streaming = on; }

    /// <summary>Chớp mạnh một thoáng (decay dần), dùng khi mở cánh, cánh tan, Phá Không.</summary>
    public void Spike(float amount) { spike = Mathf.Max(spike, amount); }

    private void Awake() { Build(); }

    private void Build()
    {
        if (built) return;
        built = true;
        buffer = new Vector3[Mathf.Max(boltPoints, 24)];
        block = new MaterialPropertyBlock();
        glowBlock = new MaterialPropertyBlock();
        nextArc = Time.time + Random.Range(arcIntervalMin, arcIntervalMax);

        for (int s = 0; s < 2; s++)
        {
            for (int k = 0; k < maxBoltsPerWing; k++)
            {
                Bolt b = new Bolt { side = s == 0 ? 1 : -1, slot = k };
                b.line = NewLine("Set_" + s + "_" + k, boltPoints);
                b.branch = NewLine("Nhanh_" + s + "_" + k, 4);
                b.nextRefresh = Time.time + Random.Range(0f, refreshMax);
                bolts.Add(b);
            }
            // Tia lửa điện ở đầu cánh
            sparks[s] = NewSparks("TiaLua_" + (s == 0 ? "Phai" : "Trai"));
        }
        for (int i = 0; i < 6; i++)
        {
            Flash f = new Flash { line = NewLine("Loe_" + i, 10) };
            flashes.Add(f);
        }
        longArc = NewLine("SetDai", 18);

        // Vệt sét: mỗi cánh một số vệt bám đầu cánh và mép lông
        for (int s = 0; s < 2; s++)
        {
            for (int k = 0; k < streaksPerWing; k++)
            {
                TrailRenderer tr = NewStreak("VetSet_" + s + "_" + k);
                streaks.Add(tr);
                streakSide.Add(s == 0 ? 1 : -1);
                int pc = wings != null ? wings.PrimaryCount : 12;
                // 0: đầu cánh (lông ngoài cùng), 1: giữa, 2: gần gốc, 3: rất gần gốc
                streakFeather.Add(Mathf.Clamp(pc - 1 - k * Mathf.Max(1, pc / 4), 0, pc - 1));
            }
        }

        // Quầng sáng ở lưng
        GameObject g = new GameObject("QuangSetLung");
        g.transform.SetParent(transform, false);
        glow = g.transform;
        g.AddComponent<MeshFilter>().sharedMesh = BuildQuad();
        glowRenderer = g.AddComponent<MeshRenderer>();
        glowRenderer.sharedMaterial = glowMaterial;
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowRenderer.enabled = false;

        // Đèn ở lưng
        GameObject lg = new GameObject("DenSetLung");
        lg.transform.SetParent(transform, false);
        backLight = lg.AddComponent<Light>();
        backLight.type = LightType.Point;
        backLight.shadows = LightShadows.None;
        backLight.color = lightColor;
        backLight.range = lightRange;
        backLight.intensity = 0f;
        backLight.enabled = false;
    }

    private LineRenderer NewLine(string name, int points)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = points;
        lr.sharedMaterial = boltMaterial;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View;
        lr.widthMultiplier = boltWidth;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.25f));
        lr.enabled = false;
        return lr;
    }

    private ParticleSystem NewSparks(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new Color(1f, 0.9f, 0.5f, 1f);
        main.gravityModifier = 0f;
        main.maxParticles = 200;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.12f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = boltMaterial;
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 1.6f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        return ps;
    }

    private TrailRenderer NewStreak(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.sharedMaterial = streakMaterial != null ? streakMaterial : boltMaterial;
        tr.time = streakTime;
        tr.minVertexDistance = 0.12f;
        tr.widthMultiplier = streakWidth;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.5f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.emitting = false;
        tr.alignment = LineAlignment.View;
        return tr;
    }

    private static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh { name = "QuangSetLung_Quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));
        return mesh;
    }

    // ================= Cập nhật =================

    private void LateUpdate()
    {
        if (!built) Build();
        if (wings == null) return;
        float dt = Time.deltaTime;

        level = Mathf.SmoothDamp(level, levelTarget, ref levelVel, levelSmoothTime);
        spike = Mathf.MoveTowards(spike, 0f, dt * 2.5f);
        float k = Mathf.Clamp01(level * intensity + spike * 0.5f * intensity);   // 0-1 sau khi nhân hệ số tổng
        bool active = !Suppressed && wings.IsVisible && intensity > 0.001f && (level > 0.01f || spike > 0.01f);

        UpdateBolts(active, k);
        UpdateFlashes();
        UpdateLongArc(active, k);
        UpdateSparks(active, k);
        UpdateStreaks(active);
        UpdateGlowAndLight(active, k);
    }

    private void ApplyColor(LineRenderer lr, float alpha)
    {
        block.SetColor(BaseColorId, new Color(boltColor.r, boltColor.g, boltColor.b, 1f) * boltHdr);
        block.SetColor(ColorId, new Color(boltColor.r, boltColor.g, boltColor.b, 1f) * boltHdr);
        lr.SetPropertyBlock(block);
        lr.startColor = lr.endColor = new Color(1f, 1f, 1f, alpha);
    }

    /// <summary>Dựng đường sét gãy khúc từ a tới b: lệch ngẫu nhiên vuông góc với đường nối, bằng 0 ở hai đầu.</summary>
    private void FillBolt(LineRenderer lr, Vector3 a, Vector3 b, int count, float jitter)
    {
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.001f) { lr.enabled = false; return; }
        dir /= len;
        Vector3 side1 = Vector3.Cross(dir, Vector3.up);
        if (side1.sqrMagnitude < 0.001f) side1 = Vector3.Cross(dir, Vector3.right);
        side1.Normalize();
        Vector3 side2 = Vector3.Cross(dir, side1);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float fall = Mathf.Sin(t * Mathf.PI);                                   // đầu và cuối không lệch
            Vector3 off = (side1 * Random.Range(-1f, 1f) + side2 * Random.Range(-1f, 1f)) * (jitter * fall);
            buffer[i] = Vector3.Lerp(a, b, t) + off;
        }
        lr.positionCount = count;
        lr.SetPositions(buffer);
    }

    private void UpdateBolts(bool active, float k)
    {
        int perWing = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minBoltsPerWing, maxBoltsPerWing, k)), 0, maxBoltsPerWing);
        float jitter = boltJitter * Mathf.Lerp(0.5f, 1.2f, k);
        float alpha = Mathf.Lerp(0.45f, 1f, k) * Mathf.Clamp01(k * 3f);
        int pc = wings.PrimaryCount;

        for (int i = 0; i < bolts.Count; i++)
        {
            Bolt b = bolts[i];
            bool on = active && b.slot < perWing;
            if (b.line.enabled != on) b.line.enabled = on;
            if (!on) { if (b.branch.enabled) b.branch.enabled = false; continue; }
            if (Time.time < b.nextRefresh) continue;
            b.nextRefresh = Time.time + Random.Range(refreshMin, refreshMax);

            // Mỗi tia chạy từ gốc cánh (hoặc một điểm gần gốc) tới đầu một chiếc lông khác nhau, nên các tia tỏa như gân cánh
            Vector3 start = wings.PathPoint(b.side, Random.Range(0f, 0.15f));
            int fi = Mathf.Clamp(pc - 1 - b.slot * Mathf.Max(1, pc / 5) + Random.Range(-1, 2), 0, pc - 1);
            Vector3 end = Random.value < 0.4f ? wings.PathPoint(b.side, Random.Range(0.8f, 1f)) : wings.FeatherTip(b.side, fi);
            FillBolt(b.line, start, end, boltPoints, jitter);
            ApplyColor(b.line, alpha);

            if (Random.value < branchChance && b.line.positionCount > 4)
            {
                int bi = Random.Range(2, b.line.positionCount - 2);
                Vector3 from = b.line.GetPosition(bi);
                Vector3 outward = (end - start).normalized;
                Vector3 to = from + (outward * 0.35f + Random.onUnitSphere * 0.4f) * Random.Range(0.6f, 1.4f);
                FillBolt(b.branch, from, to, 4, jitter * 0.6f);
                ApplyColor(b.branch, alpha * 0.8f);
                b.branch.widthMultiplier = boltWidth * 0.6f;
                b.branch.enabled = true;
            }
            else b.branch.enabled = false;
        }
    }

    // ----- Hai luồng sét lóe sau vai khi mở cánh -----

    /// <summary>Hai luồng sét vàng lóe ở sau vai (lúc mở cánh) trong khoảng duration giây.</summary>
    public void PlayShoulderFlash(float duration = 0.35f)
    {
        if (!built) Build();
        Vector3 center = transform.position;
        Vector3 up = transform.up, right = transform.right, back = -transform.forward;
        for (int s = 0; s < 2 && s < flashes.Count; s++)
        {
            Flash f = flashes[s];
            float sign = s == 0 ? 1f : -1f;
            f.a = center + right * sign * 0.1f;
            f.b = center + right * sign * 0.55f + up * 0.9f + back * 0.25f;
            f.end = Time.time + duration;
            f.refresh = 0f;
        }
        spike = Mathf.Max(spike, 1.2f);
    }

    /// <summary>Loạt sét dày tỏa quanh người trong duration giây (cánh tan thành tia sét, Phá Không).</summary>
    public void PlayBurst(float duration = 0.4f, float radius = 1.4f)
    {
        if (!built) Build();
        Vector3 center = transform.position;
        for (int i = 2; i < flashes.Count; i++)
        {
            Flash f = flashes[i];
            f.a = center + Random.onUnitSphere * 0.2f;
            f.b = center + Random.onUnitSphere * radius;
            f.end = Time.time + duration * Random.Range(0.6f, 1f);
            f.refresh = 0f;
        }
        spike = Mathf.Max(spike, 1.4f);
    }

    private void UpdateFlashes()
    {
        for (int i = 0; i < flashes.Count; i++)
        {
            Flash f = flashes[i];
            bool on = Time.time < f.end && intensity > 0.001f;
            if (f.line.enabled != on) f.line.enabled = on;
            if (!on) continue;
            if (Time.time < f.refresh) continue;
            f.refresh = Time.time + 0.04f;
            FillBolt(f.line, f.a, f.b, 10, 0.18f);
            f.line.widthMultiplier = boltWidth * 2.2f;
            ApplyColor(f.line, Mathf.Clamp01(intensity));
        }
    }

    private void UpdateLongArc(bool active, float k)
    {
        if (longArc.enabled && Time.time >= arcEnd) longArc.enabled = false;
        if (active && Time.time >= nextArc && k > 0.1f)
        {
            nextArc = Time.time + Random.Range(arcIntervalMin, arcIntervalMax) / Mathf.Max(0.5f, k + 0.5f);
            int side = Random.value < 0.5f ? 1 : -1;
            Vector3 a = wings.PathPoint(side, Random.Range(0.4f, 1f));
            Vector3 dir = (transform.right * side * 0.7f + transform.up * Random.Range(-0.3f, 0.6f) - transform.forward * Random.Range(0f, 0.8f) + Random.onUnitSphere * 0.4f).normalized;
            Vector3 b = a + dir * Random.Range(arcLength.x, arcLength.y);
            FillBolt(longArc, a, b, 18, 0.45f);
            longArc.widthMultiplier = boltWidth * 1.8f;
            ApplyColor(longArc, 1f);
            longArc.enabled = true;
            arcEnd = Time.time + 0.12f;
            spike = Mathf.Max(spike, 0.5f);
        }
    }

    private void UpdateSparks(bool active, float k)
    {
        for (int s = 0; s < 2; s++)
        {
            ParticleSystem ps = sparks[s];
            var em = ps.emission;
            em.rateOverTime = active ? Mathf.Lerp(sparksMin, sparksMax, k * k) * intensity : 0f;
            ps.transform.position = wings.PathPoint(s == 0 ? 1 : -1, 1f);
            if (active && !ps.isPlaying) ps.Play();
            else if (!active && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateStreaks(bool active)
    {
        bool emit = active && streaming && wings.FullyOpen;
        for (int i = 0; i < streaks.Count; i++)
        {
            TrailRenderer tr = streaks[i];
            Vector3 p = wings.FeatherTip(streakSide[i], streakFeather[i]);
            tr.transform.position = p + Random.insideUnitSphere * 0.03f;   // rung nhẹ cho vệt gãy khúc như sét
            if (tr.emitting != emit)
            {
                if (emit) tr.Clear();
                tr.emitting = emit;
            }
        }
    }

    private void UpdateGlowAndLight(bool active, float k)
    {
        bool show = active && glowMaterial != null;
        if (glowRenderer.enabled != show) glowRenderer.enabled = show;
        if (backLight.enabled != show) backLight.enabled = show;
        if (!show) return;

        // Nhấp nháy không đều: nhiễu Perlin hai tần số + chớp ngẫu nhiên
        float n = Mathf.PerlinNoise(Time.time * 9f, 0.37f) * 0.6f + Mathf.PerlinNoise(Time.time * 23f, 4.1f) * 0.4f;
        float flick = Mathf.Clamp01(0.35f + n * 0.9f + (Random.value < 0.08f ? 0.5f : 0f));

        Camera cam = Camera.main;
        Vector3 back = -transform.forward;
        glow.position = transform.position + back * 0.05f;
        if (cam != null) glow.rotation = Quaternion.LookRotation(glow.position - cam.transform.position, Vector3.up);
        float size = glowSize * Mathf.Lerp(0.7f, 1.4f, k) * (0.85f + 0.3f * flick);
        glow.localScale = new Vector3(size, size, 1f);
        Color c = new Color(1f, 0.82f, 0.35f, Mathf.Clamp01(0.55f * k * flick * intensity + 0.1f * spike));
        glowBlock.SetColor(BaseColorId, c);
        glowBlock.SetColor(ColorId, c);
        glowRenderer.SetPropertyBlock(glowBlock);

        backLight.transform.position = transform.position + back * 0.35f;
        backLight.intensity = lightIntensity * k * (0.4f + 0.9f * flick) * intensity + spike * lightIntensity * 0.6f;
        backLight.range = lightRange;
        backLight.color = lightColor;
    }
}
