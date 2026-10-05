using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Điều khiển thử trong scene TestPhaKhong (gắn vào MainCamera): phím O mở cổng A, C đóng cổng A, B mở/đóng cổng B.
/// Giữ chuột phải + di chuột để xoay camera, W A S D di chuyển, Q xuống, E lên, giữ Shift để đi nhanh.
/// Chỉ để xem hiệu ứng, không dính tới nhân vật chơi.
/// </summary>
public class CongKhongThuNghiem : MonoBehaviour
{
    [Tooltip("Cổng A (phím O mở, C đóng). Tự tìm theo tên CongKhongGian_A nếu để trống.")]
    [SerializeField] private CongKhongGian congA;

    [Tooltip("Cổng B (phím B mở/đóng). Tự tìm theo tên CongKhongGian_B nếu để trống.")]
    [SerializeField] private CongKhongGian congB;

    [Tooltip("Tốc độ bay camera (mét/giây).")]
    [SerializeField, Min(0.5f)] private float tocDo = 5f;

    [Tooltip("Độ nhạy xoay camera bằng chuột phải.")]
    [SerializeField, Min(0.01f)] private float doNhay = 0.15f;

    private float yaw, pitch;

    private void Start()
    {
        if (congA == null) { GameObject g = GameObject.Find("CongKhongGian_A"); if (g != null) congA = g.GetComponent<CongKhongGian>(); }
        if (congB == null) { GameObject g = GameObject.Find("CongKhongGian_B"); if (g != null) congB = g.GetComponent<CongKhongGian>(); }
        Vector3 e = transform.eulerAngles;
        yaw = e.y; pitch = e.x > 180f ? e.x - 360f : e.x;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse ms = Mouse.current;
        if (kb == null) return;
        if (congA != null)
        {
            if (kb.oKey.wasPressedThisFrame) congA.Mo();
            if (kb.cKey.wasPressedThisFrame) congA.Dong();
        }
        if (congB != null && kb.bKey.wasPressedThisFrame) { if (congB.DoMo > 0.5f) congB.Dong(); else congB.Mo(); }

        if (ms != null && ms.rightButton.isPressed)
        {
            Vector2 d = ms.delta.ReadValue();
            yaw += d.x * doNhay;
            pitch = Mathf.Clamp(pitch - d.y * doNhay, -85f, 85f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.dKey.isPressed) move += transform.right;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move -= Vector3.up;
        float speed = tocDo * (kb.leftShiftKey.isPressed ? 3f : 1f);
        transform.position += move * (speed * Time.deltaTime);
    }
}
