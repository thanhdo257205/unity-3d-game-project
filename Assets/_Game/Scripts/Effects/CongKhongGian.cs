using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cổng không gian của Phá Không. Prefab Assets/_Game/VFX/CongKhongGian.prefab, dựng bằng menu Phàm Kiếm Lục → Cổng không gian → "Dựng prefab CongKhongGian".
/// Một xoáy tròn đường kính ngoài 3,2m, mặt xoáy vuông góc trục Z của đối tượng (nhân vật lao dọc trục Z qua tâm), nhìn được cả hai mặt, không khung.
/// Ba lớp tách nhau dọc trục Z cho có chiều sâu (nhìn chéo thấy như cái phễu): LÕI tối lùi ra sau 0,4m, VÒNG GIỮA (mây xanh lơ trắng xoáy nhiều sợi, đặc) ở 0,
/// VÒNG LỬA vàng kim nhô ra trước 0,1m (xem shader PhamKiemLuc/CongKhongGian). Dải khói cuốn theo xoáy (hạt có vệt) và đốm sáng bị hút vào tâm rải trong ±0,5m.
/// Sét vàng/xanh chạy ở vòng ngoài và bắn ngắn ra ngoài, dùng chung bộ sinh tia TiaSet với sét Phong Lôi Sí. Quầng sáng QuangTron và một đèn điểm yếu.
/// - Mo(): 0,3s: điểm sáng trắng lóe, nở thành xoáy (quay nhanh rồi chậm lại), vòng lửa ngoài bung ra cuối cùng.
/// - Dong(): 0,25s: xoáy co về tâm quay nhanh dần, lóe một cái rồi tắt, để lại vài dải khói và tia lửa tan dần.
/// - doMo (0-1): kéo tay trong Inspector để xem từng pha (0 = tắt, 1 = mở hết).
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

    [Tooltip("Lõi tối lùi ra sau (theo hướng bay, trục +Z) bấy nhiêu mét so với vòng giữa.")]
    [SerializeField, Min(0f)] private float doSauLoi = 0.4f;

    [Tooltip("Vòng lửa nhô ra trước (ngược hướng bay, trục -Z) bấy nhiêu mét so với vòng giữa.")]
    [SerializeField, Min(0f)] private float doNhoLua = 0.1f;

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

    [Header("Sét ở vòng ngoài (bộ sinh tia TiaSet dùng chung với Phong Lôi Sí)")]
    [Tooltip("Số tia sét chạy dọc vòng ngoài mỗi giây khi cổng mở hết.")]
    [SerializeField, Min(0f)] private float tiaVongMoiGiay = 4f;

    [Tooltip("Số tia sét bắn ngắn ra ngoài mỗi giây khi cổng mở hết.")]
    [SerializeField, Min(0f)] private float tiaToaMoiGiay = 5f;

    [Tooltip("Số tia sét tối đa cùng lúc (giới hạn để nhẹ máy).")]
    [SerializeField, Range(2, 24)] private int soTiaToiDa = 10;

    [Tooltip("Bề ngang LÕI tia sét (mét): mảnh như sợi chỉ. Lớp ngoài rộng gấp 4 lần.")]
    [SerializeField, Range(0.004f, 0.04f)] private float beRongLoiTia = 0.012f;

    [Tooltip("Độ sáng lõi tia (HDR) lúc thường và lúc bùng. Lớp ngoài vàng kim / xanh lơ trong ảnh tia của cổng sáng hơn ảnh tia cánh nên vẫn thấy rõ màu ở mức sáng thấp này.")]
    [SerializeField] private Vector2 doSangLoiTia = new Vector2(3.5f, 5.5f);

    [Tooltip("Thời gian sống mỗi tia (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 songTia = new Vector2(0.1f, 0.2f);

    [Tooltip("Mỗi bao nhiêu giây một tia đổi hình (giây): nhỏ nhất - lớn nhất.")]
    [SerializeField] private Vector2 doiHinhTia = new Vector2(0.03f, 0.05f);

    [Header("Hạt")]
    [Tooltip("Số dải khói cuốn theo xoáy mỗi giây khi mở hết.")]
    [SerializeField, Min(0f)] private float khoiMoiGiay = 48f;

    [Tooltip("Số dải khói văng ra ngoài vòng lửa mỗi giây khi mở hết.")]
    [SerializeField, Min(0f)] private float khoiVangMoiGiay = 14f;

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
    [SerializeField] private Renderer tamMeo;
    [SerializeField] private Renderer tamLoi;
    [SerializeField] private Renderer tamGiua;
    [SerializeField] private Renderer tamLua;
    [SerializeField] private Renderer tamQuang;
    [SerializeField] private ParticleSystem khoiXoay;
    [SerializeField] private ParticleSystem khoiVang;
    [SerializeField] private ParticleSystem domHut;
    [SerializeField] private Light den;
    [SerializeField] private Material matSetVang;
    [SerializeField] private Material matSetXanh;

    private class Arc
    {
        public LineRenderer lr;
        public bool on, ring, gold;
        public float start, end, nextShape, angle, span, length, radius;
        public readonly List<Vector3> pts = new List<Vector3>(48);
    }

    private readonly List<Arc> arcs = new List<Arc>();
    private readonly List<Vector3> baseBuf = new List<Vector3>(16);
    private Vector3[] posArr = new Vector3[64];
    private Transform runtime;
    private Material mGold, mBlue;
    private MaterialPropertyBlock block, glowBlock;
    private int anim;            // 1 = đang mở, -1 = đang đóng, 0 = đứng yên
    private float angle;
    private float arcAccumRing, arcAccumRadial;
    private bool leftoverEmitted, built;

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

    /// <summary>Đã tắt hẳn (doMo = 0, không còn đang mở).</summary>
    public bool DaDong => anim != 1 && doMo <= 0.001f;

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

    /// <summary>Đặt thời gian mở / đóng (giây) lúc chạy (kỹ năng Phá Không dùng).</summary>
    public void DatThoiGian(float mo, float dong) { thoiGianMo = Mathf.Max(0.05f, mo); thoiGianDong = Mathf.Max(0.05f, dong); }

    [ContextMenu("Mở cổng")] private void MenuMo() { if (Application.isPlaying) Mo(); else doMo = 1f; }
    [ContextMenu("Đóng cổng")] private void MenuDong() { if (Application.isPlaying) Dong(); else doMo = 0f; }

    // ----- Vòng đời -----

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
            arc.lr = TiaSet.MakeLine(runtime, "Tia_" + a, arc.gold ? mGold : mBlue, beRongLoiTia, true);
            arc.lr.enabled = false;
            arcs.Add(arc);
        }
        if (den != null)
        {
            UniversalAdditionalLightData ld = den.GetComponent<UniversalAdditionalLightData>();
            if (ld != null) ld.renderingLayers = 1u;   // chỉ chiếu lớp môi trường (mặt đất), không nhuộm nhân vật
        }
        ApplyVisuals();
    }

    private void OnEnable()
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
    }

    private void OnValidate() { if (!Application.isPlaying) { OnEnable(); ApplyVisuals(); } }

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
        ApplyVisuals();
        if (Application.isPlaying) { UpdateParticles(); UpdateLightning(dt); }
    }

    private bool Closing => anim == -1;

    /// <summary>Điểm sáng trắng: lóe ở khoảng doMo 0.1 (cả lúc mở lẫn lúc đóng).</summary>
    private float FlashAmount() { return Mathf.Clamp01(1f - Mathf.Abs(doMo - 0.1f) / 0.1f); }

    private void ApplyLayer(Renderer r, float zOffset, float size, bool visible)
    {
        if (r == null) return;
        if (r.enabled != visible) r.enabled = visible;
        r.transform.localPosition = new Vector3(0f, 0f, zOffset);
        r.transform.localScale = new Vector3(size, size, 1f);
        r.SetPropertyBlock(block);
    }

    private void ApplyVisuals()
    {
        if (tamGiua == null || block == null) return;
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.75f, doMo));    // xoáy nở dần
        float fire = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, doMo));       // vòng lửa bung cuối cùng
        float flash = FlashAmount();
        bool visible = doMo > 0.002f;
        float size = duongKinhNgoai / PortalFrac;
        float flicker = 1f + 0.015f * Mathf.Sin(Time.time * 23f) * fire;
        block.Clear();
        block.SetFloat(OpenId, open * flicker);
        block.SetFloat(FireId, fire);
        block.SetFloat(FlashId, flash);
        block.SetFloat(AngleId, angle);
        block.SetFloat(IntensityId, doSang);
        ApplyLayer(tamMeo, 0f, size, visible);
        ApplyLayer(tamLoi, doSauLoi, size, visible);
        ApplyLayer(tamGiua, 0f, size, visible);
        ApplyLayer(tamLua, -doNhoLua, size, visible);
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
            Emit(khoiXoay, 8);
        }
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTime = rate;
        if (!ps.isPlaying) ps.Play();
    }

    // ----- Sét: chạy dọc vòng ngoài và bắn ngắn ra ngoài (hình tia dựng bằng TiaSet.BuildPath, đổi hình 0.03-0.05s) -----

    private float Radius => duongKinhNgoai * 0.5f * transform.lossyScale.x;

    private void UpdateLightning(float dt)
    {
        if (mGold == null || mBlue == null) return;
        float fire = Mathf.InverseLerp(0.7f, 1f, doMo);
        float flash = FlashAmount();
        float hdr = Mathf.Lerp(doSangLoiTia.x, doSangLoiTia.y, Mathf.Max(flash, fire > 0.9f ? 0.3f : 0f));
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
            int cnt = a.pts.Count;
            if (posArr.Length < cnt) posArr = new Vector3[cnt + 16];
            for (int i = 0; i < cnt; i++) posArr[i] = a.pts[i];
            a.lr.positionCount = cnt;
            a.lr.SetPositions(posArr);
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
            free.span = Random.Range(0.5f, 1.3f) * (Random.value < 0.5f ? 1f : -1f);
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
        baseBuf.Clear();
        if (a.ring)
        {
            // Đường gốc là cung tròn của vòng ngoài; lệch ra/vào bán kính (trong mặt phẳng cổng) và nhẹ ra khỏi mặt phẳng
            const int n = 10;
            for (int i = 0; i <= n; i++)
            {
                float ang = a.angle + a.span * (i / (float)n);
                baseBuf.Add(ToWorld(Mathf.Cos(ang) * a.radius, Mathf.Sin(ang) * a.radius, 0f));
            }
            TiaSet.BuildPath(a.pts, baseBuf, 0.06f, transform.forward, 0f, 0, 0f, true);
            return;
        }
        // Tia tỏa: từ vòng ngoài bắn ra, gãy khúc, 1-2 nhánh con
        Vector3 start = ToWorld(Mathf.Cos(a.angle) * a.radius, Mathf.Sin(a.angle) * a.radius, 0f);
        float dirAng = a.angle + Random.Range(-0.5f, 0.5f);
        Vector3 d = transform.right * Mathf.Cos(dirAng) + transform.up * Mathf.Sin(dirAng);
        const int m = 7;
        for (int i = 0; i <= m; i++) baseBuf.Add(start + d * (a.length * (i / (float)m)));
        TiaSet.BuildPath(a.pts, baseBuf, a.length * 0.14f, transform.forward, 0.3f, Random.Range(1, 3), a.length, false);
    }
}
