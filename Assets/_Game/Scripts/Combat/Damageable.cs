using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thông tin một lần bị đánh trúng.</summary>
public struct DamageInfo
{
    /// <summary>Lượng sát thương.</summary>
    public float amount;
    /// <summary>Điểm va chạm trong thế giới.</summary>
    public Vector3 point;
    /// <summary>Hướng đòn đánh bay tới (vector đơn vị).</summary>
    public Vector3 direction;
    /// <summary>Đòn quyền cước (kẻ địch bị loạng choạng, ngắt đòn đang ra).</summary>
    public bool melee;
}

/// <summary>
/// Gắn vào bất cứ vật thể nào có thể bị đánh (hình nộm, quái, boss...).
/// Quản lý máu, nhận sát thương và báo sự kiện để các script khác (hiệu ứng, AI) phản ứng.
/// Tự đăng ký vào danh sách <see cref="All"/> để phi kiếm tìm mục tiêu nhanh.
/// </summary>
public class Damageable : MonoBehaviour
{
    [Header("Máu")]
    [Tooltip("Lượng máu tối đa.")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Ngắm bắn")]
    [Tooltip("Điểm mà phi kiếm sẽ nhắm vào. Để trống thì lấy tâm khối va chạm của vật thể.")]
    [SerializeField] private Transform aimPointOverride;

    /// <summary>Danh sách mọi vật thể đang có thể bị đánh trong scene.</summary>
    public static readonly List<Damageable> All = new List<Damageable>();

    private static readonly Dictionary<Collider, Damageable> ByCollider = new Dictionary<Collider, Damageable>();

    private Collider[] colliders;

    /// <summary>Hệ số nhân sát thương nhận vào (chênh lệch cảnh giới). Kẻ địch tự cập nhật mỗi khung hình.</summary>
    public float DamageTakenMultiplier { get; set; } = 1f;

    /// <summary>Máu hiện tại.</summary>
    public float Health { get; private set; }

    /// <summary>Máu tối đa.</summary>
    public float MaxHealth => maxHealth;

    /// <summary>Còn sống (máu lớn hơn 0).</summary>
    public bool IsAlive => Health > 0f;

    /// <summary>Báo mỗi khi bị đánh trúng (kể cả đòn kết liễu).</summary>
    public event Action<DamageInfo> Damaged;

    /// <summary>Báo khi máu về 0.</summary>
    public event Action<Damageable> Died;

    /// <summary>Báo khi được hồi sinh.</summary>
    public event Action<Damageable> Revived;

    /// <summary>Báo khi bị đẩy lùi (tham số là độ dời trong thế giới).</summary>
    public event Action<Vector3> KnockedBack;

    /// <summary>Báo khi BẤT KỲ vật thể nào bị đánh trúng (HUD dùng để hiện số sát thương bay lên).</summary>
    public static event Action<Damageable, DamageInfo> AnyDamaged;

    private void Awake()
    {
        Health = maxHealth;
        colliders = GetComponentsInChildren<Collider>();
    }

    private void OnEnable()
    {
        All.Add(this);
        if (colliders == null) colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++) ByCollider[colliders[i]] = this;
    }

    private void OnDisable()
    {
        All.Remove(this);
        if (colliders == null) return;
        for (int i = 0; i < colliders.Length; i++) ByCollider.Remove(colliders[i]);
    }

    /// <summary>Tìm Damageable theo collider vừa chạm. Nhanh hơn GetComponentInParent.</summary>
    public static bool TryGet(Collider collider, out Damageable damageable)
    {
        return ByCollider.TryGetValue(collider, out damageable);
    }

    /// <summary>Điểm để phi kiếm nhắm tới (tâm vật thể).</summary>
    public Vector3 AimPoint
    {
        get
        {
            if (aimPointOverride != null) return aimPointOverride.position;
            if (colliders != null && colliders.Length > 0 && colliders[0] != null) return colliders[0].bounds.center;
            return transform.position;
        }
    }

    /// <summary>Nhận một đòn đánh. Không làm gì nếu đã chết.</summary>
    public void TakeDamage(DamageInfo info)
    {
        if (!IsAlive) return;
        info.amount *= DamageTakenMultiplier;
        bool infiniteEnemyHealth = AdminPanel.MauDichVoHan && GetComponentInParent<EnemyBase>() != null;
        if (!infiniteEnemyHealth)
        {
            if (AdminPanel.MotDonHaGuc) info.amount = Mathf.Max(info.amount, Health);
            Health = Mathf.Max(0f, Health - info.amount);
        }
        Damaged?.Invoke(info);
        AnyDamaged?.Invoke(this, info);
        if (Health <= 0f) Died?.Invoke(this);
    }

    /// <summary>Đẩy lùi vật thể một đoạn delta (mét) theo thế giới.</summary>
    public void Knockback(Vector3 delta)
    {
        if (delta.sqrMagnitude > 0.0001f) KnockedBack?.Invoke(delta);
    }

    /// <summary>Hồi đầy máu và báo sự kiện hồi sinh.</summary>
    public void Revive()
    {
        Health = maxHealth;
        Revived?.Invoke(this);
    }
}
