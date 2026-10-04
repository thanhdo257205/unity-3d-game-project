using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tài nguyên của người chơi: Máu, Pháp lực, Kiếm ý và Cảnh giới (Thần thức = số kiếm tối đa).
/// Các script khác (SwordSwarm, HUD, PlayerAnimator...) đọc và gọi qua đây.
/// Phím thử nghiệm (chỉ trong Editor): F1 hồi đầy máu và pháp lực, F2 tự gây 20 sát thương, F3 đầy Kiếm ý, F4/F5 hạ/nâng cảnh giới.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Máu")]
    [Tooltip("Máu tối đa của người chơi.")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Tooltip("Thời gian bất tử sau khi bị đánh (giây). Trong lúc này không nhận thêm sát thương.")]
    [SerializeField, Min(0f)] private float invulnerableTime = 0.5f;

    [Header("Pháp lực")]
    [Tooltip("Tốc độ hồi pháp lực bình thường (mỗi giây).")]
    [SerializeField, Min(0f)] private float manaRegen = 5f;

    [Tooltip("Tốc độ hồi pháp lực khi đã không dùng pháp lực một lúc (mỗi giây).")]
    [SerializeField, Min(0f)] private float manaRegenFast = 12f;

    [Tooltip("Sau bao nhiêu giây không dùng pháp lực thì chuyển sang hồi nhanh.")]
    [SerializeField, Min(0f)] private float manaFastDelay = 2f;

    [Header("Kiếm ý")]
    [Tooltip("Kiếm ý tối đa.")]
    [SerializeField, Min(1f)] private float maxSwordIntent = 100f;

    [Tooltip("Sau bao nhiêu giây không đánh trúng thì Kiếm ý bắt đầu tụt.")]
    [SerializeField, Min(0f)] private float intentDecayDelay = 8f;

    [Tooltip("Tốc độ Kiếm ý tụt (mỗi giây) khi đã quá thời gian trên.")]
    [SerializeField, Min(0f)] private float intentDecayRate = 1f;

    [Header("Chênh lệch cảnh giới (Phần 5.3)")]
    [Tooltip("Hệ số sát thương nhận vào khi đối thủ mạnh hơn mình một đại cảnh giới (mỗi bậc nhân thêm một lần).")]
    [SerializeField, Min(1f)] private float gapDamageTaken = 2.5f;

    [Tooltip("Hệ số sát thương gây ra khi đối thủ mạnh hơn mình một đại cảnh giới (mỗi bậc nhân thêm một lần).")]
    [SerializeField, Range(0.01f, 1f)] private float gapDamageDealt = 0.4f;

    [Header("Cảnh giới (Thần thức)")]
    [Tooltip("Thang cảnh giới theo thứ tự từ thấp đến cao (kéo các file RealmData vào). F4/F5 chuyển lên xuống thang này.")]
    [SerializeField] private RealmData[] realmLadder;

    [Tooltip("Vị trí trong thang của cảnh giới hiện tại. 2 = Luyện Khí tầng 7 nếu thang bắt đầu từ Luyện Khí tầng 1.")]
    [SerializeField, Min(0)] private int realmIndex = 2;

    private float health;
    private float mana;
    private float swordIntent;
    private float lastManaUseTime = -100f;
    private float lastDrainTime = -100f;
    private float lastIntentGainTime = -100f;
    private float invulnerableUntil;
    private bool intentWasFull;

    /// <summary>Máu hiện tại.</summary>
    public float Health => health;
    /// <summary>Máu tối đa.</summary>
    public float MaxHealth => maxHealth;
    /// <summary>Còn sống không.</summary>
    public bool IsAlive => health > 0f;
    /// <summary>Pháp lực hiện tại.</summary>
    public float Mana => mana;
    /// <summary>Pháp lực tối đa theo cảnh giới.</summary>
    public float MaxMana => Realm != null ? Realm.MaxMana : 100f;
    /// <summary>Kiếm ý hiện tại.</summary>
    public float SwordIntent => swordIntent;
    /// <summary>Kiếm ý tối đa.</summary>
    public float MaxSwordIntent => maxSwordIntent;
    /// <summary>Kiếm ý đã đầy chưa.</summary>
    public bool IntentFull => swordIntent >= maxSwordIntent - 0.001f;
    /// <summary>Đang trong thời gian bất tử không.</summary>
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    /// <summary>Cảnh giới hiện tại.</summary>
    public RealmData Realm => (realmLadder != null && realmLadder.Length > 0) ? realmLadder[Mathf.Clamp(realmIndex, 0, realmLadder.Length - 1)] : null;
    /// <summary>Số kiếm tối đa (Thần thức) của cảnh giới hiện tại.</summary>
    public int MaxSwords => Realm != null ? Realm.MaxSwords : 3;

    /// <summary>Người chơi duy nhất trong scene (kẻ địch, túi trữ vật dùng để tìm).</summary>
    public static PlayerStats Instance { get; private set; }

    /// <summary>Số linh thạch đang có.</summary>
    public int SpiritStones { get; private set; }

    /// <summary>Đại cảnh giới hiện tại (0 = Luyện Khí...).</summary>
    public int MajorRealm => Realm != null ? Realm.MajorRealm : 0;

    /// <summary>Báo khi nhặt được linh thạch (tham số: số vừa nhận).</summary>
    public event Action<int> SpiritStonesGained;

    /// <summary>Báo khi bị đánh trúng (sát thương thật đã trừ).</summary>
    public event Action<float> Damaged;
    /// <summary>Báo khi hết máu.</summary>
    public event Action Died;
    /// <summary>Báo khi hồi sinh.</summary>
    public event Action Respawned;
    /// <summary>Báo khi cảnh giới đổi.</summary>
    public event Action<RealmData> RealmChanged;
    /// <summary>Báo khi định dùng pháp lực mà không đủ (dòng chữ cảnh báo trên HUD).</summary>
    public event Action ManaInsufficient;
    /// <summary>Báo khi Kiếm ý vừa đầy.</summary>
    public event Action IntentBecameFull;

    private void Awake()
    {
        Instance = this;
        RealmRules.TakenPerGap = gapDamageTaken;
        RealmRules.DealtPerGap = gapDamageDealt;
        health = maxHealth;
        mana = MaxMana;
    }

    private void Update()
    {
        if (!IsAlive) return;
        UpdateMana();
        UpdateIntent();
#if UNITY_EDITOR
        HandleDebugKeys();
#endif
    }

    private void UpdateMana()
    {
        float max = MaxMana;
        if (mana >= max) { mana = max; return; }
        // Đang bị trừ liên tục (đội hình Vòng) thì không hồi, nếu không Vòng gần như miễn phí
        if (Time.time - lastDrainTime < 0.15f) return;
        bool fast = Time.time - lastManaUseTime >= manaFastDelay;
        mana = Mathf.Min(max, mana + (fast ? manaRegenFast : manaRegen) * Time.deltaTime);
    }

    private void UpdateIntent()
    {
        if (swordIntent <= 0f) return;
        if (Time.time - lastIntentGainTime >= intentDecayDelay)
        {
            swordIntent = Mathf.Max(0f, swordIntent - intentDecayRate * Time.deltaTime);
            if (swordIntent < maxSwordIntent - 0.001f) intentWasFull = false;
        }
    }

    /// <summary>Trừ pháp lực một lần. Không đủ thì không trừ, báo cảnh báo và trả về false.</summary>
    public bool TrySpendMana(float amount)
    {
        if (!IsAlive) return false;
        if (mana + 0.0001f < amount)
        {
            ManaInsufficient?.Invoke();
            return false;
        }
        mana = Mathf.Max(0f, mana - amount);
        lastManaUseTime = Time.time;
        return true;
    }

    /// <summary>Hồi pháp lực (dùng đan dược). Không vượt quá mức tối đa.</summary>
    public void RestoreMana(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        mana = Mathf.Min(MaxMana, mana + amount);
    }

    /// <summary>Báo "pháp lực không đủ" mà không trừ gì (ví dụ khi đội hình Vòng cạn pháp lực).</summary>
    public void ReportManaShortage()
    {
        ManaInsufficient?.Invoke();
    }

    /// <summary>Kiểm tra đủ pháp lực mà không trừ (không báo cảnh báo).</summary>
    public bool HasMana(float amount) => mana + 0.0001f >= amount;

    /// <summary>Trừ pháp lực liên tục (ví dụ đội hình Vòng). Trả về false khi đã cạn.</summary>
    public bool DrainMana(float amount)
    {
        if (mana <= 0f) return false;
        mana = Mathf.Max(0f, mana - amount);
        lastManaUseTime = Time.time;
        lastDrainTime = Time.time;
        return mana > 0f;
    }

    /// <summary>Cộng Kiếm ý (khi kiếm đánh trúng).</summary>
    public void AddSwordIntent(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        swordIntent = Mathf.Min(maxSwordIntent, swordIntent + amount);
        lastIntentGainTime = Time.time;
        if (IntentFull && !intentWasFull)
        {
            intentWasFull = true;
            IntentBecameFull?.Invoke();
        }
    }

    /// <summary>Cộng linh thạch (nhặt túi trữ vật).</summary>
    public void AddSpiritStones(int amount)
    {
        if (amount <= 0) return;
        SpiritStones += amount;
        SpiritStonesGained?.Invoke(amount);
    }

    /// <summary>Nhận sát thương. Bỏ qua nếu đang bất tử hoặc đã chết. Trả về true nếu thật sự bị trừ máu.</summary>
    public bool TakeDamage(float amount)
    {
        if (!IsAlive || IsInvulnerable || amount <= 0f) return false;
        health = Mathf.Max(0f, health - amount);
        invulnerableUntil = Time.time + invulnerableTime;
        Damaged?.Invoke(amount);
        if (health <= 0f) Died?.Invoke();
        return true;
    }

    /// <summary>Cho bất tử thêm seconds giây (Phá Không: đang xuyên không gian thì không nhận sát thương).</summary>
    public void GrantInvulnerability(float seconds)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    }

    /// <summary>Hồi sinh: đầy máu, đầy pháp lực, xóa Kiếm ý.</summary>
    public void Revive()
    {
        health = maxHealth;
        mana = MaxMana;
        swordIntent = 0f;
        intentWasFull = false;
        invulnerableUntil = Time.time + 1f;
        Respawned?.Invoke();
    }

    /// <summary>Đặt cảnh giới theo vị trí trong thang (có giới hạn). Giữ nguyên tỉ lệ pháp lực.</summary>
    public void SetRealmIndex(int index)
    {
        if (realmLadder == null || realmLadder.Length == 0) return;
        index = Mathf.Clamp(index, 0, realmLadder.Length - 1);
        if (index == realmIndex && Realm != null) return;
        float ratio = MaxMana > 0f ? mana / MaxMana : 1f;
        realmIndex = index;
        mana = Mathf.Clamp(ratio, 0f, 1f) * MaxMana;
        RealmChanged?.Invoke(Realm);
    }

    /// <summary>Vị trí cảnh giới hiện tại trong thang.</summary>
    public int RealmIndex => realmIndex;

    /// <summary>Số cảnh giới trong thang.</summary>
    public int RealmCount => realmLadder != null ? realmLadder.Length : 0;

#if UNITY_EDITOR
    private void HandleDebugKeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame) { health = maxHealth; mana = MaxMana; }
        if (kb.f2Key.wasPressedThisFrame) TakeDamage(20f);
        if (kb.f3Key.wasPressedThisFrame) AddSwordIntent(maxSwordIntent);
        if (kb.f4Key.wasPressedThisFrame) SetRealmIndex(realmIndex - 1);
        if (kb.f5Key.wasPressedThisFrame) SetRealmIndex(realmIndex + 1);
    }
#endif
}
