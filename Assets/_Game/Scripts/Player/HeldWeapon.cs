using System;
using UnityEngine;

/// <summary>
/// Vũ khí cầm tay của nhân vật. Chọn ô có vũ khí trên thanh ô đồ thì kiếm xuất hiện ở túi trữ vật bên hông trái,
/// lóe sáng, bay một đường cong lên tay phải (có vệt sáng xanh) rồi gắn vào xương RightHand.
/// Đổi sang ô khác thì kiếm bay ngược về túi rồi biến mất.
/// Có hai tư thế cầm: lúc đứng/chạy ("Cầm thường") và lúc chém ("Cầm chém", một tay).
/// Mọi con số chỉnh trong Inspector.
/// </summary>
[DefaultExecutionOrder(40)]   // chạy sau Animator để bám đúng xương tay đã cập nhật
public class HeldWeapon : MonoBehaviour
{
    /// <summary>Vị trí và góc xoay của kiếm so với xương bàn tay phải.</summary>
    [Serializable]
    public struct GripPose
    {
        [Tooltip("Vị trí của tâm chuôi kiếm so với xương bàn tay phải (mét, theo trục của xương).")]
        public Vector3 position;

        [Tooltip("Góc xoay của kiếm so với xương bàn tay phải (độ). Mũi kiếm là trục Z của kiếm.")]
        public Vector3 euler;
    }

    /// <summary>Trạng thái vũ khí.</summary>
    public enum WeaponState { Sheathed, Drawing, Held, Sheathing }

    [Header("Tham chiếu")]
    [Tooltip("Thanh ô đồ. Để trống thì tự tìm trên cùng đối tượng.")]
    [SerializeField] private Hotbar hotbar;

    [Tooltip("Animator của mô hình nhân vật (để tìm xương bàn tay phải). Để trống thì tự tìm trong các đối tượng con.")]
    [SerializeField] private Animator animator;

