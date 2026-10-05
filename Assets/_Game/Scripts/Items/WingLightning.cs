using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

/// <summary>
/// Sấm sét của Phong Lôi Sí theo phong cách phim (nằm trong prefab PhongLoiSi, đối tượng con "HieuUngSet"; dựng bằng menu
/// Phàm Kiếm Lục → Sấm sét Phong Lôi Sí → Gắn vào prefab PhongLoiSi). Hai hệ màu: LÔI vàng kim (lõi gần trắng, quầng cam vàng) và PHONG xanh lơ ngọc
/// (lõi gần trắng, quầng xanh). Toàn bộ tia vẽ bằng LineRenderer sinh bằng code (chia đoạn, lệch ngẫu nhiên, đẻ nhánh), mọi tia đều bắt đầu từ một điểm
/// trên cánh nên không tia nào rời cánh bay lạc.
/// - Tia dọc cánh: mỗi cánh 2 tia (vàng và xanh, đi lệch nhau), dày, đổi hình liên tục, luôn hiện (mờ hơn khi cường độ thấp).
/// - Tia tỏa: từ các điểm trên cánh bắn ra xa thân người 0.5-1.5m, sống 0.1-0.2s, mỗi tia có 1-3 nhánh con; luôn có vài tia nhỏ, đợt bùng thì dày đặc.
/// - Đợt bùng: cứ 0.5-1.5s một đợt 0.2-0.3s: tia dày và sáng hơn, nhiều tia tỏa, viền lông cánh, quầng sáng sau lưng và hai đèn (vàng/xanh) sáng lên.
/// - Quầng sáng sau lưng: quad cộng sáng luôn quay về camera, đặt ngay sau nhân vật nhìn từ camera nên không che người.
/// - Vệt ở đầu cánh khi bay: một dải vàng cam cuộn như lửa + một dải xanh mảnh thẳng sát bên; tăng tốc dài hơn, sáng hơn, kèm tia lửa vàng/xanh bay ngược ra sau.
/// - Bụi đáp đất (BuiDapDat.vfx) khi hạ cánh.
/// Cường độ gom vào biến "cuongDoSet" (0-1) do PhongLoiSi đặt theo trạng thái (xếp 0, đứng đất 0.35, bay thường 0.65, tăng tốc 1).
/// </summary>
[DefaultExecutionOrder(50)]   // sau WingVisual (đặt vị trí/hướng cánh), để các điểm sét đã ở đúng chỗ
public class WingLightning : MonoBehaviour
{
    [Header("Cường độ")]
    [Tooltip("Cường độ sét hiện tại, 0-1 (0 = tắt hết, 1 = mạnh nhất). PhongLoiSi đặt theo trạng thái: cánh xếp 0, đứng trên đất 0.35, bay thường 0.65, tăng tốc 1. Chạy mượt theo giá trị PhongLoiSi đặt; muốn chỉnh tay để thử thì bật 'Đặt tay'.")]
    [SerializeField, Range(0f, 1f)] private float cuongDoSet = 0f;

    [Tooltip("Bật: bỏ qua giá trị PhongLoiSi đặt, dùng đúng số 'cuongDoSet' kéo trong Inspector (để thử hiệu ứng). Cảnh TestVFX tự bật.")]
    [SerializeField] private bool datTay;

    [Tooltip("Thời gian chuyển mượt khi cường độ đổi (giây).")]
    [SerializeField, Min(0.01f)] private float levelSmoothTime = 0.25f;

    [Header("Độ sáng (HDR, để Bloom bắt rõ)")]
    [Tooltip("Độ sáng tia sét lúc thường (nhân với màu trong ảnh tia).")]
    [SerializeField, Min(0.1f)] private float boltHdr = 1.0f;

    [Tooltip("Độ sáng tia sét lúc bùng.")]
    [SerializeField, Min(0.1f)] private float boltHdrBurst = 1.5f;

    [Header("Hai tia chính dọc cánh")]
    [Tooltip("Độ dày hai tia chính dọc cánh (mét) ở cường độ 1. Ảnh tia có lõi gần trắng ở giữa nên lõi mảnh hơn nhiều.")]
    [SerializeField, Min(0.01f)] private float mainWidth = 0.14f;

    [Tooltip("Tia xanh chạy lệch vào phía lông cánh bấy nhiêu mét so với tia vàng chạy theo xương cánh.")]
    [SerializeField, Min(0f)] private float mainSideOffset = 0.17f;

    [Tooltip("Độ lệch ngẫu nhiên của tia chính (mét): càng lớn càng ngoằn ngoèo.")]
    [SerializeField, Min(0f)] private float mainJitter = 0.03f;

