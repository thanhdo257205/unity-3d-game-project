using UnityEngine;

/// <summary>
/// Hình dáng và chuyển động của đôi cánh Phong Lôi Sí, dùng mô hình thật (prefab PhongLoiSi: 風雷翅 của 芒果小子, đã xử lý cho game)
/// với bốn animation Open (xòe 0.5s), Flap (vỗ, lặp), Glide (lướt, cánh duỗi hơi cụp về sau, lặp), Close (gập 0.4s).
/// - Gắn ở xương ngực (Spine2), lệch xuống và ra sau lưng theo "Lệch gốc cánh", luôn cùng hướng với thân người (kể cả khi thân nghiêng lúc bay).
/// - Open/Close: phát animation xòe/gập; gập xong thì ẩn cánh.
/// - SetMotion do PhongLoiSi gọi theo trạng thái: biên độ nhỏ (đứng yên) hoặc bay nhanh thì lướt (Glide); còn lại vỗ (Flap) với tốc độ theo nhịp.
/// - Độ sáng emission mép lông (dải sáng mảnh chạy dọc mép) sáng lên theo nhịp vỗ.
/// - Cung cấp các điểm trên cánh (gốc, khuỷu, đầu cánh, mép lông) để WingLightning vẽ tia sét và vệt sét.
/// Mọi con số chỉnh trong Inspector.
/// </summary>
public class WingVisual : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Xương ngực (Spine2) mà cánh gắn vào. Để trống thì PhongLoiSi gán lúc chạy.")]
    public Transform chestBone;

    [Tooltip("Mô hình nhân vật: cánh luôn cùng hướng với thân người (kể cả khi thân nghiêng lúc bay). Để trống thì PhongLoiSi gán lúc chạy.")]
    public Transform bodyRoot;

    [Tooltip("Prefab mô hình cánh (Assets/_Game/Prefabs/PhongLoiSi.prefab).")]
    [SerializeField] private GameObject modelPrefab;

    [Header("Vị trí gốc cánh")]
    [Tooltip("Lệch của gốc cánh so với xương ngực (Spine2), tính theo hướng thân người (phải, lên, trước). Y âm = hạ xuống (-0.10 là khoảng giữa lưng); Z âm = ra sau lưng để khung cánh không đâm vào đầu và vai áo.")]
    [SerializeField] private Vector3 rootOffset = new Vector3(0f, -0.10f, -0.32f);

    [Header("Xòe, gập, vỗ")]
    [Tooltip("Thời gian cánh xòe (giây). Clip gốc dài 0.5s; số khác thì phát nhanh/chậm lại cho khớp.")]
    [SerializeField, Min(0.05f)] private float openTime = 0.5f;

    [Tooltip("Thời gian cánh gập rồi tan (giây). Clip gốc dài 0.4s.")]
    [SerializeField, Min(0.05f)] private float closeTime = 0.4f;

    [Tooltip("Thời gian chuyển giữa lướt và vỗ (giây).")]
    [SerializeField, Min(0.01f)] private float blendTime = 0.3f;

    [Tooltip("Biên độ vỗ (độ) nhỏ hơn mức này thì cánh chỉ lướt (không vỗ), ví dụ khi đứng yên trên mặt đất.")]
    [SerializeField, Min(0f)] private float minFlapAmplitude = 8f;

    [Tooltip("Số nhịp mỗi giây của clip Flap gốc (clip dài 1.2s = 0.83 nhịp/giây). Nhịp yêu cầu chia cho số này ra tốc độ phát.")]
    [SerializeField, Min(0.1f)] private float clipFlapHz = 0.83f;

    [Header("Độ sáng mép lông (emission)")]
    [Tooltip("Độ sáng dải mép lông lúc nghỉ.")]
    [SerializeField, Min(0f)] private float emissionBase = 0.6f;

    [Tooltip("Độ sáng mép lông tăng thêm theo nhịp vỗ.")]
    [SerializeField, Min(0f)] private float emissionPulse = 1.1f;

    private static readonly int EdgeIntensityId = Shader.PropertyToID("_EdgeIntensity");

    private Animator animator;
    private Transform[] boneL = new Transform[3];   // xương cánh trái: Canh_T1..3 (trái = -X của nhân vật)
    private Transform[] boneR = new Transform[3];   // xương cánh phải: Canh_P1..3
    private readonly System.Collections.Generic.List<Renderer> renderers = new System.Collections.Generic.List<Renderer>();
    private Material featherInstance;
    private bool wantOpen;
    private float spread;            // 0 khép, 1 xòe hết (theo thời gian)
    private string currentState = "";
    private float flapSpeed = 1f;
    private bool wantGlide = true;
    private bool flickering;
    private float pulse;
    private bool built;

    /// <summary>Độ xòe làm mượt (0 khép, 1 xòe hết).</summary>
    public float Spread => Mathf.SmoothStep(0f, 1f, spread);

    /// <summary>Cánh đang hiện (đã bắt đầu xòe và chưa khép hết).</summary>
    public bool IsVisible => wantOpen || spread > 0.001f;

    /// <summary>Cánh đã xòe hết sải.</summary>
    public bool FullyOpen => wantOpen && spread >= 0.999f;

    /// <summary>Nhịp vỗ hiện tại (0-1), emission và tia sét dùng để sáng theo nhịp.</summary>
    public float Pulse => pulse;

    /// <summary>Ẩn cánh tạm thời (Phá Không: nhân vật vô hình) mà không đổi trạng thái mở/đóng.</summary>
    public bool Hidden { get; set; }

    /// <summary>Cánh đang gập lại (đã gọi Close nhưng chưa khép hết).</summary>
    public bool IsClosing => !wantOpen && spread > 0.001f;

    /// <summary>Số "lông dài" dùng để chia điểm mép lông (tia sét).</summary>
    public int PrimaryCount => 12;

    private void Awake() { Build(); }

    /// <summary>Dựng mô hình từ prefab (chỉ một lần).</summary>
    private void Build()
    {
        if (built) return;
        built = true;
        if (modelPrefab == null) { Debug.LogError("WingVisual: thiếu prefab mô hình cánh.", this); return; }
        GameObject inst = Instantiate(modelPrefab, transform);
        inst.name = "MoHinhCanh";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        animator = inst.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
        }
        for (int i = 0; i < 3; i++)
        {
            boneL[i] = FindDeep(inst.transform, "Canh_T" + (i + 1));
            boneR[i] = FindDeep(inst.transform, "Canh_P" + (i + 1));
        }
        foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderers.Add(smr);
            if (smr.name.StartsWith("Long_"))
            {
                if (featherInstance == null) featherInstance = new Material(smr.sharedMaterial);
                smr.sharedMaterial = featherInstance;
            }
            smr.enabled = false;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform t = FindDeep(root.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    private void SetRenderersEnabled(bool on)
    {
        for (int i = 0; i < renderers.Count; i++) if (renderers[i] != null && renderers[i].enabled != on) renderers[i].enabled = on;
    }

    private void PlayState(string state, float fade)
    {
        if (animator == null || currentState == state) return;
        currentState = state;
        if (fade <= 0f) animator.Play(state, 0, 0f);
        else animator.CrossFadeInFixedTime(state, fade, 0);
    }

    // ================= Điều khiển từ PhongLoiSi =================

    /// <summary>Bắt đầu xòe cánh (từ gốc ra hết sải trong "Thời gian xòe").</summary>
    public void Open()
    {
        if (!built) Build();
        bool was = wantOpen;
        wantOpen = true;
        if (!was)
        {
            currentState = "";
            if (animator != null) animator.speed = 0.5f / Mathf.Max(0.05f, openTime);
            PlayState("Open", 0f);
        }
    }

    /// <summary>Bắt đầu gập cánh lại (rồi biến mất trong "Thời gian gập").</summary>
    public void Close()
    {
        if (!wantOpen) return;
        wantOpen = false;
        currentState = "";
        if (animator != null) animator.speed = 0.4f / Mathf.Max(0.05f, closeTime);
        PlayState("Close", Mathf.Min(0.1f, blendTime));
    }

    /// <summary>Đặt ngay trạng thái mở/đóng không có hoạt ảnh (hồi sinh, kiểm thử).</summary>
    public void SnapTo(bool open)
    {
        if (!built) Build();
        wantOpen = open;
        spread = open ? 1f : 0f;
        currentState = "";
        if (animator != null) { animator.speed = 1f; PlayState(open ? "Glide" : "Close", 0f); }
        if (!open) SetRenderersEnabled(false);
    }

    /// <summary>
    /// Đặt kiểu vỗ cánh: biên độ (độ), số nhịp mỗi giây, độ cụp đầu cánh về sau (0-1, bay nhanh là 1).
    /// Biên độ nhỏ hoặc bay nhanh thì lướt (Glide); còn lại vỗ (Flap) với tốc độ theo nhịp.
    /// </summary>
    public void SetMotion(float amplitudeDeg, float hz, float sweep01)
    {
        wantGlide = sweep01 > 0.5f || amplitudeDeg < minFlapAmplitude;
        flapSpeed = Mathf.Clamp(hz / Mathf.Max(0.1f, clipFlapHz), 0.3f, 3f);
    }

    /// <summary>Cánh chập chờn (hết pháp lực giữa trời): lông chớp tắt ngẫu nhiên.</summary>
    public void SetFlicker(bool on) { flickering = on; }

    // ================= Điểm trên cánh (cho tia sét) =================

    private Transform[] BonesOf(int side) { return side > 0 ? boneR : boneL; }

    /// <summary>
    /// Điểm trên cánh theo tham số u (0 = gốc, 0.5 = khuỷu, 1 = đầu cánh), side +1 phải / -1 trái.
    /// </summary>
    public Vector3 PathPoint(int side, float u)
    {
        Transform[] b = BonesOf(side);
        if (b[0] == null || b[1] == null || b[2] == null) return transform.position;
        Vector3 root = b[0].position, elbow = b[1].position, wrist = b[2].position;
        Vector3 tip = wrist + (wrist - elbow);
        u = Mathf.Clamp01(u);
        if (u < 0.34f) return Vector3.Lerp(root, elbow, u / 0.34f);
        if (u < 0.67f) return Vector3.Lerp(elbow, wrist, (u - 0.34f) / 0.33f);
        return Vector3.Lerp(wrist, tip, (u - 0.67f) / 0.33f);
    }

    /// <summary>
    /// Đầu lông thứ index (0 = lông gốc, PrimaryCount-1 = lông ngoài cùng): xấp xỉ theo vòng cung mép sau của cánh.
    /// </summary>
    public Vector3 FeatherTip(int side, int index)
    {
        Transform[] b = BonesOf(side);
        if (b[0] == null || b[1] == null || b[2] == null) return transform.position;
        float t = Mathf.Clamp01(index / (float)(PrimaryCount - 1));
        Vector3 outer = PathPoint(side, 1f);
        Vector3 baseEdge = b[1].position + (b[1].position - b[0].position).normalized * 0.1f - transform.up * 0.5f;   // lông gốc rủ xuống
        return Vector3.Lerp(baseEdge, outer, Mathf.Pow(t, 0.8f));
    }

    // ================= Cập nhật =================

    private void LateUpdate()
    {
        if (!built) Build();
        float dt = Time.deltaTime;

        // Gắn theo thân người
        if (chestBone != null && bodyRoot != null)
        {
            transform.rotation = bodyRoot.rotation;
            transform.position = chestBone.position + bodyRoot.rotation * rootOffset;
        }

        // Theo dõi tiến độ xòe/gập (theo thời gian, khớp với tốc độ phát clip)
        float rate = 1f / (wantOpen ? openTime : closeTime);
        spread = Mathf.MoveTowards(spread, wantOpen ? 1f : 0f, rate * dt);
        bool visible = spread > 0.001f || wantOpen;
        bool show = visible && !Hidden;
        if (flickering) show = show && Random.value > 0.35f;
        SetRenderersEnabled(show);

        if (!visible)
        {
            pulse = 0f;
            return;
        }

        // Xòe xong thì chuyển sang lướt/vỗ theo SetMotion
        if (wantOpen && spread >= 0.999f)
        {
            if (animator != null && !Mathf.Approximately(animator.speed, wantGlide ? 1f : flapSpeed)) animator.speed = wantGlide ? 1f : flapSpeed;
            PlayState(wantGlide ? "Glide" : "Flap", blendTime);
        }

        // Nhịp vỗ: lấy từ thời điểm chuẩn hóa của clip Flap
        if (animator != null && currentState == "Flap")
            pulse = 0.5f + 0.5f * Mathf.Sin(animator.GetCurrentAnimatorStateInfo(0).normalizedTime * Mathf.PI * 2f);
        else pulse = Mathf.MoveTowards(pulse, 0.25f, dt * 2f);

        if (featherInstance != null)
        {
            float e = emissionBase + emissionPulse * pulse;
            if (flickering) e *= Random.Range(0.1f, 1.2f);
            featherInstance.SetFloat(EdgeIntensityId, e);
        }
    }

    private void OnDestroy()
    {
        if (featherInstance != null) Destroy(featherInstance);
    }
}
