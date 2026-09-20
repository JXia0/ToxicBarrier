using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime-created red screen-edge warning driven by nearby disguised parasites.</summary>
[DefaultExecutionOrder(1000)]
public class ParasiteWarningUI : MonoBehaviour
{
    private const float MaxAlpha = 0.68f;

    private static ParasiteWarningUI _instance;

    private CanvasGroup _canvasGroup;
    private Transform _player;
    private float _proximity;
    private float _fadeVelocity;
    private float _pulsePhase;
    private bool _explained;

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
        // Above the world, below the health bars and menus.
        canvas.sortingOrder = 5;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        GameObject vignette = new GameObject("SoftWarning", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(ParasiteVignetteGraphic));
        vignette.transform.SetParent(transform, false);
        RectTransform rect = vignette.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (_canvasGroup == null) return;

        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsPlaying)
        {
            _canvasGroup.alpha = 0f;
            _proximity = _fadeVelocity = _pulsePhase = 0f;
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
        float target = Mathf.SmoothStep(0f, 1f, intensity);
        if (!_explained && intensity > .1f && GameFlowManager.Instance != null)
        {
            _explained = true;
            GameFlowManager.Instance.ShowGameplayNotice("隐藏怪靠近  |  红色边缘越强越危险，绕开可疑掉落物", 8, true);
        }
        _proximity = Mathf.SmoothDamp(_proximity, target, ref _fadeVelocity,
            target > _proximity ? 0.2f : 0.5f, Mathf.Infinity, Time.unscaledDeltaTime);
        _pulsePhase = Mathf.Repeat(_pulsePhase + Time.unscaledDeltaTime *
            Mathf.Lerp(0.6f, 1.05f, _proximity) * Mathf.PI * 2f, Mathf.PI * 2f);
        float pulse = 0.86f + Mathf.Sin(_pulsePhase) * 0.14f;
        _canvasGroup.alpha = _proximity * pulse * MaxAlpha;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