    [Tooltip("Mỗi bao nhiêu giây tia chính đổi hình một lần (giây).")]
    [SerializeField, Min(0.01f)] private float mainRefresh = 0.045f;

    [Header("Tia tỏa ra ngoài (có nhánh)")]
    [Tooltip("Số tia tỏa mỗi giây cho mỗi cánh khi bình thường: (cường độ thấp nhất, cường độ 1).")]
    [SerializeField] private Vector2 arcRate = new Vector2(2.5f, 9f);

    [Tooltip("Lúc bùng, số tia tỏa nhân thêm bấy nhiêu lần.")]
    [SerializeField, Min(1f)] private float burstArcMultiplier = 2.2f;

    [Tooltip("Số tia tỏa tối đa cùng lúc cho mỗi cánh (giới hạn để không nặng máy; giảm số này trước nếu bị chậm). Đổi số này cần dựng lại prefab hoặc khởi động lại Play.")]
    [SerializeField, Range(2, 24)] private int arcPoolPerWing = 7;

    [Tooltip("Chiều dài tia tỏa (mét): nhỏ nhất - lớn nhất. Cường độ thấp thì tia ngắn hơn.")]
    [SerializeField] private Vector2 arcLength = new Vector2(0.5f, 1.5f);

    [Tooltip("Thời gian sống của mỗi tia tỏa (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 arcLife = new Vector2(0.1f, 0.2f);

    [Tooltip("Số nhánh con của mỗi tia tỏa (nhỏ nhất - lớn nhất).")]
    [SerializeField] private Vector2Int arcBranches = new Vector2Int(1, 3);

    [Tooltip("Độ dày tia tỏa (mét).")]
    [SerializeField, Min(0.01f)] private float arcWidth = 0.07f;

    [Header("Đợt bùng")]
    [Tooltip("Khoảng cách giữa hai đợt bùng (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 burstInterval = new Vector2(0.5f, 1.5f);

    [Tooltip("Mỗi đợt bùng kéo dài (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 burstDuration = new Vector2(0.2f, 0.3f);

    [Header("Quầng sáng sau lưng")]
    [Tooltip("Kích thước quầng sáng (mét).")]
    [SerializeField, Min(0.5f)] private float glowSize = 3.6f;

    [Tooltip("Độ đậm quầng lúc thường (rất nhạt) và lúc bùng.")]
    [SerializeField] private Vector2 glowAlpha = new Vector2(0.07f, 0.55f);

    [Tooltip("Quầng đặt xa hơn nhân vật bấy nhiêu mét theo hướng ra xa camera, để nằm sau người, không che.")]
    [SerializeField, Min(0.3f)] private float glowDistance = 1.2f;

    [Header("Hai đèn (vàng - xanh)")]
    [Tooltip("Độ sáng mỗi đèn lúc bùng ở cường độ 1 (yếu). Đèn chỉ chiếu mặt đất/môi trường, không chiếu nhân vật nên không nhuộm màu người.")]
    [SerializeField, Min(0f)] private float lightPeak = 1.3f;

    [Tooltip("Tầm chiếu của đèn (mét).")]
    [SerializeField, Min(0.5f)] private float lightRange = 4f;

    [Header("Vệt ở đầu cánh khi bay")]
    [Tooltip("Chiều dài vệt khi bay thường (mét), mờ.")]
    [SerializeField, Min(0.1f)] private float trailLengthCruise = 1.5f;

    [Tooltip("Chiều dài vệt khi tăng tốc (mét), sáng.")]
    [SerializeField, Min(0.1f)] private float trailLengthBoost = 4.5f;

    [Tooltip("Bề rộng đầu dải lửa (mét), thu về 0 ở đuôi.")]
    [SerializeField, Min(0.01f)] private float trailWidth = 0.2f;

    [Tooltip("Bề rộng đầu dải xanh (mét).")]
    [SerializeField, Min(0.01f)] private float ribbonWidth = 0.09f;

    [Tooltip("Dải xanh nằm lệch khỏi dải lửa bấy nhiêu mét.")]
    [SerializeField, Min(0f)] private float ribbonGap = 0.22f;

    [Tooltip("Tốc độ trôi của hoa văn nhiễu dọc theo dải lửa.")]
    [SerializeField] private float trailScroll = 2.2f;

    [Header("Tia lửa vàng/xanh")]
    [Tooltip("Số tia lửa mỗi giây ở mỗi màu mỗi đầu cánh: (bay thường, tăng tốc).")]
    [SerializeField] private Vector2 sparkRate = new Vector2(8f, 55f);

    [Tooltip("Số tia lửa trong chùm lúc cất cánh / hạ cánh / gập cánh, mỗi màu mỗi đầu cánh.")]
    [SerializeField, Min(0)] private int sparkBurstCount = 30;

    [Header("Bụi đáp đất")]
    [Tooltip("Số hạt bụi mỗi giây trong lúc phát (ngắn), tổng số hạt = số này x thời gian phát.")]
    [SerializeField, Min(0f)] private float dustRate = 320f;

    [Tooltip("Thời gian phát bụi một lần (giây).")]
    [SerializeField, Min(0.02f)] private float dustTime = 0.15f;

    [Header("Tham chiếu (do menu dựng sấm sét gán, không cần chỉnh)")]
    [SerializeField] private Transform[] pointsLeft = new Transform[8];
    [SerializeField] private Transform[] pointsRight = new Transform[8];
    [SerializeField] private Material matGold;
    [SerializeField] private Material matBlue;
    [SerializeField] private Material matGlow;
    [SerializeField] private Material matFire;
    [SerializeField] private Material matRibbon;
    [SerializeField] private Material matSpark;
    [SerializeField] private VisualEffect dustVfx;

    private const int PointCount = 8;
    private const int MainSub = 4;
    private const int MainCount = (PointCount - 1) * MainSub + 1;

    private class Arc
    {
        public LineRenderer lr;
        public bool on, gold;
        public float start, end, nextShape;
        public int side, anchor;
        public Vector3 dirLocal;
        public float length;
        public readonly List<Vector3> local = new List<Vector3>(32);
        public Vector3[] world = new Vector3[32];
    }

    private class MainBolt
    {
        public LineRenderer lr;
        public int side;
        public bool gold;
        public Vector3[] noise = new Vector3[MainCount];
        public Vector3[] pos = new Vector3[MainCount];
        public float nextRoll;
    }

    /// <summary>Chỉ dùng khi kiểm thử (chụp ảnh bằng camera tạm): camera mà quầng sáng quay mặt về. Null = camera chính.</summary>
    public static Camera ViewCamera;

    private WingVisual wings;
    private Transform runtime;
    private readonly List<Arc> arcs = new List<Arc>();
    private readonly List<MainBolt> mains = new List<MainBolt>();
    private Material mGold, mBlue, mGlow, mFire, mRibbon, mSpark;
    private readonly List<Vector3> shapeMain = new List<Vector3>(8);       // bộ đệm dùng lại khi dựng hình tia (tránh rác bộ nhớ)
    private readonly bool[] shapeHas = new bool[7];
    private readonly Vector3[][] shapeBranch = new Vector3[7][];
    private float lastTrailAlpha = -1f;
    private Gradient gradFire, gradBlue;
    private readonly ParticleSystem[,] sparks = new ParticleSystem[2, 2];   // [cánh, 0 vàng / 1 xanh]
    private readonly TrailRenderer[] fireTrail = new TrailRenderer[2];
    private readonly TrailRenderer[] blueTrail = new TrailRenderer[2];
    private Transform glowQuad;
    private MeshRenderer glowRenderer;
    private readonly Light[] lights = new Light[2];
    private Animator standaloneAnimator;
    private MaterialPropertyBlock glowBlock, fireBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    private float levelTarget, levelVel;
    private float burst, burstStart = -10f, burstEnd = -10f, nextBurst;
    private float arcAccum0, arcAccum1;
    private float boost01, boostTarget;
    private float dustEnd;
    private bool flying;
    private bool visible = true;
    private Vector3 flightVelocity;

    // ----- API cho PhongLoiSi -----

    /// <summary>Cường độ sét hiện tại 0-1 (cuongDoSet, đã làm mượt).</summary>
    public float CuongDoSet => cuongDoSet;

    /// <summary>Cường độ nhân thêm (Phá Không có thể đặt tạm >1). Mặc định 1.</summary>
    public float Intensity { get; set; } = 1f;

    /// <summary>Đang trong đợt bùng.</summary>
    public bool IsFlashing => Time.time < burstEnd;

    /// <summary>Thời điểm (Time.time) đợt bùng gần nhất bắt đầu.</summary>
    public float LastFlashStart => burstStart;

    /// <summary>PhongLoiSi đặt cường độ mục tiêu theo trạng thái (0-1). Bị bỏ qua khi bật 'Đặt tay'.</summary>
    public void SetLevel(float value01) { levelTarget = Mathf.Clamp01(value01); }

    /// <summary>Bật/tắt chế độ tăng tốc của vệt (vệt dài, sáng, kèm tia lửa bay ngược).</summary>
    public void SetStreaks(bool on) { boostTarget = on ? 1f : 0f; }

    /// <summary>Đang bay (vệt ở đầu cánh) hay không.</summary>
    public void SetFlying(bool on) { flying = on; }

    /// <summary>Vận tốc bay hiện tại (hướng vệt và tia lửa).</summary>
    public void SetFlightVelocity(Vector3 v) { flightVelocity = v; }

    /// <summary>Tắt hết hiệu ứng tạm thời (Phá Không: nhân vật vô hình).</summary>
    public bool Suppressed { get; set; }

    /// <summary>Bùng ngay một đợt (Phá Không dùng).</summary>
    public void Spike(float amount) { if (amount > 0f) StartBurst(Random.Range(burstDuration.x, burstDuration.y)); }

    /// <summary>Bùng ngay một đợt (mở cánh).</summary>
    public void PlayShoulderFlash(float duration = 0.35f) { StartBurst(Mathf.Clamp(duration, burstDuration.x, 0.4f)); }

    /// <summary>Bùng + chùm tia lửa (gập cánh).</summary>
    public void PlayBurst(float duration = 0.4f, float radius = 1.4f) { StartBurst(0.3f); SparkBurst(); }

    /// <summary>Bùng + chùm tia lửa mạnh ở hai đầu cánh (cất cánh).</summary>
    public void PlayTakeoffBurst() { StartBurst(0.25f); SparkBurst(); }

    /// <summary>Chùm tia lửa + bụi tại groundPoint (hạ cánh).</summary>
    public void PlayLandingBurst(Vector3 groundPoint) { SparkBurst(); PlayDust(groundPoint); StartBurst(0.2f); }

    /// <summary>Phát bụi một lần tại vị trí chạm đất.</summary>
    public void PlayDust(Vector3 groundPoint)
    {
        if (dustVfx == null) return;
        dustVfx.transform.position = groundPoint;
        dustVfx.SetFloat("Spawn Rate", dustRate);
        dustVfx.SendEvent("OnPlay");
        dustEnd = Time.time + dustTime;
    }

    // ================= Khởi tạo =================

    private void Awake()
    {
        wings = GetComponentInParent<WingVisual>();
        if (wings == null)
        {
            // Chạy riêng (cảnh TestVFX): cánh vỗ liên tục, chỉnh cường độ tay trong Inspector
            standaloneAnimator = transform.root.GetComponentInChildren<Animator>();
            if (standaloneAnimator != null) standaloneAnimator.Play("Flap");
            datTay = true;
            if (cuongDoSet < 0.01f) cuongDoSet = 0.65f;
        }
        if (dustVfx != null) dustVfx.transform.SetParent(null, true);   // bụi đứng yên ở chỗ phát, không đi theo nhân vật
        mGold = Clone(matGold); mBlue = Clone(matBlue); mGlow = Clone(matGlow);
        mFire = Clone(matFire); mRibbon = Clone(matRibbon); mSpark = Clone(matSpark);
        glowBlock = new MaterialPropertyBlock();
        fireBlock = new MaterialPropertyBlock();

        GameObject rt = new GameObject("TiaSet_ChayLuc");
        runtime = rt.transform;
        runtime.SetParent(transform, false);
        for (int s = 0; s < 2; s++)
        {
            mains.Add(new MainBolt { lr = MakeLine("TiaChinh_Vang_" + s, mGold, mainWidth), side = s, gold = true });
            mains.Add(new MainBolt { lr = MakeLine("TiaChinh_Xanh_" + s, mBlue, mainWidth), side = s, gold = false });
            for (int a = 0; a < arcPoolPerWing; a++)
            {
                Arc arc = new Arc { lr = MakeLine("TiaToa_" + s + "_" + a, a % 2 == 0 ? mGold : mBlue, arcWidth), side = s, gold = a % 2 == 0 };
                arc.lr.enabled = false;
                arcs.Add(arc);
            }
            Transform tip = (s == 0 ? pointsLeft : pointsRight)[PointCount - 1];
            for (int c = 0; c < 2; c++) sparks[s, c] = MakeSparks("TiaLua_" + s + "_" + c, c == 0, tip);
            fireTrail[s] = MakeTrail("DaiLua_" + s, mFire, tip, trailWidth, true);
            blueTrail[s] = MakeTrail("DaiXanh_" + s, mRibbon, tip, ribbonWidth, false);
        }
        // Quầng sáng sau lưng
        GameObject g = new GameObject("QuangLung");
        g.transform.SetParent(runtime, false);
        MeshFilter mf = g.AddComponent<MeshFilter>();
        mf.sharedMesh = QuadMesh();
        glowRenderer = g.AddComponent<MeshRenderer>();
        glowRenderer.sharedMaterial = mGlow;
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowQuad = g.transform;
        // Hai đèn: vàng và xanh, chỉ chiếu lớp môi trường (không nhuộm màu nhân vật)
        for (int i = 0; i < 2; i++)
        {
            GameObject lg = new GameObject(i == 0 ? "Den_Vang" : "Den_Xanh");
            lg.transform.SetParent(runtime, false);
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = i == 0 ? new Color(1f, 0.74f, 0.3f) : new Color(0.3f, 0.82f, 1f);
            l.range = lightRange;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            UniversalAdditionalLightData ld = lg.AddComponent<UniversalAdditionalLightData>();
            ld.renderingLayers = 1u;   // chỉ lớp 0 (môi trường); nhân vật nằm ở lớp 1 nên không bị nhuộm
            lights[i] = l;
        }
        if (dustVfx != null) ApplyDustLook();
        nextBurst = Time.time + Random.Range(0.3f, 0.8f);
    }

    private static Material Clone(Material m)
    {
        if (m == null) { Debug.LogError("[WingLightning] Thiếu vật liệu, hãy chạy menu Gắn vào prefab PhongLoiSi."); return null; }
        return new Material(m);
    }

    private LineRenderer MakeLine(string name, Material mat, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(runtime, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;     // bo tròn góc gãy để tia dày không nhọn như mảnh kính
        lr.sharedMaterial = mat;
        lr.widthMultiplier = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 0;
        return lr;
    }

    private ParticleSystem MakeSparks(string name, bool gold, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = gold ? new Color(2.4f, 1.5f, 0.35f, 1f) : new Color(0.5f, 1.8f, 2.6f, 1f);
        main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = 0f;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 16f; shape.radius = 0.03f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.05f;
        pr.lengthScale = 2.5f;
        pr.sharedMaterial = mSpark;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    private TrailRenderer MakeTrail(string name, Material mat, Transform parent, float width, bool fire)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TrailRenderer t = go.AddComponent<TrailRenderer>();
        t.alignment = LineAlignment.View;
        t.minVertexDistance = fire ? 0.06f : 0.1f;
        t.widthMultiplier = width;
        t.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        t.textureMode = fire ? LineTextureMode.Tile : LineTextureMode.Stretch;
        if (fire) t.textureScale = new Vector2(0.8f, 1f);
        t.sharedMaterial = mat;
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.receiveShadows = false;
        t.time = 0.15f;
        t.emitting = false;
        return t;
    }

    private static Mesh quad;
    private static Mesh QuadMesh()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "QuangLung_Quad" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
        return quad;
    }

    private void ApplyDustLook()
    {
        Gradient dg = new Gradient();
        dg.SetKeys(new[] { new GradientColorKey(new Color(0.55f, 0.53f, 0.5f), 0f), new GradientColorKey(new Color(0.45f, 0.44f, 0.42f), 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0f, 1f) });
        dustVfx.SetGradient("Color Over Life", dg);
        dustVfx.SetFloat("LifeTime", 1.1f);
        dustVfx.SetFloat("Soft Particle Fade Distance", 0.05f);   // mặc định 1m làm bụi sát đất gần như biến mất
        dustVfx.SetAnimationCurve("Size Over Life", new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 1.4f)));
        dustVfx.SetFloat("Spawn Rate", 0f);
        dustVfx.SetVector3("Initial Velocity_vector", new Vector3(0f, 0.7f, 0f));
        dustVfx.SetFloat("Initial Velocity Divergence", 3f);
        dustVfx.SetVector3("Initial Position_position", Vector3.zero);
        dustVfx.SetVector3("Bounds_center", new Vector3(0f, 1f, 0f));
        dustVfx.SetVector3("Bounds_size", new Vector3(10f, 5f, 10f));
    }

    private void OnDestroy()
    {
        if (dustVfx != null) Destroy(dustVfx.gameObject);
        foreach (Material m in new[] { mGold, mBlue, mGlow, mFire, mRibbon, mSpark }) if (m != null) Destroy(m);
    }

    // ================= Điều khiển =================

    private void StartBurst(float duration)
    {
        burstStart = Time.time;
        burstEnd = Time.time + duration;
        nextBurst = Mathf.Max(nextBurst, burstEnd);
    }

    private void SparkBurst()
    {
        if (sparks[0, 0] == null) return;
        for (int s = 0; s < 2; s++)
            for (int c = 0; c < 2; c++)
            {
                sparks[s, c].transform.rotation = Quaternion.LookRotation(RandomBackDir());
                sparks[s, c].Emit(sparkBurstCount);
            }
    }

    private Vector3 RandomBackDir()
    {
        Vector3 b = flightVelocity.sqrMagnitude > 1f ? -flightVelocity.normalized : -transform.forward;
        return (b + Random.insideUnitSphere * 0.9f).normalized;
    }

    private void Update()
    {
        if (runtime == null) return;
        float dt = Time.deltaTime;
        float target = datTay ? cuongDoSet : levelTarget;
        bool hidden = Suppressed || (wings != null && (wings.Hidden || !wings.IsVisible));
        if (hidden) target = 0f;
        float k = datTay ? Mathf.Clamp01(target) : Mathf.SmoothDamp(cuongDoSet, Mathf.Clamp01(target), ref levelVel, levelSmoothTime);
        if (!datTay) cuongDoSet = k;
        bool on = k > 0.01f;
        if (on != visible) { visible = on; runtime.gameObject.SetActive(on); }
        if (!on) { burst = 0f; return; }
        float kk = Mathf.Clamp01(k * Intensity);

        // Lịch đợt bùng: cứ 0.5-1.5s một đợt 0.2-0.3s
        if (Time.time >= nextBurst && Time.time >= burstEnd)
        {
            StartBurst(Random.Range(burstDuration.x, burstDuration.y));
            nextBurst = burstStart + Random.Range(burstInterval.x, burstInterval.y);
            if (nextBurst < burstEnd + 0.1f) nextBurst = burstEnd + 0.1f;
        }
        float targetBurst = Time.time < burstEnd ? 1f : 0f;
        burst = targetBurst > burst ? Mathf.MoveTowards(burst, 1f, dt / 0.04f) : Mathf.MoveTowards(burst, 0f, dt / 0.12f);
        boost01 = Mathf.MoveTowards(boost01, boostTarget, dt / 0.25f);

        // Độ sáng chung
        float hdr = Mathf.Lerp(boltHdr, boltHdrBurst, burst) * Mathf.Lerp(0.55f, 1f, kk);
        SetColor(mGold, hdr); SetColor(mBlue, hdr);

        // Tia tỏa: sinh theo nhịp, nhiều hơn lúc bùng
        float rate = Mathf.Lerp(arcRate.x, arcRate.y, kk) * Mathf.Lerp(1f, burstArcMultiplier, burst);
        SpawnArcs(0, rate * dt, ref arcAccum0, kk);
        SpawnArcs(1, rate * dt, ref arcAccum1, kk);

        UpdateGlowAndLights(kk);
        if (wings != null) wings.SetBurstGlow(burst * kk);
        UpdateSparksAndTrails(kk);

        if (dustEnd > 0f && Time.time >= dustEnd)
        {
            dustEnd = 0f;
            if (dustVfx != null) { dustVfx.SetFloat("Spawn Rate", 0f); dustVfx.SendEvent("OnStop"); }
        }
    }

    private static void SetColor(Material m, float hdr)
    {
        if (m != null) m.SetColor(BaseColorId, new Color(hdr, hdr, hdr, 1f));
    }

    private Transform[] Pts(int side) { return side == 0 ? pointsLeft : pointsRight; }

    private void LateUpdate()
    {
        if (runtime == null || !visible) return;
        float k = Mathf.Clamp01(cuongDoSet * Intensity);
        UpdateMainBolts(k);
        UpdateArcs();
        UpdateGlowTransform();
        UpdateTrailTransforms();
    }

    // ----- Hai tia chính dọc cánh -----

    private void UpdateMainBolts(float kk)
    {
        float width = mainWidth * Mathf.Lerp(0.55f, 1f, kk) * Mathf.Lerp(1f, 1.3f, burst);
        Vector3 up = transform.up;
        foreach (MainBolt b in mains)
        {
            Transform[] pts = Pts(b.side);
            if (Time.time >= b.nextRoll)
            {
                b.nextRoll = Time.time + mainRefresh * Random.Range(0.8f, 1.3f);
                for (int i = 0; i < MainCount; i++)
                {
                    float t = i / (float)(MainCount - 1);
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.6f);
                    b.noise[i] = Random.onUnitSphere * (mainJitter * env * Mathf.Lerp(1f, 1.5f, burst));
                }
            }
            Vector3 side = b.gold ? Vector3.zero : up * mainSideOffset;
            for (int seg = 0; seg < PointCount - 1; seg++)
            {
                Vector3 a = pts[seg].position, c = pts[seg + 1].position;
                for (int j = 0; j < MainSub; j++)
                {
                    int idx = seg * MainSub + j;
                    b.pos[idx] = Vector3.Lerp(a, c, j / (float)MainSub) + side + b.noise[idx];
                }
            }
            b.pos[MainCount - 1] = pts[PointCount - 1].position + side;
            b.lr.widthMultiplier = width;
            b.lr.positionCount = MainCount;
            b.lr.SetPositions(b.pos);
            float a0 = Mathf.Lerp(0.75f, 1f, kk);
            b.lr.startColor = b.lr.endColor = new Color(1f, 1f, 1f, a0);
        }
    }

    // ----- Tia tỏa có nhánh -----

    private void SpawnArcs(int side, float amount, ref float accum, float kk)
    {
        accum += amount;
        while (accum >= 1f)
        {
            accum -= 1f;
            Arc free = null;
            foreach (Arc a in arcs) if (a.side == side && !a.on) { free = a; break; }
            if (free == null) { accum = 0f; return; }
            BeginArc(free, kk);
        }
    }

    private void BeginArc(Arc a, float kk)
    {
        Transform[] pts = Pts(a.side);
        a.anchor = Random.Range(1, PointCount);
        Vector3 outward = pts[a.anchor].position - transform.position;
        outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : transform.right * (a.side == 0 ? -1f : 1f);
        Vector3 dir = (outward + Random.insideUnitSphere * 0.75f + transform.up * 0.15f).normalized;
        if (Vector3.Dot(dir, outward) < 0.15f) dir = (dir + outward * 1.2f).normalized;   // luôn hướng ra xa thân người
        a.dirLocal = transform.InverseTransformDirection(dir);
        a.length = Random.Range(arcLength.x, arcLength.y) * Mathf.Lerp(0.6f, 1f, kk) * Mathf.Lerp(1f, 1.25f, burst);
        a.gold = Random.value < 0.5f;
        a.lr.sharedMaterial = a.gold ? mGold : mBlue;
        a.start = Time.time;
        a.end = Time.time + Random.Range(arcLife.x, arcLife.y);
        a.nextShape = 0f;
        a.on = true;
        a.lr.enabled = true;
        a.lr.widthMultiplier = arcWidth * Mathf.Lerp(0.8f, 1.3f, burst);
    }

    /// <summary>Dựng hình tia tỏa trong hệ trục cánh (offset từ điểm gốc): thân tia chia 6 đoạn lệch ngẫu nhiên + 1-3 nhánh con (đi ra rồi quay lại điểm đẻ nhánh).</summary>
    private void ShapeArc(Arc a, float kk)
    {
        List<Vector3> main = shapeMain;
        main.Clear();
        const int n = 6;
        Vector3 d = a.dirLocal;
        Vector3 perpA = Vector3.Cross(d, Random.onUnitSphere).normalized;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Vector3 p = d * (a.length * t);
            if (i > 0) p += (perpA * Random.Range(-1f, 1f) + Vector3.Cross(d, perpA) * Random.Range(-1f, 1f)) * (a.length * 0.11f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f)) + 0.01f);
            main.Add(p);
        }
        int branches = Random.Range(arcBranches.x, arcBranches.y + 1);
        if (kk < 0.5f) branches = Mathf.Min(branches, 1);
        bool[] has = shapeHas;
        System.Array.Clear(has, 0, has.Length);
        Vector3[][] bpts = shapeBranch;
        for (int b = 0; b < branches; b++)
        {
            int at = Random.Range(2, n);
            if (has[at]) continue;
            has[at] = true;
            Vector3 axis = Random.onUnitSphere;
            Vector3 bd = Quaternion.AngleAxis(Random.Range(25f, 55f) * (Random.value < 0.5f ? 1f : -1f), Vector3.Cross(d, axis).normalized) * d;
            float bl = a.length * Random.Range(0.22f, 0.45f);
            Vector3[] bp = bpts[at] != null ? bpts[at] : (bpts[at] = new Vector3[3]);
            Vector3 cur = main[at];
            for (int i = 0; i < 3; i++)
            {
                cur += bd * (bl / 3f) + Random.onUnitSphere * (bl * 0.12f);
                bp[i] = cur;
            }
        }
        a.local.Clear();
        for (int i = 0; i <= n; i++)
        {
            a.local.Add(main[i]);
            if (has[i]) { foreach (Vector3 q in bpts[i]) a.local.Add(q); a.local.Add(main[i]); }
        }
    }

