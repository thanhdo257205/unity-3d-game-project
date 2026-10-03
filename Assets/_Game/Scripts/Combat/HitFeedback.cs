using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Tạo "lực" cho đòn đánh (kiếm lẫn quyền cước): dừng hình một khoảnh khắc và rung camera bằng Cinemachine Impulse.
/// Hạ gục được địch thì dừng lâu hơn và rung mạnh hơn.
/// SwordSwarm và PlayerMelee gọi <see cref="OnHit"/> mỗi khi một đòn trúng.
/// Có công tắc riêng để tắt rung hoặc tắt dừng hình.
/// </summary>
public class HitFeedback : MonoBehaviour
{
    [Header("Công tắc")]
    [Tooltip("Bật/tắt rung camera khi trúng đòn.")]
    [SerializeField] private bool enableShake = true;

    [Tooltip("Bật/tắt dừng hình khi trúng đòn.")]
    [SerializeField] private bool enableHitStop = true;

    [Header("Rung camera (Cinemachine Impulse)")]
    [Tooltip("Nguồn phát rung. CinemachineCamera cần có thêm 'Cinemachine Impulse Listener' để nhận rung.")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Tooltip("Độ giật camera khi trúng đòn (mét). 0.04 là rung nhẹ.")]
    [SerializeField, Min(0f)] private float hitShake = 0.04f;

    [Tooltip("Độ giật camera khi hạ gục mục tiêu (mét). Mạnh hơn khi trúng thường.")]
    [SerializeField, Min(0f)] private float killShake = 0.1f;

    [Header("Dừng hình khi trúng")]
    [Tooltip("Thời gian dừng hình khi đòn trúng mục tiêu (giây, tính theo thời gian thật).")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.04f;

    [Tooltip("Thời gian dừng hình khi đòn hạ gục mục tiêu (giây).")]
    [SerializeField, Min(0f)] private float killHitStopDuration = 0.08f;

    [Tooltip("Khoảng cách tối thiểu giữa hai lần dừng hình (giây). Tránh hình bị khựng liên tục khi cả loạt kiếm cùng trúng.")]
    [SerializeField, Min(0f)] private float hitStopMinInterval = 0.12f;

    private float hitStopEndTime;   // theo thời gian thật
    private float lastHitStopTime = -100f;
    private bool stopping;

    private void Awake()
    {
        ConfigureImpulse();
    }

    /// <summary>Đặt dạng xung rung: một cú giật ngắn rồi trở về (Bump), phát ở kênh 1 như Impulse Listener.</summary>
    private void ConfigureImpulse()
    {
        if (impulseSource == null) return;
        CinemachineImpulseDefinition def = impulseSource.ImpulseDefinition;
        def.ImpulseChannel = 1;
        def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        def.ImpulseDuration = 0.22f;
    }

    /// <summary>Gọi khi một đòn trúng. killed = đòn này làm mục tiêu hết máu.</summary>
    public void OnHit(bool killed)
    {
        if (enableShake && impulseSource != null)
        {
            float amp = killed ? killShake : hitShake;
            Vector3 dir = Random.onUnitSphere;
            dir.z *= 0.3f;                                  // giật chủ yếu theo chiều ngang/dọc màn hình
            impulseSource.GenerateImpulseWithVelocity(dir.normalized * amp);
        }

        if (!enableHitStop) return;
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
