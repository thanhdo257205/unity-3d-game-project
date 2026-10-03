using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Điều khiển nhân vật người chơi: đi bộ, chạy, nhảy bằng CharacterController.
/// Hướng đi tính theo hướng camera (xem CameraOrbit): bấm W là đi về phía camera đang nhìn.
/// Mọi con số có thể chỉnh trong Inspector mà không cần sửa code.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Đối tượng camera xoay bằng chuột. Hướng đi (W/A/S/D) sẽ lấy theo hướng nhìn của nó. Kéo đối tượng CameraTarget vào đây.")]
    [SerializeField] private CameraOrbit cameraOrbit;

    [Header("Tốc độ di chuyển")]
    [Tooltip("Tốc độ đi bộ, tính bằng mét/giây.")]
    [SerializeField, Min(0f)] private float walkSpeed = 4.5f;

    [Tooltip("Tốc độ chạy khi giữ phím Shift, tính bằng mét/giây.")]
    [SerializeField, Min(0f)] private float runSpeed = 8f;

    [Tooltip("Độ nhạy lúc bắt đầu đi/dừng (mét/giây mỗi giây). Số càng lớn nhân vật càng chạy/dừng gắt, số nhỏ thì trượt mượt.")]
    [SerializeField, Min(0.1f)] private float acceleration = 40f;

    [Tooltip("Tốc độ xoay thân người về hướng đang đi, tính bằng độ/giây. Số lớn = quay nhanh.")]
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Tooltip("Tốc độ xoay thân người về hướng camera khi đang bắn kiếm, tính bằng độ/giây. Số lớn để kiếm bay đúng hướng nhìn gần như ngay.")]
    [SerializeField, Min(0f)] private float aimTurnSpeed = 1080f;

    [Header("Nhảy và rơi")]
    [Tooltip("Độ cao một cú nhảy, tính bằng mét.")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;

    [Tooltip("Sức nặng trọng lực (mét/giây²). Số lớn = rơi nhanh, nhảy ngắn gọn hơn. Mặc định 25 cho cảm giác nhảy chắc tay.")]
    [SerializeField, Min(0.1f)] private float gravity = 25f;

    [Tooltip("Tốc độ rơi tối đa, tính bằng mét/giây.")]
    [SerializeField, Min(1f)] private float maxFallSpeed = 40f;

    [Tooltip("Thời gian ân hạn (giây): vừa bước khỏi mép vẫn còn nhảy được. Giúp nhảy qua khe dễ hơn.")]
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;

    [Tooltip("Thời gian nhớ phím Space (giây): bấm Space hơi sớm trước khi chạm đất vẫn tính là nhảy.")]
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

    [Tooltip("Khả năng đổi hướng khi đang ở trên không (0 = không đổi được, 1 = như dưới đất).")]
    [SerializeField, Range(0f, 1f)] private float airControl = 0.5f;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float lastGroundedTime = -100f;
    private float lastJumpPressedTime = -100f;

    /// <summary>Nhân vật đang chạm đất không.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Nhân vật đang giữ Shift để chạy không.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Bật khi đang bắn kiếm: thân người luôn quay mặt theo hướng camera (vẫn đi sang ngang/lùi được).
    /// Do SwordSwarm điều khiển.
    /// </summary>
    public bool FaceCamera { get; set; }

    /// <summary>Tốc độ di chuyển ngang hiện tại (mét/giây). Dùng cho animation sau này.</summary>
    public float HorizontalSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Vector2 input = ReadMoveInput();
        Keyboard kb = Keyboard.current;
        IsRunning = kb != null && kb.leftShiftKey.isPressed;

        // Hướng di chuyển tính theo hướng nhìn ngang của camera
        float yaw = cameraOrbit != null ? cameraOrbit.Yaw : transform.eulerAngles.y;
        Vector3 moveDir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        UpdateHorizontal(moveDir);
        UpdateVertical(kb);

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
        IsGrounded = controller.isGrounded;
    }

    /// <summary>Đọc phím W/A/S/D (và phím mũi tên) thành vector 2 chiều.</summary>
    private static Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = 0f, y = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    /// <summary>Tăng/giảm tốc độ đi ngang và xoay thân người về hướng đi.</summary>
    private void UpdateHorizontal(Vector3 moveDir)
    {
        float targetSpeed = IsRunning ? runSpeed : walkSpeed;
        Vector3 targetVelocity = moveDir * targetSpeed;

        float accel = IsGrounded ? acceleration : acceleration * airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * Time.deltaTime);

        if (FaceCamera && cameraOrbit != null)
        {
            // Đang bắn: quay mặt theo hướng camera nhìn
            Quaternion aimRotation = Quaternion.Euler(0f, cameraOrbit.Yaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, aimRotation, aimTurnSpeed * Time.deltaTime);
        }
        else if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    /// <summary>Xử lý trọng lực và cú nhảy (có "thời gian ân hạn" và "nhớ phím").</summary>
    private void UpdateVertical(Keyboard kb)
    {
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) verticalVelocity = -2f; // ép nhẹ xuống đất để bám dốc/bậc thang
        }

        if (kb != null && kb.spaceKey.wasPressedThisFrame)
            lastJumpPressedTime = Time.time;

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (canJump && wantsJump)
        {
            verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            lastGroundedTime = -100f;
            lastJumpPressedTime = -100f;
            IsGrounded = false;
        }

        verticalVelocity -= gravity * Time.deltaTime;
        if (verticalVelocity < -maxFallSpeed) verticalVelocity = -maxFallSpeed;
    }
}
