using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 在屏幕上方居中显示：当前 Tower 等级 + 升到下一级所需资源。
/// 使用 TextMesh Pro 显示文字。挂到任意物体上；若不指定引用，会在运行时自动查找 Tower 与 PlayerInventory。
/// </summary>
public class TowerUpgradePanelUI : MonoBehaviour
{
    [Header("References (optional)")]
    [SerializeField] private Tower tower;
    [SerializeField] private PlayerInventory inventory;

    [Header("Optional: existing UI")]
    [Tooltip("若不指定，会在运行时自动创建上方居中的 Canvas 与面板。")]
    [SerializeField] private RectTransform panelRoot;
    [Tooltip("第 1 行：等级与下一级。")]
    [SerializeField] private TextMeshProUGUI textLine1;
    [Tooltip("第 2 行：Wood 和 Stone。")]
    [SerializeField] private TextMeshProUGUI textLine2;
    [Tooltip("第 3 行：Teeth 和 Gel。")]
    [SerializeField] private TextMeshProUGUI textLine3;

    private int _lastLevel = -999;

    private void Start()
    {
        if (tower == null)
            tower = FindObjectOfType<Tower>();

        if (inventory == null)
            inventory = FindObjectOfType<PlayerInventory>();

        if (tower == null)
        {
            Debug.LogWarning("TowerUpgradePanelUI: 未找到 Tower。请指定或确保场景中存在 Tower。");
            return;
        }

        if (panelRoot == null || textLine1 == null)
            CreateDefaultUI();

        if (inventory != null)
            inventory.OnResourceCountChanged.AddListener(OnInventoryChanged);

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnResourceCountChanged.RemoveListener(OnInventoryChanged);
    }

    private void Update()
    {
        if (tower == null) return;
        if (tower.CurrentLevel != _lastLevel)
            RefreshAll();
    }

    private void OnInventoryChanged(LootManager.ResourceType type, int count)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (tower == null) return;

        _lastLevel = tower.CurrentLevel;
        int nextLevel = Mathf.Min(3, tower.CurrentLevel + 1);
        bool isMax = tower.CurrentLevel >= 3;
        Tower.TowerUpgradeCost cost = tower.GetUpgradeCostForNextLevel();

        if (textLine1 != null)
            textLine1.text = isMax ? $"Tower Level: {tower.CurrentLevel}  |  Next: MAX" : $"Tower Level: {tower.CurrentLevel}  |  Next: Level {nextLevel}";

        if (textLine2 != null)
            textLine2.text = FormatPair(LootManager.ResourceType.Wood, cost.wood, LootManager.ResourceType.Stone, cost.stone);

        if (textLine3 != null)
            textLine3.text = FormatPair(LootManager.ResourceType.Teeth, cost.teeth, LootManager.ResourceType.Gel, cost.gel);
    }

    private string FormatPair(LootManager.ResourceType typeA, int needA, LootManager.ResourceType typeB, int needB)
    {
        int haveA = inventory != null ? inventory.GetCount(typeA) : 0;
        int haveB = inventory != null ? inventory.GetCount(typeB) : 0;
        if (inventory != null)
            return $"{TypeName(typeA)}: {haveA}/{needA}    {TypeName(typeB)}: {haveB}/{needB}";
        return $"{TypeName(typeA)}: {needA}    {TypeName(typeB)}: {needB}";
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
        var canvasGo = new GameObject("TowerUpgradeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("TowerUpgradePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -20f);
        panelRect.sizeDelta = new Vector2(420f, 120f);

        var img = panelGo.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        var layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        textLine1 = CreateRow(panelGo.transform, "Tower Level: 0  |  Next: Level 1", 24, TextAlignmentOptions.Center);
        textLine2 = CreateRow(panelGo.transform, "Wood: 0/0    Stone: 0/0", 22, TextAlignmentOptions.Center);
        textLine3 = CreateRow(panelGo.transform, "Teeth: 0/0    Gel: 0/0", 22, TextAlignmentOptions.Center);

        panelRoot = panelRect;
    }

    private static TextMeshProUGUI CreateRow(Transform parent, string label, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject("TMP_Text_" + label);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 28f);
        return tmp;
    }
}

