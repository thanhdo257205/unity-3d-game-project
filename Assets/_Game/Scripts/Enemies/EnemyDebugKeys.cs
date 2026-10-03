using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Phím thử nghiệm cho kẻ địch (chỉ hoạt động trong Editor):
/// F6 hồi sinh toàn bộ kẻ địch về chỗ ban đầu, đầy máu. F7 bật/tắt AI (kẻ địch đứng yên) để chụp ảnh.
/// </summary>
public class EnemyDebugKeys : MonoBehaviour
{
#if UNITY_EDITOR
    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f6Key.wasPressedThisFrame) EnemyBase.ReviveAll();
        if (kb.f7Key.wasPressedThisFrame)
        {
            EnemyBase.AIEnabled = !EnemyBase.AIEnabled;
            Debug.Log("AI kẻ địch: " + (EnemyBase.AIEnabled ? "BẬT" : "TẮT (đứng yên)"));
        }
    }
#endif
}
