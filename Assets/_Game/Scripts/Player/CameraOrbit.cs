using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// "Điểm ngắm" của camera: bám theo đầu nhân vật và xoay theo chuột.
/// Camera Cinemachine (góc nhìn thứ ba sau vai) sẽ đi theo đối tượng này,
/// nên xoay chuột = xoay camera quanh nhân vật.
/// Phím F đổi giữa góc nhìn sau vai và góc nhìn thứ nhất.
/// Nhấn Esc để thả chuột ra khỏi cửa sổ Game, bấm chuột trái vào Game để khóa lại.
/// (Rung camera khi trúng đòn do HitFeedback + Cinemachine Impulse đảm nhiệm.)
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

    [Header("Góc nhìn thứ nhất")]
    [Tooltip("Phím đổi giữa góc nhìn thứ nhất và góc nhìn sau vai. E đã dành cho Phá Không.")]
    [SerializeField] private Key switchViewKey = Key.F;

    [Tooltip("Độ cao mắt so với chân nhân vật. Không bám xương đầu để tránh rung theo animation.")]
    [SerializeField, Min(0.1f)] private float firstPersonHeight = 1.65f;

    private CinemachineCamera viewCamera;
    private CinemachineThirdPersonFollow cameraBody;
    private Vector3 savedShoulder, savedDamping;
    private float savedArmLength, savedDistance, savedNearClip;
    private readonly Dictionary<Renderer, ShadowCastingMode> hiddenRenderers = new Dictionary<Renderer, ShadowCastingMode>();

    public bool IsFirstPerson { get; private set; }

    private float yaw;
    private float pitch;

    /// <summary>Khi tắt, camera không xoay theo chuột và không tự khóa lại con trỏ (dùng lúc hiện màn hình chết).</summary>
    public bool InputEnabled { get; set; } = true;

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
        if (!InputEnabled) return;
        HandleCursorLock();
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Keyboard kb = Keyboard.current;
        if (kb != null && switchViewKey != Key.None && kb[switchViewKey].wasPressedThisFrame)
            SetFirstPerson(!IsFirstPerson);

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

    /// <summary>Đổi góc nhìn, giữ nguyên hướng ngắm và camera đang dùng cho chiến đấu/hiệu ứng.</summary>
    public void SetFirstPerson(bool enabled)
    {
        if (enabled == IsFirstPerson) return;
        if (enabled)
        {
            if (followTarget == null) return;
            foreach (var candidate in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                if (candidate.Follow != transform) continue;
                var body = candidate.GetComponent<CinemachineThirdPersonFollow>();
                if (body == null) continue;
                viewCamera = candidate;
                cameraBody = body;
                break;
            }
            if (viewCamera == null || cameraBody == null) return;

            savedShoulder = cameraBody.ShoulderOffset;
            savedDamping = cameraBody.Damping;
            savedArmLength = cameraBody.VerticalArmLength;
            savedDistance = cameraBody.CameraDistance;
            savedNearClip = viewCamera.Lens.NearClipPlane;
            cameraBody.ShoulderOffset = Vector3.zero;
            cameraBody.Damping = Vector3.zero;
            cameraBody.VerticalArmLength = 0f;
            cameraBody.CameraDistance = 0f;
            var lens = viewCamera.Lens;
            lens.NearClipPlane = 0.03f;
            viewCamera.Lens = lens;

            // Giữ bóng đổ, ẩn thân/áo/tóc để không che mắt. Không đổi enabled:
            // Phá Không vẫn có thể tự ẩn/hiện nhân vật độc lập.
            var model = followTarget.GetComponentInChildren<Animator>();
            if (model != null)
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                    hiddenRenderers[renderer] = renderer.shadowCastingMode;
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
        }
        else
        {
            if (cameraBody != null)
            {
                cameraBody.ShoulderOffset = savedShoulder;
                cameraBody.Damping = savedDamping;
                cameraBody.VerticalArmLength = savedArmLength;
                cameraBody.CameraDistance = savedDistance;
            }
            if (viewCamera != null)
            {
                var lens = viewCamera.Lens;
                lens.NearClipPlane = savedNearClip;
                viewCamera.Lens = lens;
            }
            foreach (var entry in hiddenRenderers)
                if (entry.Key != null) entry.Key.shadowCastingMode = entry.Value;
            hiddenRenderers.Clear();
        }
        IsFirstPerson = enabled;
        SnapToTarget();
        if (viewCamera != null) viewCamera.PreviousStateIsValid = false;
    }

    private void OnDisable()
    {
        SetFirstPerson(false);
    }

    private Vector3 glideFrom;
    private float glideStart = -10f, glideDuration = 0.3f;

    /// <summary>
    /// Camera lướt mượt từ vị trí fromPos tới chỗ nhân vật (ngay cả khi nhân vật vừa dịch chuyển xa) trong seconds giây, không nhảy cắt (Phá Không).
    /// Gọi ngay trước khi dời nhân vật, với fromPos là vị trí điểm ngắm hiện tại (transform.position của đối tượng này).
    /// </summary>
    public void Glide(Vector3 fromPos, float seconds)
    {
        glideFrom = fromPos;
        glideStart = Time.time;
        glideDuration = Mathf.Max(0.02f, seconds);
    }

    /// <summary>Đặt vị trí theo nhân vật và áp dụng góc xoay hiện tại.</summary>
    private void SnapToTarget()
    {
        if (followTarget != null)
        {
            Vector3 target = followTarget.position + Vector3.up * (IsFirstPerson ? firstPersonHeight : heightOffset);
            float g = (Time.time - glideStart) / glideDuration;
            if (!IsFirstPerson && g >= 0f && g < 1f) target = Vector3.Lerp(glideFrom, target, g * g * (3f - 2f * g));
            transform.position = target;
        }
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

    /// <summary>Khóa/thả con trỏ chuột.</summary>
    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
