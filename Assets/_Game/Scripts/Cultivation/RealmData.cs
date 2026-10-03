using UnityEngine;

/// <summary>
/// Dữ liệu một cảnh giới tu luyện (bảng 5.1 trong CLAUDE.md). Mỗi cảnh giới là một file trong Assets/_Game/Data/Realms.
/// Chỉnh số trực tiếp trong Inspector của từng file. Số kiếm tối đa chính là "Thần thức".
/// </summary>
[CreateAssetMenu(fileName = "CanhGioi_", menuName = "Phàm Kiếm Lục/Cảnh giới")]
public class RealmData : ScriptableObject
{
    [Tooltip("Tên hiển thị trên HUD, ví dụ 'Luyện Khí tầng 7'.")]
    [SerializeField] private string displayName = "Luyện Khí tầng 1";

    [Tooltip("Thứ tự của cảnh giới trong thang tu luyện (tăng dần). Dùng để sắp xếp và để F4/F5 chuyển cảnh giới.")]
    [SerializeField, Min(0)] private int order;

    [Tooltip("Thần thức: số phi kiếm tối đa điều khiển cùng lúc.")]
    [SerializeField, Min(0)] private int maxSwords = 3;

    [Tooltip("Pháp lực tối đa.")]
    [SerializeField, Min(1f)] private float maxMana = 100f;

    [Tooltip("Ghi chú mở khóa theo bảng thiết kế (chỉ để tham khảo, chưa khóa tính năng nào).")]
    [SerializeField, TextArea] private string unlockNote;

    /// <summary>Tên hiển thị trên HUD.</summary>
    public string DisplayName => displayName;

    /// <summary>Thứ tự trong thang tu luyện.</summary>
    public int Order => order;

    /// <summary>Số kiếm tối đa (Thần thức).</summary>
    public int MaxSwords => maxSwords;

    /// <summary>Pháp lực tối đa.</summary>
    public float MaxMana => maxMana;

    /// <summary>Ghi chú mở khóa.</summary>
    public string UnlockNote => unlockNote;
}
