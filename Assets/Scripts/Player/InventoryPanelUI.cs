using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 在屏幕右上角显示玩家背包资源数量。使用 TextMesh Pro 显示文字。
/// 挂到任意物体上，在 Inspector 里指定 PlayerInventory；若留空则运行时自动查找。
/// </summary>
public class InventoryPanelUI : MonoBehaviour
{
    [Header("背包引用")]
    [SerializeField] private PlayerInventory inventory;

    [Header("可选：已有 UI 时指定")]
    [Tooltip("若不指定，会在运行时自动创建右上角 Canvas 与面板。")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TextMeshProUGUI textGel;
    [SerializeField] private TextMeshProUGUI textTeeth;
    [SerializeField] private TextMeshProUGUI textWood;
    [SerializeField] private TextMeshProUGUI textStone;

    private void Start()
    {
        if (inventory == null)
            inventory = FindObjectOfType<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogWarning("InventoryPanelUI: 未找到 PlayerInventory，请指定或确保场景中有带 PlayerInventory 的玩家。");
            return;
        }

        if (panelRoot == null || textGel == null)
            CreateDefaultUI();

        inventory.OnResourceCountChanged.AddListener(OnCountChanged);
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnResourceCountChanged.RemoveListener(OnCountChanged);
    }

    private void OnCountChanged(LootManager.ResourceType type, int count)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (inventory == null) return;
        SetText(textGel, LootManager.ResourceType.Gel);
        SetText(textTeeth, LootManager.ResourceType.Teeth);
        SetText(textWood, LootManager.ResourceType.Wood);
        SetText(textStone, LootManager.ResourceType.Stone);
    }

    private void SetText(TextMeshProUGUI text, LootManager.ResourceType type)
    {
        if (text == null) return;
        int count = inventory.GetCount(type);
        text.text = $"{TypeName(type)}: {count}";
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
        var canvasGo = new GameObject("InventoryCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("InventoryPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -20f);
        panelRect.sizeDelta = new Vector2(200f, 140f);
        var img = panelGo.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        var layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperRight;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        textGel = CreateRow(panelGo.transform, "Gel");
        textTeeth = CreateRow(panelGo.transform, "Teeth");
        textWood = CreateRow(panelGo.transform, "Wood");
        textStone = CreateRow(panelGo.transform, "Stone");

        panelRoot = panelRect;
    }

    private static TextMeshProUGUI CreateRow(Transform parent, string label)
    {
        var go = new GameObject("TMP_Text_" + label);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = label + ": 0";
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 22;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineRight;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 28f);
        return tmp;
    }
}
