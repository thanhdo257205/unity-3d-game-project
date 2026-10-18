using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Reference combat: jade ward, stored counterstroke and a charged golden lotus relic.</summary>
public class ThanThongCombat : MonoBehaviour
{
    public static ThanThongCombat Instance { get; private set; }
    public GameObject lotusPrefab;
    public Material auraMaterial;
    public TMP_FontAsset hudFont;
    [SerializeField] private float wardCost = 8f;
    [SerializeField] private float wardDrain = 5f;
    [SerializeField] private float lotusCost = 35f;
    [SerializeField] private float lotusCooldown = 8f;
    private PlayerStats stats;
    private PlayerController controller;
    private PlayerAnimator poses;
    private GameObject ward, charging, hudRoot;
    private TMP_Text hud;
    private float wardStarted, wardReady, lotusReady, releaseAt;
    private int absorbed;
    public bool IsGuarding => ward != null;
    public bool IsCharging => charging != null;
    public int Absorbed => absorbed;
    public float LotusRemaining => Mathf.Max(0, lotusReady - Time.time);

#if UNITY_EDITOR
    public static event System.Action PreviewFrame;
    private void LateUpdate() { PreviewFrame?.Invoke(); }
#endif

    private void Awake()
    {
        Instance = this; stats = GetComponent<PlayerStats>(); controller = GetComponent<PlayerController>();
        poses = GetComponent<PlayerAnimator>();
        hudRoot = new GameObject("HUD_ThanThong", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = hudRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
        var scaler = hudRoot.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var label = new GameObject("KyNang", typeof(RectTransform)); label.transform.SetParent(hudRoot.transform, false);
        hud = label.AddComponent<TextMeshProUGUI>(); if (hudFont != null) hud.font = hudFont;
        hud.fontSize = 22; hud.alignment = TextAlignmentOptions.BottomRight; hud.raycastTarget = false;
        hud.color = new Color(0.82f, 0.94f, 0.88f, 0.9f);
        var rect = label.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0); rect.anchoredPosition = new Vector2(-24, 145); rect.sizeDelta = new Vector2(440, 76);
    }

