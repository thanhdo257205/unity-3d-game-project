using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Bảng ADMIN (công cụ thử nghiệm) - bật/tắt bằng phím ` (dấu huyền, cạnh phím 1). Prefab Assets/_Game/UI/AdminPanel.prefab, dựng bằng menu
/// Phàm Kiếm Lục → Bảng Admin → "Dựng prefab và đặt vào SanTap". Giao diện tự dựng bằng code lúc chạy (uGUI + TextMeshPro).
/// - Mở bảng: hiện con trỏ chuột, camera ngừng xoay theo chuột, game vẫn chạy. Đóng: trả lại như cũ.
/// - Cảnh giới (− / + / danh sách), 5 công tắc, các nút bấm một lần, thanh trượt tốc độ game, thông tin chỉ đọc.
/// - Các hệ thống khác CHỈ hỏi qua thuộc tính tĩnh ở đây (MauVoHan, PhapLucVoHan, KhongHoiChieu, KeDichDungYen, MotDonHaGuc, TocDoGame),
///   không có logic admin rải rác trong PlayerController.
/// - Trạng thái công tắc KHÔNG lưu giữa các lần chạy: mỗi lần Play đều bắt đầu tắt hết. Có công tắc bật thì chữ "ADMIN" màu cam hiện ở góc phải trên cả khi bảng đóng.
/// </summary>
public class AdminPanel : MonoBehaviour
{
    // ================= Thuộc tính tĩnh cho các hệ thống khác =================

    /// <summary>Máu vô hạn: vẫn báo sát thương nhận vào nhưng máu không giảm (PlayerStats hỏi).</summary>
    public static bool MauVoHan { get; set; }

    /// <summary>Pháp lực vô hạn: mọi kỹ năng dùng được, thanh pháp lực luôn đầy (PlayerStats hỏi).</summary>
    public static bool PhapLucVoHan { get; set; }

    /// <summary>Không hồi chiêu: mọi kỹ năng dùng lại ngay (PhaKhong, Hotbar hỏi).</summary>
    public static bool KhongHoiChieu { get; set; }

    /// <summary>Một đòn hạ gục: mọi đòn của người chơi lên Damageable giết ngay (Damageable hỏi).</summary>
    public static bool MotDonHaGuc { get; set; }

    /// <summary>Kẻ địch đứng yên, không tấn công. Đặt giá trị này sẽ đặt luôn EnemyBase.AIEnabled (phím F7 cũng dùng).</summary>
    public static bool KeDichDungYen
    {
        get => keDichDungYen;
        set { keDichDungYen = value; EnemyBase.AIEnabled = !value; }
    }
    private static bool keDichDungYen;

    /// <summary>Tốc độ game đặt trong bảng (0,1-2; mặc định 1). HitFeedback trả Time.timeScale về số này sau khi dừng hình.</summary>
    public static float TocDoGame { get; private set; } = 1f;

    /// <summary>Bảng đang mở.</summary>
    public static bool DangMo { get; private set; }

    /// <summary>Có công tắc nào đang bật (hoặc tốc độ game khác 1) không.</summary>
    public static bool CoCongTacBat => MauVoHan || PhapLucVoHan || KhongHoiChieu || KeDichDungYen || MotDonHaGuc || !Mathf.Approximately(TocDoGame, 1f);

    /// <summary>Bảng duy nhất trong scene.</summary>
    public static AdminPanel Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MauVoHan = PhapLucVoHan = KhongHoiChieu = MotDonHaGuc = false;
        keDichDungYen = false;
        EnemyBase.AIEnabled = true;
        TocDoGame = 1f;
        DangMo = false;
        Instance = null;
    }

    // ================= Cài đặt =================

    [Header("Tham chiếu (do menu dựng prefab gán)")]
    [Tooltip("Phông TextMeshPro có đủ dấu tiếng Việt (Inter_Regular_SDF). Để trống thì dùng phông mặc định của TextMeshPro.")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Tooltip("Prefab Thanh Lang để nút 'Sinh Thanh Lang' tạo ra.")]
    [SerializeField] private GameObject thanhLangPrefab;

    [Tooltip("Prefab Tà tu để nút 'Sinh Tà tu' tạo ra.")]
    [SerializeField] private GameObject taTuPrefab;

    [Header("Cài đặt")]
    [Tooltip("Phím bật/tắt bảng. Mặc định dấu huyền (cạnh phím 1).")]
    [SerializeField] private Key toggleKey = Key.Backquote;

    [Tooltip("Sinh kẻ địch cách nhân vật bấy nhiêu mét, phía trước mặt.")]
    [SerializeField, Min(1f)] private float spawnDistance = 8f;

    [Tooltip("Độ rộng bảng (điểm ảnh trên màn hình chuẩn 1920x1080).")]
    [SerializeField, Min(300f)] private float panelWidth = 420f;

    // ----- Tham chiếu tự tìm -----
    private PlayerStats stats;
    private PlayerController player;
    private PhongLoiSi wings;
    private PhaKhong phaKhong;
    private CameraOrbit camOrbit;
    private Vector3 startPos;
    private Quaternion startRot;

    // ----- Giao diện -----
    private Canvas canvas;
    private RectTransform panel, realmList;
    private GameObject adminLabel;
    private TextMeshProUGUI realmText, realmButtonText, speedText, infoText, titleText;
    private Slider speedSlider;
    private readonly List<RectTransform> realmRows = new List<RectTransform>();
    private readonly List<Action> toggleRefresh = new List<Action>();
    private bool prevLocked, prevInput;
    private float fps, fpsTimer;
    private int lastRealmIndex = -1;
    private Sprite white;

    private static readonly Color PanelBack = new Color(0.04f, 0.05f, 0.08f, 0.95f);
    private static readonly Color Gold = new Color(0.9f, 0.75f, 0.3f, 1f);
    private static readonly Color BtnColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    private static readonly Color BtnHover = new Color(0.28f, 0.3f, 0.4f, 1f);
    private static readonly Color Orange = new Color(1f, 0.6f, 0.15f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.94f, 0.88f, 1f);

    // ================= Khởi tạo =================

    private void Awake()
    {
        Instance = this;
        ResetStatics();
        Instance = this;
        Time.timeScale = 1f;
        white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
        BuildUI();
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(transform, false);
        }
    }

    private void Start()
    {
        stats = PlayerStats.Instance != null ? PlayerStats.Instance : FindAnyObjectByType<PlayerStats>();
        player = FindAnyObjectByType<PlayerController>();
        wings = FindAnyObjectByType<PhongLoiSi>();
        phaKhong = FindAnyObjectByType<PhaKhong>();
        camOrbit = FindAnyObjectByType<CameraOrbit>();
        if (player != null) { startPos = player.transform.position; startRot = player.transform.rotation; }
        BuildRealmList();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (DangMo) SetOpen(false);
        if (Instance == this) { ResetStatics(); }
        Time.timeScale = 1f;
    }

    // ================= Mở / đóng =================

    /// <summary>Mở (true) hoặc đóng (false) bảng.</summary>
    public void SetOpen(bool open)
    {
        if (open == DangMo && panel != null && panel.gameObject.activeSelf == open) return;
        if (open)
        {
            prevLocked = Cursor.lockState == CursorLockMode.Locked;
            if (camOrbit != null) { prevInput = camOrbit.InputEnabled; camOrbit.InputEnabled = false; }
            CameraOrbit.SetCursorLocked(false);
        }
        else if (DangMo)
        {
            if (camOrbit != null) camOrbit.InputEnabled = prevInput;
            CameraOrbit.SetCursorLocked(prevLocked);
        }
        DangMo = open;
        if (panel != null) panel.gameObject.SetActive(open);
        if (realmList != null) realmList.gameObject.SetActive(false);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) SetOpen(!DangMo);

        // Nhãn ADMIN
        bool any = CoCongTacBat;
        if (adminLabel != null && adminLabel.activeSelf != any) adminLabel.SetActive(any);

        if (!DangMo) return;
        for (int i = 0; i < toggleRefresh.Count; i++) toggleRefresh[i]();
        UpdateRealmText();
        UpdateInfo();
    }

    // ================= Dựng giao diện =================

    private void BuildUI()
    {
        GameObject cgo = new GameObject("AdminCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        RectTransform root = cgo.GetComponent<RectTransform>();

        // Nhãn ADMIN (góc phải trên, luôn hiện khi có công tắc bật)
        TextMeshProUGUI lab = NewText(root, "NhanAdmin", "ADMIN", 26, TextAlignmentOptions.TopRight, Orange);
        lab.rectTransform.anchorMin = lab.rectTransform.anchorMax = new Vector2(1f, 1f);
        lab.rectTransform.pivot = new Vector2(1f, 1f);
        lab.rectTransform.anchoredPosition = new Vector2(-24f, -14f);
        lab.rectTransform.sizeDelta = new Vector2(160f, 34f);
        lab.fontStyle = FontStyles.Bold;
        adminLabel = lab.gameObject;
        adminLabel.SetActive(false);

        // Bảng
        Image back = NewImage(root, "BangAdmin", PanelBack);
        panel = back.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(18f, 0f);

        float w = panelWidth, pad = 14f, y = -10f, inner = w - pad * 2f;
        titleText = AddText(panel, "BẢNG ADMIN   (phím ` để đóng)", pad, y, inner, 32f, 24, Gold, TextAlignmentOptions.Left);
        titleText.fontStyle = FontStyles.Bold;
        y -= 38f;

        // 1. Cảnh giới
        y = AddSection(panel, "Cảnh giới", y, pad, inner);
        realmText = AddText(panel, "", pad, y, inner, 28f, 22, TextColor, TextAlignmentOptions.Left);
        y -= 32f;
        AddButton(panel, "-", pad, y, 46f, 36f, () => ChangeRealm(-1));
        Button dd = AddButton(panel, "", pad + 52f, y, inner - 104f, 36f, () => { if (realmList != null) { realmList.SetAsLastSibling(); realmList.gameObject.SetActive(!realmList.gameObject.activeSelf); } });
        realmButtonText = dd.GetComponentInChildren<TextMeshProUGUI>();
        AddButton(panel, "+", pad + inner - 46f, y, 46f, 36f, () => ChangeRealm(1));
        float listTop = y - 38f;
        realmList = NewImage(panel, "DanhSachCanhGioi", new Color(0.07f, 0.08f, 0.12f, 0.98f)).rectTransform;
        realmList.anchorMin = realmList.anchorMax = new Vector2(0f, 1f);
        realmList.pivot = new Vector2(0f, 1f);
        realmList.anchoredPosition = new Vector2(pad + 52f, listTop);
        realmList.sizeDelta = new Vector2(inner - 104f, 100f);
        y -= 46f;

        // 2. Công tắc
        y = AddSection(panel, "Công tắc", y, pad, inner);
        y = AddToggle(panel, "Máu vô hạn", pad, y, inner, () => MauVoHan, v => MauVoHan = v);
        y = AddToggle(panel, "Pháp lực vô hạn", pad, y, inner, () => PhapLucVoHan, v => PhapLucVoHan = v);
        y = AddToggle(panel, "Không hồi chiêu", pad, y, inner, () => KhongHoiChieu, v => KhongHoiChieu = v);
        y = AddToggle(panel, "Kẻ địch đứng yên", pad, y, inner, () => KeDichDungYen, v => KeDichDungYen = v);
        y = AddToggle(panel, "Một đòn hạ gục", pad, y, inner, () => MotDonHaGuc, v => MotDonHaGuc = v);
        y -= 4f;

        // 3. Nút bấm
        y = AddSection(panel, "Nút bấm", y, pad, inner);
        float bw = (inner - 8f) * 0.5f;
        AddButton(panel, "Hồi đầy máu + pháp lực", pad, y, bw, 31f, HoiDay);
        AddButton(panel, "Về điểm xuất phát", pad + bw + 8f, y, bw, 31f, VeXuatPhat);
        y -= 36f;
        AddButton(panel, "Sinh Thanh Lang", pad, y, bw, 31f, () => SinhKeDich(thanhLangPrefab));
        AddButton(panel, "Sinh Tà tu", pad + bw + 8f, y, bw, 31f, () => SinhKeDich(taTuPrefab));
        y -= 36f;
        AddButton(panel, "Xóa hết kẻ địch", pad, y, bw, 31f, XoaKeDich);
        AddButton(panel, "Hồi sinh kẻ địch", pad + bw + 8f, y, bw, 31f, () => EnemyBase.ReviveAll());
        y -= 36f;
        AddButton(panel, "Tự gây 20 sát thương", pad, y, bw, 31f, () => { if (stats != null) stats.TakeDamage(20f); });
        AddButton(panel, "Đầy Kiếm ý", pad + bw + 8f, y, bw, 31f, () => { if (stats != null) stats.AddSwordIntent(stats.MaxSwordIntent); });
        y -= 46f;

        // 4. Tốc độ game
        y = AddSection(panel, "Tốc độ game", y, pad, inner);
        speedSlider = AddSlider(panel, pad, y, inner - 150f, 26f, 0.1f, 2f, 1f, v => { TocDoGame = Mathf.Round(v * 20f) / 20f; if (Time.timeScale > 0f) Time.timeScale = TocDoGame; });
        speedText = AddText(panel, "1.00x", pad + inner - 140f, y + 2f, 60f, 26f, 20, TextColor, TextAlignmentOptions.Left);
        AddButton(panel, "Đặt lại 1", pad + inner - 76f, y + 1f, 76f, 28f, () => { speedSlider.value = 1f; });
        y -= 38f;

        // 5. Thông tin
        y = AddSection(panel, "Thông tin", y, pad, inner);
        infoText = AddText(panel, "", pad, y, inner, 96f, 19, TextColor, TextAlignmentOptions.TopLeft);
        infoText.textWrappingMode = TextWrappingModes.Normal;
        y -= 102f;

        panel.sizeDelta = new Vector2(w, -y + 8f);
        realmList.SetAsLastSibling();   // danh sách thả xuống vẽ sau cùng để nằm trên các phần tử bên dưới
        panel.gameObject.SetActive(false);
    }

    private void BuildRealmList()
    {
        if (stats == null || realmList == null) return;
        foreach (RectTransform r in realmRows) if (r != null) Destroy(r.gameObject);
        realmRows.Clear();
        int n = stats.RealmCount;
        float rowH = 30f;
        realmList.sizeDelta = new Vector2(realmList.sizeDelta.x, n * rowH + 6f);
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            RealmData rd = stats.GetRealm(i);
            Button b = AddButton(realmList, rd != null ? rd.DisplayName : "?", 3f, -3f - i * rowH, realmList.sizeDelta.x - 6f, rowH - 2f, () => { SetRealm(idx); realmList.gameObject.SetActive(false); });
            realmRows.Add((RectTransform)b.transform);
        }
    }

    // ----- Phần tử giao diện -----

    private Image NewImage(RectTransform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = white;
        img.color = color;
        return img;
    }

    private TextMeshProUGUI NewText(RectTransform parent, string name, string text, int size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void SetTopLeft(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private TextMeshProUGUI AddText(RectTransform parent, string text, float x, float y, float w, float h, int size, Color color, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = NewText(parent, "Chu", text, size, align, color);
        SetTopLeft(t.rectTransform, x, y, w, h);
        return t;
    }

    private float AddSection(RectTransform parent, string title, float y, float pad, float inner)
    {
        Image line = NewImage(parent, "Vach", new Color(Gold.r, Gold.g, Gold.b, 0.45f));
        SetTopLeft(line.rectTransform, pad, y - 2f, inner, 1.5f);
        TextMeshProUGUI t = AddText(parent, title, pad, y - 4f, inner, 26f, 19, Gold, TextAlignmentOptions.Left);
        t.fontStyle = FontStyles.Bold;
        return y - 30f;
    }

    private Button AddButton(RectTransform parent, string label, float x, float y, float w, float h, Action onClick)
    {
        Image img = NewImage(parent, "Nut_" + label, BtnColor);
        SetTopLeft(img.rectTransform, x, y, w, h);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.5f, 1.5f, 1.7f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1f;
        b.colors = cb;
        Navigation nav = new Navigation { mode = Navigation.Mode.None };
        b.navigation = nav;
        if (onClick != null) b.onClick.AddListener(() => onClick());
        TextMeshProUGUI t = NewText(img.rectTransform, "Chu", label, 20, TextAlignmentOptions.Center, TextColor);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(4f, 0f); t.rectTransform.offsetMax = new Vector2(-4f, 0f);
        t.enableAutoSizing = true; t.fontSizeMin = 13; t.fontSizeMax = 20;
        return b;
    }

    private float AddToggle(RectTransform parent, string label, float x, float y, float w, Func<bool> get, Action<bool> set)
    {
        Button row = AddButton(parent, "", x, y, w, 29f, null);
        row.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.17f, 1f);
        Image box = NewImage((RectTransform)row.transform, "Hop", new Color(0.3f, 0.3f, 0.35f, 1f));
        SetTopLeft(box.rectTransform, 8f, -4.5f, 20f, 20f);
        TextMeshProUGUI lab = row.GetComponentInChildren<TextMeshProUGUI>();
        lab.text = label;
        lab.alignment = TextAlignmentOptions.Left;
        lab.rectTransform.offsetMin = new Vector2(38f, 0f);
        TextMeshProUGUI state = NewText((RectTransform)row.transform, "TrangThai", "TẮT", 18, TextAlignmentOptions.Right, new Color(0.6f, 0.6f, 0.65f));
        state.rectTransform.anchorMin = Vector2.zero; state.rectTransform.anchorMax = Vector2.one;
        state.rectTransform.offsetMin = Vector2.zero; state.rectTransform.offsetMax = new Vector2(-10f, 0f);
        row.onClick.AddListener(() => set(!get()));
        toggleRefresh.Add(() =>
        {
            bool on = get();
            box.color = on ? new Color(1f, 0.6f, 0.15f, 1f) : new Color(0.3f, 0.3f, 0.35f, 1f);
            state.text = on ? "BẬT" : "TẮT";
            state.color = on ? Orange : new Color(0.6f, 0.6f, 0.65f);
        });
        return y - 32f;
    }

    private Slider AddSlider(RectTransform parent, float x, float y, float w, float h, float min, float max, float value, Action<float> onChange)
    {
        GameObject go = new GameObject("ThanhTruot", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        SetTopLeft((RectTransform)go.transform, x, y, w, h);
        Image bg = NewImage((RectTransform)go.transform, "Nen", new Color(0.12f, 0.14f, 0.2f, 1f));
        bg.rectTransform.anchorMin = new Vector2(0f, 0.3f); bg.rectTransform.anchorMax = new Vector2(1f, 0.7f);
        bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
        RectTransform fillArea = new GameObject("VungDay", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(go.transform, false);
        fillArea.anchorMin = new Vector2(0f, 0.3f); fillArea.anchorMax = new Vector2(1f, 0.7f);
        fillArea.offsetMin = new Vector2(6f, 0f); fillArea.offsetMax = new Vector2(-6f, 0f);
        Image fill = NewImage(fillArea, "Day", Orange);
        fill.rectTransform.anchorMin = new Vector2(0f, 0f); fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(8f, 0f);
        RectTransform handleArea = new GameObject("VungTay", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(go.transform, false);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f); handleArea.offsetMax = new Vector2(-10f, 0f);
        Image handle = NewImage(handleArea, "Tay", new Color(0.95f, 0.9f, 0.8f, 1f));
        handle.rectTransform.anchorMin = new Vector2(0f, 0f); handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(16f, 0f);
        Slider s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min; s.maxValue = max; s.wholeNumbers = false;
        s.value = value;
        s.onValueChanged.AddListener(v => { onChange(v); if (speedText != null) speedText.text = TocDoGame.ToString("F2") + "x"; });
        return s;
    }

    // ================= Chức năng =================

    private void ChangeRealm(int delta)
    {
        if (stats == null) return;
        SetRealm(Mathf.Clamp(stats.RealmIndex + delta, 0, Mathf.Max(0, stats.RealmCount - 1)));
    }

    /// <summary>Nhảy tới cảnh giới thứ index trong thang. Cập nhật mọi thứ phụ thuộc (pháp lực tối đa, số kiếm, tầm Phá Không) rồi hồi đầy máu và pháp lực.</summary>
    public void SetRealm(int index)
    {
        if (stats == null) return;
        stats.SetRealmIndex(index);   // phát RealmChanged: SwordSwarm đặt lại số kiếm theo Thần thức; MaxMana và tầm Phá Không đọc từ cảnh giới hiện tại
        stats.RestoreAll();
    }

    private void UpdateRealmText()
    {
        if (stats == null) return;
        string name = stats.Realm != null ? stats.Realm.DisplayName : "?";
        if (realmText != null)
        {
            string t = name + "   (tầm Phá Không " + (stats.Realm != null ? stats.Realm.PhaKhongDistance.ToString("F0") : "?") + "m, kiếm tối đa " + stats.MaxSwords + ", pháp lực tối đa " + Mathf.CeilToInt(stats.MaxMana) + ")";
            if (realmText.text != t) realmText.text = t;
            realmText.textWrappingMode = TextWrappingModes.NoWrap;
            realmText.fontSize = 17;
        }
        if (realmButtonText != null && stats.RealmIndex != lastRealmIndex)
        {
            realmButtonText.text = name + "  v";
            lastRealmIndex = stats.RealmIndex;
        }
    }

    /// <summary>Hồi đầy máu và pháp lực.</summary>
    public void HoiDay()
    {
        if (stats == null) return;
        stats.RestoreAll();
        if (phaKhong != null) phaKhong.ResetCooldown();
    }

    /// <summary>Đưa nhân vật về điểm xuất phát (vị trí lúc bắt đầu chạy).</summary>
    public void VeXuatPhat()
    {
        if (player == null) return;
        if (wings != null && wings.IsFlying) wings.EndFlight(true);
        if (camOrbit != null) camOrbit.Glide(camOrbit.transform.position, 0.25f);
        player.TeleportTo(startPos, startRot);
        if (wings != null) wings.ResetFallTracking();
    }

    /// <summary>Sinh một kẻ địch trước mặt nhân vật spawnDistance mét, đặt lên NavMesh.</summary>
    public GameObject SinhKeDich(GameObject prefab)
    {
        if (prefab == null || player == null) return null;
        Vector3 fwd = player.transform.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        Vector3 pos = player.transform.position + fwd * spawnDistance;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, 6f, NavMesh.AllAreas)) pos = hit.position;
        else
        {
            RaycastHit g;
            if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out g, 60f)) pos = g.point;
        }
        return Instantiate(prefab, pos, Quaternion.LookRotation(-fwd, Vector3.up));
    }

    /// <summary>Xóa hết kẻ địch (EnemyBase) trong scene.</summary>
    public void XoaKeDich()
    {
        EnemyBase[] all = EnemyBase.All.ToArray();
        for (int i = 0; i < all.Length; i++) if (all[i] != null) Destroy(all[i].gameObject);
    }

    private void UpdateInfo()
    {
        fpsTimer += Time.unscaledDeltaTime;
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.1f);
        if (infoText == null || player == null) return;
        Vector3 p = player.transform.position;
        float height = -1f;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore) )
        {
            // bỏ qua chính nhân vật: bắn lại từ dưới chân nếu trúng thân
            if (hit.collider.transform.IsChildOf(player.transform) && Physics.Raycast(p - Vector3.up * 0.05f, Vector3.down, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore)) height = p.y - hit.point.y;
            else if (!hit.collider.transform.IsChildOf(player.transform)) height = p.y - hit.point.y;
        }
        string state = "Đất";
        if (phaKhong != null && phaKhong.IsCasting) state = "Phá Không";
        else if (wings != null && wings.IsBoosting) state = "Bay tăng tốc";
        else if (wings != null && wings.IsFlying) state = "Bay";
        else if (!player.IsGrounded) state = "Trên không (nhảy / rơi)";
        string t = "FPS: " + fps.ToString("F0") + "   (thời gian game x" + Time.timeScale.ToString("F2") + ")\n"
                 + "Vị trí: " + p.x.ToString("F1") + ", " + p.y.ToString("F1") + ", " + p.z.ToString("F1") + "\n"
                 + "Độ cao so với đất: " + (height >= 0f ? height.ToString("F1") + " m" : "--") + "\n"
                 + "Trạng thái: " + state;
        if (infoText.text != t) infoText.text = t;
    }
}
