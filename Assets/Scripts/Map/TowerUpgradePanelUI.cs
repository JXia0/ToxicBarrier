using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shows the tower upgrade panel only while the player is inside the tower trigger.
/// Materials are checked and consumed only when the upgrade button is pressed.
/// </summary>
public class TowerUpgradePanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tower tower;
    [SerializeField] private PlayerInventory inventory;

    [Header("Optional existing UI")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TextMeshProUGUI textLine1;
    [SerializeField] private TextMeshProUGUI textLine2;
    [SerializeField] private TextMeshProUGUI textLine3;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI woodCostText;
    [SerializeField] private TextMeshProUGUI stoneCostText;
    [SerializeField] private TextMeshProUGUI gelCostText;
    [SerializeField] private TextMeshProUGUI teethCostText;
    [SerializeField] private Image towerPreview;
    [SerializeField] private Sprite[] towerSprites;

    private PlayerController _playerController;
    private bool _upgradeInProgress;
    private GameFlowManager _flow;

    private void Start()
    {
        if (tower == null)
            tower = FindObjectOfType<Tower>();
        if (inventory == null)
            inventory = FindObjectOfType<PlayerInventory>();

        if (inventory != null)
            _playerController = inventory.GetComponent<PlayerController>();
        if (_playerController == null)
            _playerController = FindObjectOfType<PlayerController>();

        if (tower == null || inventory == null)
        {
            Debug.LogWarning("TowerUpgradePanelUI requires a Tower and PlayerInventory.", this);
            return;
        }

        if (panelRoot == null || textLine1 == null)
            CreateDefaultUI();
        else if (upgradeButton == null)
            CreateUpgradeButton(panelRoot);

        EnsureEventSystem();

        tower.OnUpgradeRangeChanged += HandleUpgradeRangeChanged;
        tower.OnLevelChanged += RefreshAll;
        inventory.OnResourceCountChanged.AddListener(OnInventoryChanged);
        upgradeButton.onClick.AddListener(HandleUpgradeClicked);
        _flow = GameFlowManager.Instance;
        if (_flow != null) _flow.OnStateChanged += HandleFlowChanged;

        RefreshAll();
        SetPanelVisible(tower.IsPlayerInUpgradeRange);
    }

    private void OnDestroy()
    {
        if (_flow != null) _flow.OnStateChanged -= HandleFlowChanged;
        if (tower != null)
        {
            tower.OnUpgradeRangeChanged -= HandleUpgradeRangeChanged;
            tower.OnLevelChanged -= RefreshAll;
        }

        if (inventory != null)
            inventory.OnResourceCountChanged.RemoveListener(OnInventoryChanged);
        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && upgradeButton != null && upgradeButton.interactable &&
            panelRoot != null && panelRoot.gameObject.activeInHierarchy)
            HandleUpgradeClicked();
    }

    private void HandleFlowChanged(GameState previous, GameState next)
    {
        SetPanelVisible(tower != null && tower.IsPlayerInUpgradeRange);
        if (next == GameState.Playing) RefreshAll();
    }

    private void HandleUpgradeRangeChanged(bool isNear)
    {
        if (isNear)
        {
            if (statusText != null) statusText.text = GameText.Translate(string.Empty);
            RefreshAll();
        }
        SetPanelVisible(isNear);
    }

    private void HandleUpgradeClicked()
    {
        if (_upgradeInProgress || (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsPlaying))
            return;

        if (!tower.CanUpgrade(inventory))
        {
            RefreshAll();
            if (statusText != null)
                statusText.text = GameText.Translate(tower.IsMaxLevel ? "净化塔已满级" : "材料不足");
            return;
        }

        _upgradeInProgress = true;
        RefreshAll();
        if (statusText != null)
            statusText.text = GameText.Translate("正在升级…");

        if (_playerController == null ||
            !_playerController.PlayTowerUpgradeAnimation(tower.transform.position, CompleteUpgrade))
            CompleteUpgrade();
    }

    private void CompleteUpgrade()
    {
        bool upgraded = (_flow == null || _flow.IsPlaying) && tower != null && tower.TryUpgrade(inventory);
        _upgradeInProgress = false;
        RefreshAll();

        if (statusText != null)
            statusText.text = GameText.Translate(upgraded ? "升级成功" : "升级已取消");
    }

    private void OnInventoryChanged(LootManager.ResourceType type, int count)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (tower == null || inventory == null || textLine1 == null) return;
        int nextLevel = tower.NextLevel;
        bool isMax = tower.IsMaxLevel;
        Tower.TowerUpgradeCost cost = tower.GetUpgradeCostForNextLevel();

        textLine1.text = GameText.Translate(isMax
            ? $"净化塔 {tower.CurrentLevel} 级  |  已满级"
            : $"净化塔 {tower.CurrentLevel} 级  →  {nextLevel} 级");
        if (woodCostText != null)
        {
            Tower.TowerLevelStats now = tower.CurrentStats;
            Tower.TowerLevelStats next = tower.GetLevelStats(nextLevel);
            textLine2.text = GameText.Translate(isMax ? $"攻击 {now.atk:0}    生命 {now.hp:0}" : $"攻击 {now.atk:0} → {next.atk:0}   生命 {now.hp:0} → {next.hp:0}");
            textLine3.text = GameText.Translate(isMax ? "守住净化塔，清空三区所有波次和敌人。" :
                (nextLevel >= 2 ? $"解锁第 {nextLevel} 区，恢复玩家与塔的全部生命。" : "强化防御，恢复玩家与塔的全部生命。"));
            SetCost(woodCostText, LootManager.ResourceType.Wood, cost.wood);
            SetCost(stoneCostText, LootManager.ResourceType.Stone, cost.stone);
            SetCost(gelCostText, LootManager.ResourceType.Gel, cost.gel);
            SetCost(teethCostText, LootManager.ResourceType.Teeth, cost.teeth);
            if (towerPreview != null && towerSprites != null && towerSprites.Length > nextLevel)
                towerPreview.sprite = towerSprites[nextLevel];
        }
        else
        {
            textLine2.text = GameText.Translate(FormatPair(LootManager.ResourceType.Wood, cost.wood, LootManager.ResourceType.Stone, cost.stone));
            textLine3.text = GameText.Translate(FormatPair(LootManager.ResourceType.Teeth, cost.teeth, LootManager.ResourceType.Gel, cost.gel));
        }

        upgradeButton.interactable = !isMax && !_upgradeInProgress && tower.CanUpgrade(inventory) && (_flow == null || _flow.IsPlaying);
        upgradeButtonText.text = GameText.Translate(isMax ? "已达满级" : (_upgradeInProgress ? "正在升级…" : "升级净化塔 [E]"));
        if (statusText != null && !_upgradeInProgress)
            statusText.text = GameText.Translate(isMax ? "最终防线已就绪" : tower.CanUpgrade(inventory) ? "材料已备齐" : "采集材料，或清区后领取补给");
    }

    private void SetCost(TextMeshProUGUI label, LootManager.ResourceType type, int required)
    {
        if (label == null) return;
        int held = inventory.GetCount(type);
        label.text = GameText.Translate(tower.IsMaxLevel ? $"{TypeName(type)}  {held}" : $"{TypeName(type)}  {held} / {required}");
        label.color = held < required ? new Color(1f, 0.55f, 0.45f) : new Color(0.84f, 0.95f, 0.73f);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.gameObject.SetActive(visible && (_flow == null || _flow.IsPlaying));
    }

    private string FormatPair(LootManager.ResourceType typeA, int needA, LootManager.ResourceType typeB, int needB)
    {
        return $"{TypeName(typeA)}: {inventory.GetCount(typeA)}/{needA}    " +
               $"{TypeName(typeB)}: {inventory.GetCount(typeB)}/{needB}";
    }

    private static string TypeName(LootManager.ResourceType type)
    {
        switch (type)
        {
            case LootManager.ResourceType.Gel: return "凝胶";
            case LootManager.ResourceType.Teeth: return "尖牙";
            case LootManager.ResourceType.Wood: return "木材";
            case LootManager.ResourceType.Stone: return "石料";
            default: return type.ToString();
        }
    }

    private void CreateDefaultUI()
    {
        var canvasGo = new GameObject(
            "TowerUpgradeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panelGo = new GameObject(
            "TowerUpgradePanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelRoot = panelGo.GetComponent<RectTransform>();
        panelRoot.anchorMin = new Vector2(0.5f, 1f);
        panelRoot.anchorMax = new Vector2(0.5f, 1f);
        panelRoot.pivot = new Vector2(0.5f, 1f);
        panelRoot.anchoredPosition = new Vector2(0f, -20f);
        panelRoot.sizeDelta = new Vector2(420f, 220f);

        var image = panelGo.GetComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        var layout = panelGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        textLine1 = CreateRow(panelGo.transform, 24);
        textLine2 = CreateRow(panelGo.transform, 22);
        textLine3 = CreateRow(panelGo.transform, 22);
        CreateUpgradeButton(panelRoot);
        statusText = CreateRow(panelGo.transform, 20);
        statusText.color = new Color(1f, 0.85f, 0.25f);
    }

    private void CreateUpgradeButton(Transform parent)
    {
        var buttonGo = new GameObject(
            "UpgradeButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonGo.transform.SetParent(parent, false);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.15f, 0.55f, 0.25f, 1f);
        upgradeButton = buttonGo.GetComponent<Button>();
        buttonGo.GetComponent<LayoutElement>().preferredHeight = 38f;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(buttonGo.transform, false);
        upgradeButtonText = textGo.GetComponent<TextMeshProUGUI>();
        upgradeButtonText.text = GameText.Translate("Upgrade");
        if (TMP_Settings.defaultFontAsset != null)
            upgradeButtonText.font = TMP_Settings.defaultFontAsset;
        upgradeButtonText.fontSize = 22f;
        upgradeButtonText.fontStyle = FontStyles.Bold;
        upgradeButtonText.alignment = TextAlignmentOptions.Center;
        upgradeButtonText.color = Color.white;

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateRow(Transform parent, int fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
        return text;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }
}
