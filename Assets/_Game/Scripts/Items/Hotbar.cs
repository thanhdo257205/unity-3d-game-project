using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Thanh ô đồ 6 ô (kiểu Minecraft). Chọn ô bằng phím 1-6 hoặc lăn chuột.
/// Ô chứa Đan dược thì chuột trái để dùng (hồi pháp lực, mất 1 viên). Ô trống = tay không.
/// Ô chứa vũ khí thì HeldWeapon lo rút/cất, PlayerMelee lo chém.
/// Giao diện do HUD vẽ, script này chỉ giữ dữ liệu và báo sự kiện.
/// </summary>
public class Hotbar : MonoBehaviour
{
    /// <summary>Số ô trên thanh.</summary>
    public const int SlotCount = 6;

    /// <summary>Một ô: vật phẩm và số lượng.</summary>
    [Serializable]
    public class Slot
    {
        [Tooltip("Vật phẩm trong ô. Để trống = ô trống (tay không).")]
        public ItemData item;

        [Tooltip("Số lượng vật phẩm trong ô.")]
        [Min(0)] public int count = 1;
    }

    [Header("Tham chiếu")]
    [Tooltip("Tài nguyên người chơi (để hồi pháp lực khi dùng đan, biết còn sống không). Để trống thì tự tìm.")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Script điều khiển nhân vật (để biết đang bị khóa điều khiển không). Để trống thì tự tìm.")]
    [SerializeField] private PlayerController player;

    [Header("Các ô (ô 1 trống = tay không, ô 2 Thanh Trúc Kiếm, ô 3 Hồi Khí Đan x3)")]
    [SerializeField] private Slot[] slots = new Slot[SlotCount];

    [Header("Chọn ô")]
    [Tooltip("Ô đang chọn lúc bắt đầu (0 = ô số 1).")]
    [SerializeField, Range(0, SlotCount - 1)] private int startIndex = 0;

    [Tooltip("Lăn chuột tới ô cuối thì quay vòng về ô đầu (và ngược lại).")]
    [SerializeField] private bool wrapScroll = true;

    [Tooltip("Đảo chiều lăn chuột (mặc định lăn xuống = ô kế tiếp, giống Minecraft).")]
    [SerializeField] private bool invertScroll = false;

    [Header("Dùng đan dược")]
    [Tooltip("Thời gian chờ giữa hai lần dùng đan (giây).")]
    [SerializeField, Min(0f)] private float useCooldown = 0.4f;

    [Tooltip("Pháp lực đã đầy thì không dùng đan (khỏi phí viên đan).")]
    [SerializeField] private bool blockUseWhenManaFull = true;

    private int selected;
    private float nextUseTime;

    /// <summary>Báo khi đổi ô được chọn (tham số: chỉ số ô 0-5).</summary>
    public event Action<int> SelectionChanged;

    /// <summary>Báo khi nội dung một ô đổi (số lượng giảm, thêm vật phẩm...).</summary>
    public event Action<int> SlotChanged;

    /// <summary>Báo khi dùng xong một vật phẩm (đan dược). HUD hiện dòng thông báo.</summary>
    public event Action<ItemData> ItemUsed;

    /// <summary>Báo khi bấm chuột trái lúc đang chọn ô chứa Pháp bảo (bật/tắt pháp bảo đó).</summary>
    public event Action<ItemData> PhapBaoActivated;

    /// <summary>Chỉ số ô đang chọn (0-5).</summary>
    public int SelectedIndex => selected;

    /// <summary>Vật phẩm ở ô đang chọn (null = tay không).</summary>
    public ItemData SelectedItem => GetItem(selected);

    /// <summary>Số lượng ở ô đang chọn.</summary>
    public int SelectedCount => GetCount(selected);

    /// <summary>Vật phẩm ở ô i (null nếu trống).</summary>
    public ItemData GetItem(int i)
    {
        if (slots == null || i < 0 || i >= slots.Length || slots[i] == null) return null;
        return slots[i].count > 0 ? slots[i].item : null;
    }

    /// <summary>Số lượng ở ô i.</summary>
    public int GetCount(int i)
    {
        if (slots == null || i < 0 || i >= slots.Length || slots[i] == null || slots[i].item == null) return 0;
        return slots[i].count;
    }

    private void Reset()
    {
        EnsureSlots();
    }

    private void OnValidate()
    {
        EnsureSlots();
    }

    private void EnsureSlots()
    {
        if (slots == null || slots.Length != SlotCount)
        {
            Slot[] old = slots;
            slots = new Slot[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                slots[i] = old != null && i < old.Length && old[i] != null ? old[i] : new Slot();
        }
        for (int i = 0; i < SlotCount; i++) if (slots[i] == null) slots[i] = new Slot();
    }

    private void Awake()
    {
        EnsureSlots();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (player == null) player = GetComponent<PlayerController>();
        selected = Mathf.Clamp(startIndex, 0, SlotCount - 1);
    }

    private void Update()
    {
        if (stats != null && !stats.IsAlive) return;
        if (player != null && player.ControlLocked) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null)
        {
            for (int i = 0; i < SlotCount; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) Select(i);
        }

        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int dir = scroll < 0f ? 1 : -1;
                if (invertScroll) dir = -dir;
                Cycle(dir);
            }

            // Chuột trái dùng đan dược (chỉ khi con trỏ đã khóa vào cửa sổ Game)
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
                TryUseSelected();
        }
    }

    /// <summary>Chọn ô i (0-5). Báo SelectionChanged nếu thật sự đổi.</summary>
    public void Select(int index)
    {
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        if (index == selected) return;
        selected = index;
        SelectionChanged?.Invoke(selected);
    }

    /// <summary>Chuyển sang ô kế tiếp (dir = 1) hoặc ô trước (dir = -1).</summary>
    public void Cycle(int dir)
    {
        int next = selected + dir;
        if (wrapScroll) next = (next % SlotCount + SlotCount) % SlotCount;
        else next = Mathf.Clamp(next, 0, SlotCount - 1);
        Select(next);
    }

    /// <summary>Đặt vật phẩm vào ô i (dùng khi nhặt đồ hoặc để thử). count được kẹp theo maxStack.</summary>
    public void SetSlot(int index, ItemData item, int count)
    {
        EnsureSlots();
        if (index < 0 || index >= SlotCount) return;
        slots[index].item = item;
        slots[index].count = item == null ? 0 : Mathf.Clamp(count, 1, item.maxStack);
        SlotChanged?.Invoke(index);
    }

    /// <summary>Dùng vật phẩm ở ô đang chọn nếu là đan dược. Trả về true nếu đã dùng.</summary>
    public bool TryUseSelected()
    {
        ItemData item = SelectedItem;
        if (item != null && item.type == ItemType.PhapBao)
        {
            // Pháp bảo: chuột trái bật/tắt, việc cụ thể do script của pháp bảo (ví dụ PhongLoiSi) lo
            if (stats == null || !stats.IsAlive) return false;
            PhapBaoActivated?.Invoke(item);
            return true;
        }
        if (item == null || item.type != ItemType.DanDuoc) return false;
        if (stats == null || !stats.IsAlive) return false;
        if (Time.time < nextUseTime && !AdminPanel.KhongHoiChieu) return false;
        if (blockUseWhenManaFull && stats.Mana >= stats.MaxMana - 0.01f) return false;

        stats.RestoreMana(item.manaRestore);
        slots[selected].count--;
        if (slots[selected].count <= 0)
        {
            slots[selected].item = null;
            slots[selected].count = 0;
        }
        nextUseTime = Time.time + useCooldown;
        SlotChanged?.Invoke(selected);
        ItemUsed?.Invoke(item);
        return true;
    }
}
