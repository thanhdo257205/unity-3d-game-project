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

    private float yaw;
    private float pitch;

    /// <summary>Góc xoay ngang (độ). PlayerController dùng để biết "phía trước" là hướng nào.</summary>
    public float Yaw => yaw;

    /// <summary>Góc cúi/ngẩng (độ).</summary>
    public float Pitch => pitch;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        if (lockCursorOnStart) SetCursorLocked(true);
        SnapToTarget();
    }

    private void Update()
    {
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
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
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
