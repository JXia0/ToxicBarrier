using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime-created red screen-edge warning driven by nearby disguised parasites.</summary>
[DefaultExecutionOrder(1000)]
public class ParasiteWarningUI : MonoBehaviour
{
    private const float EdgeThickness = 48f;
    private const float MaxAlpha = 0.78f;

    private static ParasiteWarningUI _instance;

    private CanvasGroup _canvasGroup;
    private Transform _player;

    public static void EnsureExists()
    {
        if (_instance != null) return;

        GameObject warningObject = new GameObject(
            "ParasiteWarningUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        _instance = warningObject.AddComponent<ParasiteWarningUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        CreateEdge("Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, EdgeThickness));
        CreateEdge("Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, EdgeThickness));
        CreateEdge("Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(EdgeThickness, 0f));
        CreateEdge("Right", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(EdgeThickness, 0f));
    }

    private void LateUpdate()
    {
        if (_canvasGroup == null) return;

        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsPlaying)
        {
            _canvasGroup.alpha = 0f;
            return;
        }

        if (_player == null)
        {
            if (MapController.Instance != null && MapController.Instance.Player != null)
                _player = MapController.Instance.Player.transform;
            else
            {
                PlayerController player = FindObjectOfType<PlayerController>();
                if (player != null)
                    _player = player.transform;
            }
        }

        float intensity = _player == null
            ? 0f
            : DisguisedParasite.GetStrongestWarning(_player.position);
        float pulse = 0.78f + Mathf.Sin(Time.unscaledTime * 8f) * 0.22f;
        float targetAlpha = intensity * pulse * MaxAlpha;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 2.5f);
    }

    private void CreateEdge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        GameObject edgeObject = new GameObject(edgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        edgeObject.transform.SetParent(transform, false);

        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        Image image = edgeObject.GetComponent<Image>();
        image.color = new Color(0.9f, 0.02f, 0.02f, 1f);
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