    private void UpdateArcs()
    {
        float kk = Mathf.Clamp01(cuongDoSet * Intensity);
        foreach (Arc a in arcs)
        {
            if (!a.on) continue;
            if (Time.time >= a.end) { a.on = false; a.lr.enabled = false; continue; }
            if (Time.time >= a.nextShape) { ShapeArc(a, kk); a.nextShape = Time.time + 0.04f; }
            Vector3 origin = Pts(a.side)[a.anchor].position;
            int cnt = a.local.Count;
            if (a.world.Length < cnt) a.world = new Vector3[cnt + 8];
            for (int i = 0; i < cnt; i++) a.world[i] = origin + transform.TransformVector(a.local[i]);
            a.lr.positionCount = cnt;
            a.lr.SetPositions(a.world);
            float age = (Time.time - a.start) / Mathf.Max(0.01f, a.end - a.start);
            float alpha = 1f - age * age;
            a.lr.startColor = a.lr.endColor = new Color(1f, 1f, 1f, alpha);
        }
    }

    // ----- Quầng sáng, đèn -----

    private void UpdateGlowTransform()
    {
        Camera cam = ViewCamera != null ? ViewCamera : Camera.main;
        if (cam == null || glowQuad == null) return;
        Vector3 center = transform.position;
        Vector3 dir = center - cam.transform.position;
        if (dir.sqrMagnitude < 0.01f) return;
        dir.Normalize();
        glowQuad.position = center + dir * glowDistance;      // xa camera hơn nhân vật: nằm sau người
        glowQuad.rotation = Quaternion.LookRotation(dir);      // mặt trước (-Z) của quad hướng về camera
        glowQuad.localScale = Vector3.one * glowSize;
    }

