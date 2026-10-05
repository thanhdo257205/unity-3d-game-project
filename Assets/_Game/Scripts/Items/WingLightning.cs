using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Sấm sét của Phong Lôi Sí (nằm trong prefab PhongLoiSi, dựng bằng menu Phàm Kiếm Lục → Dựng sấm sét Phong Lôi Sí). Gồm:
/// - Sét chính dọc cánh: mỗi cánh có 8 điểm (Transform rỗng gắn vào xương cánh, rải từ gốc tới đầu cánh nên đi theo khi cánh vỗ) nạp vào hai hiệu ứng
///   VFX Graph chồng nhau: lõi trắng hơi vàng (SetCanh.vfx, mảnh) và quầng tím nhạt (SetCanhQuang.vfx, dày hơn). Không chạy liên tục: mỗi cánh lóe một lần
///   sau mỗi khoảng ngẫu nhiên, mỗi lần kéo dài một khoảng ngắn ngẫu nhiên; hai cánh không lóe cùng lúc.
/// - Sét phụ: hạt dùng texture TX_Lighting sinh ngẫu nhiên trên bề mặt lông cánh, rất ngắn.
/// - Tia lửa trắng xanh ở hai đầu cánh (TiaLuaCanh.vfx): nhẹ khi đang bay, chùm mạnh lúc cất cánh và hạ cánh.
/// - Đèn Point Light tím nhạt ở giữa lưng, bình thường tắt, chớp lên cùng lúc sét chính lóe.
/// - Vệt sáng mảnh kéo sau hai đầu cánh khi tăng tốc.
/// - Bụi đáp đất (BuiDapDat.vfx), phát một lần khi chạm đất.
/// Cường độ gom vào biến "cuongDoSet" (0-1): PhongLoiSi đặt theo trạng thái (xếp 0, đứng đất 0.3, bay thường 0.6, tăng tốc 1);
/// cường độ càng thấp thì sét lóe càng thưa, đèn và tia lửa càng yếu.
/// </summary>
public class WingLightning : MonoBehaviour
{
    [Header("Cường độ")]
    [Tooltip("Cường độ sét hiện tại, 0-1 (0 = tắt hết, 1 = mạnh nhất). PhongLoiSi đặt theo trạng thái: cánh xếp 0, đứng trên đất 0.3, bay thường 0.6, tăng tốc 1. Số này chạy mượt theo giá trị PhongLoiSi đặt; muốn chỉnh tay để thử thì bật 'Đặt tay'.")]
    [SerializeField, Range(0f, 1f)] private float cuongDoSet = 0f;

    [Tooltip("Bật: bỏ qua giá trị PhongLoiSi đặt, dùng đúng số 'cuongDoSet' kéo trong Inspector (để thử hiệu ứng). Cảnh TestVFX tự bật.")]
    [SerializeField] private bool datTay;

    [Tooltip("Thời gian chuyển mượt khi cường độ đổi (giây).")]
    [SerializeField, Min(0.01f)] private float levelSmoothTime = 0.25f;

    [Header("Sét chính dọc cánh")]
    [Tooltip("Mỗi cánh lóe một lần sau khoảng ngẫu nhiên này (giây, nhỏ nhất - lớn nhất) khi cường độ = 1. Cường độ thấp hơn thì khoảng này dài ra (chia cho cường độ).")]
    [SerializeField] private Vector2 flashInterval = new Vector2(0.4f, 1.2f);

    [Tooltip("Mỗi lần lóe kéo dài khoảng ngẫu nhiên này (giây, nhỏ nhất - lớn nhất).")]
    [SerializeField] private Vector2 flashDuration = new Vector2(0.08f, 0.15f);

    [Tooltip("Thời gian sống mặc định của hiệu ứng sét trong file .vfx (giây). Tốc độ phát = số này / thời gian lóe, nên không đổi số này trừ khi sửa file .vfx.")]
    [SerializeField, Min(0.02f)] private float vfxBaseLifetime = 0.15f;

    [Header("Sét phụ trên mặt lông")]
    [Tooltip("Số hạt sét phụ mỗi giây cho mỗi cánh: (khi cường độ thấp nhất, khi cường độ = 1).")]
    [SerializeField] private Vector2 secondaryRate = new Vector2(3f, 6f);

