using System;
using UnityEngine;

/// <summary>
/// Hồi Kiếm - Vòng: các kiếm xếp thành vòng tròn nằm ngang quanh người, quay liên tục, mũi hướng theo chiều quay.
/// Kiếm trong vòng chém mọi thứ áp sát. Lớp ngoài quay ngược chiều lớp trong.
/// </summary>
[Serializable]
public class RingFormation : IFormation
{
    [Tooltip("Bán kính vòng: khoảng cách từ nhân vật tới chuôi kiếm (mét).")]
    [Min(0.3f)] public float radius = 1.0f;

    [Tooltip("Tốc độ quay của vòng (độ/giây).")]
    public float spinSpeed = 180f;

    [Tooltip("Số kiếm tối đa trong một vòng. Nhiều hơn thì thêm vòng ngoài quay ngược chiều.")]
    [Min(1)] public int layerCapacity = 10;

    [Tooltip("Khoảng cách giữa các vòng (mét).")]
    [Min(0f)] public float layerSpacing = 0.3f;

    [Tooltip("Độ cao vòng so với điểm neo ngang ngực (mét). Số âm là thấp xuống, ví dụ -0.2 là ngang bụng.")]
    public float heightOffset = -0.3f;

    public string DisplayName => "Vòng";

    public bool ContactDamage => true;

    public SwordSlot GetSlot(int index, int count, in FormationContext ctx)
    {
        int layer = index / layerCapacity;
        int indexInLayer = index % layerCapacity;
        int layerCount = Mathf.Min(layerCapacity, count - layer * layerCapacity);

        float dirSign = layer % 2 == 0 ? 1f : -1f;
        float stagger = layer % 2 == 0 ? 0f : 180f / Mathf.Max(layerCount, 1);
        float angle = ctx.time * spinSpeed * dirSign + 360f * indexInLayer / Mathf.Max(layerCount, 1) + stagger;

        Quaternion spin = Quaternion.Euler(0f, angle, 0f);
        Vector3 radial = spin * Vector3.forward;
        Vector3 tangent = spin * Vector3.right * dirSign;   // hướng đang bay quanh vòng

        SwordSlot slot;
        slot.position = ctx.origin + Vector3.up * heightOffset + radial * (radius + layer * layerSpacing);
        slot.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        return slot;
    }
}
