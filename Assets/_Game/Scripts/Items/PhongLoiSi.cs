using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Phong Lôi Sí: pháp bảo đôi cánh sấm sét (ô 4 của thanh ô đồ). Gắn ở đối tượng NhanVat.
/// - Chọn ô 4 rồi bấm chuột trái để bật/tắt (bật tốn pháp lực). Cánh xòe ra sau vai trong 0.5 giây, rung camera nhẹ,
///   và ở lại kể cả khi đổi sang ô khác (vừa bay vừa cầm kiếm / dùng Ngự Kiếm Thuật). Cánh mở thì đẩy Kiếm Luân ra sau thêm một đoạn.
/// - Cánh mở: bấm Space khi đang ở trên không (hoặc giữ Space 0.3 giây) để cất cánh. Bay: WASD theo hướng camera, Space lên, Ctrl xuống,
///   Shift tăng tốc; có quán tính; trần bay so với mặt đất; chạm đất khi đang hạ hoặc bấm Space hai lần nhanh thì hạ cánh (cánh vẫn mở).
/// - Dáng bay: clip NhanVat@Bay (dáng nằm ngang sẵn) làm nền, thân xoay quanh hông bằng code theo tốc độ (lơ lửng đứng thẳng, bay thường nghiêng,
///   tăng tốc gần nằm ngang), nghiêng người theo hướng rẽ, chuyển giữa các dáng mượt. Chém và phóng kiếm vẫn phát ở lớp thân trên.
/// - Pháp lực: cánh mở trên đất, đang bay, bay tăng tốc mỗi mức một tốc độ tiêu hao; không tự hồi. Cạn thì cánh chập chờn rồi tắt, nhân vật rơi
///   (rơi từ trên "độ cao rơi an toàn" thì mất máu theo độ cao).
/// - Camera lùi xa và mở rộng góc nhìn khi bay (thêm khi tăng tốc), mượt.
/// Mọi con số chỉnh trong Inspector.
/// </summary>
[DefaultExecutionOrder(-40)]   // sau PlayerAnimator, trước WingVisual/SwordSwarm: nghiêng thân xong thì cánh và kiếm mới bám theo
public class PhongLoiSi : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Vật phẩm Phong Lôi Sí trên thanh ô đồ (loại Pháp bảo). Chọn ô chứa nó rồi bấm chuột trái sẽ bật/tắt cánh.")]
    [SerializeField] private ItemData wingItem;

    [Tooltip("Hình dáng đôi cánh (đối tượng PhongLoiSi_Canh).")]
    [SerializeField] private WingVisual visual;

    [Tooltip("Sấm sét của cánh. Thường để trống: tự lấy từ prefab cánh (WingVisual.Lightning) sau khi cánh được dựng.")]
    [SerializeField] private WingLightning lightning;

    [Tooltip("Đàn kiếm: Kiếm Luân bị đẩy ra sau khi cánh mở. Có thể để trống.")]
    [SerializeField] private SwordSwarm swarm;

    [Tooltip("Rung camera khi mở cánh. Có thể để trống.")]
    [SerializeField] private HitFeedback feedback;

    [Tooltip("Lớp vật cản/mặt đất dùng để đo độ cao so với mặt đất (trần bay). Mặc định lớp MoiTruong.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Mở cánh")]
    [Tooltip("Pháp lực tốn mỗi lần mở cánh.")]
    [SerializeField, Min(0f)] private float activationCost = 25f;

    [Tooltip("Khi cánh mở và Kiếm Luân cũng bật: đẩy Kiếm Luân ra sau thêm bao nhiêu mét để không cắm vào cánh.")]
    [SerializeField, Min(0f)] private float wheelPush = 0.5f;

    [Tooltip("Rung camera khi mở cánh (mét). 0 = không rung.")]
    [SerializeField, Min(0f)] private float openShake = 0.03f;

    [Header("Tiêu hao pháp lực (mỗi giây)")]
    [Tooltip("Cánh mở nhưng đang đứng/đi trên mặt đất.")]
    [SerializeField, Min(0f)] private float groundDrain = 1f;

    [Tooltip("Đang bay thường (kể cả lơ lửng).")]
    [SerializeField, Min(0f)] private float flyDrain = 4f;

    [Tooltip("Đang bay tăng tốc (giữ Shift).")]
    [SerializeField, Min(0f)] private float boostDrain = 9f;

    [Tooltip("Hết pháp lực giữa trời: cánh chập chờn bấy nhiêu giây rồi tắt, nhân vật rơi.")]
    [SerializeField, Min(0f)] private float flutterTime = 1f;

    [Header("Cất cánh và hạ cánh")]
    [Tooltip("Giữ Space bấy nhiêu giây (khi cánh đã mở) thì cất cánh. Bấm Space khi đang ở trên không thì cất cánh ngay.")]
    [SerializeField, Min(0.05f)] private float takeoffHoldTime = 0.3f;

    [Tooltip("Hai lần bấm Space cách nhau chưa tới bấy nhiêu giây (lúc đang bay) thì hạ cánh.")]
    [SerializeField, Min(0.05f)] private float doubleTapWindow = 0.3f;

    [Tooltip("Tốc độ hạ xuống khi hạ cánh bằng hai lần Space (mét/giây).")]
    [SerializeField, Min(0.5f)] private float landingDescendSpeed = 8f;

    [Tooltip("Vận tốc đẩy lên lúc vừa cất cánh khi đang đứng trên đất (mét/giây).")]
    [SerializeField, Min(0f)] private float takeoffLift = 6f;

    [Header("Tốc độ bay")]
    [Tooltip("Tốc độ bay thường (mét/giây).")]
    [SerializeField, Min(0.5f)] private float cruiseSpeed = 12f;

    [Tooltip("Tốc độ bay tăng tốc khi giữ Shift (mét/giây).")]
    [SerializeField, Min(0.5f)] private float boostSpeed = 24f;

    [Tooltip("Tốc độ lên (Space) và xuống (Ctrl) (mét/giây).")]
    [SerializeField, Min(0.5f)] private float climbSpeed = 8f;

    [Tooltip("Gia tốc khi tăng tốc (mét/giây²). Số nhỏ = quán tính nặng hơn.")]
    [SerializeField, Min(1f)] private float acceleration = 20f;

    [Tooltip("Gia tốc khi giảm tốc / dừng (mét/giây²). Số nhỏ = trôi lâu hơn.")]
    [SerializeField, Min(1f)] private float deceleration = 14f;

    [Tooltip("Tốc độ quay thân người theo hướng camera khi bay (độ/giây).")]
    [SerializeField, Min(30f)] private float turnSpeed = 540f;

    [Tooltip("Trần bay: cao tối đa so với mặt đất bên dưới (mét).")]
    [SerializeField, Min(1f)] private float ceiling = 60f;

    [Header("Dáng bay (xoay thân bằng code)")]
    [Tooltip("Góc nghiêng sẵn có của clip NhanVat@Bay (độ so với phương thẳng đứng, 90 = nằm ngang). Đo từ clip: khoảng 66. Code chỉ xoay thêm (góc mong muốn - số này) để không cộng dồn thành chúc đầu xuống.")]
    [SerializeField, Range(0f, 90f)] private float clipPitch = 66f;

    [Tooltip("Góc nghiêng thân (so với phương thẳng đứng) khi lơ lửng. 0 = đứng thẳng.")]
    [SerializeField, Range(-30f, 90f)] private float hoverPitch = 0f;

    [Tooltip("Góc nghiêng thân khi bay thường tới trước (đạt ở tốc độ bay thường).")]
    [SerializeField, Range(0f, 90f)] private float cruisePitch = 50f;

    [Tooltip("Góc nghiêng thân khi bay tăng tốc (gần nằm ngang).")]
    [SerializeField, Range(0f, 90f)] private float boostPitch = 75f;

    [Tooltip("Góc ngả ra sau khi bay lùi (độ, số âm).")]
    [SerializeField, Range(-40f, 0f)] private float backwardPitch = -12f;

    [Tooltip("Nghiêng người tối đa khi rẽ (độ). Chỉ nghiêng khi rẽ (bay ngang sang bên hoặc xoay camera), bay thẳng thì không nghiêng; tự cân bằng lại.")]
    [SerializeField, Range(0f, 60f)] private float maxBank = 30f;

    [Tooltip("Nghiêng thêm khi xoay camera lúc đang bay nhanh: số độ nghiêng cho mỗi độ/giây tốc độ quay. 0 = chỉ nghiêng theo bay ngang.")]
    [SerializeField, Range(0f, 1f)] private float bankPerTurnRate = 0.12f;

    [Header("Độ ngả của cánh theo tốc độ")]
    [Tooltip("Cánh ngả về phía chân bao nhiêu độ (so với phương thẳng đứng của thế giới; lông cánh xuôi ra sau theo hướng bay) khi lơ lửng. 0 = dựng đứng.")]
    [SerializeField, Range(0f, 90f)] private float wingBackHover = 0f;

    [Tooltip("Cánh ngả ra sau bao nhiêu độ khi bay thường (đạt ở tốc độ bay thường).")]
    [SerializeField, Range(0f, 90f)] private float wingBackCruise = 35f;

    [Tooltip("Cánh ngả ra sau bao nhiêu độ khi tăng tốc (đạt ở tốc độ tăng tốc). Chuyển mượt giữa ba mức theo tốc độ. Lông cánh không được đè lên đầu hay xuyên lưng ở mọi mức: đã đo, xem CLAUDE.md.")]
    [SerializeField, Range(0f, 90f)] private float wingBackBoost = 60f;

    [Tooltip("Khi thân chúi tới (bay thường, tăng tốc), dịch thêm gốc cánh theo hệ trục thân (phải, lên, trước; mét), đạt đủ ở góc chúi bay thường, tăng thêm tới 1,6 lần khi tăng tốc. Y âm = hạ gốc cánh xuống lưng để camera sau lưng không bị tay cánh che mất đầu nhân vật.")]
    [SerializeField] private Vector3 wingRootShiftInFlight = new Vector3(0f, -0.2f, 0f);

    [Tooltip("Khi tăng tốc, cánh khép bớt về phía sau bấy nhiêu phần (0-1; 0.2 = 20%, trộn clip Glide với dáng gập của clip Close) để dáng thuôn như chim lao.")]
    [SerializeField, Range(0f, 1f)] private float boostWingFold = 0.2f;

    [Tooltip("Bù độ nghiêng sẵn có của clip Bay: clip làm vai/hông nghiêng sang một bên khoảng 7 độ khi bay thẳng. Số này (độ, âm = nghiêng ngược lại; tăng dần theo độ chúi của thân) cộng vào để người không lăn sang bên khi bay thẳng. Chỉ bù cho thân, không làm cánh nghiêng theo.")]
    [SerializeField, Range(-20f, 20f)] private float clipRollTrim = -6f;

    [Tooltip("Thời gian chuyển giữa các dáng (giây).")]
    [SerializeField, Min(0.05f)] private float poseSmoothTime = 0.3f;

    [Tooltip("Độ cao tâm xoay của thân (mét tính từ chân). Thân xoay quanh điểm này để không lún xuống đất hay trồi lên.")]
    [SerializeField, Min(0.2f)] private float pivotHeight = 1f;

    [Header("Dáng người khi bay (ghi đè xương bằng code, sau khi Animator chạy)")]
    [Tooltip("Bật chỉnh dáng: chân duỗi thẳng theo trục thân, tay xuôi dọc thân, đầu ngẩng nhìn hướng bay. Tắt = giữ nguyên dáng của clip Bay.")]
    [SerializeField] private bool shapePose = true;

    [Tooltip("Tốc độ bay (mét/giây) mà từ đó dáng duỗi hoàn toàn (chân khép, thẳng, mũi chân duỗi). Dưới mức này trộn dần về dáng lơ lửng.")]
    [SerializeField, Min(0.5f)] private float fullStretchSpeed = 12f;

    [Tooltip("Lơ lửng: hai chân lệch nhau (một chân ra trước, một chân ra sau) bấy nhiêu độ để trông tự nhiên.")]
    [SerializeField, Range(0f, 30f)] private float hoverLegStagger = 9f;

    [Tooltip("Lơ lửng: cẳng chân gập nhẹ ra sau bấy nhiêu độ.")]
    [SerializeField, Range(0f, 40f)] private float hoverKneeBend = 8f;

    [Tooltip("Lơ lửng: hai chân tách ra hai bên bấy nhiêu độ.")]
    [SerializeField, Range(-20f, 20f)] private float hoverLegSplay = 5f;

    [Tooltip("Bay thường/tăng tốc: hai chân tách ra hai bên bấy nhiêu độ. Số ÂM = ép hai chân vào nhau (chụm ngược), để tà áo không bị kéo xòe thành hình chuông.")]
    [SerializeField, Range(-20f, 20f)] private float flyLegSplay = -4f;

    [Tooltip("Mũi chân duỗi thẳng nối dài cẳng chân (0 = giữ như clip, 1 = duỗi hoàn toàn).")]
    [SerializeField, Range(0f, 1f)] private float toePoint = 1f;

    [Tooltip("Lơ lửng: hai tay dang ra khỏi thân bấy nhiêu độ.")]
    [SerializeField, Range(0f, 60f)] private float hoverArmSpread = 28f;

    [Tooltip("Bay nhanh: hai tay xuôi dọc thân, hơi tách ra bấy nhiêu độ.")]
    [SerializeField, Range(0f, 40f)] private float flyArmSpread = 12f;

    [Tooltip("Khuỷu tay gập nhẹ ra trước bấy nhiêu độ.")]
    [SerializeField, Range(0f, 40f)] private float elbowBend = 8f;

    [Tooltip("Đầu ngẩng nhìn theo hướng bay: góc xoay tối đa của cổ + đầu so với dáng trong clip (độ).")]
    [SerializeField, Range(0f, 90f)] private float headLookLimit = 60f;

    [Tooltip("Phần góc ngẩng do xương cổ chịu (0-1), phần còn lại do xương đầu.")]
    [SerializeField, Range(0f, 1f)] private float neckShare = 0.4f;

    [Header("Vỗ cánh")]
    [Tooltip("Cánh mở trên mặt đất: biên độ vỗ (độ) và nhịp (lần/giây).")]
    [SerializeField] private Vector2 groundFlap = new Vector2(6f, 0.5f);

    [Tooltip("Lơ lửng: biên độ (độ) và nhịp. Vỗ chậm.")]
    [SerializeField] private Vector2 hoverFlap = new Vector2(12f, 0.7f);

    [Tooltip("Đang lên cao: biên độ và nhịp. Vỗ nhanh hơn.")]
    [SerializeField] private Vector2 climbFlap = new Vector2(24f, 1.5f);

    [Tooltip("Bay thường: biên độ và nhịp.")]
    [SerializeField] private Vector2 cruiseFlap = new Vector2(18f, 0.9f);

    [Tooltip("Bay tăng tốc: xòe hết sải, đầu cánh cụp về sau, vỗ từng nhịp dài và mạnh (biên độ lớn, nhịp chậm).")]
    [SerializeField] private Vector2 boostFlap = new Vector2(34f, 0.55f);

    [Header("Sấm sét theo trạng thái (cuongDoSet 0-1; cánh xếp = 0)")]
    [Tooltip("Cường độ sét (cuongDoSet) khi cánh mở nhưng đứng trên đất (nhẹ, 35%).")]
    [SerializeField, Range(0f, 1f)] private float levelGround = 0.35f;

    [Tooltip("Cường độ sét khi bay thường (vừa, 65%; lơ lửng thấp hơn một chút).")]
    [SerializeField, Range(0f, 1f)] private float levelFly = 0.65f;

    [Tooltip("Cường độ sét khi bay tăng tốc (100%, kèm vệt sáng mảnh sau đầu cánh).")]
    [SerializeField, Range(0f, 1f)] private float levelBoost = 1f;

    [Header("Camera khi bay")]
    [Tooltip("Camera lùi xa thêm bấy nhiêu mét khi đang bay.")]
    [SerializeField, Min(0f)] private float flightExtraDistance = 1.5f;

    [Tooltip("Mở rộng góc nhìn thêm bấy nhiêu độ khi đang bay.")]
    [SerializeField, Min(0f)] private float flightExtraFov = 8f;

    [Tooltip("Mở rộng thêm bấy nhiêu độ nữa khi tăng tốc (Shift).")]
    [SerializeField, Min(0f)] private float boostExtraFov = 6f;

    [Tooltip("Camera lùi thêm bấy nhiêu mét nữa khi tăng tốc.")]
    [SerializeField, Min(0f)] private float boostExtraDistance = 0.8f;

    [Tooltip("Thời gian chuyển mượt của camera (giây).")]
    [SerializeField, Min(0.05f)] private float cameraSmoothTime = 0.45f;

    [Header("Rơi")]
    [Tooltip("Rơi từ độ cao này trở xuống thì không mất máu (mét).")]
    [SerializeField, Min(1f)] private float fallSafeHeight = 12f;

    [Tooltip("Mất bao nhiêu máu cho mỗi mét rơi vượt quá độ cao an toàn.")]
    [SerializeField, Min(0f)] private float fallDamagePerMeter = 4f;

    // ----- Tham chiếu tự tìm -----
    private PlayerController player;
    private PlayerStats stats;
    private Hotbar hotbar;
    private PlayerMelee melee;
    private Animator animator;
    private Transform model;
    private CinemachineCamera cmCamera;
    private CinemachineThirdPersonFollow thirdPerson;
    private float baseFov = 60f, baseDistance = 3.2f;

    // ----- Trạng thái -----
    private bool isOpen;
    private bool flying;
    private bool landing;
    private bool boosting;
    private bool fluttering;
    private float flutterEnd;
    private Vector3 flightVelocity;
    private float spaceHeld;
    private float lastSpaceTap = -100f;
    private float groundY;
    private float fallPeak;
    private Vector3 modelBasePos;
    private float bodyTilt, tiltVel, clipWeight, clipVel, appliedBank, bankVel;
    private float lastYaw, turnRate;
    private float wingBack, wingBackVel, wingBackTarget, wingFold, wingFoldVel, wingFoldTarget;
    private Quaternion bodyRotation = Quaternion.identity;

    // Xương dùng để chỉnh dáng khi bay: [0] trái, [1] phải
    private readonly Transform[] upLeg = new Transform[2], lowLeg = new Transform[2], foot = new Transform[2], toes = new Transform[2];
    private readonly Transform[] upArm = new Transform[2], lowArm = new Transform[2], hand = new Transform[2];
    private Transform neckBone, headBone;
    private Vector3 headFwdLocal;
    private bool headCaptured;
    private float poseWeight, armWeight, poseSpeed;
    private Vector3 lookDir = Vector3.forward;
    private int upperLayer = -1;
    private static readonly int EmptyUpperId = Animator.StringToHash("Trong");
    private float camDist, camDistVel, camFov, camFovVel;
    private float flapAmp, flapAmpVel, flapHz, flapHzVel, levelNow, levelVel;
    private bool built;

    // ----- Giá trị cho script khác -----

    /// <summary>Cánh đang mở (kể cả đang xòe).</summary>
    public bool IsOpen => isOpen;

    /// <summary>Đang bay.</summary>
    public bool IsFlying => flying;

    /// <summary>Đang bay tăng tốc.</summary>
    public bool IsBoosting => flying && boosting;

    /// <summary>Hết pháp lực, cánh đang chập chờn.</summary>
    public bool IsFluttering => fluttering;

    /// <summary>Vận tốc bay hiện tại.</summary>
    public Vector3 FlightVelocity => flightVelocity;

    /// <summary>Pháp lực cánh đang rút mỗi giây (HUD cộng với Ngự Kiếm Thuật).</summary>
    public float CurrentDrainPerSecond { get; private set; }

    /// <summary>Cánh cần có để thi triển Phá Không (đã mở xong).</summary>
    public bool ReadyForRift => isOpen && visual != null && visual.Spread > 0.5f;

    /// <summary>PhaKhong đặt true trong lúc xuyên không gian: giữ nhân vật đứng yên, không nhận điều khiển bay.</summary>
    public bool FrozenForRift { get; set; }

    /// <summary>Độ mở rộng góc nhìn thêm do Phá Không (độ), PhaKhong đặt.</summary>
    public float RiftFovBoost { get; set; }

    /// <summary>Mức khép cánh do Phá Không đặt (0-1; 0 = không ép): khép nhanh lúc lao vào cổng, bung nhanh lúc ra khỏi cổng.</summary>
    public float RiftFold { get; set; }

    private float fastFoldUntil;

    /// <summary>Tốc độ bay thường (mét/giây): vận tốc nhân vật giữ khi ra khỏi cổng Phá Không giữa trời.</summary>
    public float CruiseSpeed => cruiseSpeed;

    /// <summary>Cường độ sét tối thiểu do Phá Không đặt (0 = không ép).</summary>
    public float RiftLevel { get; set; }

    /// <summary>Pháp lực tốn mỗi lần mở cánh (HUD có thể hiện).</summary>
    public float ActivationCost => activationCost;

    /// <summary>Báo khi cánh mở (true) hoặc đóng (false).</summary>
    public event Action<bool> OpenChanged;

    /// <summary>Báo khi bắt đầu bay (true) hoặc hạ cánh/rơi (false).</summary>
    public event Action<bool> FlightChanged;

    /// <summary>Hình dáng cánh (cho PhaKhong gập cánh).</summary>
    public WingVisual Visual => visual;

    /// <summary>Sấm sét của cánh (cho PhaKhong tăng cường).</summary>
    public WingLightning Lightning => Lx;

    /// <summary>Sấm sét lấy lười: prefab cánh được dựng lúc chạy nên lấy từ WingVisual khi cần.</summary>
    private WingLightning Lx
    {
        get
        {
            if (lightning == null && visual != null) lightning = visual.Lightning;
            return lightning;
        }
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        stats = GetComponent<PlayerStats>();
        hotbar = GetComponent<Hotbar>();
        melee = GetComponent<PlayerMelee>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) model = animator.transform;
        if (swarm == null) swarm = FindAnyObjectByType<SwordSwarm>();
        if (feedback == null) feedback = FindAnyObjectByType<HitFeedback>();
        cmCamera = FindAnyObjectByType<CinemachineCamera>();
        if (cmCamera != null)
        {
            thirdPerson = cmCamera.GetComponent<CinemachineThirdPersonFollow>();
            baseFov = cmCamera.Lens.FieldOfView;
        }
        if (thirdPerson != null) baseDistance = thirdPerson.CameraDistance;
        camDist = baseDistance;
        camFov = baseFov;
        if (model != null) modelBasePos = model.localPosition;
        groundY = transform.position.y;
        fallPeak = transform.position.y;

        if (animator != null)
        {
            upperLayer = animator.GetLayerIndex("UpperBody");
            upLeg[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            upLeg[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lowLeg[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lowLeg[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            foot[0] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            foot[1] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            toes[0] = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            toes[1] = animator.GetBoneTransform(HumanBodyBones.RightToes);
            upArm[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            upArm[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lowArm[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lowArm[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand[0] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            hand[1] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        lastYaw = transform.eulerAngles.y;

        if (visual != null && animator != null)
        {
            visual.chestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (visual.chestBone == null) visual.chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            visual.bodyRoot = model;
        }
        built = true;
    }

    private void OnEnable()
    {
        if (hotbar == null) hotbar = GetComponent<Hotbar>();
        if (hotbar != null) hotbar.PhapBaoActivated += OnPhapBao;
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.Died += OnDied;
            stats.Respawned += OnRespawned;
        }
    }

    private void OnDisable()
    {
        if (hotbar != null) hotbar.PhapBaoActivated -= OnPhapBao;
        if (stats != null)
        {
            stats.Died -= OnDied;
            stats.Respawned -= OnRespawned;
        }
    }

    private void OnPhapBao(ItemData item)
    {
        if (item == wingItem) Toggle();
    }

    private void OnDied() { ForceClose(); }

    private void OnRespawned() { ForceClose(); fallPeak = transform.position.y; }

    // ================= Mở / đóng cánh =================

    /// <summary>Bật cánh (tốn pháp lực, không đủ thì thanh pháp lực nháy đỏ) hoặc tắt cánh. Đang bay mà tắt thì rơi.</summary>
    public void Toggle()
    {
        if (!isOpen) Open(true);
        else Close();
    }

    /// <summary>Mở cánh. charge = true thì trừ pháp lực.</summary>
    public void Open(bool charge)
    {
        if (isOpen || stats == null || !stats.IsAlive) return;
        if (charge && !stats.TrySpendMana(activationCost)) return;
        isOpen = true;
        fluttering = false;
        if (visual != null) { visual.SetFlicker(false); visual.Open(); }
        if (Lx != null) Lx.PlayShoulderFlash();
        if (feedback != null) feedback.Shake(openShake);
        if (swarm != null) swarm.WheelPushTarget = wheelPush;
        OpenChanged?.Invoke(true);
    }

    /// <summary>Gập cánh (tan thành tia sét). Đang bay thì ngừng bay và rơi.</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        fluttering = false;
        if (flying) EndFlight(false);
        if (visual != null) { visual.SetFlicker(false); visual.Close(); }
        if (Lx != null) { Lx.PlayBurst(0.4f); Lx.SetStreaks(false); Lx.SetFlying(false); }
        if (swarm != null) swarm.WheelPushTarget = 0f;
        CurrentDrainPerSecond = 0f;
        OpenChanged?.Invoke(false);
    }

    private void ForceClose()
    {
        bool was = isOpen;
        isOpen = false;
        fluttering = false;
        if (flying) EndFlight(false);
        if (visual != null) { visual.SetFlicker(false); visual.SnapTo(false); }
        if (Lx != null) { Lx.SetLevel(0f); Lx.SetStreaks(false); Lx.SetFlying(false); }
        if (swarm != null) swarm.WheelPushTarget = 0f;
        FrozenForRift = false;
        RiftFovBoost = 0f;
        RiftLevel = 0f;
        RiftFold = 0f;
        CurrentDrainPerSecond = 0f;
        if (was) OpenChanged?.Invoke(false);
    }

    // ================= Bay =================

    /// <summary>Cất cánh: vào chế độ bay với vận tốc đầu initial (mét/giây). Chỉ khi cánh đã mở.</summary>
    public void BeginFlight(Vector3 initialVelocity)
    {
        if (flying || !isOpen) return;
        flying = true;
        landing = false;
        boosting = false;
        flightVelocity = initialVelocity;
        lastSpaceTap = -100f;
        player.FlightActive = true;
        if (melee != null && melee.IsAttacking && !melee.AttackInFlight) melee.CancelAttack();
        if (animator != null) animator.CrossFadeInFixedTime("Bay", 0.3f, 0);
        if (Lx != null) { Lx.SetFlying(true); Lx.PlayTakeoffBurst(); }
        FlightChanged?.Invoke(true);
    }

    /// <summary>
    /// Ngừng bay. landed = true: chạm đất đàng hoàng (về đi bộ). false: hết bay giữa trời (bị rơi theo vận tốc hiện tại).
    /// </summary>
    public void EndFlight(bool landed)
    {
        if (!flying) return;
        flying = false;
        landing = false;
        boosting = false;
        player.FlightActive = false;
        if (landed)
        {
            player.SetVerticalVelocity(-2f);
            player.SetHorizontalVelocity(Vector3.zero);
        }
        else
        {
            player.SetVerticalVelocity(Mathf.Min(flightVelocity.y, 2f));
            player.SetHorizontalVelocity(flightVelocity * 0.5f);
        }
        if (animator != null && stats != null && stats.IsAlive)
            animator.CrossFadeInFixedTime(landed ? "Locomotion" : "Fall", 0.2f, 0);
        fallPeak = transform.position.y;
        if (Lx != null)
        {
            Lx.SetStreaks(false);
            Lx.SetFlying(false);
            if (landed) Lx.PlayLandingBurst(transform.position);
        }
        FlightChanged?.Invoke(false);
    }

    /// <summary>Đặt vận tốc bay (Phá Không xong thì bay tiếp theo hướng cũ).</summary>
    public void SetFlightVelocity(Vector3 v) { flightVelocity = v; }

    /// <summary>Đặt lại mốc độ cao để tính rơi (sau khi dịch chuyển, không tính chênh lệch độ cao là rơi).</summary>
    public void ResetFallTracking()
    {
        fallPeak = transform.position.y;
        groundY = Mathf.Min(groundY, transform.position.y);
    }

    private void Update()
    {
        if (!built || stats == null) return;
        float dt = Time.deltaTime;
        if (!stats.IsAlive) return;
        Keyboard kb = Keyboard.current;

        UpdateGroundReference();

        // Điều kiện cất cánh
        if (isOpen && !flying && !FrozenForRift && (kb != null || testActive) && !player.ControlLocked)
        {
            spaceHeld = SpaceHeldNow(kb) ? spaceHeld + dt : 0f;
            bool inAir = !player.IsGrounded && Time.time - LastGrounded() > 0.08f;
            bool pressedInAir = SpacePressedNow(kb) && inAir && visual != null && visual.Spread > 0.3f;
            bool held = spaceHeld >= takeoffHoldTime && visual != null && visual.Spread > 0.3f;
            if (pressedInAir || held)
            {
                Vector3 v = new Vector3(0f, Mathf.Max(player.VerticalVelocity, player.IsGrounded ? takeoffLift : 2f), 0f);
                BeginFlight(v);
                spaceHeld = 0f;
            }
        }
        else if (!SpaceHeldNow(kb)) spaceHeld = 0f;

        if (flying) UpdateFlight(dt, kb);
        else UpdateFall();

        UpdateDrain(dt);
    }

    private float lastGroundedStamp;
    private float LastGrounded()
    {
        if (player.IsGrounded) lastGroundedStamp = Time.time;
        return lastGroundedStamp;
    }

    // ----- Đầu vào (bàn phím thật, hoặc giả lập để kiểm thử qua code) -----
    private bool testActive, testShift, testSpaceTap;
    private Vector2 testMove;
    private float testVert;

    /// <summary>
    /// Kiểm thử qua code: giả lập đầu vào bay (move = W/A/S/D, vert = Space(+1)/Ctrl(-1), shift = tăng tốc) thay cho bàn phím.
    /// Gọi <see cref="TestClearInput"/> để trả lại bàn phím thật.
    /// </summary>
    public void TestSetInput(Vector2 move, float vert, bool shift) { testActive = true; testMove = move; testVert = vert; testShift = shift; }

    /// <summary>Kiểm thử qua code: giả lập một lần bấm Space (tính ở khung hình kế tiếp).</summary>
    public void TestTapSpace() { testActive = true; testSpaceTap = true; }

    /// <summary>Kiểm thử qua code: trả đầu vào về bàn phím thật.</summary>
    public void TestClearInput() { testActive = false; testMove = Vector2.zero; testVert = 0f; testShift = false; testSpaceTap = false; }

    private bool SpacePressedNow(Keyboard kb)
    {
        if (testActive) { bool t = testSpaceTap; testSpaceTap = false; return t; }
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    private bool SpaceHeldNow(Keyboard kb)
    {
        if (testActive) return testVert > 0.5f;
        return kb != null && kb.spaceKey.isPressed;
    }

    /// <summary>Vật phẩm Phong Lôi Sí (HUD dùng để biết ô nào đang bật).</summary>
    public ItemData WingItem => wingItem;

    /// <summary>Đo độ cao mặt đất bên dưới (để tính trần bay).</summary>
    private void UpdateGroundReference()
    {
        if (player.IsGrounded) groundY = transform.position.y;
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 400f, groundMask, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;
    }

    private void UpdateFlight(float dt, Keyboard kb)
    {
        if (FrozenForRift) { flightVelocity = Vector3.zero; return; }
        Camera cam = Camera.main;
        Vector2 mv = Vector2.zero;
        float vert = 0f;
        bool shift = false;
        if ((kb != null || testActive) && !player.ControlLocked)
        {
            if (testActive)
            {
                mv = Vector2.ClampMagnitude(testMove, 1f);
                vert = testVert;
                shift = testShift;
            }
            else
            {
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1f;
                mv = Vector2.ClampMagnitude(mv, 1f);
                if (kb.spaceKey.isPressed) vert += 1f;
                if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) vert -= 1f;
                shift = kb.leftShiftKey.isPressed;
            }

            // Hai lần Space nhanh: hạ cánh
            if (SpacePressedNow(kb))
            {
                if (Time.time - lastSpaceTap <= doubleTapWindow) landing = true;
                lastSpaceTap = Time.time;
            }
        }

        Vector3 camFwd = cam != null ? cam.transform.forward : transform.forward;
        Vector3 camRight = cam != null ? cam.transform.right : transform.right;
        camRight.y = 0f;
        camRight = camRight.sqrMagnitude > 0.0001f ? camRight.normalized : transform.right;
        Vector3 dir = camFwd * mv.y + camRight * mv.x;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        boosting = shift && dir.sqrMagnitude > 0.04f && !fluttering;
        float speed = boosting ? boostSpeed : cruiseSpeed;
        Vector3 target = dir * speed;
        float vy = vert * climbSpeed;
        if (landing) vy = -landingDescendSpeed;
        target = new Vector3(target.x, target.y + vy, target.z);

        // Có quán tính: tăng tốc và giảm tốc mượt
        float rate = target.magnitude > flightVelocity.magnitude ? acceleration : deceleration;
        flightVelocity = Vector3.MoveTowards(flightVelocity, target, rate * dt);

        // Trần bay
        float maxY = groundY + ceiling;
        if (transform.position.y >= maxY && flightVelocity.y > 0f) flightVelocity.y = 0f;

        // Xoay thân theo hướng nhìn của camera
        if (cam != null && cameraOrbitYaw(out float yaw)) player.RotateToYaw(yaw, turnSpeed);

        player.FlightMove(flightVelocity, dt);

        // Chạm đất khi đang hạ: về đi bộ
        if (player.IsGrounded && flightVelocity.y <= 0.5f && vert <= 0f && Time.time - lastSpaceTap > 0.12f)
            EndFlight(true);
    }

    private bool cameraOrbitYaw(out float yaw)
    {
        Camera cam = Camera.main;
        yaw = 0f;
        if (cam == null) return false;
        Vector3 f = cam.transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) return false;
        yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        return true;
    }

    /// <summary>Theo dõi độ cao rơi (kể cả khi hết bay giữa trời); chạm đất từ quá độ cao an toàn thì mất máu theo độ cao.</summary>
    private void UpdateFall()
    {
        float y = transform.position.y;
        if (player.IsGrounded)
        {
            float drop = fallPeak - y;
            if (drop > fallSafeHeight && fallDamagePerMeter > 0f && stats != null)
                stats.TakeDamage((drop - fallSafeHeight) * fallDamagePerMeter);
            fallPeak = y;
        }
        else fallPeak = Mathf.Max(fallPeak, y);
    }

    private void UpdateDrain(float dt)
    {
        if (!isOpen) { CurrentDrainPerSecond = 0f; return; }
        float rate = flying ? (boosting ? boostDrain : flyDrain) : groundDrain;
        CurrentDrainPerSecond = rate;
        bool ok = rate <= 0f || stats.DrainMana(rate * dt);
        if (!ok)
        {
            if (!fluttering)
            {
                fluttering = true;
                flutterEnd = Time.time + flutterTime;
                if (visual != null) visual.SetFlicker(true);
                stats.ReportManaShortage();
            }
        }
        else if (fluttering && stats.Mana > 0.5f)
        {
            fluttering = false;
            if (visual != null) visual.SetFlicker(false);
        }
        if (fluttering && Time.time >= flutterEnd) Close();
    }

    // ================= Hình ảnh: dáng thân, vỗ cánh, sấm sét, camera =================

    private void LateUpdate()
    {
        if (!built) return;
        float dt = Time.deltaTime;
        UpdateBodyPose(dt);
        if (shapePose) ApplyFlightPose(dt);
        UpdateWingAnchor();
        UpdateWingMotion(dt);
        UpdateCamera(dt);
    }

    /// <summary>
    /// Xoay thân quanh hông theo tốc độ: lơ lửng đứng thẳng, bay thường nghiêng, tăng tốc gần nằm ngang. Nghiêng (roll) CHỈ khi rẽ
    /// (bay ngang sang bên hoặc xoay camera lúc đang bay nhanh), quanh trục tiến ngang của nhân vật, rồi tự cân bằng lại.
    /// bodyTilt = góc nghiêng thật của thân (so với phương thẳng đứng); mô hình chỉ xoay phần chênh với góc có sẵn trong clip.
    /// </summary>
    private void UpdateBodyPose(float dt)
    {
        if (model == null) return;
        float targetTilt = 0f, targetBank = 0f;

        // Tốc độ quay thân (độ/giây) để nghiêng khi xoay camera
        float yawNow = transform.eulerAngles.y;
        float yawDelta = Mathf.DeltaAngle(lastYaw, yawNow);
        lastYaw = yawNow;
        float instantRate = dt > 0.0001f ? yawDelta / dt : 0f;
        turnRate = Mathf.Lerp(turnRate, Mathf.Clamp(instantRate, -400f, 400f), 1f - Mathf.Exp(-12f * dt));

        wingBackTarget = wingBackHover;
        wingFoldTarget = 0f;
        if (flying)
        {
            Vector3 planar = new Vector3(flightVelocity.x, 0f, flightVelocity.z);
            float fwd = Vector3.Dot(planar, transform.forward);
            float lateral = Vector3.Dot(planar, transform.right);
            if (fwd >= 0f)
            {
                targetTilt = fwd <= cruiseSpeed
                    ? Mathf.Lerp(hoverPitch, cruisePitch, fwd / cruiseSpeed)
                    : Mathf.Lerp(cruisePitch, boostPitch, Mathf.Clamp01((fwd - cruiseSpeed) / Mathf.Max(0.1f, boostSpeed - cruiseSpeed)));
            }
            else targetTilt = Mathf.Lerp(hoverPitch, backwardPitch, Mathf.Clamp01(-fwd / cruiseSpeed));
            // Độ ngả của cánh theo tốc độ tiến: lơ lửng -> bay thường -> tăng tốc
            float boostT = Mathf.Clamp01((fwd - cruiseSpeed) / Mathf.Max(0.1f, boostSpeed - cruiseSpeed));
            wingBackTarget = fwd <= 0f ? wingBackHover
                : (fwd <= cruiseSpeed ? Mathf.Lerp(wingBackHover, wingBackCruise, fwd / cruiseSpeed) : Mathf.Lerp(wingBackCruise, wingBackBoost, boostT));
            wingFoldTarget = boostWingFold * boostT;
            // Bay ngang sang trái (lateral < 0) thì nghiêng về trái; xoay camera sang trái (turnRate < 0) cũng vậy, chỉ khi đang bay đủ nhanh
            float speed01 = Mathf.Clamp01(planar.magnitude / cruiseSpeed);
            targetBank = (-Mathf.Clamp(lateral / boostSpeed, -1f, 1f) * maxBank) + (-turnRate * bankPerTurnRate * speed01);
            targetBank = Mathf.Clamp(targetBank, -maxBank, maxBank);
        }
        bodyTilt = Mathf.SmoothDamp(bodyTilt, targetTilt, ref tiltVel, poseSmoothTime);
        clipWeight = Mathf.SmoothDamp(clipWeight, flying ? 1f : 0f, ref clipVel, poseSmoothTime);
        appliedBank = Mathf.SmoothDamp(appliedBank, targetBank, ref bankVel, poseSmoothTime);

        // Clip Bay đã nằm nghiêng sẵn clipPitch độ (theo mức trộn clip): mô hình chỉ xoay phần chênh lệch
        float appliedPitch = bodyTilt - clipPitch * clipWeight;
        // Clip nghiêng vai/hông nhiều nhất khi thân đã chúi tới (đo: ~3 độ lúc lơ lửng, ~7 độ khi bay), nên số bù tăng theo độ chúi
        float trim = clipRollTrim * clipWeight * Mathf.Clamp01(bodyTilt / Mathf.Max(1f, cruisePitch));
        bodyRotation = transform.rotation * Quaternion.AngleAxis(appliedBank, Vector3.forward) * Quaternion.AngleAxis(bodyTilt, Vector3.right);

        if (Mathf.Abs(appliedPitch) < 0.01f && Mathf.Abs(appliedBank + trim) < 0.01f)
        {
            model.localRotation = Quaternion.identity;
            model.localPosition = modelBasePos;
            return;
        }
        Quaternion r = Quaternion.AngleAxis(appliedBank + trim, Vector3.forward) * Quaternion.AngleAxis(appliedPitch, Vector3.right);
        Vector3 pivot = new Vector3(0f, pivotHeight, 0f);
        model.localRotation = r;
        model.localPosition = pivot + r * (modelBasePos - pivot);
    }

    /// <summary>Hướng đôi cánh = thân người (trục phải song song trục phải nhân vật, trục lên theo góc nghiêng và rẽ của thân), không theo xương ngực.</summary>
    private void UpdateWingAnchor()
    {
        if (visual == null) return;
        wingBack = Mathf.SmoothDamp(wingBack, wingBackTarget, ref wingBackVel, poseSmoothTime);
        if (RiftFold > 0.01f) fastFoldUntil = Time.time + 0.6f;   // Phá Không: khép và bung cánh rất nhanh
        wingFold = Mathf.SmoothDamp(wingFold, Mathf.Max(wingFoldTarget, RiftFold), ref wingFoldVel, Time.time < fastFoldUntil ? 0.07f : poseSmoothTime);
        visual.SetFold(wingFold);
        visual.SetExtraOffset(wingRootShiftInFlight * Mathf.Clamp(bodyTilt / Mathf.Max(1f, cruisePitch), 0f, 1.6f));
        // Ngả về phía chân: lông cánh (rũ xuống từ xương cánh) hất ra sau theo hướng bay, xương cánh (mép đầu) chúi về phía trước cùng thân người,
        // nên khi thân nằm ngang lông cánh xuôi về phía chân như chim lao. (Dấu trừ trước đây xoay ngược: lông chĩa về phía đầu, cánh nổi tách khỏi lưng.)
        float tilt = wingBack;
        Quaternion q = transform.rotation * Quaternion.AngleAxis(appliedBank, Vector3.forward) * Quaternion.AngleAxis(tilt, Vector3.right);
        visual.SetOrientation(q, bodyRotation);   // độ lệch gốc cánh theo trục thân để gốc cánh luôn sát mặt lưng
    }

    // ----- Chỉnh dáng người khi bay -----

    /// <summary>
    /// Ghi đè xương sau khi Animator chạy: chân duỗi thẳng dọc trục thân (lơ lửng thì hơi so le, bay nhanh thì khép + mũi chân duỗi),
    /// tay xuôi dọc thân hơi tách ra (lơ lửng thì dang hơn), cổ và đầu ngẩng nhìn hướng bay. Trộn mượt vào/ra theo lúc cất/hạ cánh.
    /// Tay chỉ ghi đè khi không chém/phóng kiếm (lớp thân trên đang rảnh) để không phá animation chém.
    /// </summary>
    private void ApplyFlightPose(float dt)
    {
        if (animator == null || model == null) return;
        poseWeight = Mathf.MoveTowards(poseWeight, flying ? 1f : 0f, dt / Mathf.Max(0.05f, poseSmoothTime));
        if (!headCaptured && !flying && poseWeight <= 0f && headBone != null && player.IsGrounded)
        {
            // Hướng mặt trong hệ xương đầu, đo lúc đứng yên (mặt nhìn theo hướng nhân vật)
            headFwdLocal = Quaternion.Inverse(headBone.rotation) * transform.forward;
            headCaptured = true;
        }
        if (poseWeight <= 0.001f) { armWeight = 0f; return; }

        bool upperBusy = false;
        if (upperLayer >= 0)
        {
            AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(upperLayer);
            upperBusy = cur.shortNameHash != EmptyUpperId || animator.IsInTransition(upperLayer);
        }
        if (melee != null && melee.IsAttacking) upperBusy = true;
        armWeight = Mathf.MoveTowards(armWeight, upperBusy ? 0f : 1f, dt * 8f);

        poseSpeed = Mathf.MoveTowards(poseSpeed, Mathf.Clamp01(flightVelocity.magnitude / fullStretchSpeed), dt * 3f);
        float hv = 1f - poseSpeed;   // 1 = lơ lửng, 0 = bay nhanh

        Vector3 down = bodyRotation * Vector3.down;
        Vector3 right = bodyRotation * Vector3.right;
        Vector3 fwd = bodyRotation * Vector3.forward;
        float w = poseWeight;

        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f;   // trái -1, phải +1
            // Chân: lơ lửng thì trái ra trước, phải ra sau; bay nhanh thì cả hai thẳng và khép
            float stagger = hv * hoverLegStagger * (s == 0 ? 1f : -1f) * Mathf.Deg2Rad;
            float splay = Mathf.Lerp(flyLegSplay, hoverLegSplay, hv) * Mathf.Deg2Rad;
            Vector3 thigh = (down * Mathf.Cos(stagger) + fwd * Mathf.Sin(stagger) + right * (side * Mathf.Sin(splay))).normalized;
            float knee = hv * hoverKneeBend * Mathf.Deg2Rad;
            Vector3 shank = (thigh * Mathf.Cos(knee) - fwd * Mathf.Sin(knee)).normalized;
            Aim(upLeg[s], lowLeg[s], thigh, w);
            Aim(lowLeg[s], foot[s], shank, w);
            Aim(foot[s], toes[s], shank, w * toePoint);

            // Tay: xuôi dọc thân, tách ra hai bên; khuỷu gập nhẹ ra trước
            float aw = w * armWeight;
            if (aw > 0.001f)
            {
                float spread = Mathf.Lerp(flyArmSpread, hoverArmSpread, hv) * Mathf.Deg2Rad;
                Vector3 upperDir = (down * Mathf.Cos(spread) + right * (side * Mathf.Sin(spread))).normalized;
                float bend = elbowBend * Mathf.Deg2Rad;
                Vector3 lowerDir = (upperDir * Mathf.Cos(bend) + fwd * Mathf.Sin(bend)).normalized;
                Aim(upArm[s], lowArm[s], upperDir, aw);
                Aim(lowArm[s], hand[s], lowerDir, aw);
            }
        }

        // Đầu: ngẩng nhìn theo hướng bay (đứng yên thì nhìn thẳng theo hướng nhân vật), giới hạn headLookLimit
        if (headBone != null && headCaptured)
        {
            Vector3 wantDir = flightVelocity.sqrMagnitude > 4f ? flightVelocity.normalized : transform.forward;
            lookDir = Vector3.Slerp(lookDir, wantDir, 1f - Mathf.Exp(-8f * dt)).normalized;
            Vector3 face = headBone.rotation * headFwdLocal;
            Quaternion delta = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(face, lookDir), headLookLimit);
            if (neckBone != null) neckBone.rotation = Quaternion.Slerp(Quaternion.identity, delta, neckShare * w) * neckBone.rotation;
            face = headBone.rotation * headFwdLocal;
            Quaternion delta2 = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(face, lookDir), headLookLimit * (1f - neckShare));
            headBone.rotation = Quaternion.Slerp(Quaternion.identity, delta2, w) * headBone.rotation;
        }
    }

    /// <summary>Xoay xương bone (theo trọng số weight) sao cho hướng từ bone tới child trùng targetDir. Tuyệt đối, nên áp lại nhiều lần vẫn không cộng dồn.</summary>
    private static void Aim(Transform bone, Transform child, Vector3 targetDir, float weight)
    {
        if (bone == null || child == null || weight <= 0.0001f) return;
        Vector3 cur = child.position - bone.position;
        if (cur.sqrMagnitude < 1e-8f) return;
        Quaternion full = Quaternion.FromToRotation(cur.normalized, targetDir) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, full, Mathf.Clamp01(weight));
    }

    private void UpdateWingMotion(float dt)
    {
        if (visual == null) return;
        Vector2 flap;
        float sweep = 0f;
        float level = levelGround;
        bool streams = false;
        if (!isOpen)
        {
            flap = groundFlap;
            level = 0f;   // cánh xếp: tắt hết
        }
        else if (!flying)
        {
            flap = groundFlap;
        }
        else
        {
            float speed = flightVelocity.magnitude;
            float climb = Mathf.Clamp01(flightVelocity.y / climbSpeed);
            float cruiseT = Mathf.Clamp01(speed / cruiseSpeed);
            flap = Vector2.Lerp(hoverFlap, cruiseFlap, cruiseT);
            flap = Vector2.Lerp(flap, climbFlap, climb);
            level = Mathf.Lerp(levelFly * 0.8f, levelFly, cruiseT);
            if (boosting)
            {
                flap = boostFlap;
                sweep = 1f;
                level = levelBoost;
                streams = true;
            }
        }
        if (RiftLevel > level) level = RiftLevel;
        flapAmp = Mathf.SmoothDamp(flapAmp, flap.x, ref flapAmpVel, 0.3f);
        flapHz = Mathf.SmoothDamp(flapHz, flap.y, ref flapHzVel, 0.3f);
        visual.SetMotion(flapAmp, flapHz, sweep);

        if (Lx != null)
        {
            Lx.SetLevel(Mathf.Clamp01(level));
            Lx.SetStreaks(streams && !FrozenForRift);
            Lx.SetFlying(flying);
            Lx.SetFlightVelocity(flightVelocity);
        }
    }

    private void UpdateCamera(float dt)
    {
        if (cmCamera == null) return;
        float targetDist = baseDistance;
        float targetFov = baseFov + RiftFovBoost;
        if (flying)
        {
            targetDist += flightExtraDistance + (boosting ? boostExtraDistance : 0f);
            targetFov += flightExtraFov + (boosting ? boostExtraFov : 0f);
        }
        camDist = Mathf.SmoothDamp(camDist, targetDist, ref camDistVel, cameraSmoothTime, Mathf.Infinity, dt);
        camFov = Mathf.SmoothDamp(camFov, targetFov, ref camFovVel, RiftFovBoost > 0.01f ? 0.08f : cameraSmoothTime, Mathf.Infinity, dt);
        if (thirdPerson != null) thirdPerson.CameraDistance = camDist;
        LensSettings lens = cmCamera.Lens;
        lens.FieldOfView = camFov;
        cmCamera.Lens = lens;
    }
}
