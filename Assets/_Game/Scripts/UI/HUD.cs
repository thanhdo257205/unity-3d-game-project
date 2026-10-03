using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Giao diện người chơi bằng tiếng Việt (tự dựng khi bấm Play, không cần kéo thả):
/// - Góc dưới trái: thanh Máu (đỏ), Pháp lực (xanh lam), Kiếm ý (vàng kim) kèm số, dòng "Kiếm: sẵn sàng/tổng", ba ô đội hình Quạt/Vòng/Mũi tên.
/// - Góc trên trái: tên cảnh giới.
/// - Số sát thương bay lên khi đánh trúng, viền đỏ màn hình khi bị đánh, cảnh báo "Pháp lực không đủ".
/// - Màn hình "Đạo tiêu thân vẫn" kèm nút "Thử lại" khi chết.
/// Màu sắc, kích thước, phông chữ chỉnh trong Inspector.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Tài nguyên người chơi (đối tượng NhanVat).")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Đàn kiếm (đối tượng BayKiem) để hiện số kiếm và đội hình.")]
    [SerializeField] private SwordSwarm swarm;

    [Tooltip("Vòng đời người chơi (hiện màn hình chết, nút Thử lại).")]
    [SerializeField] private PlayerLife life;

    [Header("Phông chữ")]
    [Tooltip("Danh sách phông chữ hệ thống thử theo thứ tự, phải hỗ trợ dấu tiếng Việt. Cái đầu tiên máy có sẽ được dùng.")]
    [SerializeField] private string[] fontNames = { "Be Vietnam Pro", "Noto Sans", "Segoe UI", "Arial" };

    [Header("Màu")]
    [Tooltip("Màu thanh Máu.")]
    [SerializeField] private Color healthColor = new Color(0.86f, 0.18f, 0.16f);

    [Tooltip("Màu thanh Pháp lực.")]
    [SerializeField] private Color manaColor = new Color(0.2f, 0.52f, 0.95f);

    [Tooltip("Màu thanh Kiếm ý (vàng kim).")]
    [SerializeField] private Color intentColor = new Color(0.95f, 0.78f, 0.25f);

    [Tooltip("Màu nền tối mờ của bảng.")]
    [SerializeField] private Color panelColor = new Color(0.04f, 0.05f, 0.08f, 0.62f);

    [Tooltip("Màu viền mảnh (vàng kim).")]
    [SerializeField] private Color borderColor = new Color(0.85f, 0.7f, 0.3f, 0.9f);

    [Tooltip("Màu chữ.")]
    [SerializeField] private Color textColor = new Color(0.96f, 0.94f, 0.88f);

    [Header("Cảnh báo và hiệu ứng")]
    [Tooltip("Thời gian thanh Pháp lực nháy đỏ và dòng 'Pháp lực không đủ' hiện (giây).")]
    [SerializeField, Min(0.1f)] private float manaWarnTime = 1.2f;

    [Tooltip("Thời gian viền đỏ ở bốn mép màn hình khi bị đánh mờ hết (giây).")]
    [SerializeField, Min(0.05f)] private float vignetteTime = 0.4f;

    [Tooltip("Độ đậm tối đa của viền đỏ lúc vừa bị đánh (0-1). 0.35 = 35%.")]
    [SerializeField, Range(0f, 1f)] private float vignetteAlpha = 0.35f;

    [Tooltip("Bề rộng dải viền đỏ tính từ mép màn hình (tỉ lệ so với nửa màn hình, 0-1). Nhỏ = viền mỏng, tâm màn hình trong hơn.")]
    [SerializeField, Range(0.05f, 0.6f)] private float vignetteWidth = 0.22f;

    [Tooltip("Máu dưới tỉ lệ này (0-1) thì giữ một viền đỏ nhạt đập theo nhịp. 0.3 = dưới 30% máu.")]
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;

    [Tooltip("Độ đậm cao nhất của viền đỏ nhạt khi sắp chết (0-1).")]
    [SerializeField, Range(0f, 1f)] private float lowHealthAlpha = 0.14f;

    [Tooltip("Nhịp đập của viền đỏ khi sắp chết (số nhịp mỗi giây).")]
    [SerializeField, Min(0.1f)] private float lowHealthPulseRate = 1.1f;

    [Tooltip("Số sát thương bay lên cao bao nhiêu điểm ảnh.")]
    [SerializeField, Min(0f)] private float damageRise = 90f;

    [Tooltip("Thời gian số sát thương bay lên rồi mờ (giây).")]
    [SerializeField, Min(0.1f)] private float damageNumberTime = 0.8f;

    [Header("Kẻ địch và linh thạch")]
    [Tooltip("Chỉ hiện thanh máu kẻ địch trong khoảng cách này (mét).")]
    [SerializeField, Min(1f)] private float enemyBarRange = 30f;

    [Tooltip("Chiều rộng thanh máu kẻ địch (điểm ảnh trên màn hình chuẩn 1920x1080).")]
    [SerializeField, Min(20f)] private float enemyBarWidth = 120f;

    [Tooltip("Màu thanh máu kẻ địch.")]
    [SerializeField] private Color enemyBarColor = new Color(0.85f, 0.22f, 0.18f);

    [Tooltip("Thời gian dòng '+N linh thạch' bay lên rồi mờ (giây).")]
    [SerializeField, Min(0.2f)] private float stonesPopupTime = 1.4f;

    [Tooltip("Tốc độ nhấp nháy của thanh Kiếm ý khi đầy (số lần mỗi giây).")]
    [SerializeField, Min(0.1f)] private float intentPulseRate = 3f;

    // ---- Phần tử UI ----
    private Font font;
    private Canvas canvas;
    private RectTransform root;
    private Image healthFill, manaFill, intentFill, manaBack, intentBack;
    private Text healthText, manaText, intentText, swordText, realmText, manaWarnText;
    private Image[] slotBack = new Image[3];
    private Text[] slotLabel = new Text[3];
    private GameObject deathPanel;
    private CanvasGroup deathGroup;
    private Sprite whiteSprite;

    private float manaWarnUntil = -10f;
    private float vignetteStart = -10f;
    private float deathFade;
    private bool deathVisible;

    // Số sát thương bay lên (dùng lại, không tạo mới liên tục)
    private class FloatingNumber
    {
        public Text text;
        public Vector3 world;
        public float start;
        public bool active;
    }
    private readonly List<FloatingNumber> numbers = new List<FloatingNumber>();

    // Thanh máu kẻ địch (vẽ trên màn hình theo vị trí đầu kẻ địch, dùng lại các phần tử)
    private class EnemyBarEntry
    {
        public RectTransform root;
        public Image fill;
        public Text nameText;
        public Text realmText;
    }
    private readonly List<EnemyBarEntry> enemyBars = new List<EnemyBarEntry>();
    private RectTransform enemyBarLayer;

    // Linh thạch
    private Text stonesText;
    private Text stonesPopup;
    private float stonesPopupStart = -10f;

    private static readonly string[] SlotNames = { "Quạt", "Vòng", "Mũi tên" };

    private void Awake()
    {
        font = Font.CreateDynamicFontFromOSFont(fontNames, 32);
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
        BuildUI();
        EnsureEventSystem();
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.Damaged += OnPlayerDamaged;
            stats.ManaInsufficient += OnManaInsufficient;
            stats.SpiritStonesGained += OnStonesGained;
        }
        if (life != null)
        {
            life.DeathScreenShown += OnDeathScreen;
            life.Respawned += OnRespawned;
        }
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.Damaged -= OnPlayerDamaged;
            stats.ManaInsufficient -= OnManaInsufficient;
            stats.SpiritStonesGained -= OnStonesGained;
        }
        if (life != null)
        {
            life.DeathScreenShown -= OnDeathScreen;
            life.Respawned -= OnRespawned;
        }
        Damageable.AnyDamaged -= OnAnyDamaged;
    }

    // ================= Dựng giao diện =================

    private void BuildUI()
    {
        GameObject cgo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = cgo.GetComponent<RectTransform>();

        enemyBarLayer = new GameObject("ThanhMauKeDich", typeof(RectTransform)).GetComponent<RectTransform>();
        enemyBarLayer.SetParent(root, false);
        Stretch(enemyBarLayer);
        BuildVignette();
        BuildStatusPanel();
        BuildSlots();
        BuildRealmPanel();
        BuildStonesUI();
        BuildDeathPanel();
    }

    /// <summary>Viền đỏ ở bốn mép màn hình: ảnh vẽ bằng code, chỉ đỏ ở dải sát mép, tâm hoàn toàn trong suốt.</summary>
    private void BuildVignette()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float band = Mathf.Max(0.02f, vignetteWidth * 0.5f);   // đổi sang tỉ lệ so với cả chiều rộng/cao ảnh
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));   // khoảng cách tới mép gần nhất (0-0.5)
                float a = Mathf.Clamp01(1f - edge / band);
                a *= a;
                tex.SetPixel(x, y, new Color(0.85f, 0.02f, 0.02f, a));
            }
        }
        tex.Apply();
        RawImage img = NewObject<RawImage>("VienDo", root);
        img.texture = tex;
        img.raycastTarget = false;
        Stretch(img.rectTransform);
        img.color = new Color(1f, 1f, 1f, 0f);
        vignetteRaw = img;
    }
    private RawImage vignetteRaw;

    private void BuildStatusPanel()
    {
        RectTransform panel = MakePanel("BangTaiNguyen", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(430f, 168f));

        float y = -14f;
        healthFill = MakeBar(panel, "Máu", healthColor, ref y, out healthText, out _);
        manaFill = MakeBar(panel, "Pháp lực", manaColor, ref y, out manaText, out manaBack);
        intentFill = MakeBar(panel, "Kiếm ý", intentColor, ref y, out intentText, out intentBack);

        swordText = MakeText(panel, "Kiem", "Kiếm: 0/0", 24, TextAnchor.MiddleLeft, textColor);
        SetRect(swordText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y - 4f), new Vector2(240f, 30f));

        manaWarnText = MakeText(panel, "CanhBaoPhapLuc", "Pháp lực không đủ", 20, TextAnchor.MiddleRight, new Color(1f, 0.4f, 0.35f));
        SetRect(manaWarnText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, y - 4f), new Vector2(220f, 30f));
        manaWarnText.gameObject.SetActive(false);
    }

    /// <summary>Một thanh tài nguyên: nhãn bên trái, thanh ở giữa, số bên phải. y được dịch xuống cho thanh kế tiếp.</summary>
    private Image MakeBar(RectTransform parent, string label, Color color, ref float y, out Text valueText, out Image back)
    {
        Text l = MakeText(parent, "Nhan_" + label, label, 22, TextAnchor.MiddleLeft, textColor);
        SetRect(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(104f, 30f));

        back = NewObject<Image>("Nen_" + label, parent);
        back.sprite = whiteSprite;
        back.color = new Color(0f, 0f, 0f, 0.55f);
        back.raycastTarget = false;
        SetRect(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f, y - 5f), new Vector2(190f, 20f));

        Image fill = NewObject<Image>("Day_" + label, back.rectTransform);
        fill.sprite = whiteSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        fill.color = color;
        fill.raycastTarget = false;
        Stretch(fill.rectTransform);

        valueText = MakeText(parent, "So_" + label, "0/0", 20, TextAnchor.MiddleRight, textColor);
        SetRect(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(318f, y), new Vector2(100f, 30f));

        y -= 34f;
        return fill;
    }

    /// <summary>Ba ô đội hình 1/2/3 nằm ngay phía trên bảng tài nguyên.</summary>
    private void BuildSlots()
    {
        float w = 130f, h = 46f, gap = 10f;
        for (int i = 0; i < 3; i++)
        {
            RectTransform slot = MakePanel("OĐộiHình" + (i + 1), root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(30f + i * (w + gap), 210f), new Vector2(w, h));
            slotBack[i] = slot.GetComponent<Image>();
            Text t = MakeText(slot, "Ten", (i + 1) + "  " + SlotNames[i], 22, TextAnchor.MiddleCenter, textColor);
            Stretch(t.rectTransform);
            slotLabel[i] = t;
        }
    }

    /// <summary>Bộ đếm linh thạch dưới tên cảnh giới và dòng "+N linh thạch" bay lên giữa màn hình.</summary>
    private void BuildStonesUI()
    {
        RectTransform panel = MakePanel("BangLinhThach", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(300f, 40f));
        stonesText = MakeText(panel, "LinhThach", "Linh thạch: 0", 22, TextAnchor.MiddleCenter, textColor);
        Stretch(stonesText.rectTransform);

        stonesPopup = MakeText(root, "CongLinhThach", "", 34, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
        SetRect(stonesPopup.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(500f, 60f));
        stonesPopup.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        Outline o = stonesPopup.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.8f);
        o.effectDistance = new Vector2(2f, -2f);
        stonesPopup.gameObject.SetActive(false);
    }

    private void BuildRealmPanel()
    {
        RectTransform panel = MakePanel("BangCanhGioi", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(300f, 48f));
        realmText = MakeText(panel, "TenCanhGioi", "", 26, TextAnchor.MiddleCenter, intentColor);
        Stretch(realmText.rectTransform);
    }

    private void BuildDeathPanel()
    {
        deathPanel = new GameObject("ManHinhChet", typeof(RectTransform), typeof(CanvasGroup));
        deathPanel.transform.SetParent(root, false);
        Stretch((RectTransform)deathPanel.transform);
        deathGroup = deathPanel.GetComponent<CanvasGroup>();

        Image dim = NewObject<Image>("Toi", (RectTransform)deathPanel.transform);
        dim.sprite = whiteSprite;
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        Stretch(dim.rectTransform);

        Text title = MakeText((RectTransform)deathPanel.transform, "TieuDe", "Đạo tiêu thân vẫn", 84, TextAnchor.MiddleCenter, new Color(0.9f, 0.25f, 0.2f));
        SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1000f, 130f));

        // Nút "Thử lại"
        GameObject btnGo = new GameObject("NutThuLai", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(deathPanel.transform, false);
        RectTransform brt = (RectTransform)btnGo.transform;
        SetRect(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(260f, 70f));
        Image bimg = btnGo.GetComponent<Image>();
        bimg.sprite = whiteSprite;
        bimg.color = new Color(0.1f, 0.1f, 0.14f, 0.95f);
        AddFrame(brt, borderColor, 2f);
        Button btn = btnGo.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1.4f, 1.3f, 1f, 1f);
        cb.pressedColor = new Color(0.8f, 0.7f, 0.5f, 1f);
        btn.colors = cb;
        btn.targetGraphic = bimg;
        btn.onClick.AddListener(OnRetryClicked);
        Text bt = MakeText(brt, "Chu", "Thử lại", 34, TextAnchor.MiddleCenter, textColor);
        Stretch(bt.rectTransform);

        deathGroup.alpha = 0f;
        deathGroup.blocksRaycasts = false;
        deathGroup.interactable = false;
        deathPanel.SetActive(false);
    }

    private void OnRetryClicked()
    {
        if (life != null) life.Respawn();
    }

    /// <summary>Nút bấm cần EventSystem; nếu scene chưa có thì tạo (dùng Input System mới).</summary>
    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.transform.SetParent(transform, false);
    }

    // ================= Cập nhật mỗi khung hình =================

    private void Update()
    {
        if (stats == null) return;
        UpdateBars();
        UpdateSwordInfo();
        UpdateWarnings();
        UpdateNumbers();
        UpdateEnemyBars();
        UpdateStones();
        UpdateDeathFade();
    }

    private void UpdateBars()
    {
        healthFill.fillAmount = Ratio(stats.Health, stats.MaxHealth);
        healthText.text = Mathf.CeilToInt(stats.Health) + "/" + Mathf.CeilToInt(stats.MaxHealth);

        manaFill.fillAmount = Ratio(stats.Mana, stats.MaxMana);
        manaText.text = Mathf.FloorToInt(stats.Mana) + "/" + Mathf.CeilToInt(stats.MaxMana);

        intentFill.fillAmount = Ratio(stats.SwordIntent, stats.MaxSwordIntent);
        intentText.text = Mathf.FloorToInt(stats.SwordIntent) + "/" + Mathf.CeilToInt(stats.MaxSwordIntent);

        // Kiếm ý đầy thì nhấp nháy vàng-trắng
        if (stats.IntentFull)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * intentPulseRate * Mathf.PI * 2f);
            intentFill.color = Color.Lerp(intentColor, new Color(1f, 0.97f, 0.8f), pulse);
            intentBack.color = Color.Lerp(new Color(0f, 0f, 0f, 0.55f), new Color(0.5f, 0.38f, 0.08f, 0.8f), pulse);
        }
        else
        {
            intentFill.color = intentColor;
            intentBack.color = new Color(0f, 0f, 0f, 0.55f);
        }

        if (stats.Realm != null) realmText.text = stats.Realm.DisplayName;
    }

    private void UpdateSwordInfo()
    {
        if (swarm == null) return;
        swordText.text = "Kiếm: " + swarm.ReadySwordCount + "/" + swarm.ActualSwordCount;

        int cur = (int)swarm.CurrentFormation;
        for (int i = 0; i < 3; i++)
        {
            bool on = i == cur;
            slotBack[i].color = on ? new Color(0.45f, 0.36f, 0.1f, 0.85f) : panelColor;
            slotLabel[i].color = on ? new Color(1f, 0.93f, 0.6f) : new Color(textColor.r, textColor.g, textColor.b, 0.6f);
        }
    }

    private void UpdateWarnings()
    {
        // Viền đỏ khi bị đánh
        float vt = (Time.unscaledTime - vignetteStart) / vignetteTime;
        float va = vt >= 0f && vt < 1f ? (1f - vt) * vignetteAlpha : 0f;
        // Sắp chết (máu thấp): viền đỏ nhạt đập theo nhịp tim
        if (stats.IsAlive && stats.MaxHealth > 0f && stats.Health / stats.MaxHealth < lowHealthThreshold)
        {
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * lowHealthPulseRate * Mathf.PI * 2f);
            va = Mathf.Max(va, lowHealthAlpha * (0.35f + 0.65f * beat));
        }
        vignetteRaw.color = new Color(1f, 1f, 1f, va);

        // Thiếu pháp lực: thanh nháy đỏ + dòng chữ nhỏ
        bool warn = Time.unscaledTime < manaWarnUntil;
        manaWarnText.gameObject.SetActive(warn);
        if (warn)
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 24f);
            manaFill.color = Color.Lerp(manaColor, new Color(1f, 0.15f, 0.12f), blink);
        }
        else manaFill.color = manaColor;
    }

    private void UpdateDeathFade()
    {
        float target = deathVisible ? 1f : 0f;
        deathFade = Mathf.MoveTowards(deathFade, target, Time.unscaledDeltaTime * 1.5f);
        deathGroup.alpha = deathFade;
        if (!deathVisible && deathFade <= 0f && deathPanel.activeSelf) deathPanel.SetActive(false);
    }

    private EnemyBarEntry NewEnemyBar()
    {
        EnemyBarEntry e = new EnemyBarEntry();
        GameObject go = new GameObject("ThanhMau", typeof(RectTransform));
        e.root = go.GetComponent<RectTransform>();
        e.root.SetParent(enemyBarLayer, false);
        e.root.anchorMin = e.root.anchorMax = Vector2.zero;
        e.root.pivot = new Vector2(0.5f, 0f);
        e.root.sizeDelta = new Vector2(enemyBarWidth, 8f);

        Image back = NewObject<Image>("Nen", e.root);
        back.sprite = whiteSprite;
        back.color = new Color(0f, 0f, 0f, 0.6f);
        back.raycastTarget = false;
        Stretch(back.rectTransform);
        AddFrame(e.root, borderColor, 1f);

        e.fill = NewObject<Image>("Day", back.rectTransform);
        e.fill.sprite = whiteSprite;
        e.fill.type = Image.Type.Filled;
        e.fill.fillMethod = Image.FillMethod.Horizontal;
        e.fill.color = enemyBarColor;
        e.fill.raycastTarget = false;
        Stretch(e.fill.rectTransform);

        e.nameText = MakeText(e.root, "Ten", "", 20, TextAnchor.LowerCenter, textColor);
        SetRect(e.nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(300f, 26f));
        e.nameText.rectTransform.pivot = new Vector2(0.5f, 0f);
        Outline o1 = e.nameText.gameObject.AddComponent<Outline>();
        o1.effectColor = new Color(0f, 0f, 0f, 0.8f); o1.effectDistance = new Vector2(1.5f, -1.5f);

        e.realmText = MakeText(e.root, "CanhGioi", "", 16, TextAnchor.UpperCenter, intentColor);
        SetRect(e.realmText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -3f), new Vector2(300f, 22f));
        e.realmText.rectTransform.pivot = new Vector2(0.5f, 1f);
        Outline o2 = e.realmText.gameObject.AddComponent<Outline>();
        o2.effectColor = new Color(0f, 0f, 0f, 0.8f); o2.effectDistance = new Vector2(1.5f, -1.5f);
        return e;
    }

    /// <summary>Thanh máu + tên + cảnh giới trên đầu từng kẻ địch còn sống, trong tầm và trước mặt camera.</summary>
    private void UpdateEnemyBars()
    {
        Camera cam = Camera.main;
        float scale = canvas.scaleFactor;
        int used = 0;
        if (cam != null)
        {
            Vector3 camPos = cam.transform.position;
            for (int i = 0; i < EnemyBase.All.Count; i++)
            {
                EnemyBase e = EnemyBase.All[i];
                if (e == null || !e.IsAlive) continue;
                if ((e.transform.position - stats.transform.position).sqrMagnitude > enemyBarRange * enemyBarRange) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.BarPosition);
                if (sp.z <= 0.1f) continue;

                while (enemyBars.Count <= used) enemyBars.Add(NewEnemyBar());
                EnemyBarEntry entry = enemyBars[used++];
                if (!entry.root.gameObject.activeSelf) entry.root.gameObject.SetActive(true);
                entry.root.anchoredPosition = new Vector2(sp.x / scale, sp.y / scale);
                entry.fill.fillAmount = e.HealthRatio;
                if (entry.nameText.text != e.DisplayName) entry.nameText.text = e.DisplayName;
                string realmName = e.RealmName;
                if (entry.realmText.text != realmName) entry.realmText.text = realmName;
            }
        }
        for (int i = used; i < enemyBars.Count; i++)
            if (enemyBars[i].root.gameObject.activeSelf) enemyBars[i].root.gameObject.SetActive(false);
    }

    private void UpdateStones()
    {
        stonesText.text = "Linh thạch: " + stats.SpiritStones;
        float t = (Time.unscaledTime - stonesPopupStart) / stonesPopupTime;
        if (t >= 0f && t < 1f)
        {
            if (!stonesPopup.gameObject.activeSelf) stonesPopup.gameObject.SetActive(true);
            stonesPopup.rectTransform.anchoredPosition = new Vector2(0f, 260f + Mathf.Sqrt(t) * 70f);
            Color c = stonesPopup.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            stonesPopup.color = c;
        }
        else if (stonesPopup.gameObject.activeSelf) stonesPopup.gameObject.SetActive(false);
    }

    private void OnStonesGained(int amount)
    {
        stonesPopup.text = "+" + amount + " linh thạch";
        stonesPopupStart = Time.unscaledTime;
    }

    // ================= Sự kiện =================

    private void OnPlayerDamaged(float amount) { vignetteStart = Time.unscaledTime; }

    private void OnManaInsufficient() { manaWarnUntil = Time.unscaledTime + manaWarnTime; }

    private void OnDeathScreen()
    {
        deathVisible = true;
        deathPanel.SetActive(true);
        deathGroup.blocksRaycasts = true;
        deathGroup.interactable = true;
    }

    private void OnRespawned()
    {
        deathVisible = false;
        deathFade = 0f;
        deathGroup.alpha = 0f;
        deathGroup.blocksRaycasts = false;
        deathGroup.interactable = false;
        deathPanel.SetActive(false);
        vignetteStart = -10f;
    }

    /// <summary>Có vật thể bị đánh: cho số sát thương bay lên tại điểm trúng.</summary>
    private void OnAnyDamaged(Damageable target, DamageInfo info)
    {
        FloatingNumber n = null;
        for (int i = 0; i < numbers.Count; i++)
            if (!numbers[i].active) { n = numbers[i]; break; }
        if (n == null)
        {
            if (numbers.Count >= 40) return;
            n = new FloatingNumber { text = MakeText(root, "SoSatThuong", "", 34, TextAnchor.MiddleCenter, Color.white) };
            n.text.raycastTarget = false;
            Outline o = n.text.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            o.effectDistance = new Vector2(2f, -2f);
            SetRect(n.text.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(160f, 50f));
            n.text.rectTransform.pivot = new Vector2(0.5f, 0.5f);   // số nằm giữa điểm trúng
            numbers.Add(n);
        }
        bool killed = !target.IsAlive;
        n.text.text = Mathf.RoundToInt(info.amount).ToString();
        n.text.fontSize = killed ? 46 : 34;
        n.text.color = killed ? new Color(1f, 0.82f, 0.3f) : Color.white;
        n.world = info.point + Vector3.up * 0.3f;
        n.start = Time.unscaledTime;
        n.active = true;
        n.text.gameObject.SetActive(true);
    }

    private void UpdateNumbers()
    {
        Camera cam = Camera.main;
        float scale = canvas.scaleFactor;
        for (int i = 0; i < numbers.Count; i++)
        {
            FloatingNumber n = numbers[i];
            if (!n.active) continue;
            float t = (Time.unscaledTime - n.start) / damageNumberTime;
            if (t >= 1f || cam == null)
            {
                n.active = false;
                n.text.gameObject.SetActive(false);
                continue;
            }
            Vector3 sp = cam.WorldToScreenPoint(n.world);
            if (sp.z <= 0f) { n.text.gameObject.SetActive(false); continue; }
            n.text.gameObject.SetActive(true);
            float rise = Mathf.Sqrt(t) * damageRise;       // bay nhanh lúc đầu rồi chậm dần
            n.text.rectTransform.anchoredPosition = new Vector2(sp.x / scale, (sp.y + rise) / scale);
            Color c = n.text.color;
            c.a = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;  // nửa sau thì mờ dần
            n.text.color = c;
        }
    }

    // ================= Hàm dựng nhỏ =================

    private static float Ratio(float v, float max) { return max > 0f ? Mathf.Clamp01(v / max) : 0f; }

    private static T NewObject<T>(string name, RectTransform parent) where T : Graphic
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        return go.GetComponent<T>();
    }

    private Text MakeText(RectTransform parent, string name, string content, int size, TextAnchor anchor, Color color)
    {
        Text t = NewObject<Text>(name, parent);
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Bảng nền tối mờ có viền mảnh vàng kim.</summary>
    private RectTransform MakePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        Image img = NewObject<Image>(name, parent);
        img.sprite = whiteSprite;
        img.color = panelColor;
        img.raycastTarget = false;
        // Góc dưới/trên trái: pivot cùng góc neo để vị trí tính từ mép màn hình
        img.rectTransform.pivot = new Vector2(anchorMin.x, anchorMin.y);
        SetRect(img.rectTransform, anchorMin, anchorMax, pos, size);
        AddFrame(img.rectTransform, borderColor, 1.5f);
        return img.rectTransform;
    }

    /// <summary>Viền mảnh bằng bốn đường kẻ (không làm nền bị tô thêm màu).</summary>
    private void AddFrame(RectTransform target, Color color, float thickness)
    {
        MakeLine(target, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);   // trên
        MakeLine(target, color, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));   // dưới
        MakeLine(target, color, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));   // trái
        MakeLine(target, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero); // phải
    }

    private void MakeLine(RectTransform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        Image line = NewObject<Image>("Vien", parent);
        line.sprite = whiteSprite;
        line.color = color;
        line.raycastTarget = false;
        RectTransform r = line.rectTransform;
        r.anchorMin = aMin;
        r.anchorMax = aMax;
        r.offsetMin = offMin;
        r.offsetMax = offMax;
    }

    /// <summary>Đặt neo và vị trí/kích thước (pivot lấy theo neo min).</summary>
    private static void SetRect(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = new Vector2(anchorMin.x, anchorMin.y);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