    [Header("Tia lửa ở đầu cánh")]
    [Tooltip("Tia lửa nhẹ mỗi giây ở mỗi đầu cánh khi đang bay, ở cường độ = 1.")]
    [SerializeField, Min(0f)] private float sparkRateFlying = 12f;

    [Tooltip("Số tia lửa trong chùm mạnh lúc cất cánh / hạ cánh (nhỏ nhất - lớn nhất) ở mỗi đầu cánh.")]
    [SerializeField] private Vector2 sparkBurstCount = new Vector2(50f, 80f);

    [Tooltip("Màu tia lửa (HDR, trắng xanh).")]
    [ColorUsage(false, true)]
    [SerializeField] private Color sparkColor = new Color(1.6f, 2.4f, 4f, 1f);

    [Header("Đèn ở giữa lưng")]
    [Tooltip("Độ sáng đèn tím khi sét chính lóe, ở cường độ = 1.")]
    [SerializeField, Min(0f)] private float lightPeak = 4f;

    [Header("Vệt sáng khi tăng tốc")]
    [Tooltip("Cường độ sét tối thiểu để vệt sáng mảnh kéo sau đầu cánh hiện (tăng tốc = 1).")]
    [SerializeField, Range(0f, 1f)] private float streakMinLevel = 0.9f;

    [Header("Bụi đáp đất")]
    [Tooltip("Số hạt bụi mỗi giây trong lúc phát (giây ngắn), tổng số hạt = số này x thời gian phát.")]
    [SerializeField, Min(0f)] private float dustRate = 320f;

    [Tooltip("Thời gian phát bụi một lần (giây).")]
    [SerializeField, Min(0.02f)] private float dustTime = 0.15f;

    [Header("Tham chiếu (do menu dựng sấm sét gán, không cần chỉnh)")]
    [SerializeField] private Transform[] pointsLeft = new Transform[8];
    [SerializeField] private Transform[] pointsRight = new Transform[8];
    [SerializeField] private VisualEffect[] coreVfx = new VisualEffect[2];      // [0] trái, [1] phải
    [SerializeField] private VisualEffect[] haloVfx = new VisualEffect[2];
    [SerializeField] private ParticleSystem[] secondary = new ParticleSystem[2];
    [SerializeField] private VisualEffect[] sparksVfx = new VisualEffect[2];
    [SerializeField] private TrailRenderer[] streaks = new TrailRenderer[2];
    [SerializeField] private Light backLight;
    [SerializeField] private VisualEffect dustVfx;

    private const int PointCount = 8;
    private static readonly string[] BurstProps = { "Burst Count Min/Max", "Loop Duration Min/Max" };

    private WingVisual wings;
    private Animator standaloneAnimator;
    private float levelTarget;
    private float levelVel;
    private readonly Texture2D[] maps = new Texture2D[2];
    private readonly Color[] mapColors = new Color[PointCount];
    private readonly float[] flashEnd = new float[2];
    private readonly float[] nextFlash = new float[2];
    private float lightEnd, lastFlashStart;
    private float lightScale = 1f;
    private float sparkBurstEnd;
    private float dustEnd;
    private float spike;
    private bool streaming, flying, sparksPlaying;

    // ----- API cho PhongLoiSi -----

    /// <summary>Cường độ sét hiện tại 0-1 (cuongDoSet, đã làm mượt).</summary>
    public float CuongDoSet => cuongDoSet;

    /// <summary>Sét chính đang lóe (ít nhất một cánh).</summary>
    public bool IsFlashing => Time.time < Mathf.Max(flashEnd[0], flashEnd[1]);

    /// <summary>Thời điểm (Time.time) lần lóe gần nhất bắt đầu.</summary>
    public float LastFlashStart => lastFlashStart;

    /// <summary>Cường độ nhân thêm (Phá Không đặt >1 để tăng cường sét tạm thời). Mặc định 1.</summary>
    public float Intensity { get; set; } = 1f;

