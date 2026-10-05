using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cổng không gian của Phá Không (chỉ phần nhìn; chưa có dịch chuyển). Prefab Assets/_Game/VFX/CongKhongGian.prefab, dựng bằng menu
/// Phàm Kiếm Lục → Cổng không gian → "Dựng prefab CongKhongGian". Một xoáy tròn đường kính ngoài 3,2m, mặt xoáy vuông góc trục Z của đối tượng
/// (nhân vật lao dọc trục Z qua tâm), nhìn được cả hai mặt, không khung. Ba vòng: lửa vàng kim bập bùng ở ngoài, mây trắng xanh xoáy 6 cánh ở giữa,
/// lõi tối mép gãy góc ở tâm (xem shader PhamKiemLuc/CongKhongGian). Thành phần phụ: dải khói cuốn theo xoáy (hạt có vệt), đốm sáng bị hút vào tâm,
/// sét vàng/xanh chạy ở vòng ngoài và bắn ngắn ra ngoài (cùng ảnh tia, độ sáng và bề rộng lõi với sét Phong Lôi Sí), quầng sáng QuangTron, một đèn điểm yếu.
/// - Mo(): 0,3s: điểm sáng trắng lóe, nở thành xoáy (quay nhanh rồi chậm lại), vòng lửa ngoài bung ra cuối cùng.
/// - Dong(): 0,25s: xoáy co về tâm quay nhanh dần, lóe một cái rồi tắt, để lại vài dải khói và tia lửa tan dần.
/// - doMo (0-1): kéo tay trong Inspector để xem từng pha (0 = tắt, 1 = mở hết).
/// Bề rộng tia sét tính theo mét thế giới (LineRenderer dùng tọa độ thế giới) nên không bị nhân theo scale của đối tượng cha.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CongKhongGian : MonoBehaviour
{
    /// <summary>Bán kính cổng / nửa cạnh tấm quad (khớp _PortalFrac của shader): tấm to hơn cổng để rìa tan dần hết trước mép tấm.</summary>
    public const float PortalFrac = 0.7273f;

    [Header("Trạng thái")]
    [Tooltip("Độ mở của cổng, 0-1 (0 = tắt, 1 = mở hết). Mo() và Dong() chạy số này theo thời gian; muốn xem từng pha thì kéo tay (gần 0.1 là điểm sáng trắng vừa nở, 0.5 là xoáy đang nở chưa có vòng lửa, 1 là đủ ba vòng).")]
    [SerializeField, Range(0f, 1f)] private float doMo = 0f;

    [Tooltip("Thời gian mở cổng (giây).")]
    [SerializeField, Min(0.05f)] private float thoiGianMo = 0.3f;

    [Tooltip("Thời gian đóng cổng (giây).")]
    [SerializeField, Min(0.05f)] private float thoiGianDong = 0.25f;

    [Header("Hình dáng")]
    [Tooltip("Đường kính ngoài của cổng (mét), tính cả vòng lửa. Rìa ngoài tan dần nên nhìn lớn hơn một chút.")]
    [SerializeField, Min(0.5f)] private float duongKinhNgoai = 3.2f;

    [Tooltip("Hướng quay: 1 = ngược chiều kim đồng hồ khi nhìn từ phía trước (mặt -Z), -1 = ngược lại.")]
    [SerializeField] private float huongQuay = 1f;

    [Tooltip("Tốc độ quay (radian/giây) lúc mới mở: quay nhanh rồi chậm lại về tốc độ giữ.")]
    [SerializeField, Min(0f)] private float tocDoQuayMoi = 14f;

    [Tooltip("Tốc độ quay khi giữ cổng mở (radian/giây).")]
    [SerializeField, Min(0f)] private float tocDoQuayGiu = 3f;

    [Tooltip("Tốc độ quay lúc sắp đóng (radian/giây): quay nhanh dần.")]
    [SerializeField, Min(0f)] private float tocDoQuayDong = 20f;

    [Tooltip("Độ sáng chung của xoáy (nhân vào màu HDR trong vật liệu). 1 = như thiết kế; cổng bị cháy trắng mất cánh xoáy thì giảm.")]
    [SerializeField, Range(0.2f, 2f)] private float doSang = 1f;

    [Header("Sét ở vòng ngoài")]
    [Tooltip("Số tia sét chạy dọc vòng ngoài mỗi giây khi cổng mở hết.")]
    [SerializeField, Min(0f)] private float tiaVongMoiGiay = 4f;

    [Tooltip("Số tia sét bắn ngắn ra ngoài mỗi giây khi cổng mở hết.")]
    [SerializeField, Min(0f)] private float tiaToaMoiGiay = 5f;

    [Tooltip("Số tia sét tối đa cùng lúc (giới hạn để nhẹ máy).")]
    [SerializeField, Range(2, 24)] private int soTiaToiDa = 10;

    [Tooltip("Bề ngang LÕI tia sét (mét), cùng giá trị đã chốt cho sét Phong Lôi Sí. Lớp ngoài rộng gấp 4 lần.")]
    [SerializeField, Range(0.005f, 0.06f)] private float beRongLoiTia = 0.02f;

    [Tooltip("Độ sáng lõi tia (HDR) lúc thường và lúc bùng, cùng giá trị đã chốt cho sét Phong Lôi Sí.")]
    [SerializeField] private Vector2 doSangLoiTia = new Vector2(8f, 10f);

    [Tooltip("Thời gian sống mỗi tia (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 songTia = new Vector2(0.1f, 0.2f);

    [Tooltip("Mỗi bao nhiêu giây một tia đổi hình (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 doiHinhTia = new Vector2(0.03f, 0.05f);

    [Header("Hạt")]
    [Tooltip("Số dải khói cuốn theo xoáy mỗi giây khi mở hết.")]
    [SerializeField, Min(0f)] private float khoiMoiGiay = 16f;

    [Tooltip("Số dải khói văng ra ngoài rìa mỗi giây khi mở hết.")]
    [SerializeField, Min(0f)] private float khoiVangMoiGiay = 4f;

    [Tooltip("Số đốm sáng bị hút vào tâm mỗi giây khi mở hết.")]
    [SerializeField, Min(0f)] private float domMoiGiay = 45f;

    [Header("Quầng sáng và đèn")]
    [Tooltip("Độ đậm quầng sáng sau cổng khi mở hết (0-1).")]
    [SerializeField, Range(0f, 1f)] private float quangDam = 0.3f;

    [Tooltip("Đường kính quầng sáng (mét).")]
    [SerializeField, Min(1f)] private float quangKichThuoc = 7f;

    [Tooltip("Độ sáng đèn điểm yếu khi mở hết (chiếu mặt đất).")]
    [SerializeField, Min(0f)] private float denDoSang = 1.1f;

    [Header("Tham chiếu (do menu dựng prefab gán, không cần chỉnh)")]
    [SerializeField] private Renderer tamXoay;
    [SerializeField] private Renderer tamQuang;
    [SerializeField] private ParticleSystem khoiXoay;
    [SerializeField] private ParticleSystem khoiVang;
    [SerializeField] private ParticleSystem domHut;
    [SerializeField] private Light den;
    [SerializeField] private Material matSetVang;
    [SerializeField] private Material matSetXanh;

    private const float CoreFraction = 0.25f;   // khớp WingLightning.CoreFraction (ảnh tia: lõi = 1/4 bề ngang)

    private class Arc
    {
        public LineRenderer lr;
        public bool on, ring, gold;
        public float start, end, nextShape, angle, span, length, radius;
        public int sign;
        public readonly List<Vector3> pts = new List<Vector3>(48);
    }

    private readonly List<Arc> arcs = new List<Arc>();
    private Transform runtime;
    private Material mGold, mBlue;
    private MaterialPropertyBlock block, glowBlock;
    private int anim;            // 1 = đang mở, -1 = đang đóng, 0 = đứng yên
    private float angle;
    private float arcAccumRing, arcAccumRadial;
    private bool leftoverEmitted;

    private static readonly int OpenId = Shader.PropertyToID("_Open");
    private static readonly int FireId = Shader.PropertyToID("_Fire");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int AngleId = Shader.PropertyToID("_Angle");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // ----- API -----

    /// <summary>Độ mở hiện tại 0-1.</summary>
    public float DoMo { get => doMo; set => doMo = Mathf.Clamp01(value); }

    /// <summary>Cổng đang mở hẳn hoặc đang mở (không phải đã đóng / đang đóng).</summary>
    public bool DangMo => anim == 1 || (anim == 0 && doMo > 0.5f);

    /// <summary>Mở cổng trong "Thời gian mở" (điểm sáng, xoáy nở, vòng lửa bung cuối).</summary>
    public void Mo()
    {
        anim = 1;
        leftoverEmitted = false;
    }

    /// <summary>Đóng cổng trong "Thời gian đóng" (co về tâm, lóe, để lại khói và tia lửa).</summary>
    public void Dong()
    {
        if (doMo <= 0.001f) { doMo = 0f; return; }
        anim = -1;
        leftoverEmitted = false;
        Emit(khoiXoay, 6);
        Emit(khoiVang, 5);
    }

    [ContextMenu("Mở cổng")] private void MenuMo() { if (Application.isPlaying) Mo(); else doMo = 1f; }
    [ContextMenu("Đóng cổng")] private void MenuDong() { if (Application.isPlaying) Dong(); else doMo = 0f; }

    // ----- Vòng đời -----

    private bool built;

    private void Awake() { BuildRuntime(); }

    /// <summary>Tạo phần chạy lúc Play (các tia sét). Gọi cả từ Awake lẫn Update vì [ExecuteAlways] + tắt nạp lại scene khi vào Play thì Awake không chạy lại.</summary>
    private void BuildRuntime()
    {
        if (built || !Application.isPlaying) return;
        built = true;
        Transform old = transform.Find("TiaSet_ChayLuc");
        if (old != null) Destroy(old.gameObject);
        if (block == null) block = new MaterialPropertyBlock();
        if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
        mGold = matSetVang != null ? new Material(matSetVang) : null;
        mBlue = matSetXanh != null ? new Material(matSetXanh) : null;
        GameObject rt = new GameObject("TiaSet_ChayLuc");
        runtime = rt.transform;
        runtime.SetParent(transform, false);
        for (int a = 0; a < soTiaToiDa; a++)
        {
            Arc arc = new Arc { gold = a % 2 == 0 };
            GameObject go = new GameObject("Tia_" + a);
            go.transform.SetParent(runtime, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 1;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f));
            lr.widthMultiplier = beRongLoiTia / CoreFraction;
            lr.sharedMaterial = arc.gold ? mGold : mBlue;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            lr.enabled = false;
            arc.lr = lr;
            arcs.Add(arc);
        }
        if (den != null)
        {
            UniversalAdditionalLightData ld = den.GetComponent<UniversalAdditionalLightData>();
            if (ld != null) ld.renderingLayers = 1u;   // chỉ chiếu lớp môi trường (mặt đất), không nhuộm nhân vật
        }
        ApplyVisuals(0f);
    }

    private void OnEnable()
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
    }

    private void OnValidate() { if (!Application.isPlaying) { OnEnable(); ApplyVisuals(0f); } }

    private void OnDestroy()
    {
        if (mGold != null) Destroy(mGold);
        if (mBlue != null) Destroy(mBlue);
    }

    private void Update()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0f;
        BuildRuntime();
        if (anim == 1)
        {
            doMo = Mathf.MoveTowards(doMo, 1f, dt / thoiGianMo);
            if (doMo >= 1f) anim = 0;
        }
        else if (anim == -1)
        {
            doMo = Mathf.MoveTowards(doMo, 0f, dt / thoiGianDong);
            if (doMo <= 0f) anim = 0;
        }
        // Tốc độ quay: mở thì nhanh rồi chậm lại; đóng thì nhanh dần
        float spin = anim == -1 ? Mathf.Lerp(tocDoQuayGiu, tocDoQuayDong, 1f - doMo)
                                : Mathf.Lerp(tocDoQuayMoi, tocDoQuayGiu, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(doMo / 0.9f)));
        angle += huongQuay * spin * dt;
        ApplyVisuals(dt);
        if (Application.isPlaying) { UpdateParticles(); UpdateLightning(dt); }
    }

    private bool Closing => anim == -1;

    /// <summary>Điểm sáng trắng: lóe ở khoảng doMo 0.1 (cả lúc mở lẫn lúc đóng).</summary>
    private float FlashAmount() { return Mathf.Clamp01(1f - Mathf.Abs(doMo - 0.1f) / 0.1f); }

    private void ApplyVisuals(float dt)
    {
        if (tamXoay == null) return;
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.75f, doMo));    // xoáy nở dần
        float fire = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, doMo));       // vòng lửa bung cuối cùng
        float flash = FlashAmount();
        bool visible = doMo > 0.002f;
        if (tamXoay.enabled != visible) tamXoay.enabled = visible;
        float size = duongKinhNgoai / PortalFrac;
        tamXoay.transform.localScale = new Vector3(size, size, 1f);
        float flicker = 1f + 0.015f * Mathf.Sin(Time.time * 23f) * fire;
        block.Clear();
        block.SetFloat(OpenId, open * flicker);
        block.SetFloat(FireId, fire);
        block.SetFloat(FlashId, flash);
        block.SetFloat(AngleId, angle);
        block.SetFloat(IntensityId, doSang);
        tamXoay.SetPropertyBlock(block);
        if (tamQuang != null)
        {
            bool gv = doMo > 0.02f;
            if (tamQuang.enabled != gv) tamQuang.enabled = gv;
            tamQuang.transform.localScale = new Vector3(quangKichThuoc, quangKichThuoc, 1f);
            glowBlock.Clear();
            glowBlock.SetFloat(AlphaId, quangDam * Mathf.Max(open * 0.8f, flash) * Mathf.Lerp(0.6f, 1f, fire));
            tamQuang.SetPropertyBlock(glowBlock);
        }
        if (den != null)
        {
            float li = denDoSang * Mathf.Max(open, flash) * (0.8f + 0.2f * Mathf.Sin(Time.time * 31f));
            den.intensity = li;
            bool en = li > 0.02f;
            if (den.enabled != en) den.enabled = en;
        }
    }

    private static void Emit(ParticleSystem ps, int n) { if (ps != null && Application.isPlaying) ps.Emit(n); }

    private void UpdateParticles()
    {
        float open = Mathf.InverseLerp(0.05f, 0.75f, doMo);
        float k = Closing ? doMo : open;
        SetRate(khoiXoay, khoiMoiGiay * k);
        SetRate(khoiVang, khoiVangMoiGiay * k);
        SetRate(domHut, domMoiGiay * k);
        // Đóng: khi sắp tắt thì xả thêm một chùm tia lửa tan dần
        if (Closing && doMo < 0.15f && !leftoverEmitted)
        {
            leftoverEmitted = true;
            Emit(domHut, 40);
            Emit(khoiXoay, 4);
        }
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTime = rate;
        if (!ps.isPlaying) ps.Play();
    }

    // ----- Sét: chạy dọc vòng ngoài và bắn ngắn ra ngoài (cùng bộ sinh tia gãy khúc, đổi hình 0.03-0.05s, nhánh đi ra rồi lùi lại đúng đường cũ) -----

    private float Radius => duongKinhNgoai * 0.5f * transform.lossyScale.x;

    private void UpdateLightning(float dt)
    {
        if (mGold == null || mBlue == null) return;
        float fire = Mathf.InverseLerp(0.7f, 1f, doMo);
        float flash = FlashAmount();
        float hdr = Mathf.Lerp(doSangLoiTia.x, doSangLoiTia.y, Mathf.Max(flash, fire > 0.9f ? 0.3f : 0f)) * Mathf.Lerp(0.7f, 1f, fire);
        mGold.SetColor(BaseColorId, new Color(hdr, hdr, hdr, 1f));
        mBlue.SetColor(BaseColorId, new Color(hdr, hdr, hdr, 1f));
        if (fire > 0.05f || flash > 0.3f)
        {
            float boost = 1f + flash * 2f;
            Spawn(true, tiaVongMoiGiay * fire * boost * dt, ref arcAccumRing);
            Spawn(false, tiaToaMoiGiay * fire * boost * dt, ref arcAccumRadial);
        }
        foreach (Arc a in arcs)
        {
            if (!a.on) continue;
            if (Time.time >= a.end) { a.on = false; a.lr.enabled = false; continue; }
            if (Time.time >= a.nextShape) { Shape(a); a.nextShape = Time.time + Random.Range(doiHinhTia.x, doiHinhTia.y); }
            a.lr.positionCount = a.pts.Count;
            Vector3[] arr = a.pts.ToArray();
            a.lr.SetPositions(arr);
            float age = (Time.time - a.start) / Mathf.Max(0.01f, a.end - a.start);
            a.lr.startColor = a.lr.endColor = new Color(1f, 1f, 1f, 1f - age * age * age * age);
        }
    }

    private void Spawn(bool ring, float amount, ref float accum)
    {
        accum += amount;
        while (accum >= 1f)
        {
            accum -= 1f;
            Arc free = null;
            foreach (Arc a in arcs) if (!a.on) { free = a; break; }
            if (free == null) { accum = 0f; return; }
            free.on = true;
            free.ring = ring;
            free.start = Time.time;
            free.end = Time.time + Random.Range(songTia.x, songTia.y);
            free.nextShape = 0f;
            free.angle = Random.value * Mathf.PI * 2f;
            free.sign = Random.value < 0.5f ? 1 : -1;
            free.span = Random.Range(0.5f, 1.3f) * free.sign;
            free.radius = Radius * Random.Range(0.9f, 1f);
            free.length = Random.Range(0.4f, 0.9f);
            free.gold = Random.value < 0.5f;
            free.lr.sharedMaterial = free.gold ? mGold : mBlue;
            free.lr.enabled = true;
        }
    }

    private Vector3 ToWorld(float x, float y, float z)
    {
        return transform.position + transform.right * x + transform.up * y + transform.forward * z;
    }

    private void Shape(Arc a)
    {
        a.pts.Clear();
        float jit = 0.06f;
        if (a.ring)
        {
            const int n = 10;
            float s = Random.value < 0.5f ? 1f : -1f;
            for (int i = 0; i <= n; i++)
            {
                float ang = a.angle + a.span * (i / (float)n);
                s = -s;
                float rad = a.radius + (i == 0 || i == n ? 0f : s * Random.Range(0.3f, 1f) * jit);
                a.pts.Add(ToWorld(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, (i == 0 || i == n) ? 0f : Random.Range(-0.05f, 0.05f)));
            }
            return;
        }
        // Tia tỏa: từ vòng ngoài bắn ra, gãy khúc, 1-2 nhánh con (đi ra rồi lùi đúng đường cũ)
        Vector2 baseP = new Vector2(Mathf.Cos(a.angle), Mathf.Sin(a.angle)) * a.radius;
        float dirAng = a.angle + Random.Range(-0.5f, 0.5f);
        Vector2 d = new Vector2(Mathf.Cos(dirAng), Mathf.Sin(dirAng));
        Vector2 perp = new Vector2(-d.y, d.x);
        const int m = 7;
        Vector3[] main = new Vector3[m + 1];
        float sg = Random.value < 0.5f ? 1f : -1f;
        for (int i = 0; i <= m; i++)
        {
            float t = i / (float)m;
            sg = -sg;
            Vector2 p = baseP + d * (a.length * t);
            if (i > 0) p += perp * (sg * Random.Range(0.4f, 1f) * 0.14f * a.length * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f)) + 0.01f * sg);
            main[i] = ToWorld(p.x, p.y, i == 0 ? 0f : Random.Range(-0.06f, 0.06f));
        }
        int branches = Random.Range(1, 3);
        int at1 = Random.Range(2, m), at2 = Random.Range(2, m);
        for (int i = 0; i <= m; i++)
        {
            a.pts.Add(main[i]);
            if ((i == at1 || (branches > 1 && i == at2 && at2 != at1)))
            {
                Vector3 bd = (transform.right * d.x + transform.up * d.y + Random.onUnitSphere * 0.9f).normalized;
                float bl = a.length * Random.Range(0.25f, 0.45f);
                Vector3 b0 = main[i] + bd * (bl / 3f) + Random.onUnitSphere * bl * 0.1f;
                Vector3 b1 = b0 + bd * (bl / 3f) + Random.onUnitSphere * bl * 0.12f;
                Vector3 b2 = b1 + bd * (bl / 3f) + Random.onUnitSphere * bl * 0.14f;
                a.pts.Add(b0); a.pts.Add(b1); a.pts.Add(b2); a.pts.Add(b1); a.pts.Add(b0); a.pts.Add(main[i]);
            }
        }
    }
}
