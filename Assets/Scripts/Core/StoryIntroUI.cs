using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Replayable, unscaled story before gameplay loads. Never advances the game simulation.</summary>
public sealed class StoryIntroUI : MonoBehaviour
{
    public const float SecondsPerImage = 5f;
    public const float FadeSeconds = .55f;
    public static readonly string[] ArtNames = { "01_TheFog", "02_TheFallen", "03_TheRefuge", "04_TheDefender" };
    public static readonly string[] Captions = {
        "一夜之间，毒雾从地下喷涌而出，吞没了中世纪的王国。",
        "哥布林占据废墟，死者走出坟墓。昔日的城镇陷入沉寂。",
        "唯有一座古塔仍在抵御毒雾，庇护最后的幸存者。",
        "你是它最后的守护者。踏入废墟，收集材料，修复高塔，守住希望。"
    };
    public const string SkipCaption = "跳过";
    public bool IsVisible => _root != null && _root.activeSelf;
    public int SlideIndex { get; private set; }
    public float SlideElapsed { get; private set; }
    private GameObject _root;
    private CanvasGroup _content;
    private RawImage _art;
    private AspectRatioFitter _aspect;
    private TextMeshProUGUI _caption, _skipLabel;
    private Material _subtitleMaterial;
    private Button _skip;
    private Texture2D[] _textures;
    private Action _onComplete;
    private int _openedFrame;

    public void Play(Action onComplete)
    {
        Cancel();
        if (_root == null) BuildView();
        _textures = new Texture2D[ArtNames.Length];
        for (int i = 0; i < ArtNames.Length; i++)
        {
            _textures[i] = Resources.Load<Texture2D>("StoryIntro/" + ArtNames[i]);
            if (_textures[i] == null) Debug.LogWarning("Missing story illustration: " + ArtNames[i], this);
        }
        _onComplete = onComplete;
        SlideIndex = 0;
        SlideElapsed = 0;
        _openedFrame = Time.frameCount;
        _skip.interactable = false; // Do not let the Start button's submit also skip this frame.
        _root.SetActive(true);
        ShowSlide();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        GameText.Changed += RefreshText;
    }

    private void Update()
    {
        if (!IsVisible || Time.frameCount == _openedFrame) return;
        _skip.interactable = true;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Skip();
            return;
        }
        SlideElapsed += Time.unscaledDeltaTime;
        if (SlideElapsed >= SecondsPerImage)
        {
            SlideIndex++;
            if (SlideIndex >= ArtNames.Length) { Complete(); return; }
            SlideElapsed = 0;
            ShowSlide();
        }
        _content.alpha = Mathf.Clamp01(Mathf.Min(SlideElapsed, SecondsPerImage - SlideElapsed) / FadeSeconds);
    }

    public void Skip()
    {
        if (IsVisible && Time.frameCount > _openedFrame) Complete();
    }

    private void Complete()
    {
        Action callback = _onComplete;
        Cancel(); // Clear first: repeated clicks cannot load the scene twice.
        callback?.Invoke();
    }

    public void Cancel()
    {
        _onComplete = null;
        GameText.Changed -= RefreshText;
        if (_root != null) _root.SetActive(false);
        if (_art != null) _art.texture = null;
        if (_textures != null)
            foreach (Texture2D texture in _textures)
                if (texture != null) Resources.UnloadAsset(texture);
        _textures = null;
    }

    private void ShowSlide()
    {
        _art.texture = _textures[SlideIndex];
        _aspect.aspectRatio = _art.texture != null ? (float)_art.texture.width / _art.texture.height : 16f / 9f;
        _content.alpha = 0;
        RefreshText();
    }

    private void RefreshText()
    {
        if (!IsVisible) return;
        _caption.text = GameText.Translate(Captions[SlideIndex]);
        _skipLabel.text = GameText.Translate(SkipCaption);
    }

    private void BuildView()
    {
        _root = new GameObject("StoryIntroCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _root.transform.SetParent(transform, false);
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        Panel("Backdrop", _root.transform, Vector2.zero, Vector2.one, Color.black).raycastTarget = true;
        RectTransform content = Rect("Story", _root.transform, Vector2.zero, Vector2.one);
        _content = content.gameObject.AddComponent<CanvasGroup>();
        RectTransform artwork = Rect("Illustration", content, Vector2.zero, Vector2.one);
        _art = artwork.gameObject.AddComponent<RawImage>();
        _art.raycastTarget = false;
        _aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
        _aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _caption = Label("Caption", content, new Vector2(.12f, .045f), new Vector2(.88f, .145f), 34, new Color(.95f, .91f, .82f));
        // Shadow belongs only to this caption; never alter the shared game font material.
        _subtitleMaterial = new Material(_caption.fontSharedMaterial);
        _subtitleMaterial.EnableKeyword("UNDERLAY_ON");
        _subtitleMaterial.SetColor("_UnderlayColor", new Color(0, 0, 0, .9f));
        _subtitleMaterial.SetFloat("_UnderlayOffsetX", .65f);
        _subtitleMaterial.SetFloat("_UnderlayOffsetY", -.65f);
        _subtitleMaterial.SetFloat("_UnderlayDilate", .12f);
        _subtitleMaterial.SetFloat("_UnderlaySoftness", .2f);
        _caption.fontSharedMaterial = _subtitleMaterial;
        Image button = Panel("SkipStory", _root.transform, new Vector2(.91f, .92f), new Vector2(.985f, .977f), Color.clear);
        button.raycastTarget = true;
        _skip = button.gameObject.AddComponent<Button>();
        _skipLabel = Label("Label", button.transform, new Vector2(.03f, .06f), new Vector2(.97f, .94f), 22, Color.white);
        _skip.targetGraphic = _skipLabel;
        ColorBlock colors = _skip.colors;
        colors.normalColor = new Color(.65f, .67f, .66f);
        colors.highlightedColor = new Color(.9f, .91f, .89f);
        colors.selectedColor = colors.highlightedColor;
        _skip.colors = colors;
        _skip.onClick.AddListener(Skip);
        _root.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        Image image = Rect(name, parent, min, max).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, Vector2 min, Vector2 max, float size, Color color)
    {
        var label = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>("Presentation/Chinese");
        label.fontSize = size; label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        return label;
    }

    private void OnDestroy()
    {
        Cancel();
        if (_subtitleMaterial != null) Destroy(_subtitleMaterial);
    }
}
