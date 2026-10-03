using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Các đội hình có thể chọn bằng phím 1/2/3.</summary>
public enum FormationType
{
    /// <summary>Phím 1: Thủ Kiếm - Quạt.</summary>
    Fan = 0,
    /// <summary>Phím 2: Hồi Kiếm - Vòng.</summary>
    Ring = 1,
    /// <summary>Phím 3: Phá Kiếm - Mũi tên.</summary>
    Arrow = 2
}

/// <summary>
/// Đàn phi kiếm của người chơi: sinh ra các thanh kiếm, xếp chúng theo đội hình, và điều khiển việc phóng kiếm.
/// Phím 1/2/3 đổi đội hình Quạt/Vòng/Mũi tên. Chuột trái phóng một kiếm về tâm ngắm (giữ = bắn liên tiếp).
/// Phím 3 lao cả đàn vào mục tiêu gần tâm ngắm nhất rồi thu về. Phím R gọi toàn bộ kiếm về.
/// Số kiếm tối đa lấy từ Thần thức (cảnh giới) trong PlayerStats. Mỗi hành động tốn pháp lực.
/// Quạt tự chặn cầu từ phía sau/hai bên, Vòng chặn cầu chạm trúng kiếm. Kiếm đang bay đi thì không chặn được.
/// </summary>
public class SwordSwarm : MonoBehaviour
{
    private const int MaxSwords = 100;

    [Header("Tham chiếu")]
    [Tooltip("Prefab phi kiếm (ThanhTrucKiem). Phải có script FlyingSword ở gốc.")]
    [SerializeField] private GameObject swordPrefab;

    [Tooltip("Nhân vật mà đàn kiếm bám theo (kéo đối tượng NhanVat vào).")]
    [SerializeField] private Transform owner;

    [Tooltip("Script điều khiển nhân vật, để quay mặt theo hướng camera lúc bắn. Có thể để trống.")]
    [SerializeField] private PlayerController player;

