#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Công cụ kiểm thử trong Editor: quay các khung hình cách đều nhau (theo thời gian trò chơi) bằng camera tạm,
/// để xem từng nhịp của một đòn đánh. Đặt Time.captureDeltaTime để mỗi khung hình đúng bằng một bước thời gian cố định.
/// Dùng qua MCP: DebugRecorder.Begin(...), đợi, rồi DebugRecorder.Finish().
/// </summary>
[DefaultExecutionOrder(500)]
public class DebugRecorder : MonoBehaviour
{
    private static DebugRecorder instance;

    private Transform target;
    private Vector3 offset;
    private float lookHeight;
    private float fov;
    private int every;
    private int remaining;
    private int frame;
    private string prefix;
    private readonly List<string> paths = new List<string>();
    private readonly List<string> labels = new List<string>();

    /// <summary>Số ảnh đã quay.</summary>
    public static int Count => instance != null ? instance.paths.Count : 0;

    /// <summary>Bắt đầu quay: cứ every khung hình chụp một ảnh, tổng cộng count ảnh. Camera đặt tại offset (phải/cao/trước) so với target.</summary>
    public static void Begin(Transform target, Vector3 offset, float lookHeight, int every, int count, string prefix, float fov = 40f)
    {
        if (instance == null) instance = new GameObject("DebugRecorder").AddComponent<DebugRecorder>();
        instance.target = target; instance.offset = offset; instance.lookHeight = lookHeight;
        instance.every = Mathf.Max(1, every); instance.remaining = count; instance.frame = 0; instance.prefix = prefix; instance.fov = fov;
        instance.paths.Clear();
        instance.labels.Clear();
        Time.captureDeltaTime = 1f / 60f;
    }

    /// <summary>Kết thúc: trả lại thời gian bình thường, ghép các ảnh thành một tấm lưới và trả về đường dẫn.</summary>
    public static string Finish(int cols, string sheetName)
    {
        Time.captureDeltaTime = 0f;
        if (instance == null || instance.paths.Count == 0) return "(chưa có ảnh)";
        return DebugShots.Combine(instance.paths.ToArray(), cols, sheetName);
    }

    /// <summary>Hàm gọi lấy chữ ghi chú cho mỗi ảnh (ví dụ thời gian trong clip). Có thể để trống.</summary>
    public static System.Func<string> Label;

    private void LateUpdate()
    {
        if (remaining <= 0 || target == null) return;
        frame++;
        if (frame % every != 0) return;
        string name = prefix + "_" + paths.Count.ToString("00");
        paths.Add(DebugShots.Shot(name, target, offset, lookHeight, 450, fov));
        labels.Add(Label != null ? Label() : "");
        remaining--;
        if (remaining == 0) Time.captureDeltaTime = 0f;
    }

    /// <summary>Các ghi chú đã lưu, mỗi ảnh một dòng.</summary>
    public static string Labels => instance != null ? string.Join("\n", instance.labels.ToArray()) : "";
}
#endif
