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

    private void Start()
    {
        if (tower == null)
            tower = FindObjectOfType<Tower>();
        if (inventory == null)
            inventory = FindObjectOfType<PlayerInventory>();

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

        RefreshAll();
        SetPanelVisible(tower.IsPlayerInUpgradeRange);
    }

    private void OnDestroy()
    {
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

    private void HandleUpgradeRangeChanged(bool isNear)
    {
        if (isNear)
        {
            if (statusText != null) statusText.text = string.Empty;
            RefreshAll();
        }
        SetPanelVisible(isNear);
    }

    private void HandleUpgradeClicked()
    {
        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsPlaying)
            return;

        bool upgraded = tower.TryUpgrade(inventory);
        RefreshAll();

        if (statusText != null)
            statusText.text = upgraded ? "Upgrade successful" : "Not enough materials";
    }

    private void OnInventoryChanged(LootManager.ResourceType type, int count)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        int nextLevel = tower.NextLevel;
        bool isMax = tower.IsMaxLevel;
        Tower.TowerUpgradeCost cost = tower.GetUpgradeCostForNextLevel();

        textLine1.text = isMax
            ? $"Tower Level: {tower.CurrentLevel}  |  MAX"
            : $"Tower Level: {tower.CurrentLevel}  |  Next: Level {nextLevel}";
        textLine2.text = FormatPair(LootManager.ResourceType.Wood, cost.wood, LootManager.ResourceType.Stone, cost.stone);
        textLine3.text = FormatPair(LootManager.ResourceType.Teeth, cost.teeth, LootManager.ResourceType.Gel, cost.gel);

        upgradeButton.interactable = !isMax;
        upgradeButtonText.text = isMax ? "MAX LEVEL" : "Upgrade";
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.gameObject.SetActive(visible);
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
            case LootManager.ResourceType.Gel: return "Gel";
            case LootManager.ResourceType.Teeth: return "Teeth";
            case LootManager.ResourceType.Wood: return "Wood";
            case LootManager.ResourceType.Stone: return "Stone";
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
        upgradeButtonText.text = "Upgrade";
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