    [Tooltip("Tài nguyên người chơi: Thần thức (số kiếm tối đa), pháp lực, Kiếm ý. Để trống thì không giới hạn và không tốn pháp lực.")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Camera dùng để ngắm. Để trống thì dùng camera chính.")]
    [SerializeField] private Camera aimCamera;

    [Tooltip("Dừng hình và rung camera khi kiếm trúng mục tiêu. Có thể để trống thì không có hiệu ứng này.")]
    [SerializeField] private HitFeedback hitFeedback;

    [Header("Số kiếm")]
    [Tooltip("Số phi kiếm đang dùng. Không thể vượt quá Thần thức của cảnh giới hiện tại (PlayerStats). Đổi cảnh giới (F4/F5) thì số kiếm tự đặt về mức tối đa mới.")]
    [SerializeField, Range(0, MaxSwords)] private int swordCount = 9;

    [Header("Đội hình")]
    [Tooltip("Đội hình lúc bắt đầu.")]
    [SerializeField] private FormationType startFormation = FormationType.Fan;

    [Tooltip("Điểm neo bám theo cơ thể nhân vật (thường là một đối tượng con của xương ngực). Có thì đội hình đi theo ngực/lưng nhân vật khi chạy, nhún. Để trống thì dùng 'Độ cao điểm neo' bên dưới.")]
    [SerializeField] private Transform anchor;

    [Tooltip("Độ cao điểm neo của đội hình so với chân nhân vật (mét), chỉ dùng khi không có 'Điểm neo' ở trên. 1.1 là khoảng ngực.")]
    [SerializeField] private float anchorHeight = 1.1f;

    [Tooltip("Thời gian kiếm bay tới chỗ mới khi đổi đội hình (giây). 0.4 cho cảm giác nhanh nhưng vẫn nhìn thấy kiếm bay.")]
    [SerializeField, Min(0.05f)] private float formationSettleTime = 0.4f;

    [Tooltip("Độ 'nảy' của lò xo. 1 = vào vị trí êm không nảy, nhỏ hơn 1 (ví dụ 0.6) = nảy nhẹ trước khi yên vị.")]
    [SerializeField, Range(0.3f, 1.5f)] private float dampingRatio = 0.8f;

    [Tooltip("Tốc độ tối đa của kiếm khi bay về đội hình (mét/giây). Chặn để kiếm không bay quá nhanh khi nhân vật chạy xa.")]
    [SerializeField, Min(1f)] private float maxSwordSpeed = 70f;

    [SerializeField] private FanFormation fan = new FanFormation();
    [SerializeField] private RingFormation ring = new RingFormation();
    [SerializeField] private ArrowFormation arrow = new ArrowFormation();

    [Header("Tiêu hao pháp lực")]
    [Tooltip("Pháp lực tốn cho mỗi lần phóng một kiếm (chuột trái).")]
    [SerializeField, Min(0f)] private float singleShotCost = 4f;

    [Tooltip("Pháp lực tốn cho MỖI kiếm trong loạt Mũi tên (phím 3).")]
    [SerializeField, Min(0f)] private float volleyCostPerSword = 3f;

    [Tooltip("Pháp lực tốn mỗi lần đổi đội hình.")]
    [SerializeField, Min(0f)] private float formationChangeCost = 5f;

    [Tooltip("Pháp lực tốn mỗi giây khi đang ở đội hình Vòng. Cạn pháp lực thì tự về Quạt.")]
    [SerializeField, Min(0f)] private float ringDrainPerSecond = 6f;

    [Header("Kiếm ý")]
    [Tooltip("Kiếm ý nhận được khi một nhát kiếm bắn lẻ trúng mục tiêu.")]
    [SerializeField, Min(0f)] private float singleHitIntent = 2f;

    [Tooltip("Kiếm ý nhận được cho MỖI kiếm trong loạt Mũi tên trúng mục tiêu.")]
    [SerializeField, Min(0f)] private float volleyHitIntent = 1f;

    [Header("Bắn lẻ (chuột trái)")]
    [Tooltip("Tốc độ kiếm lao đi khi bắn lẻ (mét/giây). Số lớn = vút nhanh hơn.")]
    [SerializeField, Min(1f)] private float shotSpeed = 80f;

    [Tooltip("Thời gian giữa hai phát khi giữ chuột trái (giây).")]
    [SerializeField, Min(0.02f)] private float fireInterval = 0.12f;

    [Tooltip("Sát thương mỗi nhát chém khi bắn lẻ.")]
    [SerializeField, Min(0f)] private float strikeDamage = 14f;

    [Tooltip("Kiếm bay thêm bao xa sau điểm ngắm rồi mới quay về (mét).")]
    [SerializeField, Min(0f)] private float shotOvershoot = 0.5f;

    [Tooltip("Thời gian kiếm bay về đội hình sau khi bắn (giây).")]
    [SerializeField, Min(0.1f)] private float shotReturnTime = 0.6f;

    [Tooltip("Tầm ngắm tối đa (mét). Nhìn ra xa hơn thì kiếm bay tới điểm ở khoảng cách này.")]
    [SerializeField, Min(1f)] private float aimRange = 60f;

    [Tooltip("Góc hỗ trợ ngắm (độ): hình nộm nằm trong góc này quanh tâm ngắm thì kiếm tự nhắm vào giữa người nó. Tâm ngắm sẽ đỏ.")]
    [SerializeField, Range(0f, 15f)] private float aimAssistAngle = 6f;

    [Tooltip("Sau lần bắn cuối, nhân vật còn quay mặt theo camera thêm bao lâu (giây).")]
    [SerializeField, Min(0f)] private float aimHoldTime = 0.8f;

    [Header("Phá Kiếm - Mũi tên (phím 3)")]
    [Tooltip("Bấm 3 khi chưa ở đội hình Mũi tên: kiếm xếp hàng xong thì lao luôn. Tắt đi thì phải bấm 3 lần nữa mới lao.")]
    [SerializeField] private bool volleyOnKey3 = true;

    [Tooltip("Tốc độ lao của loạt kiếm (mét/giây). Thiết kế gốc là 30, đã tăng lên cho cảm giác nhanh hơn.")]
    [SerializeField, Min(1f)] private float volleySpeed = 55f;

    [Tooltip("Khoảng cách thời gian giữa hai kiếm liên tiếp xuất phát (giây).")]
    [SerializeField, Min(0f)] private float volleyInterval = 0.08f;

    [Tooltip("Thời gian thu kiếm về sau loạt lao (giây). Trong lúc này lưng nhân vật trống.")]
    [SerializeField, Min(0.1f)] private float volleyReturnTime = 1.5f;

    [Tooltip("Sát thương mỗi kiếm trong loạt lao.")]
    [SerializeField, Min(0f)] private float volleyDamage = 14f;

    [Tooltip("Góc tìm mục tiêu quanh tâm ngắm (độ). Mục tiêu gần tâm ngắm nhất trong góc này sẽ bị nhắm.")]
    [SerializeField, Range(1f, 60f)] private float volleyLockAngle = 25f;

    [Tooltip("Tầm tìm mục tiêu cho loạt lao (mét).")]
    [SerializeField, Min(1f)] private float volleyRange = 50f;

    [Tooltip("Kiếm xuyên qua mục tiêu thêm bao xa rồi mới quay về (mét).")]
    [SerializeField, Min(0f)] private float volleyOvershoot = 2.5f;

    [Header("Va chạm và thu kiếm")]
    [Tooltip("Sát thương mỗi lần kiếm trong đội hình Vòng chạm vào địch.")]
    [SerializeField, Min(0f)] private float ringContactDamage = 6f;

    [Tooltip("Mỗi thanh kiếm Vòng chỉ chém lại cùng một mục tiêu sau ít nhất bấy nhiêu giây.")]
    [SerializeField, Min(0.05f)] private float contactHitInterval = 0.35f;

    [Tooltip("Thời gian kiếm bay về khi bấm R gọi kiếm về (giây).")]
    [SerializeField, Min(0.1f)] private float recallTime = 0.5f;

    [Tooltip("Phần vận tốc giữ lại khi kiếm bắt đầu quay về (0 = dừng khựng rồi bay về, 1 = giữ nguyên đà). Số nhỏ cho kiếm quay đầu gọn.")]
    [SerializeField, Range(0f, 1f)] private float returnVelocityKeep = 0.15f;

    [Header("Chặn cầu (phòng thủ)")]
    [Tooltip("Bật/tắt khả năng chặn cầu của Quạt và Vòng.")]
    [SerializeField] private bool defenseEnabled = true;

    [Tooltip("Cung phía sau lưng mà Quạt chặn được (độ). 160 nghĩa là 80 độ mỗi bên so với hướng thẳng sau lưng. Cầu tới từ phía trước thì không chặn.")]
    [SerializeField, Range(10f, 360f)] private float fanBlockArc = 160f;

    [Tooltip("Khi cầu vào trong khoảng này (mét), Quạt cử một thanh kiếm ra đón đường.")]
    [SerializeField, Min(1f)] private float guardDetectRange = 8f;

    [Tooltip("Thanh kiếm đón đường đứng cách điểm neo bao xa theo hướng cầu tới (mét).")]
    [SerializeField, Min(0.5f)] private float guardDistance = 1.5f;

    [Tooltip("Thời gian kiếm bay ra vị trí đón (giây). Số nhỏ = ra rất nhanh.")]
    [SerializeField, Min(0.03f)] private float guardSettleTime = 0.12f;

    [Tooltip("Kiếm đón đường chờ tối đa bao lâu (giây) rồi mới về chỗ nếu cầu không tới.")]
    [SerializeField, Min(0.1f)] private float guardHoldTime = 1f;

    [Tooltip("Độ rung của kiếm khi chặn được một quả cầu (mét).")]
    [SerializeField, Min(0f)] private float blockShake = 0.12f;

    [Tooltip("Thời gian kiếm rung sau khi chặn (giây).")]
    [SerializeField, Min(0.05f)] private float blockShakeTime = 0.25f;

    [Tooltip("Hệ hạt tia lửa phát ra khi chặn được cầu.")]
    [SerializeField] private ParticleSystem blockSparks;

    [Tooltip("Số tia lửa mỗi lần chặn.")]
    [SerializeField, Min(0)] private int blockSparkCount = 16;

    private readonly List<FlyingSword> swords = new List<FlyingSword>();
    private readonly List<FlyingSword> temp = new List<FlyingSword>();
    private readonly RaycastHit[] rayHits = new RaycastHit[32];

    private FormationType current;
    private float nextFireTime;
    private float aimUntil;
    private float pendingVolleyTime = -1f;
    private float pendingFireTime;       // giờ phóng kiếm đang chờ tay duỗi tới điểm bắn
    private int pendingFireKind;         // 0 = không chờ, 1 = bắn lẻ, 2 = loạt Mũi tên
    private int nextShooter;

    // ----- Giá trị cho FlyingSword đọc -----
    public float FormationSettleTime => formationSettleTime;
    public float DampingRatio => dampingRatio;
    public float MaxSwordSpeed => maxSwordSpeed;
    public float ReturnVelocityKeep => returnVelocityKeep;
    public float RingContactDamage => ringContactDamage;
    public float ContactHitInterval => contactHitInterval;
    public float SingleHitIntent => singleHitIntent;
    public float VolleyHitIntent => volleyHitIntent;

    /// <summary>Đội hình đang dùng.</summary>
    public FormationType CurrentFormation => current;

    /// <summary>Đội hình đang dùng ở dạng giao diện IFormation.</summary>
    public IFormation ActiveFormation => GetFormation(current);

    /// <summary>Giới hạn số kiếm hiện tại: Thần thức của cảnh giới (nếu có PlayerStats), nếu không thì mức cứng.</summary>
    public int SwordCap => stats != null ? stats.MaxSwords : MaxSwords;

    /// <summary>Số kiếm mong muốn (có thể gán lúc đang chạy). Luôn bị giới hạn bởi Thần thức.</summary>
    public int SwordCount
    {
        get => swordCount;
        set => swordCount = Mathf.Clamp(value, 0, SwordCap);
    }

    /// <summary>Số kiếm đang tồn tại thực tế.</summary>
    public int ActualSwordCount => swords.Count;

    /// <summary>Số kiếm đang sẵn sàng trong đội hình (chưa bay đi tấn công hay đang thu về). HUD hiện "Kiếm: sẵn sàng/tổng".</summary>
    public int ReadySwordCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < swords.Count; i++)
                if (swords[i].Mode == SwordMode.Formation && !swords[i].HasScheduledStrike) n++;
            return n;
        }
    }

    /// <summary>Danh sách kiếm (chỉ đọc).</summary>
    public IReadOnlyList<FlyingSword> Swords => swords;

    /// <summary>Báo khi đội hình đổi (nhân vật niệm quyết).</summary>
    public event System.Action<FormationType> FormationChanged;

    /// <summary>Báo mỗi lần phóng kiếm: bắn lẻ hoặc loạt Mũi tên (nhân vật vung tay).</summary>
    public event System.Action StrikeFired;

    /// <summary>Báo mỗi khi MỘT thanh kiếm rời đội hình để phóng đi (bắn lẻ: 1 lần; loạt Mũi tên: mỗi kiếm 1 lần). Nhân vật giật tay theo nhịp này.</summary>
    public event System.Action SwordLaunched;

    /// <summary>
    /// Cổng tư thế bắn (PlayerAnimator gán): được gọi ngay khi bấm bắn, đưa tay vào tư thế phóng kiếm
    /// và trả về số giây còn lại cho tới khi tay tới điểm bắn (0 nếu tay đã ở đó). Kiếm chỉ rời đội hình sau khoảng chờ này.
    /// </summary>
    public System.Func<float> ShootPoseGate;

    /// <summary>FlyingSword gọi khi một kiếm trong loạt tới giờ lao đi.</summary>
    public void NotifySwordLaunched() { SwordLaunched?.Invoke(); }

    /// <summary>Điểm neo hiện tại của đội hình trong thế giới.</summary>
    public Vector3 AnchorPosition => anchor != null ? anchor.position : owner.position + Vector3.up * anchorHeight;

    /// <summary>Điểm đang ngắm trong thế giới (nơi tia từ giữa màn hình chạm vào).</summary>
    public Vector3 AimPoint { get; private set; }

    /// <summary>Mục tiêu đang được hỗ trợ ngắm (null nếu không có). Tâm ngắm UI dùng để đổi màu.</summary>
    public Damageable AimTarget { get; private set; }

    private void Reset()
    {
        owner = transform;
    }

    private void OnValidate()
    {
        swordCount = Mathf.Clamp(swordCount, 0, MaxSwords);
        if (stats != null) swordCount = Mathf.Min(swordCount, stats.MaxSwords);
    }

    private void Awake()
    {
        current = startFormation;
        if (owner == null) owner = transform;
        if (stats != null) swordCount = Mathf.Min(swordCount, stats.MaxSwords);
        if (blockSparks != null) blockSparks.Play();   // đã tắt phát liên tục, chỉ phát khi gọi Emit
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.RealmChanged += OnRealmChanged;
            stats.Respawned += OnPlayerRespawned;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.RealmChanged -= OnRealmChanged;
            stats.Respawned -= OnPlayerRespawned;
        }
    }

    /// <summary>Đổi cảnh giới: bầy kiếm đầy theo Thần thức mới.</summary>
    private void OnRealmChanged(RealmData realm)
    {
        swordCount = Mathf.Clamp(realm.MaxSwords, 0, MaxSwords);
    }

    private void OnPlayerRespawned()
    {
        pendingVolleyTime = -1f;
        RecallAll();
    }

    private void Update()
    {
        bool alive = stats == null || stats.IsAlive;
        if (alive) HandleInput();
        UpdateAim();

        if (alive && pendingVolleyTime > 0f && Time.time >= pendingVolleyTime)
        {
            pendingVolleyTime = -1f;
            if (current == FormationType.Arrow) RequestVolley();
        }

        // Kiếm đang chờ tay duỗi tới điểm bắn: đến giờ thì phóng
        if (pendingFireKind != 0)
        {
            if (!alive) pendingFireKind = 0;
            else if (Time.time >= pendingFireTime)
            {
                int kind = pendingFireKind;
                pendingFireKind = 0;
                if (kind == 1) FireSingle(); else FireVolley();
            }
        }

        // Đội hình Vòng tốn pháp lực mỗi giây; cạn thì tự về Quạt
        if (alive && current == FormationType.Ring && stats != null && ringDrainPerSecond > 0f)
        {
            if (!stats.DrainMana(ringDrainPerSecond * Time.deltaTime))
            {
                stats.ReportManaShortage();
                SetFormation(FormationType.Fan);
            }
        }

        if (player != null) player.FaceCamera = alive && Time.time < aimUntil;
    }

    private void LateUpdate()
    {
        // Giới hạn Thần thức: ô "Số kiếm" trong Inspector không vượt quá mức tối đa
        int cap = SwordCap;
        if (swordCount > cap) swordCount = cap;

        SyncSwordCount();
        if (swords.Count == 0 || owner == null) return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (defenseEnabled) UpdateDefense();

        FormationContext ctx;
        ctx.origin = AnchorPosition;
        ctx.body = Quaternion.Euler(0f, owner.eulerAngles.y, 0f);
        ctx.time = Time.time;

        IFormation formation = ActiveFormation;
        int count = swords.Count;
        for (int i = 0; i < count; i++)
        {
            SwordSlot slot = formation.GetSlot(i, count, ctx);
            swords[i].Tick(slot, dt);
        }
    }

    // ================= Đầu vào =================

    private void HandleInput()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) TryChangeFormation(FormationType.Fan);
            if (kb.digit2Key.wasPressedThisFrame) TryChangeFormation(FormationType.Ring);
            if (kb.digit3Key.wasPressedThisFrame) OnArrowKey();
            if (kb.rKey.wasPressedThisFrame) RecallAll();
        }

        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        if (cursorLocked && mouse != null && mouse.leftButton.isPressed)
        {
            aimUntil = Mathf.Max(aimUntil, Time.time + aimHoldTime);
            if (Time.time >= nextFireTime && pendingFireKind == 0 && RequestSingle())
                nextFireTime = (pendingFireKind != 0 ? pendingFireTime : Time.time) + fireInterval;
        }
    }

    /// <summary>
    /// Yêu cầu bắn một kiếm: đưa tay vào tư thế bắn trước (qua ShootPoseGate), kiếm rời đội hình khi tay tới nơi.
    /// Trả về true nếu đã bắn hoặc đã hẹn bắn.
    /// </summary>
    private bool RequestSingle()
    {
        float delay = 0f;
        // Chỉ bắt tay giơ lên khi chắc chắn bắn được (còn kiếm rảnh và đủ pháp lực); thiếu thì để FireSingle báo cảnh báo
        if (ShootPoseGate != null && HasFreeSword() && (stats == null || stats.HasMana(singleShotCost)))
            delay = ShootPoseGate();
        if (delay <= 0.001f) return FireSingle();

        pendingFireKind = 1;
        pendingFireTime = Time.time + delay;
        aimUntil = Mathf.Max(aimUntil, pendingFireTime + aimHoldTime);   // thân quay theo camera ngay từ lúc bấm
        return true;
    }

    /// <summary>Giống RequestSingle nhưng cho loạt Mũi tên.</summary>
    private void RequestVolley()
    {
        float delay = 0f;
        int ready = ReadySwordCount;
        if (ShootPoseGate != null && ready > 0 && (stats == null || stats.HasMana(volleyCostPerSword * ready)))
            delay = ShootPoseGate();
        if (delay <= 0.001f) { FireVolley(); return; }

        pendingFireKind = 2;
        pendingFireTime = Time.time + delay;
        aimUntil = Mathf.Max(aimUntil, pendingFireTime + aimHoldTime);
    }

    /// <summary>Còn thanh kiếm nào rảnh (đứng trong đội hình, chưa hẹn lao) không.</summary>
    private bool HasFreeSword()
    {
        for (int i = 0; i < swords.Count; i++)
            if (swords[i].Mode == SwordMode.Formation && !swords[i].HasScheduledStrike) return true;
        return false;
    }

    /// <summary>Phím 3: vào đội hình Mũi tên; nếu đã ở đó (hoặc bật "lao luôn") thì lao cả đàn.</summary>
    private void OnArrowKey()
    {
        if (current != FormationType.Arrow)
        {
            if (!TryChangeFormation(FormationType.Arrow)) return;
            if (volleyOnKey3) pendingVolleyTime = Time.time + formationSettleTime * 0.75f;
        }
        else
        {
            RequestVolley();
        }
    }

    // ================= Công khai (UI, kiểm thử) =================

    /// <summary>Đổi đội hình có tốn pháp lực. Không đủ thì không đổi. Cùng đội hình thì không làm gì.</summary>
    public bool TryChangeFormation(FormationType type)
    {
        if (type == current) return false;
        if (stats != null && !stats.TrySpendMana(formationChangeCost)) return false;
        SetFormation(type);
        return true;
    }

    /// <summary>Đổi đội hình không tốn pháp lực (dùng nội bộ và để kiểm thử). Kiếm bay sang chỗ mới trong "Thời gian đổi đội hình".</summary>
    public void SetFormation(FormationType type)
    {
        pendingVolleyTime = -1f;
        pendingFireKind = 0;
        if (type == current) return;
        current = type;
        for (int i = 0; i < swords.Count; i++)
        {
            swords[i].ClearGuard();
            swords[i].OnFormationChanged();
        }
        FormationChanged?.Invoke(type);
    }

    /// <summary>FlyingSword gọi khi một nhát chém trúng mục tiêu: cộng Kiếm ý, dừng hình và rung camera.</summary>
    public void NotifyStrikeHit(Damageable target, float intentGain)
    {
        if (stats != null) stats.AddSwordIntent(intentGain);
        if (hitFeedback != null) hitFeedback.OnHit(!target.IsAlive);
    }

    /// <summary>Bấm R: gọi toàn bộ kiếm về đội hình, hủy mọi đòn đang hẹn giờ.</summary>
    public void RecallAll()
    {
        pendingVolleyTime = -1f;
        pendingFireKind = 0;
        for (int i = 0; i < swords.Count; i++)
        {
            FlyingSword s = swords[i];
            s.ClearGuard();
            if (s.Mode == SwordMode.Formation && !s.HasScheduledStrike) continue;
            s.CancelAndBeginReturn(recallTime);
        }
    }

    /// <summary>Phóng một thanh kiếm về tâm ngắm rồi tự quay về. Trả về false nếu không có kiếm rảnh hoặc không đủ pháp lực.</summary>
    public bool FireSingle()
    {
        FlyingSword s = PickShooter();
        if (s == null) return false;

        if (stats != null && !stats.TrySpendMana(singleShotCost))
        {
            nextFireTime = Time.time + 0.35f;   // đừng báo thiếu pháp lực mỗi khung hình khi đang giữ chuột
            return false;
        }

        Vector3 end = AimTarget != null ? AimTarget.AimPoint : AimPoint;
        Vector3 to = end - s.transform.position;
        Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : s.transform.forward;
        s.ClearGuard();
        s.LaunchStrike(end + dir * shotOvershoot, shotSpeed, strikeDamage, shotReturnTime, singleHitIntent);

        aimUntil = Mathf.Max(aimUntil, Time.time + aimHoldTime);
        StrikeFired?.Invoke();
        SwordLaunched?.Invoke();
        return true;
    }

    /// <summary>Lao cả đàn (các kiếm đang rảnh) vào mục tiêu gần tâm ngắm nhất, mỗi kiếm cách nhau một chút. Sau đó thu về. Tốn pháp lực theo số kiếm.</summary>
    public bool FireVolley()
    {
        temp.Clear();
        for (int i = 0; i < swords.Count; i++)
        {
            FlyingSword s = swords[i];
            if (s.Mode == SwordMode.Formation && !s.HasScheduledStrike) temp.Add(s);
        }
        if (temp.Count == 0) return false;

        if (stats != null && !stats.TrySpendMana(volleyCostPerSword * temp.Count)) return false;

        Damageable target = FindTarget(volleyLockAngle, volleyRange);
        Vector3 aim = target != null ? target.AimPoint : AimPoint;

        for (int k = 0; k < temp.Count; k++)
        {
            temp[k].ClearGuard();
            temp[k].ScheduleStrike(Time.time + k * volleyInterval, aim, volleyOvershoot, volleySpeed, volleyDamage, volleyReturnTime, volleyHitIntent);
        }

        aimUntil = Mathf.Max(aimUntil, Time.time + temp.Count * volleyInterval + aimHoldTime);
        StrikeFired?.Invoke();
        return true;
    }

    // ================= Chặn cầu =================

    /// <summary>Vị trí pos có nằm trong cung phía sau lưng (fanBlockArc độ) của nhân vật không.</summary>
    private bool InRearArc(Vector3 pos)
    {
        Vector3 rel = pos - owner.position;
        rel.y = 0f;
        if (rel.sqrMagnitude < 0.01f) return true;
        Vector3 back = -owner.forward;
        back.y = 0f;
        return Vector3.Angle(back, rel) <= fanBlockArc * 0.5f;
    }

    /// <summary>
    /// Mỗi khung hình: với từng quả cầu địch, nếu đội hình cho phép thì cử kiếm ra đón (Quạt) và kiểm tra va chạm với kiếm đang ở đội hình.
    /// Kiếm đang phóng đi/thu về/hẹn giờ lao thì không tham gia chặn.
    /// </summary>
    private void UpdateDefense()
    {
        List<EnemyOrb> orbs = EnemyOrb.All;
        if (orbs.Count == 0 || owner == null) return;
        if (current == FormationType.Arrow) return;

        Vector3 center = AnchorPosition;
        for (int o = orbs.Count - 1; o >= 0; o--)
        {
            EnemyOrb orb = orbs[o];
            if (orb == null || orb.IsDone) continue;
            Vector3 op = orb.Position;

            if (current == FormationType.Fan)
            {
                if (!InRearArc(op)) continue;   // cầu từ phía trước: không chặn
                if (!orb.HasGuard && orb.IsApproaching(center) && (op - center).sqrMagnitude < guardDetectRange * guardDetectRange)
                    AssignGuard(orb, center);
            }

            for (int i = 0; i < swords.Count; i++)
            {
                FlyingSword s = swords[i];
                if (s.Mode != SwordMode.Formation || s.HasScheduledStrike) continue;
                Vector3 closest;
                if (s.OverlapsSphere(op, orb.Radius, out closest))
                {
                    BlockOrb(orb, s, closest);
                    break;
                }
            }
        }
    }

    /// <summary>Chọn thanh kiếm rảnh gần điểm đón nhất và cho nó bay ra chắn đường cầu.</summary>
    private void AssignGuard(EnemyOrb orb, Vector3 center)
    {
        Vector3 dir = (orb.Position - center).normalized;
        Vector3 guardPoint = center + dir * guardDistance;

        FlyingSword best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < swords.Count; i++)
        {
            FlyingSword s = swords[i];
            if (s.Mode != SwordMode.Formation || s.HasScheduledStrike || s.IsGuarding) continue;
            float d = (s.transform.position - guardPoint).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best == null) return;   // hết kiếm rảnh: lưng trống

        Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        best.SetGuard(guardPoint, Quaternion.LookRotation(dir, up), guardHoldTime, guardSettleTime);
        orb.HasGuard = true;
    }

    /// <summary>Cầu bị kiếm chặn: tia lửa, kiếm rung nhẹ, hủy cầu.</summary>
    private void BlockOrb(EnemyOrb orb, FlyingSword sword, Vector3 point)
    {
        orb.Block();
        sword.ClearGuard();
        sword.Shake(blockShake, blockShakeTime);
        EmitBlockSparks(point, (orb.Position - sword.transform.position).normalized);
    }

    /// <summary>Phát tia lửa tại điểm chặn.</summary>
    public void EmitBlockSparks(Vector3 point, Vector3 normal)
    {
        if (blockSparks == null || blockSparkCount <= 0) return;
        Transform t = blockSparks.transform;
        t.position = point;
        if (normal.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(normal);
        blockSparks.Emit(blockSparkCount);
    }

    // ================= Nội bộ =================

    private IFormation GetFormation(FormationType type)
    {
        switch (type)
        {
            case FormationType.Ring: return ring;
            case FormationType.Arrow: return arrow;
            default: return fan;
        }
    }

    /// <summary>Chọn thanh kiếm rảnh tiếp theo để bắn, luân phiên đều các kiếm.</summary>
    private FlyingSword PickShooter()
    {
        int n = swords.Count;
        for (int k = 0; k < n; k++)
        {
            int idx = (nextShooter + k) % n;
            FlyingSword s = swords[idx];
            if (s.Mode == SwordMode.Formation && !s.HasScheduledStrike)
            {
                nextShooter = (idx + 1) % n;
                return s;
            }
        }
        return null;
    }

    private Camera GetCamera()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        return aimCamera;
    }

    /// <summary>Bắn tia từ giữa màn hình để biết điểm ngắm, và tìm mục tiêu được hỗ trợ ngắm.</summary>
    private void UpdateAim()
    {
        Camera cam = GetCamera();
        if (cam == null) return;

        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        float best = aimRange;
        Vector3 point = origin + dir * aimRange;

        int n = Physics.RaycastNonAlloc(origin, dir, rayHits, aimRange, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayHits[i];
            if (owner != null && h.collider.transform.IsChildOf(owner)) continue; // bỏ qua chính nhân vật
            if (h.distance < best)
            {
                best = h.distance;
                point = h.point;
            }
        }

        AimPoint = point;
        AimTarget = FindTarget(aimAssistAngle, aimRange);
    }

    /// <summary>Tìm Damageable còn sống nằm gần tâm ngắm nhất, trong góc và tầm cho phép.</summary>
    private Damageable FindTarget(float maxAngle, float range)
    {
        Camera cam = GetCamera();
        if (cam == null) return null;

        Vector3 camPos = cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        Damageable best = null;
        float bestAngle = maxAngle;

        List<Damageable> all = Damageable.All;
        for (int i = 0; i < all.Count; i++)
        {
            Damageable d = all[i];
            if (!d.IsAlive) continue;
            Vector3 v = d.AimPoint - camPos;
            float dist = v.magnitude;
            if (dist > range || dist < 0.01f) continue;
            float angle = Vector3.Angle(fwd, v);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                best = d;
            }
        }
        return best;
    }

    /// <summary>Thêm/bớt kiếm cho khớp "Số kiếm". Kiếm mới sinh ra ở người nhân vật rồi bay ra chỗ của nó.</summary>
    private void SyncSwordCount()
    {
        if (swordPrefab == null || owner == null) return;

        while (swords.Count < swordCount)
        {
            Vector3 spawn = AnchorPosition;
            GameObject go = Instantiate(swordPrefab, spawn, Quaternion.identity, transform);
            go.name = "Kiem_" + swords.Count;
            FlyingSword s = go.GetComponent<FlyingSword>();
            if (s == null)
            {
                Debug.LogError("Prefab phi kiếm thiếu script FlyingSword ở gốc.", swordPrefab);
                Destroy(go);
                swordCount = swords.Count;
                return;
            }
            s.Init(this, swords.Count);
            swords.Add(s);
        }

        while (swords.Count > swordCount)
        {
            FlyingSword last = swords[swords.Count - 1];
            swords.RemoveAt(swords.Count - 1);
            if (last != null) Destroy(last.gameObject);
        }
        if (nextShooter >= swords.Count) nextShooter = 0;
    }
}
