using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Vòng đời người chơi: khi hết máu thì khóa điều khiển, 2 giây sau hiện màn "Đạo tiêu thân vẫn";
/// bấm "Thử lại" thì hồi sinh tại chỗ xuất phát. HUD lắng nghe các sự kiện ở đây để hiện/ẩn màn hình chết.
/// </summary>
public class PlayerLife : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Tài nguyên người chơi (đối tượng NhanVat).")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Script điều khiển nhân vật.")]
    [SerializeField] private PlayerController player;

    [Tooltip("Điểm ngắm camera: tắt xoay chuột khi hiện màn chết. Có thể để trống.")]
    [SerializeField] private CameraOrbit cameraOrbit;

    [Header("Hồi sinh")]
    [Tooltip("Sau khi chết bao nhiêu giây thì hiện màn hình 'Đạo tiêu thân vẫn' và nút Thử lại.")]
    [SerializeField, Min(0f)] private float deathScreenDelay = 2f;

    /// <summary>Báo khi cần hiện màn hình chết.</summary>
    public event Action DeathScreenShown;

    /// <summary>Báo khi đã hồi sinh xong (ẩn màn hình chết).</summary>
    public event Action Respawned;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Coroutine deathRoutine;

    private void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (player == null) player = GetComponent<PlayerController>();
        // Chỗ xuất phát = vị trí lúc bắt đầu chơi
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void OnEnable() { if (stats != null) stats.Died += OnDied; }

    private void OnDisable()
    {
        if (stats != null) stats.Died -= OnDied;
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
    }

    private void OnDied()
    {
        if (player != null)
        {
            player.ControlLocked = true;
            player.MoveLocked = false;
            player.FaceCameraForAttack = false;
            player.FaceCamera = false;
        }
        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(deathScreenDelay);
        if (cameraOrbit != null) cameraOrbit.InputEnabled = false;
        CameraOrbit.SetCursorLocked(false);
        DeathScreenShown?.Invoke();
        deathRoutine = null;
    }

    /// <summary>Hồi sinh tại chỗ xuất phát (nút "Thử lại" gọi hàm này).</summary>
    public void Respawn()
    {
        if (stats == null || stats.IsAlive) return;
        EnemyOrb.DestroyAll();
        if (player != null)
        {
            player.TeleportTo(spawnPosition, spawnRotation);
            player.ControlLocked = false;
        }
        stats.Revive();
        if (cameraOrbit != null) cameraOrbit.InputEnabled = true;
        CameraOrbit.SetCursorLocked(true);
        Respawned?.Invoke();
    }
}