    [Tooltip("Tài nguyên người chơi. Để trống thì tự tìm.")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Túi trữ vật bên hông trái: kiếm bay ra từ đây và bay về đây. Là đối tượng con của xương hông (NeoTuiTruVat).")]
    [SerializeField] private Transform pouchAnchor;

    [Header("Rút và cất kiếm")]
    [Tooltip("Thời gian kiếm bay từ túi lên tay phải (giây).")]
    [SerializeField, Min(0.05f)] private float drawTime = 0.35f;

    [Tooltip("Thời gian kiếm bay ngược về túi rồi biến mất (giây).")]
    [SerializeField, Min(0.05f)] private float sheatheTime = 0.25f;

    [Tooltip("Độ cong của đường bay: điểm uốn nhô lên cao bao nhiêu so với đường thẳng (mét).")]
    [SerializeField, Min(0f)] private float arcHeight = 0.4f;

    [Tooltip("Điểm uốn của đường bay lệch ra phía trước nhân vật bao nhiêu (mét).")]
    [SerializeField, Min(0f)] private float arcForward = 0.35f;

    [Tooltip("Kích thước kiếm khi vừa xuất hiện ở túi so với cỡ thật (kiếm to dần lên 1 trong lúc bay).")]
    [SerializeField, Range(0.1f, 1f)] private float pouchScale = 0.35f;

    [Header("Cách cầm kiếm")]
    [Tooltip("Cầm thường (đứng, đi, chạy): chỉnh sao cho mũi kiếm không đâm xuống đất hay vào tà áo.")]
    [SerializeField] private GripPose carryPose = new GripPose { position = new Vector3(0f, 0.08f, 0.03f), euler = new Vector3(0f, 0f, 0f) };

    [Tooltip("Cầm chém một tay (Sword and Shield Pack): chuôi nằm trong lòng bàn tay phải, lưỡi chĩa theo hướng chém.")]
    [SerializeField] private GripPose slashPose = new GripPose { position = new Vector3(0f, 0.08f, 0.03f), euler = new Vector3(0f, 180f, 90f) };

    [Tooltip("Thời gian chuyển giữa cầm thường và cầm chém (giây).")]
    [SerializeField, Min(0.01f)] private float poseBlendTime = 0.12f;

    [Header("Hiệu ứng")]
    [Tooltip("Hệ hạt lóe sáng ở túi trữ vật lúc rút kiếm và lúc kiếm tới tay. Có thể để trống.")]
    [SerializeField] private ParticleSystem flash;

    [Tooltip("Số hạt lóe mỗi lần.")]
    [SerializeField, Min(0)] private int flashCount = 14;

    [Tooltip("Đèn chớp sáng cùng lúc lóe (đặt ở túi trữ vật). Có thể để trống.")]
    [SerializeField] private Light flashLight;

    [Tooltip("Độ sáng đỉnh của đèn chớp.")]
    [SerializeField, Min(0f)] private float flashLightIntensity = 6f;

    [Tooltip("Thời gian đèn chớp tắt dần (giây).")]
    [SerializeField, Min(0.02f)] private float flashLightTime = 0.2f;

    private Transform hand;
    private ItemData currentItem;
    private Transform sword;
    private TrailRenderer flightTrail;
    private SlashTrail slashTrail;
    private WeaponState state = WeaponState.Sheathed;
    private float stateTime;
    private float stateDuration;
    private Vector3 flightStart;
    private Quaternion flightStartRot;
    private bool slashWanted;
    private bool overrideWanted;
    private GripPose overridePose;
    private Vector3 curPos;             // tư thế kiếm đang áp dụng (trượt dần về tư thế đích)
    private Quaternion curRot = Quaternion.identity;
    private float flashEnd = -1f;

    /// <summary>Trạng thái hiện tại của vũ khí.</summary>
    public WeaponState State => state;

    /// <summary>Kiếm đã nằm trên tay, sẵn sàng chém.</summary>
    public bool IsReady => state == WeaponState.Held;

    /// <summary>Ô đang chọn là một vũ khí (kể cả khi kiếm đang bay lên tay).</summary>
    public bool WantsWeapon => WantedItem() != null;

    /// <summary>Thanh kiếm đang dùng (null nếu chưa rút lần nào).</summary>
    public Transform Sword => sword;

    /// <summary>Báo khi kiếm vừa tới tay.</summary>
    public event Action Drawn;

    private void Awake()
    {
        if (hotbar == null) hotbar = GetComponent<Hotbar>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (flashLight != null) flashLight.enabled = false;
    }

    private void OnDisable()
    {
        if (sword != null) sword.gameObject.SetActive(false);
        state = WeaponState.Sheathed;
    }

    private ItemData WantedItem()
    {
        if (hotbar == null) return null;
        ItemData item = hotbar.SelectedItem;
        return item != null && item.type == ItemType.VuKhi && item.prefab != null ? item : null;
    }

    private void Update()
    {
        UpdateFlashLight();
        if (hand == null) return;

        ItemData wanted = WantedItem();
        bool alive = stats == null || stats.IsAlive;

        switch (state)
        {
            case WeaponState.Sheathed:
                if (wanted != null && alive) BeginDraw(wanted);
                break;
            case WeaponState.Drawing:
                if (wanted != currentItem) BeginSheathe();
                break;
            case WeaponState.Held:
                if (wanted != currentItem) BeginSheathe();
                break;
            case WeaponState.Sheathing:
                if (wanted != null && wanted == currentItem && alive) BeginDraw(wanted);
                break;
        }
    }

    private void LateUpdate()
    {
        if (hand == null || sword == null) return;
        float dt = Time.deltaTime;

        switch (state)
        {
            case WeaponState.Drawing: TickDraw(dt); break;
            case WeaponState.Sheathing: TickSheathe(dt); break;
            case WeaponState.Held: TickHeld(dt); break;
        }
    }

    // ================= Rút kiếm =================

    private void EnsureSword(ItemData item)
    {
        if (sword != null && currentItem == item) return;
        if (sword != null) Destroy(sword.gameObject);
        GameObject go = Instantiate(item.prefab);
        go.name = "KiemCamTay";
        sword = go.transform;
        flightTrail = go.GetComponent<TrailRenderer>();
        slashTrail = go.GetComponent<SlashTrail>();
        currentItem = item;
    }

    private void BeginDraw(ItemData item)
    {
        bool wasVisible = sword != null && sword.gameObject.activeSelf;
        EnsureSword(item);
        sword.gameObject.SetActive(true);
        if (!wasVisible || state == WeaponState.Sheathed)
        {
            // Xuất hiện ở túi trữ vật, mũi kiếm chĩa lên
            Vector3 p = pouchAnchor != null ? pouchAnchor.position : transform.position + Vector3.up;
            sword.SetParent(null, true);
            sword.position = p;
            sword.rotation = Quaternion.LookRotation(Vector3.up, transform.forward);
            sword.localScale = Vector3.one * pouchScale;
            PlayFlash(p);
        }
        else sword.SetParent(null, true);

        flightStart = sword.position;
        flightStartRot = sword.rotation;
        if (flightTrail != null) { flightTrail.Clear(); flightTrail.emitting = true; }
        state = WeaponState.Drawing;
        stateTime = 0f;
        stateDuration = drawTime;
    }

    private void TickDraw(float dt)
    {
        stateTime += dt;
        float u = Mathf.Clamp01(stateTime / stateDuration);
        float e = u * u * (3f - 2f * u);

        Pose target = GripWorld();
        Vector3 mid = (flightStart + target.position) * 0.5f + Vector3.up * arcHeight + transform.forward * arcForward;
        Vector3 pos = Bezier(flightStart, mid, target.position, e);
        Vector3 tangent = BezierTangent(flightStart, mid, target.position, e);

        Quaternion look = tangent.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(tangent.normalized, Vector3.up) : flightStartRot;
        float toGrip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.65f) / 0.35f));
        sword.SetPositionAndRotation(pos, Quaternion.Slerp(look, target.rotation, toGrip));
        sword.localScale = Vector3.one * Mathf.Lerp(pouchScale, 1f, e);

        if (u >= 1f) FinishDraw();
    }

    private void FinishDraw()
    {
        sword.SetParent(hand, false);
        sword.localScale = Vector3.one;
        ApplyHeldPose();
        if (flightTrail != null) flightTrail.emitting = false;
        state = WeaponState.Held;
        PlayFlash(sword.position);
        Drawn?.Invoke();
    }

    // ================= Cất kiếm =================

    private void BeginSheathe()
    {
        if (sword == null) { state = WeaponState.Sheathed; return; }
        sword.SetParent(null, true);
        flightStart = sword.position;
        flightStartRot = sword.rotation;
        if (flightTrail != null) { flightTrail.Clear(); flightTrail.emitting = true; }
        if (slashTrail != null) slashTrail.SetEmitting(false);
        state = WeaponState.Sheathing;
        stateTime = 0f;
        stateDuration = sheatheTime;
    }

    private void TickSheathe(float dt)
    {
        stateTime += dt;
        float u = Mathf.Clamp01(stateTime / stateDuration);
        float e = u * u;                      // ban đầu chậm, rồi vút về túi
        Vector3 end = pouchAnchor != null ? pouchAnchor.position : transform.position + Vector3.up;
        Vector3 mid = (flightStart + end) * 0.5f + Vector3.up * (arcHeight * 0.6f);
        Vector3 pos = Bezier(flightStart, mid, end, e);
        Vector3 tangent = BezierTangent(flightStart, mid, end, e);
        Quaternion look = tangent.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(tangent.normalized, Vector3.up) : flightStartRot;
        sword.SetPositionAndRotation(pos, Quaternion.Slerp(flightStartRot, look, Mathf.Clamp01(u * 3f)));
        sword.localScale = Vector3.one * Mathf.Lerp(1f, pouchScale, e);

        if (u >= 1f)
        {
            if (flightTrail != null) flightTrail.emitting = false;
            sword.gameObject.SetActive(false);
            state = WeaponState.Sheathed;
        }
    }

    // ================= Đang cầm =================

    /// <summary>PlayerMelee gọi: true = đang ra đòn chém (chuyển sang tư thế cầm chém). Có thể kèm tư thế riêng cho từng nhát (ví dụ nhát đâm).</summary>
    public void SetSlashPose(bool on, bool useOverride = false, GripPose pose = default(GripPose))
    {
        slashWanted = on;
        overrideWanted = on && useOverride;
        overridePose = pose;
    }

    /// <summary>PlayerMelee gọi: bật/tắt vệt chém theo lưỡi kiếm.</summary>
    public void SetSlashTrail(bool on)
    {
        if (slashTrail != null) slashTrail.SetEmitting(on && state == WeaponState.Held);
    }

    private void TickHeld(float dt)
    {
        // Trượt dần từ tư thế hiện tại tới tư thế đích (cầm thường / cầm chém / tư thế riêng của nhát chém)
        GripPose target = slashWanted ? (overrideWanted ? overridePose : slashPose) : carryPose;
        float k = 1f - Mathf.Exp(-3f * dt / Mathf.Max(0.01f, poseBlendTime));
        curPos = Vector3.Lerp(curPos, target.position, k);
        curRot = Quaternion.Slerp(curRot, Quaternion.Euler(target.euler), k);
        sword.localPosition = curPos;
        sword.localRotation = curRot;
    }

    private void ApplyHeldPose()
    {
        curPos = carryPose.position;
        curRot = Quaternion.Euler(carryPose.euler);
        sword.localPosition = curPos;
        sword.localRotation = curRot;
    }

    /// <summary>Vị trí và góc xoay thế giới mà kiếm phải có khi nằm trên tay (cầm thường).</summary>
    private Pose GripWorld()
    {
        Vector3 pos = hand.TransformPoint(carryPose.position);
        Quaternion rot = hand.rotation * Quaternion.Euler(carryPose.euler);
        return new Pose(pos, rot);
    }

    // ================= Hiệu ứng =================

    private void PlayFlash(Vector3 position)
    {
        if (flash != null && flashCount > 0)
        {
            flash.transform.position = position;
            flash.Emit(flashCount);
        }
        if (flashLight != null)
        {
            flashLight.transform.position = position;
            flashLight.enabled = true;
            flashLight.intensity = flashLightIntensity;
            flashEnd = Time.time + flashLightTime;
        }
    }

    private void UpdateFlashLight()
    {
        if (flashLight == null || flashEnd < 0f) return;
        float left = flashEnd - Time.time;
        if (left <= 0f)
        {
            flashLight.enabled = false;
            flashEnd = -1f;
            return;
        }
        flashLight.intensity = flashLightIntensity * (left / flashLightTime);
    }

    // ================= Đường cong =================

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float s = 1f - t;
        return s * s * a + 2f * s * t * b + t * t * c;
    }

    private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return 2f * (1f - t) * (b - a) + 2f * t * (c - b);
    }
}
