using UnityEngine;
using UnityEngine.InputSystem;

public enum ReferenceSkillKind { FireIce, Mirrors, Reincarnation, BloodSword, ThunderSword, GoldenNet, MindPearl }

[CreateAssetMenu(menuName = "Phàm Kiếm Lục/Chiêu theo video")]
public class ReferenceSkillData : ScriptableObject
{
    public ReferenceSkillKind kind;
    public string displayName;
    public Key key;
    public AnimationClip motion;
    [Min(0)] public float manaCost = 25;
    [Min(0)] public float cooldown = 8;
    [Min(.1f)] public float chargeTime = .9f;
    [Min(.1f)] public float recoveryTime = .45f;
    [Min(.1f)] public float duration = 2.4f;
    [Min(1)] public float range = 24;
    [Min(.1f)] public float radius = 3;
    [Min(0)] public float damage = 35;
    [Min(0)] public float stunTime;
    public Color primary = Color.cyan;
    public Color secondary = Color.magenta;
    public string referenceTime;
}
