using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Seven reference spells for the player. Cast timing drives both Blender poses and VFX release.</summary>
[DefaultExecutionOrder(80)]
public class NamCungCombat : MonoBehaviour
{
    public ReferenceSkillData[] skills;
    public Material energyMaterial, surfaceMaterial;
    public Material jadeMaterial, bloodMaterial, goldMaterial;
    public GameObject relicModels;
    public TMP_FontAsset font;
    public static NamCungCombat Instance { get; private set; }

    private PlayerStats stats;
    private PlayerController player;
    private PlayerAnimator poses;
    private PlayerMelee melee;
    private Animator animator;
    private CameraOrbit orbit;
    private int layer = -1, casting = -1;
    private bool released, ownsLock;
    private float started, layerWeight;
    private float[] ready;
    private ReferenceSkillFx gathering;
    private readonly List<ReferenceSkillFx> effects = new List<ReferenceSkillFx>();
    private Canvas hudCanvas;
    private TMP_Text label;
    private readonly StringBuilder hud = new StringBuilder(512);
    private static readonly int MotionTimeId = Animator.StringToHash("NCKTime");

    public bool IsCasting => casting >= 0;
    public ReferenceSkillData CurrentSkill => IsCasting && skills != null ? skills[casting] : null;
    public float Remaining(int index) => ready != null && index >= 0 && index < ready.Length ? Mathf.Max(0, ready[index] - Time.time) : 0;

    private void Awake()
    {
        Instance = this;
        stats = GetComponent<PlayerStats>(); player = GetComponent<PlayerController>();
        poses = GetComponent<PlayerAnimator>(); melee = GetComponent<PlayerMelee>();
        animator = GetComponentInChildren<Animator>(); orbit = FindFirstObjectByType<CameraOrbit>();
        layer = animator != null ? animator.GetLayerIndex("NCK_Reference") : -1;
        ready = new float[skills != null ? skills.Length : 0];
        BuildHud();
    }

    private void OnEnable() { if (stats != null) stats.Damaged += Interrupt; }
    private void OnDisable()
    {
        if (stats != null) stats.Damaged -= Interrupt;
        CancelCast();
        foreach (var effect in effects) if (effect != null) Destroy(effect.gameObject);
        effects.Clear();
        if (hudCanvas != null) hudCanvas.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (hudCanvas != null) Destroy(hudCanvas.gameObject);
    }

    private void Interrupt(float damage) { if (IsCasting) CancelCast(); }

    private bool CanBegin => isActiveAndEnabled && stats != null && stats.IsAlive && player != null
        && !player.ControlLocked && !AdminPanel.DangMo && !IsCasting && layer >= 0
        && (melee == null || !melee.IsAttacking)
        && (ThanThongCombat.Instance == null || (!ThanThongCombat.Instance.IsCharging && !ThanThongCombat.Instance.IsGuarding));

    public bool TryCast(int index)
    {
        if (!CanBegin || skills == null || index < 0 || index >= skills.Length || skills[index] == null) return false;
        var skill = skills[index];
        if (Remaining(index) > 0 && !AdminPanel.KhongHoiChieu) return false;
        if (!stats.TrySpendMana(skill.manaCost)) return false;
        ready[index] = Time.time + skill.cooldown;
        casting = index; started = Time.time; released = false;
        ownsLock = true; player.ControlLocked = true;
        animator.SetFloat(MotionTimeId, 0);
        animator.CrossFadeInFixedTime(skill.kind.ToString(), .12f, layer, 0);
        gathering = CreateEffect(skill, true);
        return true;
    }