    private void UpdateGlowAndLights(float kk)
    {
        float a = Mathf.Lerp(glowAlpha.x, glowAlpha.y, burst) * Mathf.Lerp(0.5f, 1f, kk);
        glowBlock.SetColor(BaseColorId, new Color(1.6f, 1.6f, 1.6f, a));
        glowRenderer.SetPropertyBlock(glowBlock);
        for (int i = 0; i < 2; i++)
        {
            float lit = lightPeak * burst * kk * (1f + 0.25f * Mathf.Sin(Time.time * 60f + i));
            lights[i].intensity = lit;
            bool en = lit > 0.02f;
            if (lights[i].enabled != en) lights[i].enabled = en;
            Vector3 side = transform.right * ((i == 0 ? -1f : 1f) * 0.7f);
            lights[i].transform.position = transform.position + side + transform.up * 0.2f;
        }
    }

    // ----- Vệt và tia lửa -----

    private void UpdateSparksAndTrails(float kk)
    {
        float speed = flightVelocity.magnitude;
        bool trailOn = flying && speed > 3f;
        float length = Mathf.Lerp(trailLengthCruise, trailLengthBoost, boost01);
        float tTime = Mathf.Clamp(length / Mathf.Max(speed, 4f), 0.05f, 0.45f);
        float alpha = Mathf.Lerp(0.35f, 1f, boost01);
        Vector3 back = speed > 1f ? -flightVelocity.normalized : -transform.forward;
        for (int s = 0; s < 2; s++)
        {
            TrailRenderer f = fireTrail[s], bl = blueTrail[s];
            if (f.emitting != trailOn) f.emitting = trailOn;
            if (bl.emitting != trailOn) bl.emitting = trailOn;
            f.time = tTime; bl.time = tTime;
            f.widthMultiplier = trailWidth * Mathf.Lerp(0.8f, 1.2f, boost01);
            if (Mathf.Abs(alpha - lastTrailAlpha) > 0.02f) { RebuildTrailGradients(alpha); lastTrailAlpha = alpha; }
            f.colorGradient = gradFire;
            bl.colorGradient = gradBlue;
            // Hoa văn nhiễu trôi dọc theo dải lửa
            fireBlock.SetVector(BaseMapStId, new Vector4(1f, 1f, -Time.time * trailScroll, 0f));
            f.SetPropertyBlock(fireBlock);

            // Tia lửa bay ngược ra sau: thường nhẹ, tăng tốc dày
            float r = trailOn ? Mathf.Lerp(sparkRate.x, sparkRate.y, boost01) * kk : 0f;
            for (int c = 0; c < 2; c++)
            {
                ParticleSystem ps = sparks[s, c];
                var em = ps.emission; em.rateOverTime = r;
                ps.transform.rotation = Quaternion.LookRotation(back);
                if (r > 0f && !ps.isPlaying) ps.Play();
                if (r <= 0f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void RebuildTrailGradients(float alpha)
    {
        if (gradFire == null) { gradFire = new Gradient(); gradBlue = new Gradient(); }
        gradFire.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0.5f), new GradientColorKey(new Color(0.8f, 0.2f, 0.02f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.07f), new GradientAlphaKey(alpha * 0.5f, 0.55f), new GradientAlphaKey(0f, 1f) });
        gradBlue.SetKeys(new[] { new GradientColorKey(new Color(0.75f, 1f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0.8f, 1f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.07f), new GradientAlphaKey(0f, 1f) });
    }

    private void UpdateTrailTransforms()
    {
        // Dải xanh nằm sát bên dải lửa
        for (int s = 0; s < 2; s++)
        {
            Transform tip = Pts(s)[PointCount - 1];
            blueTrail[s].transform.position = tip.position + transform.up * ribbonGap;
            fireTrail[s].transform.position = tip.position;
        }
    }
}