    /// <summary>PhongLoiSi đặt cường độ mục tiêu theo trạng thái (0-1). Bị bỏ qua khi bật 'Đặt tay'.</summary>
    public void SetLevel(float value01) { levelTarget = Mathf.Clamp01(value01); }

    /// <summary>Bật/tắt vệt sáng mảnh kéo sau đầu cánh (khi tăng tốc).</summary>
    public void SetStreaks(bool on) { streaming = on; }

    /// <summary>Đang bay (tia lửa nhẹ ở đầu cánh) hay không.</summary>
    public void SetFlying(bool on) { flying = on; }

    /// <summary>Tắt hết hiệu ứng tạm thời (Phá Không: nhân vật vô hình).</summary>
    public bool Suppressed { get; set; }

    /// <summary>Làm sét lóe mạnh hơn trong chốc lát (Phá Không dùng): lóe cả hai cánh ngay.</summary>
    public void Spike(float amount) { if (amount > 0f) PlayShoulderFlash(); }

    /// <summary>Lóe cả hai cánh một lần (mở cánh).</summary>
    public void PlayShoulderFlash(float duration = 0.35f)
    {
        float d = Mathf.Clamp(duration, flashDuration.x, 0.25f);
        Fire(0, d);
        Fire(1, d);
        lightScale = 1.5f;
    }

    /// <summary>Chùm tia lửa mạnh + lóe cả hai cánh (gập cánh).</summary>
    public void PlayBurst(float duration = 0.4f, float radius = 1.4f)
    {
        PlayShoulderFlash();
        SparkBurst();
    }

    /// <summary>Chùm tia lửa mạnh ở hai đầu cánh (cất cánh).</summary>
    public void PlayTakeoffBurst() { SparkBurst(); Fire(0, flashDuration.y); Fire(1, flashDuration.y); }

    /// <summary>Chùm tia lửa mạnh ở hai đầu cánh + bụi đáp đất tại groundPoint (hạ cánh).</summary>
    public void PlayLandingBurst(Vector3 groundPoint)
    {
        SparkBurst();
        PlayDust(groundPoint);
    }

    /// <summary>Phát bụi một lần tại vị trí chạm đất.</summary>
    public void PlayDust(Vector3 groundPoint)
    {
        if (dustVfx == null) return;
        dustVfx.transform.position = groundPoint;
        dustVfx.SetFloat("Spawn Rate", dustRate);
        dustVfx.SendEvent("OnPlay");
        dustEnd = Time.time + dustTime;
    }

    // ----- Khởi tạo -----