    private void Update()
    {
        if (stats == null || !stats.IsAlive)
        {
            CancelCast();
            foreach (var effect in effects) if (effect != null) Destroy(effect.gameObject);
            effects.Clear();
        }
        if (IsCasting)
        {
            var skill = skills[casting];
            float elapsed = Time.time - started;
            if (orbit != null) player.RotateToYaw(orbit.Yaw, 720);
            if (poses != null) poses.HoldSkillPose();
            float motionTime = elapsed < skill.chargeTime ? .56f * elapsed / skill.chargeTime
                : .56f + .44f * Mathf.Clamp01((elapsed - skill.chargeTime) / skill.recoveryTime);
            animator.SetFloat(MotionTimeId, motionTime);
            if (!released && elapsed >= skill.chargeTime)
            {
                released = true;
                if (gathering != null) Destroy(gathering.gameObject);
                gathering = null;
                CreateEffect(skill, false);
                var feedback = GetComponent<HitFeedback>();
                if (feedback != null) feedback.Shake(skill.kind == ReferenceSkillKind.ThunderSword ? .045f : .018f);
            }
            if (elapsed >= skill.chargeTime + skill.recoveryTime) CancelCast();
        }
        else if (Cursor.lockState == CursorLockMode.Locked && Keyboard.current != null && skills != null)
        {
            for (int i = 0; i < skills.Length; i++)
                if (skills[i] != null && skills[i].key != Key.None && Keyboard.current[skills[i].key].wasPressedThisFrame)
                { TryCast(i); break; }
        }
        layerWeight = Mathf.MoveTowards(layerWeight, IsCasting ? 1f : 0f, Time.deltaTime * 9f);
        if (animator != null && layer >= 0) animator.SetLayerWeight(layer, layerWeight);
        UpdateHud();
        effects.RemoveAll(e => e == null);
    }

    public void CancelCast()
    {
        if (gathering != null) Destroy(gathering.gameObject);
        gathering = null;
        if (ownsLock && player != null && stats != null && stats.IsAlive) player.ControlLocked = false;
        ownsLock = false;
        casting = -1;
    }

    private ReferenceSkillFx CreateEffect(ReferenceSkillData skill, bool charging)
    {
        effects.RemoveAll(e => e == null);
        // Oldest visuals expire before allowing unbounded overlapping particle/line counts.
        if (effects.Count >= 4) { Destroy(effects[0].gameObject); effects.RemoveAt(0); }
        var go = new GameObject("NCK_" + skill.kind + (charging ? "_TuLuc" : "_XuatChieu"));
        var effect = go.AddComponent<ReferenceSkillFx>();
        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
        Damageable locked = SwordSwarm.Instance != null ? SwordSwarm.Instance.LockedTarget : null;
        Vector3 origin = transform.position + Vector3.up * 1.3f;
        Vector3 destination = locked != null && locked.IsAlive && Vector3.Distance(origin, locked.AimPoint) < skill.range
            ? locked.AimPoint : origin + forward * skill.range;
        effect.Initialize(this, skill, origin, destination, charging);
        effects.Add(effect);
        return effect;
    }

    public static bool TryBlock(IEnemyProjectile projectile, Vector3 from, Vector3 to)
    {
        if (Instance == null || Instance.stats == null || !Instance.stats.IsAlive || projectile.IsDone) return false;
        foreach (var effect in Instance.effects)
            if (effect != null && effect.BlockProjectile(projectile, from, to)) return true;
        return false;
    }

    private void BuildHud()
    {
        var go = new GameObject("HUD_ChieuNamCung", typeof(Canvas), typeof(CanvasScaler));
        hudCanvas = go.GetComponent<Canvas>(); hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay; hudCanvas.sortingOrder = 6;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var text = new GameObject("PhimChieu", typeof(RectTransform)); text.transform.SetParent(go.transform, false);
        label = text.AddComponent<TextMeshProUGUI>(); if (font != null) label.font = font;
        label.fontSize = 18; label.color = new Color(.83f, .9f, .94f, .92f);
        label.alignment = TextAlignmentOptions.BottomRight; label.raycastTarget = false;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(1, 0);
        label.rectTransform.anchoredPosition = new Vector2(-24, 25); label.rectTransform.sizeDelta = new Vector2(450, 210);
    }

    private void UpdateHud()
    {
        if (label == null || skills == null) return;
        hudCanvas.gameObject.SetActive(!AdminPanel.DangMo && stats != null && stats.IsAlive && isActiveAndEnabled);
        hud.Clear();
        for (int i = 0; i < skills.Length; i++)
        {
            var skill = skills[i]; if (skill == null) continue;
            hud.Append(skill.key.ToString()).Append("  ").Append(skill.displayName);
            if (casting == i) hud.Append(released ? " · thu thế" : " · tụ lực");
            else if (Remaining(i) > .05f) hud.Append(" · ").Append(Remaining(i).ToString("0.0")).Append('s');
            else hud.Append(" · ").Append(skill.manaCost.ToString("0"));
            hud.AppendLine();
        }
        label.SetText(hud);
    }
}
