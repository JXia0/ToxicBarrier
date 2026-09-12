using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Menu-only settings. Closing restores focus without resuming a paused run.</summary>
public sealed class GameSettingsUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectsSlider;
    [SerializeField] private TextMeshProUGUI musicValue;
    [SerializeField] private TextMeshProUGUI effectsValue;
    [SerializeField] private TextMeshProUGUI displayValue;
    [SerializeField] private TextMeshProUGUI bestRecord;
    [SerializeField] private TextMeshProUGUI languageValue;
    private GameObject _previousFocus;
    private CanvasGroup _parentMenu;
    private int _resolutionIndex;
    private bool _fullscreen;
    private bool _displayDirty;
    private static GameSettingsUI _open;
    public bool IsOpen => panel != null && panel.activeSelf;
    private static readonly Vector2Int[] Resolutions = { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };

    private void Start()
    {
        RefreshLanguageLabel();
        _resolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt("ResolutionIndex", 0), 0, 2);
        _fullscreen = PlayerPrefs.GetInt("Fullscreen", 0) == 1;
        musicSlider.onValueChanged.AddListener(_ => UpdateVolume());
        effectsSlider.onValueChanged.AddListener(_ => UpdateVolume());
        if (bestRecord != null)
        {
            float best = PlayerPrefs.GetFloat("BestClearSeconds", 0);
            bestRecord.text = GameText.Translate(best > 0 ? $"最快净化 {Mathf.FloorToInt(best / 60):00}:{Mathf.FloorToInt(best % 60):00}  |  通关 {PlayerPrefs.GetInt("CompletedRuns", 0)} 次" : "首次任务：把塔升至 3 级，清空三区敌人");
        }
#if !UNITY_EDITOR
        if (PlayerPrefs.HasKey("ResolutionIndex")) ApplyDisplay();
#endif
    }

    public void Open()
    {
        var flow = GameFlowManager.Instance;
        if (flow == null || (flow.CurrentState != GameState.MainMenu && flow.CurrentState != GameState.Paused)) return;
        _previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        GameObject parentMenu = GameObject.Find(flow.CurrentState == GameState.MainMenu ? "MainMenuPanel" : "PauseMenuPanel");
        if (parentMenu != null)
        {
            _parentMenu = parentMenu.GetComponent<CanvasGroup>();
            if (_parentMenu == null) _parentMenu = parentMenu.AddComponent<CanvasGroup>();
            _parentMenu.interactable = false; _parentMenu.blocksRaycasts = false;
        }
        _resolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt("ResolutionIndex", 0), 0, 2);
        _fullscreen = PlayerPrefs.GetInt("Fullscreen", 0) == 1;
        panel.SetActive(true); _open = this;
        musicSlider.SetValueWithoutNotify(GameAudio.Instance.MusicVolume);
        effectsSlider.SetValueWithoutNotify(GameAudio.Instance.EffectsVolume);
        _displayDirty = false;
        UpdateVolume(); RefreshDisplay();
        RefreshLanguageLabel();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(musicSlider.gameObject);
    }

    public void Close()
    {
        PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
        PlayerPrefs.SetFloat("EffectsVolume", effectsSlider.value);
        PlayerPrefs.Save(); panel.SetActive(false); _open = null;
        if (_parentMenu != null) { _parentMenu.interactable = true; _parentMenu.blocksRaycasts = true; }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_previousFocus);
    }

    public static bool CloseIfOpen()
    {
        if (_open == null || !_open.IsOpen) return false;
        _open.Close(); return true;
    }
    private void OnDestroy() { if (_open == this) _open = null; }
    public void ToggleLanguage()
    {
        GameText.SetEnglish(!GameText.English);
        RefreshLanguageLabel(); UpdateVolume(); RefreshDisplay();
        if (bestRecord != null)
        {
            float best = PlayerPrefs.GetFloat("BestClearSeconds", 0);
            bestRecord.text = GameText.Translate(best > 0 ? $"最快净化 {Mathf.FloorToInt(best / 60):00}:{Mathf.FloorToInt(best % 60):00}  |  通关 {PlayerPrefs.GetInt("CompletedRuns", 0)} 次" : "首次任务：把塔升至 3 级，清空三区敌人");
        }
    }
    private void RefreshLanguageLabel() { if (languageValue != null) languageValue.text = GameText.Translate(GameText.English ? "Language: English" : "语言：简体中文"); }
    private void UpdateVolume()
    {
        GameAudio.Instance?.SetVolumes(musicSlider.value, effectsSlider.value);
        musicValue.text = GameText.Translate(Mathf.RoundToInt(musicSlider.value * 100) + "%");
        effectsValue.text = GameText.Translate(Mathf.RoundToInt(effectsSlider.value * 100) + "%");
    }
    public void NextResolution() { _resolutionIndex = (_resolutionIndex + 1) % Resolutions.Length; _displayDirty = true; RefreshDisplay(); }
    public void ToggleFullscreen() { _fullscreen = !_fullscreen; _displayDirty = true; RefreshDisplay(); }
    public void ApplyDisplay()
    {
        var size = Resolutions[_resolutionIndex];
#if !UNITY_EDITOR
        Screen.SetResolution(size.x, size.y, _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        PlayerPrefs.SetInt("ResolutionIndex", _resolutionIndex);
        PlayerPrefs.SetInt("Fullscreen", _fullscreen ? 1 : 0);
        PlayerPrefs.Save(); _displayDirty = false; RefreshDisplay();
    }
    private void RefreshDisplay()
    {
        var size = Resolutions[_resolutionIndex];
        displayValue.text = GameText.Translate($"{size.x} × {size.y}  |  {(_fullscreen ? "全屏" : "窗口")}\n{(_displayDirty ? "点击应用显示设置以生效" : "显示设置已保存（编辑器中不改变窗口）")}");
#if !UNITY_EDITOR
        if (!_displayDirty) displayValue.text = GameText.Translate($"{size.x} × {size.y}  |  {(_fullscreen ? "全屏" : "窗口")}");
#endif
    }
}