    private void Awake()
    {
        wings = GetComponentInParent<WingVisual>();
        if (wings == null)
        {
            // Chạy riêng (cảnh TestVFX): cánh vỗ liên tục, chỉnh cường độ tay trong Inspector
            standaloneAnimator = GetComponentInParent<Animator>();
            if (standaloneAnimator == null) standaloneAnimator = transform.root.GetComponentInChildren<Animator>();
            if (standaloneAnimator != null) standaloneAnimator.Play("Flap");
            datTay = true;
            flying = true;
            if (cuongDoSet < 0.01f) cuongDoSet = 0.6f;
        }
        // Đưa hiệu ứng bụi ra khỏi nhân vật (đứng yên ở chỗ phát, không đi theo)
        if (dustVfx != null) dustVfx.transform.SetParent(null, true);
        for (int s = 0; s < 2; s++)
        {
            maps[s] = new Texture2D(PointCount, 1, TextureFormat.RGBAFloat, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SetCanh_PositionMap" + s };
            nextFlash[s] = Time.time + Random.Range(0.1f, 0.5f) + s * 0.25f;   // hai cánh lệch pha
            if (coreVfx[s] != null) { coreVfx[s].SetTexture("PositionMap", maps[s]); coreVfx[s].SetInt("PositionCount", PointCount); }
            if (haloVfx[s] != null) { haloVfx[s].SetTexture("PositionMap", maps[s]); haloVfx[s].SetInt("PositionCount", PointCount); }
        }
        ApplySparkLook();
        SetSparkBurst(false);
    }

    private void OnDestroy()
    {
        for (int s = 0; s < 2; s++) if (maps[s] != null) Destroy(maps[s]);
        if (dustVfx != null) Destroy(dustVfx.gameObject);
    }

    private void OnDisable()
    {
        for (int s = 0; s < 2; s++) { if (streaks[s] != null) streaks[s].emitting = false; }
        if (backLight != null) backLight.enabled = false;
    }

    private void ApplySparkLook()
    {
        // Gradient trắng -> trắng xanh -> trong suốt, HDR
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(sparkColor * 1.3f, 0f), new GradientColorKey(sparkColor, 0.5f), new GradientColorKey(sparkColor * 0.4f, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        for (int s = 0; s < 2; s++)
        {
            VisualEffect v = sparksVfx[s];
            if (v == null) continue;
            v.SetGradient("Sparkle Fire Gradient", g);
            v.SetGradient("Color Over Life", g);
            v.SetVector2("Lifetime Min/Max", new Vector2(0.25f, 0.7f));
            v.SetVector2("Size Min/Max", new Vector2(0.012f, 0.03f));
            v.SetVector3("Initial Velocity_vector", new Vector3(0f, 0f, 0f));
            v.SetFloat("Initial Velocity Divergence", 1f);
            v.SetFloat("Sparkle Width", 0.12f);
            v.SetVector3("Gravity Vector", new Vector3(0f, -4f, 0f));
            v.SetVector3("Bounds_center", Vector3.zero);
            v.SetVector3("Bounds_size", new Vector3(10f, 10f, 10f));
        }
        if (dustVfx != null)
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
    }

    private void SetSparkBurst(bool on)
    {
        for (int s = 0; s < 2; s++)
        {
            VisualEffect v = sparksVfx[s];
            if (v == null) continue;
            v.SetVector2(BurstProps[0], on ? sparkBurstCount : Vector2.zero);
            v.SetVector2(BurstProps[1], on ? new Vector2(0.2f, 0.2f) : new Vector2(0.2f, 1f));
        }
    }

    private void SparkBurst()
    {
        SetSparkBurst(true);
        sparkBurstEnd = Time.time + 0.3f;
        sparksPlaying = false;   // sau chùm mạnh, vòng Update sẽ gửi lại OnPlay/OnStop theo trạng thái bay
        for (int s = 0; s < 2; s++) if (sparksVfx[s] != null) { sparksVfx[s].SetFloat("Spawn Rate", 0f); sparksVfx[s].SendEvent("OnPlay"); }
    }

    // ----- Cập nhật -----

    private void Update()
    {
        float dt = Time.deltaTime;
        float target = datTay ? cuongDoSet : levelTarget;
        bool hidden = Suppressed || (wings != null && (wings.Hidden || !wings.IsVisible));
        if (hidden) target = 0f;
        float k = datTay ? Mathf.Clamp01(target) : Mathf.SmoothDamp(cuongDoSet, Mathf.Clamp01(target), ref levelVel, levelSmoothTime);
        if (!datTay) cuongDoSet = k;
        bool on = k > 0.01f;
        float kk = Mathf.Clamp01(k * Intensity);

        // Sét chính: lóe lần lượt, hai cánh không lóe cùng lúc
        if (on)
        {
            for (int s = 0; s < 2; s++)
            {
                if (Time.time < nextFlash[s]) continue;
                int o = 1 - s;
                if (Time.time < flashEnd[o]) { nextFlash[s] = flashEnd[o] + 0.02f; continue; }   // cánh kia đang lóe: đợi
                float d = Random.Range(flashDuration.x, flashDuration.y);
                Fire(s, d);
                float gap = Random.Range(flashInterval.x, flashInterval.y) / Mathf.Max(0.25f, kk);
                nextFlash[s] = Time.time + d + gap;
                if (nextFlash[s] < flashEnd[o] + 0.03f) nextFlash[s] = flashEnd[o] + 0.03f;
            }
        }

        // Sét phụ trên mặt lông
        for (int s = 0; s < 2; s++)
        {
            if (secondary[s] == null) continue;
            ParticleSystem.EmissionModule em = secondary[s].emission;
            em.rateOverTime = on ? Mathf.Lerp(secondaryRate.x, secondaryRate.y, kk) : 0f;
            if (on && !secondary[s].isPlaying) secondary[s].Play();
            if (!on && secondary[s].isPlaying) secondary[s].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Tia lửa nhẹ ở đầu cánh khi đang bay; chùm mạnh có hạn giờ
        if (sparkBurstEnd > 0f && Time.time >= sparkBurstEnd) { SetSparkBurst(false); sparkBurstEnd = 0f; }
        bool wantSparks = on && flying && !hidden;
        for (int s = 0; s < 2; s++)
        {
            VisualEffect v = sparksVfx[s];
            if (v == null) continue;
            v.SetFloat("Spawn Rate", (wantSparks && sparkBurstEnd <= 0f) ? sparkRateFlying * kk : 0f);
        }
        if (wantSparks != sparksPlaying && sparkBurstEnd <= 0f)
        {
            sparksPlaying = wantSparks;
            for (int s = 0; s < 2; s++) if (sparksVfx[s] != null) sparksVfx[s].SendEvent(wantSparks ? "OnPlay" : "OnStop");
        }

        // Vệt sáng tăng tốc
        bool wantStreak = streaming && k >= streakMinLevel && !hidden;
        for (int s = 0; s < 2; s++) if (streaks[s] != null && streaks[s].emitting != wantStreak) streaks[s].emitting = wantStreak;

        // Bụi: hết thời gian phát thì dừng tốc độ phát
        if (dustEnd > 0f && Time.time >= dustEnd)
        {
            dustEnd = 0f;
            if (dustVfx != null) { dustVfx.SetFloat("Spawn Rate", 0f); dustVfx.SendEvent("OnStop"); }
        }

        // Đèn: chớp theo sét chính
        if (backLight != null)
        {
            float lit = Time.time < lightEnd ? lightPeak * kk * lightScale * Random.Range(0.75f, 1.1f) : 0f;
            backLight.intensity = Mathf.Lerp(backLight.intensity, lit, lit > backLight.intensity ? 1f : 1f - Mathf.Exp(-40f * dt));
            bool enable = backLight.intensity > 0.02f;
            if (backLight.enabled != enable) backLight.enabled = enable;
            if (Time.time >= lightEnd) lightScale = 1f;
        }
    }

    private void LateUpdate()
    {
        // Nạp vị trí 8 điểm cho hiệu ứng sét trong lúc đang lóe (và thêm một chút sau đó để đuôi sét không đứng yên)
        for (int s = 0; s < 2; s++)
        {
            if (Time.time < flashEnd[s] + vfxBaseLifetime) UploadPoints(s);
            else if (Time.time > flashEnd[s] + vfxBaseLifetime * 3f)
            {
                // Không lóe: tạm dừng mô phỏng hai hiệu ứng sét để đỡ tốn khung hình
                if (coreVfx[s] != null && !coreVfx[s].pause) coreVfx[s].pause = true;
                if (haloVfx[s] != null && !haloVfx[s].pause) haloVfx[s].pause = true;
            }
        }
    }

    private void UploadPoints(int s)
    {
        Transform[] pts = s == 0 ? pointsLeft : pointsRight;
        for (int i = 0; i < PointCount; i++)
        {
            Vector3 p = pts[i] != null ? pts[i].position : transform.position;
            mapColors[i] = new Color(p.x, p.y, p.z, 1f);
        }
        maps[s].SetPixels(mapColors);
        maps[s].Apply(false);
    }

    private void Fire(int s, float duration)
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        flashEnd[s] = Time.time + duration;
        lastFlashStart = Time.time;
        UploadPoints(s);
        float rate = Mathf.Clamp(vfxBaseLifetime / Mathf.Max(0.02f, duration), 0.4f, 4f);
        if (coreVfx[s] != null) { coreVfx[s].pause = false; coreVfx[s].playRate = rate; coreVfx[s].SendEvent("OnPlay"); }
        if (haloVfx[s] != null) { haloVfx[s].pause = false; haloVfx[s].playRate = rate; haloVfx[s].SendEvent("OnPlay"); }
        lightEnd = Mathf.Max(lightEnd, Time.time + duration);
    }
}
