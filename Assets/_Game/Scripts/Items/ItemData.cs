using UnityEngine;

/// <summary>Loại vật phẩm trên thanh ô đồ.</summary>
public enum ItemType
{
    /// <summary>Vũ khí cầm tay (kiếm...). Chọn ô này thì nhân vật rút vũ khí ra.</summary>
    VuKhi,
    /// <summary>Pháp bảo (để dành cho giai đoạn sau).</summary>
    PhapBao,
    /// <summary>Đan dược: bấm chuột trái để dùng, mỗi lần mất 1 viên.</summary>
    DanDuoc,
    /// <summary>Phù chú (để dành cho giai đoạn sau).</summary>
    Phu
}

/// <summary>
/// Dữ liệu một loại vật phẩm (tạo bằng: chuột phải trong Project → Create → Phàm Kiếm Lục → Vật phẩm).
/// Thanh ô đồ (Hotbar) chỉ giữ tham chiếu tới file này và số lượng.
/// </summary>
[CreateAssetMenu(menuName = "Phàm Kiếm Lục/Vật phẩm", fileName = "VatPhamMoi")]
public class ItemData : ScriptableObject
{
    [Header("Chung")]
    [Tooltip("Tên hiển thị phía trên thanh ô đồ khi chọn ô.")]
    public string displayName = "Vật phẩm";

    [Tooltip("Biểu tượng hiện trong ô (ảnh PNG nền trong suốt, kiểu Sprite).")]
    public Sprite icon;

    [Tooltip("Loại vật phẩm: VuKhi (cầm tay), PhapBao, DanDuoc (dùng bằng chuột trái), Phu.")]
    public ItemType type = ItemType.VuKhi;

    [Tooltip("Prefab của vật phẩm. Vũ khí: mô hình cầm trên tay (có điểm DayLuoi và MuiKiem để vẽ vệt chém). Đan dược: viên đan (chỉ để chụp biểu tượng).")]
    public GameObject prefab;

    [Tooltip("Số lượng tối đa chồng được trong một ô.")]
    [Min(1)] public int maxStack = 1;

    [Header("Đan dược")]
    [Tooltip("Pháp lực hồi lại mỗi lần dùng một viên (chỉ dùng cho loại DanDuoc).")]
    [Min(0f)] public float manaRestore = 80f;
}