    private bool CanCast => stats != null && stats.IsAlive && !controller.ControlLocked && !AdminPanel.DangMo;
    private void Update()
    {
        bool allowed = CanCast && Cursor.lockState == CursorLockMode.Locked;
        var keyboard = Keyboard.current;
        if (allowed && keyboard != null)
        {
            if (keyboard.gKey.wasPressedThisFrame) BeginWard();
            if (keyboard.gKey.wasReleasedThisFrame) EndWard(true);
            if (keyboard.tKey.wasPressedThisFrame) CastLotus();
        }
        if (!CanCast)
        {
            EndWard(false); CancelCharge();
        }
        if (IsGuarding)
        {
            if (Time.time - wardStarted >= 3f || !stats.DrainMana(wardDrain * Time.deltaTime)) EndWard(true);
            else
            {
                ward.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01((Time.time - wardStarted) / 0.22f));
                ward.transform.Rotate(Vector3.up, 24f * Time.deltaTime);
            }
        }
        if (IsCharging)
        {
            charging.transform.position = CastOrigin;
            charging.transform.Rotate(Vector3.up, 160f * Time.deltaTime, Space.World);
            charging.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.3f, 1f - Mathf.Clamp01((releaseAt - Time.time) / 0.8f));
            var chargeFx = charging.GetComponent<TienHiepVfx>();
            if (chargeFx != null) chargeFx.SetCharge(1f - Mathf.Clamp01((releaseAt - Time.time) / 0.8f));
            if (Time.time >= releaseAt)
            {
                var relic = charging; charging = null;
                Launch(relic, 45f, 5f);
            }
        }
        if ((IsGuarding || IsCharging) && poses != null) poses.HoldSkillPose();
        if (hud != null)
        {
            hud.gameObject.SetActive(stats != null && stats.IsAlive && !AdminPanel.DangMo);
            string guard = IsGuarding ? "Hộ thể • tích lực " + absorbed + "/4" : "Giữ G: Hộ thể / thả: Phản kích";
            string lotus = IsCharging ? "Kim Liên đang tụ lực…" : LotusRemaining > 0 && !AdminPanel.KhongHoiChieu
                ? "T: Kim Liên • " + LotusRemaining.ToString("0.0") + "s" : "T: Kim Liên • 35 pháp lực";
            hud.text = guard + "\n" + lotus;
        }
    }

    private Vector3 Forward => Camera.main != null ? Camera.main.transform.forward : transform.forward;
    private Vector3 CastOrigin
    {
        get
        {
            Vector3 center = transform.position + Vector3.up * 1.25f;
            Vector3 muzzle = center + Forward * 1.35f;
            // Never create the relic on the far side of a nearby wall.
            return SwordFlightMath.HitsEnvironment(center, muzzle, 0.45f, transform) ? center : muzzle;
        }
    }
    public bool BeginWard()
    {
        if (!CanCast || IsGuarding || IsCharging || Time.time < wardReady || auraMaterial == null) return false;
        if (!stats.TrySpendMana(wardCost)) return false;
        wardStarted = Time.time; absorbed = 0;
        ward = LinhPhapVisual.Ward(transform, auraMaterial);
        return true;
    }

    public void EndWard(bool counter)
    {
        if (!IsGuarding) return;
        var wardFx = ward.GetComponent<LinhPhapVisual>();
        if (wardFx != null) wardFx.Dissolve(); else Destroy(ward);
        ward = null; wardReady = Time.time + 0.75f;
        if (counter && absorbed > 0 && CanCast && lotusPrefab != null)
        {
            var relic = Instantiate(lotusPrefab, CastOrigin, Quaternion.identity);
            relic.transform.localScale = Vector3.one * 0.65f;
            Launch(relic, 14f + absorbed * 7f, 2.5f);
        }
    }

    public bool CastLotus()
    {
        if (!CanCast || IsCharging || lotusPrefab == null || (Time.time < lotusReady && !AdminPanel.KhongHoiChieu)) return false;
        if (!stats.TrySpendMana(lotusCost)) return false;
        EndWard(false); lotusReady = Time.time + lotusCooldown; releaseAt = Time.time + 0.8f;
        charging = Instantiate(lotusPrefab, CastOrigin, Quaternion.identity);
        charging.transform.localScale = Vector3.one * 0.25f;
        return true;
    }

    private void Launch(GameObject relic, float damage, float radius)
    {
        Damageable target = SwordSwarm.Instance != null ? SwordSwarm.Instance.LockedTarget : null;
        Vector3 aim = target != null && target.IsAlive ? target.AimPoint : CastOrigin + Forward * 45f;
        relic.AddComponent<LinhPhapProjectile>().Launch(transform, target, aim, damage, radius, auraMaterial);
        LinhPhapVisual.Pulse(CastOrigin, auraMaterial, new Color(1f, 0.65f, 0.12f, 0.4f), 1.4f, 0.3f);
        var feedback = GetComponent<HitFeedback>(); if (feedback != null) feedback.Shake(0.025f);
    }

    public static bool TryBlockProjectile(IEnemyProjectile projectile, Vector3 from, Vector3 to)
    {
        if (NamCungCombat.TryBlock(projectile, from, to)) return true;
        var skill = Instance;
        if (skill == null || !skill.IsGuarding || !skill.CanCast || projectile.IsDone) return false;
        Vector3 center = skill.transform.position;
        Vector3 contact;
        float distance = SwordFlightMath.SegmentDistanceSquared(from, to, center + Vector3.up * 0.4f,
            center + Vector3.up * 2.7f, out contact);
        float radius = 1.55f + projectile.Radius;
        if (distance > radius * radius) return false;
        if (!skill.stats.TrySpendMana(1f)) { skill.EndWard(false); return false; }
        var wardFx = skill.ward.GetComponent<LinhPhapVisual>(); if (wardFx != null) wardFx.Hit(contact);
        projectile.Block(); skill.absorbed++;
        LinhPhapVisual.Pulse(contact, skill.auraMaterial, new Color(0.1f, 0.95f, 0.65f, 0.45f), 0.8f, 0.25f);
        if (skill.absorbed >= 4) skill.EndWard(true);
        return true;
    }

    private void CancelCharge() { if (charging != null) Destroy(charging); charging = null; }
    private void OnDisable() { EndWard(false); CancelCharge(); if (hudRoot != null) hudRoot.SetActive(false); }
    private void OnEnable() { if (hudRoot != null) hudRoot.SetActive(true); }
    private void OnDestroy() { if (Instance == this) Instance = null; if (hudRoot != null) Destroy(hudRoot); }
}
