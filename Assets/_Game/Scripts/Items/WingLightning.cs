using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

/// <summary>
/// Sấm sét của Phong Lôi Sí theo phong cách phim (nằm trong prefab PhongLoiSi, đối tượng con "HieuUngSet"; dựng bằng menu
/// Phàm Kiếm Lục → Sấm sét Phong Lôi Sí → Gắn vào prefab PhongLoiSi). Hai hệ màu: LÔI vàng kim và PHONG xanh lơ ngọc. Mỗi tia là MỘT LineRenderer cộng sáng
/// với ảnh mặt cắt có LÕI gần trắng mảnh và nét (rộng 1/4 bề ngang dải, độ sáng HDR rất cao) bao bởi LỚP NGOÀI màu rộng gấp 4 lần, sáng thấp hơn; phần tỏa sáng
/// còn lại do Bloom. Tia vẽ bằng code (chia đoạn ngắn, lệch ngẫu nhiên thành đường GÃY, đẻ nhánh), mọi tia đều bắt đầu từ một điểm trên cánh nên không tia nào bay lạc.
/// - Tia dọc cánh: mỗi cánh 2 tia (vàng và xanh, đi lệch nhau), đổi hình mỗi 0.03-0.05s, luôn hiện (mờ hơn khi cường độ thấp).
/// - Tia tỏa: từ các điểm trên cánh bắn ra xa thân người 0.5-1.5m, sống 0.1-0.2s, mỗi tia có 1-3 nhánh con; luôn có vài tia nhỏ, đợt bùng thì dày đặc.
/// - Đợt bùng: cứ 0.5-1.5s một đợt 0.2-0.3s: tia sáng hơn, nhiều tia tỏa, viền lông cánh, quầng sáng sau lưng, mảng tối sau cánh và hai đèn (vàng/xanh) hiện rõ.
/// - Quầng sáng sau lưng và mảng tối mềm sau mỗi cánh: tấm tròn quay về camera, tròn mờ dần về đúng 0 trước mép tấm (shader PhamKiemLuc/QuangTron), đặt
///   sau nhân vật nhìn từ camera nên không che người. Mảng tối làm nền cho tia cộng sáng nổi lên trên nền trời sáng.
/// - Vệt ở đầu cánh khi bay: dải lửa vàng cam thuôn nhọn, hoa văn là sợi dài trôi ra sau + một dải xanh mảnh sát bên; tăng tốc dài hơn, sáng hơn, kèm tia lửa vàng/xanh bay ngược ra sau.
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

    [Header("Độ sáng lõi (HDR, để Bloom bắt rõ)")]
    [Tooltip("Độ sáng LÕI tia lúc thường (nhân vào ảnh tia; lớp ngoài tự sáng bằng khoảng 0.22 lần số này). Thử 8-12.")]
    [SerializeField, Min(0.1f)] private float coreHdr = 8f;

    [Tooltip("Độ sáng LÕI tia lúc bùng.")]
    [SerializeField, Min(0.1f)] private float coreHdrBurst = 10f;

    [Header("Độ dày tia")]
    [Tooltip("Bề ngang của LÕI hai tia chính dọc cánh (mét), ở cường độ 1. Lớp ngoài rộng gấp 4 lần số này (tự tính). Nên 0.015-0.03.")]
    [SerializeField, Range(0.005f, 0.06f)] private float mainCoreWidth = 0.028f;

    [Tooltip("Tia xanh chạy lệch vào phía lông cánh bấy nhiêu mét so với tia vàng chạy theo xương cánh.")]
    [SerializeField, Min(0f)] private float mainSideOffset = 0.17f;

    [Header("Độ gãy của tia")]
    [Tooltip("Độ lệch ngẫu nhiên của mỗi đỉnh tia chính (mét): càng lớn càng gãy khúc. Tia chính chia nhiều đoạn ngắn nên đường gãy góc, không uốn mềm.")]
    [SerializeField, Min(0f)] private float mainJitter = 0.05f;

    [Tooltip("Mỗi bao nhiêu giây tia chính đổi hình một lần (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 mainRefresh = new Vector2(0.03f, 0.05f);

    [Tooltip("Mỗi bao nhiêu giây tia tỏa đổi hình một lần (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 arcRefresh = new Vector2(0.03f, 0.05f);

    [Tooltip("Số đoạn của thân mỗi tia tỏa (nhiều đoạn ngắn thì gãy góc nhiều hơn).")]
    [SerializeField, Range(4, 14)] private int arcSegments = 9;

    [Tooltip("Độ lệch ngang của tia tỏa so với chiều dài tia (0.14 = lệch tối đa 14% chiều dài).")]
    [SerializeField, Range(0.02f, 0.4f)] private float arcZigzag = 0.15f;

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

    [Tooltip("Bề ngang của LÕI tia tỏa (mét); lớp ngoài rộng gấp 4 lần. Nên 0.015-0.03.")]
    [SerializeField, Range(0.005f, 0.06f)] private float arcCoreWidth = 0.02f;

    [Header("Đợt bùng")]
    [Tooltip("Khoảng cách giữa hai đợt bùng (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 burstInterval = new Vector2(0.5f, 1.5f);

    [Tooltip("Mỗi đợt bùng kéo dài (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 burstDuration = new Vector2(0.2f, 0.3f);

    [Header("Quầng sáng sau lưng")]
    [Tooltip("Đường kính quầng sáng (mét). Quầng tròn, mờ dần về đúng 0 ở rìa.")]
    [SerializeField, Min(0.5f)] private float glowSize = 3.6f;

    [Tooltip("Độ đậm quầng lúc thường (rất nhạt) và lúc bùng.")]
    [SerializeField] private Vector2 glowAlpha = new Vector2(0.07f, 0.55f);

    [Tooltip("Quầng đặt xa hơn nhân vật bấy nhiêu mét theo hướng ra xa camera, để nằm sau người, không che.")]
    [SerializeField, Min(0.3f)] private float glowDistance = 1.2f;

    [Header("Mảng tối mềm sau cánh (cho tia nổi trên nền sáng)")]
    [Tooltip("Đường kính mảng tối sau mỗi cánh (mét). Tròn, mờ dần về đúng 0 ở rìa.")]
    [SerializeField, Min(0.5f)] private float darkSize = 3.0f;

    [Tooltip("Độ đậm mảng tối: lúc thường (gần như không thấy) và lúc bùng (0.25-0.4).")]
    [SerializeField] private Vector2 darkAlpha = new Vector2(0.06f, 0.4f);

    [Tooltip("Mảng tối đặt xa hơn cánh bấy nhiêu mét theo hướng ra xa camera, để nằm sau cánh và sau người.")]
    [SerializeField, Min(0.1f)] private float darkBehind = 0.55f;

    [Header("Hai đèn (vàng - xanh)")]
    [Tooltip("Độ sáng mỗi đèn lúc bùng ở cường độ 1 (yếu). Đèn chỉ chiếu mặt đất/môi trường, không chiếu nhân vật nên không nhuộm màu người.")]
    [SerializeField, Min(0f)] private float lightPeak = 1.3f;

    [Tooltip("Tầm chiếu của đèn (mét).")]
    [SerializeField, Min(0.5f)] private float lightRange = 4f;

    [Header("Vệt ở đầu cánh khi bay")]
    [Tooltip("Chiều dài vệt khi bay thường (mét), mờ.")]
    [SerializeField, Min(0.1f)] private float trailLengthCruise = 1.5f;

    [Tooltip("Chiều dài vệt khi tăng tốc (mét), sáng.")]
    [SerializeField, Min(0.1f)] private float trailLengthBoost = 3f;

    [Tooltip("Độ đậm tối đa của dải lửa ở đầu vệt khi tăng tốc (0-1; 0.5 = nhìn xuyên qua được, thấy sợi bên trong). Bay thường nhạt hơn. Vệt mờ nhanh về phía đuôi.")]
    [SerializeField, Range(0.05f, 1f)] private float trailMaxAlpha = 0.5f;

    [Tooltip("Bề rộng dải lửa ở đầu cánh (mét), thuôn nhọn dần về 0 ở đuôi.")]
    [SerializeField, Min(0.01f)] private float trailWidth = 0.2f;

    [Tooltip("Bề rộng dải xanh ở đầu cánh (mét), cũng thuôn về 0.")]
    [SerializeField, Min(0.01f)] private float ribbonWidth = 0.08f;

    [Tooltip("Dải xanh nằm lệch khỏi dải lửa bấy nhiêu mét (nhỏ = sát bên).")]
    [SerializeField, Min(0f)] private float ribbonGap = 0.15f;

    [Tooltip("Tốc độ trôi của các sợi trong dải lửa về phía sau (đơn vị ảnh mỗi giây; đổi dấu nếu thấy trôi ngược).")]
    [SerializeField] private float trailScroll = 1.8f;

    [Tooltip("Số lần lặp ảnh sợi trên mỗi mét chiều dài vệt (nhỏ = sợi dài hơn).")]
    [SerializeField, Min(0.1f)] private float trailTiling = 0.45f;

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
    [SerializeField] private Material matDark;
    [SerializeField] private Material matFire;
    [SerializeField] private Material matRibbon;
    [SerializeField] private Material matSpark;
    [SerializeField] private VisualEffect dustVfx;

    /// <summary>Lõi chiếm bấy nhiêu bề ngang dải trong ảnh tia (khớp WingLightningBuilder.MakeBoltTexture): bề ngang dải = bề ngang lõi / số này = 4 lần lõi.</summary>
    public const float CoreFraction = TiaSet.CoreFraction;

    private const int PointCount = 8;
    private const int MainSub = 3;     // mỗi đoạn xương chia 3 đoạn ngắn (~5cm) để đường gãy góc
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
    private Material mGold, mBlue, mGlow, mDark, mFire, mRibbon, mSpark;
    private readonly List<Vector3> shapeBase = new List<Vector3>(16);       // đường gốc của tia tỏa (bộ đệm dùng lại)
    private float lastTrailAlpha = -1f;
    private Gradient gradFire, gradBlue;
    private readonly ParticleSystem[,] sparks = new ParticleSystem[2, 2];   // [cánh, 0 vàng / 1 xanh]
    private readonly TrailRenderer[] fireTrail = new TrailRenderer[2];
    private readonly TrailRenderer[] blueTrail = new TrailRenderer[2];
    private Transform glowQuad;
    private readonly Transform[] darkQuads = new Transform[2];
    private readonly Light[] lights = new Light[2];
    private Animator standaloneAnimator;
    private MaterialPropertyBlock fireBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

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
        mGold = Clone(matGold); mBlue = Clone(matBlue); mGlow = Clone(matGlow); mDark = Clone(matDark);
        mFire = Clone(matFire); mRibbon = Clone(matRibbon); mSpark = Clone(matSpark);
        fireBlock = new MaterialPropertyBlock();

        GameObject rt = new GameObject("TiaSet_ChayLuc");
        runtime = rt.transform;
        runtime.SetParent(transform, false);
        for (int s = 0; s < 2; s++)
        {
            mains.Add(new MainBolt { lr = MakeLine("TiaChinh_Vang_" + s, mGold, mainCoreWidth / CoreFraction, false), side = s, gold = true });
            mains.Add(new MainBolt { lr = MakeLine("TiaChinh_Xanh_" + s, mBlue, mainCoreWidth / CoreFraction, false), side = s, gold = false });
            for (int a = 0; a < arcPoolPerWing; a++)
            {
                Arc arc = new Arc { lr = MakeLine("TiaToa_" + s + "_" + a, a % 2 == 0 ? mGold : mBlue, arcCoreWidth / CoreFraction, true), side = s, gold = a % 2 == 0 };
                arc.lr.enabled = false;
                arcs.Add(arc);
            }
            Transform tip = (s == 0 ? pointsLeft : pointsRight)[PointCount - 1];
            for (int c = 0; c < 2; c++) sparks[s, c] = MakeSparks("TiaLua_" + s + "_" + c, c == 0, tip);
            fireTrail[s] = MakeTrail("DaiLua_" + s, mFire, tip, trailWidth, true);
            blueTrail[s] = MakeTrail("DaiXanh_" + s, mRibbon, tip, ribbonWidth, false);
        }
        // Quầng sáng sau lưng và hai mảng tối mềm sau hai cánh (tấm tròn quay về camera)
        glowQuad = MakeRoundQuad("QuangLung", mGlow);
        darkQuads[0] = MakeRoundQuad("MangToi_0", mDark);
        darkQuads[1] = MakeRoundQuad("MangToi_1", mDark);
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

    private Transform MakeRoundQuad(string name, Material mat)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(runtime, false);
        g.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return g.transform;
    }

    private LineRenderer MakeLine(string name, Material mat, float width, bool taper)
    {
        // Bộ tạo tia dùng chung với cổng không gian (TiaSet); width ở đây là bề ngang cả dải
        return TiaSet.MakeLine(runtime, name, mat, width * TiaSet.CoreFraction, taper);
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
        // Đầu vệt bo tròn (vào từ 0.3 lên 1 trong 8% đầu, không cụt vuông), rồi thuôn nhọn dần về 0 ở đuôi
        t.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.08f, 1f), new Keyframe(1f, 0f));
        t.textureMode = fire ? LineTextureMode.Tile : LineTextureMode.Stretch;
        if (fire) t.textureScale = new Vector2(trailTiling, 1f);
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
        foreach (Material m in new[] { mGold, mBlue, mGlow, mDark, mFire, mRibbon, mSpark }) if (m != null) Destroy(m);
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
        float hdr = Mathf.Lerp(coreHdr, coreHdrBurst, burst) * Mathf.Lerp(0.55f, 1f, kk);
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
        float width = mainCoreWidth / CoreFraction * Mathf.Lerp(0.6f, 1f, kk) * Mathf.Lerp(1f, 1.25f, burst);
        Vector3 up = transform.up;
        foreach (MainBolt b in mains)
        {
            Transform[] pts = Pts(b.side);
            if (Time.time >= b.nextRoll)
            {
                b.nextRoll = Time.time + Random.Range(mainRefresh.x, mainRefresh.y);
                // Mỗi đỉnh lệch ngẫu nhiên vuông góc với đoạn xương, đổi chiều xen kẽ nên đường gãy khúc thành hình răng cưa chứ không uốn mềm
                float sign = Random.value < 0.5f ? 1f : -1f;
                for (int i = 0; i < MainCount; i++)
                {
                    float t = i / (float)(MainCount - 1);
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.5f);
                    Vector3 dir = pts[Mathf.Min(PointCount - 1, (i / MainSub) + 1)].position - pts[Mathf.Min(PointCount - 2, i / MainSub)].position;
                    dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.right;
                    Vector3 n = Random.onUnitSphere;
                    n -= dir * Vector3.Dot(n, dir);
                    n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.up;
                    sign = -sign;
                    float mag = mainJitter * env * Random.Range(0.4f, 1.2f) * Mathf.Lerp(1f, 1.4f, burst);
                    b.noise[i] = (n * 0.55f + Vector3.Cross(dir, n) * 0.35f * sign) * mag * (i % 2 == 0 ? 1f : -1f);
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
        a.lr.widthMultiplier = arcCoreWidth / CoreFraction * Mathf.Lerp(0.8f, 1.3f, burst);
    }

    /// <summary>Dựng hình tia tỏa trong hệ trục cánh (offset từ điểm gốc) bằng bộ sinh tia dùng chung TiaSet: đường gốc thẳng chia arcSegments đoạn, lệch gãy khúc + 1-3 nhánh con.</summary>
    private void ShapeArc(Arc a, float kk)
    {
        int n = arcSegments;
        shapeBase.Clear();
        for (int i = 0; i <= n; i++) shapeBase.Add(a.dirLocal * (a.length * (i / (float)n)));
        int branches = Random.Range(arcBranches.x, arcBranches.y + 1);
        if (kk < 0.5f) branches = Mathf.Min(branches, 1);
        TiaSet.BuildPath(a.local, shapeBase, a.length * arcZigzag, Random.onUnitSphere, 0.3f, branches, a.length, false);
    }

    private void UpdateArcs()
    {
        float kk = Mathf.Clamp01(cuongDoSet * Intensity);
        foreach (Arc a in arcs)
        {
            if (!a.on) continue;
            if (Time.time >= a.end) { a.on = false; a.lr.enabled = false; continue; }
            if (Time.time >= a.nextShape) { ShapeArc(a, kk); a.nextShape = Time.time + Random.Range(arcRefresh.x, arcRefresh.y); }
            Vector3 origin = Pts(a.side)[a.anchor].position;
            int cnt = a.local.Count;
            if (a.world.Length < cnt) a.world = new Vector3[cnt + 8];
            for (int i = 0; i < cnt; i++) a.world[i] = origin + transform.TransformVector(a.local[i]);
            a.lr.positionCount = cnt;
            a.lr.SetPositions(a.world);
            float age = (Time.time - a.start) / Mathf.Max(0.01f, a.end - a.start);
            float alpha = 1f - age * age * age * age;     // sáng đủ gần hết đời tia rồi tắt nhanh (không để tia mờ xám)
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
        // Mảng tối: giữa mỗi cánh (điểm giữa các điểm sét), đẩy ra xa camera
        for (int s = 0; s < 2; s++)
        {
            Transform[] pts = Pts(s);
            Vector3 mid = (pts[2].position + pts[5].position) * 0.5f;
            Vector3 d2 = mid - cam.transform.position;
            if (d2.sqrMagnitude < 0.01f) continue;
            d2.Normalize();
            darkQuads[s].position = mid + d2 * darkBehind;
            darkQuads[s].rotation = Quaternion.LookRotation(d2);
            darkQuads[s].localScale = Vector3.one * darkSize;
        }
    }

    private void UpdateGlowAndLights(float kk)
    {
        // Quầng sáng và mảng tối tính độ đậm trong shader (tròn, về đúng 0 trước mép tấm)
        mGlow.SetFloat(AlphaId, Mathf.Lerp(glowAlpha.x, glowAlpha.y, burst) * Mathf.Lerp(0.5f, 1f, kk));
        mDark.SetFloat(AlphaId, Mathf.Lerp(darkAlpha.x, darkAlpha.y, burst) * Mathf.Lerp(0.6f, 1f, kk));
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
        float alpha = trailMaxAlpha * Mathf.Lerp(0.6f, 1f, boost01);
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
        // Đầu vệt (sát đầu cánh) nóng và đậm nhất, mờ dần về đuôi; alpha về 0 hẳn ở đuôi nên đuôi nhọn, không có đầu cụt
        // Vàng kim sáng, gần trắng ở đầu, vàng kim ở giữa, vàng đậm ở đuôi (không đỏ/cam). Độ đậm tối đa = alpha (0.5) ở đầu, mờ nhanh: còn 45% ở 25% chiều dài, 12% ở nửa vệt, hết ở 80%.
        gradFire.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.98f, 0.85f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0.3f), new GradientColorKey(new Color(1f, 0.72f, 0.2f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.06f), new GradientAlphaKey(alpha * 0.45f, 0.25f), new GradientAlphaKey(alpha * 0.12f, 0.5f), new GradientAlphaKey(0f, 0.8f), new GradientAlphaKey(0f, 1f) });
        float ra = Mathf.Min(1f, alpha * 1.6f);
        gradBlue.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 1f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0.8f, 1f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(ra, 0.07f), new GradientAlphaKey(ra * 0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
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
