using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tâm ngắm giữa màn hình: chỉ hiện khi Ngự Kiếm Thuật đang bật (có phi kiếm để ngắm).
/// Các thanh trắng đổi sang màu đỏ khi có mục tiêu nằm trong vùng hỗ trợ ngắm.
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Tooltip("Đàn kiếm để hỏi xem có đang ngắm trúng mục tiêu không.")]
    [SerializeField] private SwordSwarm swarm;

    [Tooltip("Các thanh/điểm của tâm ngắm sẽ được đổi màu.")]
    [SerializeField] private Graphic[] parts;

    [Tooltip("Màu bình thường.")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("Màu khi ngắm trúng mục tiêu.")]
    [SerializeField] private Color targetColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Tooltip("Màu tâm ngắm khi đã khóa mục tiêu bằng Tab; Tab lần nữa để bỏ khóa.")]
    [SerializeField] private Color lockedColor = new Color(0.2f, 1f, 0.7f, 1f);

    private void Update()
    {
        if (swarm == null || parts == null) return;
        bool show = swarm.IsActive;
        Color c = swarm.LockedTarget != null ? lockedColor : swarm.AimTarget != null ? targetColor : normalColor;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;
            parts[i].enabled = show;
            parts[i].color = c;
        }
    }
}
