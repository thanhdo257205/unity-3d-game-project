using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// "Điểm ngắm" của camera: bám theo đầu nhân vật và xoay theo chuột.
/// Camera Cinemachine (góc nhìn thứ ba sau vai) sẽ đi theo đối tượng này,
/// nên xoay chuột = xoay camera quanh nhân vật.
/// Nhấn Esc để thả chuột ra khỏi cửa sổ Game, bấm chuột trái vào Game để khóa lại.
/// </summary>
public class CameraOrbit : MonoBehaviour
{
    [Header("Bám theo nhân vật")]
    [Tooltip("Nhân vật mà camera đi theo (kéo đối tượng NhanVat vào đây).")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Độ cao điểm ngắm so với chân nhân vật, tính bằng mét. Khoảng ngang vai/đầu (1.6) là hợp lý với nhân vật cao 1.8m.")]
    [SerializeField] private float heightOffset = 1.6f;

    [Header("Chuột")]
    [Tooltip("Độ nhạy chuột: số độ xoay cho mỗi điểm ảnh chuột di chuyển. Tăng lên nếu thấy xoay chậm, giảm nếu thấy xoay quá nhanh.")]
    [SerializeField, Min(0.001f)] private float sensitivity = 0.12f;

    [Tooltip("Bật nếu muốn kéo chuột lên là nhìn xuống (kiểu máy bay).")]
    [SerializeField] private bool invertY = false;

    [Tooltip("Góc ngẩng đầu tối đa (độ). Số âm = nhìn lên. Ví dụ -30 là ngẩng lên 30 độ.")]
    [SerializeField] private float minPitch = -30f;

    [Tooltip("Góc cúi đầu tối đa (độ). Số dương = nhìn xuống. Ví dụ 60 là cúi xuống 60 độ.")]
    [SerializeField] private float maxPitch = 60f;

    [Tooltip("Tự động khóa và ẩn con trỏ chuột khi bấm Play.")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Rung camera")]
    [Tooltip("Góc rung tối đa (độ) khi rung mạnh nhất. Số lớn = rung dữ hơn. Mỗi đòn đánh trúng chỉ rung một phần nhỏ của mức này.")]
    [SerializeField, Min(0f)] private float maxShakeAngle = 3f;

    [Tooltip("Tần số rung (lần/giây). Số lớn = rung rất nhanh và mịn, số nhỏ = lắc chậm.")]
    [SerializeField, Min(1f)] private float shakeFrequency = 28f;

    [Tooltip("Tốc độ tắt dần của rung (mỗi giây). 2.5 nghĩa là rung mạnh nhất tắt hẳn sau khoảng 0.4 giây.")]
    [SerializeField, Min(0.1f)] private float shakeDecay = 2.5f;

    [Tooltip("Mức rung tối đa khi nhiều đòn cùng trúng một lúc (0 đến 1). Giữ dưới 1 để cả loạt kiếm trúng không làm hình rung quá dữ.")]
    [SerializeField, Range(0.1f, 1f)] private float maxTrauma = 0.7f;

    private float yaw;
    private float pitch;
    private float trauma;          // mức rung hiện tại, 0 đến 1
    private float noiseSeed;

    /// <summary>Góc xoay ngang (độ). PlayerController dùng để biết "phía trước" là hướng nào.</summary>
    public float Yaw => yaw;

    /// <summary>Góc cúi/ngẩng (độ).</summary>
    public float Pitch => pitch;

    /// <summary>
    /// Làm camera rung. amount từ 0 đến 1: ~0.4 là rung nhẹ khi trúng đòn, ~0.8 là rung mạnh khi hạ địch.
    /// Các lần rung cộng dồn nhưng không vượt quá "Mức rung tối đa".
    /// </summary>
    public void AddShake(float amount)
    {
        trauma = Mathf.Min(maxTrauma, trauma + Mathf.Max(0f, amount));
    }

    private void Start()
    {
        noiseSeed = Random.value * 100f;
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        if (lockCursorOnStart) SetCursorLocked(true);
        SnapToTarget();
    }

    private void Update()
    {
        // Rung tắt dần theo thời gian thật để vẫn chạy trong lúc dừng hình
        trauma = Mathf.MoveTowards(trauma, 0f, shakeDecay * Time.unscaledDeltaTime);
        HandleCursorLock();
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * sensitivity;
        pitch += (invertY ? delta.y : -delta.y) * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        SnapToTarget();
    }

    /// <summary>Đặt vị trí theo nhân vật và áp dụng góc xoay hiện tại.</summary>
    private void SnapToTarget()
    {
        if (followTarget != null)
            transform.position = followTarget.position + Vector3.up * heightOffset;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f) * ShakeRotation();
    }

    /// <summary>Góc lệch ngẫu nhiên mượt (nhiễu Perlin) do rung. Chỉ ảnh hưởng hình ảnh, không đổi hướng đi/ngắm đã lưu.</summary>
    private Quaternion ShakeRotation()
    {
        if (trauma <= 0f) return Quaternion.identity;
        float power = trauma * trauma * maxShakeAngle;
        float t = Time.unscaledTime * shakeFrequency;
        float x = (Mathf.PerlinNoise(noiseSeed, t) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(noiseSeed + 31.7f, t) - 0.5f) * 2f;
        float z = (Mathf.PerlinNoise(noiseSeed + 67.3f, t) - 0.5f) * 2f;
        return Quaternion.Euler(x * power, y * power, z * power * 0.5f);
    }

    /// <summary>Esc = thả chuột; bấm chuột trái vào cửa sổ Game = khóa lại.</summary>
    private void HandleCursorLock()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            SetCursorLocked(true);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
