using UnityEngine;

/// <summary>
/// Tạo "lực" cho đòn đánh: dừng hình một khoảnh khắc và rung camera khi kiếm trúng mục tiêu.
/// Hạ gục được địch thì dừng lâu hơn và rung mạnh hơn.
/// SwordSwarm gọi <see cref="OnHit"/> mỗi khi một nhát chém trúng.
/// </summary>
public class HitFeedback : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Camera xoay bằng chuột (đối tượng CameraTarget) để rung. Có thể để trống thì không rung.")]
    [SerializeField] private CameraOrbit cameraOrbit;

    [Header("Dừng hình khi trúng")]
    [Tooltip("Thời gian dừng hình khi nhát chém trúng mục tiêu (giây, tính theo thời gian thật). 0 = tắt.")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.05f;

    [Tooltip("Thời gian dừng hình khi nhát chém hạ gục mục tiêu (giây).")]
    [SerializeField, Min(0f)] private float killHitStopDuration = 0.1f;

    [Tooltip("Khoảng cách tối thiểu giữa hai lần dừng hình (giây). Tránh hình bị khựng liên tục khi cả loạt kiếm cùng trúng.")]
    [SerializeField, Min(0f)] private float hitStopMinInterval = 0.12f;

    [Header("Rung camera")]
    [Tooltip("Mức rung khi trúng đòn, từ 0 đến 1. 0.4 là rung nhẹ.")]
    [SerializeField, Range(0f, 1f)] private float hitShake = 0.4f;

    [Tooltip("Mức rung khi hạ gục mục tiêu, từ 0 đến 1.")]
    [SerializeField, Range(0f, 1f)] private float killShake = 0.8f;

    private float hitStopEndTime;   // theo thời gian thật
    private float lastHitStopTime = -100f;
    private bool stopping;

    /// <summary>Gọi khi một nhát chém trúng. killed = nhát này làm mục tiêu hết máu.</summary>
    public void OnHit(bool killed)
    {
        if (cameraOrbit != null) cameraOrbit.AddShake(killed ? killShake : hitShake);

        float duration = killed ? killHitStopDuration : hitStopDuration;
        if (duration <= 0f) return;

        float now = Time.unscaledTime;
        if (!stopping && now - lastHitStopTime < hitStopMinInterval && !killed) return;

        hitStopEndTime = Mathf.Max(hitStopEndTime, now + duration);
        lastHitStopTime = now;
        stopping = true;
        Time.timeScale = 0f;
    }

    private void Update()
    {
        if (stopping && Time.unscaledTime >= hitStopEndTime)
        {
            stopping = false;
            Time.timeScale = 1f;
        }
    }

    private void OnDisable()
    {
        // Đừng để lại trạng thái dừng hình nếu đối tượng bị tắt giữa chừng
        if (stopping)
        {
            stopping = false;
            Time.timeScale = 1f;
        }
    }
}
