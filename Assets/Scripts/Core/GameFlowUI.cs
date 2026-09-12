using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Scene-authored view for Main Menu, Pause Menu, Death UI, loading state, and pause button.
/// All referenced Canvas, panels, and buttons are created and saved by GameFlowSetupTool.
/// </summary>
[DefaultExecutionOrder(-900)]
public class GameFlowUI : MonoBehaviour
{
    [Header("Scene UI")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI deathReason;
    [SerializeField] private TextMeshProUGUI deathSummary;
    [SerializeField] private TextMeshProUGUI victorySummary;

    private void Start()
    {
        RegisterWithManager();
    }

    private void OnEnable()
    {
        RegisterWithManager();
    }

    private void OnDisable()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.UnregisterUI(this);
    }

    private void RegisterWithManager()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.RegisterUI(this);
    }

    public void ShowState(GameState state)
    {
        GameSettingsUI.CloseIfOpen();
        SetActive(mainMenuPanel, state == GameState.MainMenu);
        SetActive(pausePanel, state == GameState.Paused);
        SetActive(deathPanel, state == GameState.Dead);
        SetActive(loadingPanel, state == GameState.Transitioning);
        SetActive(pauseButton, state == GameState.Playing);
        SetActive(victoryPanel, state == GameState.Won);
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null)
        {
            if (deathReason != null) deathReason.text = GameText.Translate(flow.EndReason);
            if (deathSummary != null) deathSummary.text = GameText.Translate(flow.RunSummary);
            if (victorySummary != null) victorySummary.text = GameText.Translate(flow.RunSummary);
        }
        GameObject activePanel = state == GameState.MainMenu ? mainMenuPanel :
            state == GameState.Paused ? pausePanel : state == GameState.Dead ? deathPanel :
            state == GameState.Won ? victoryPanel : null;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            Button first = activePanel != null ? activePanel.GetComponentInChildren<Button>() : null;
            if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    public void StartNewGame()
    {
        GameFlowManager.Instance?.StartNewGame();
    }

    public void PauseGame()
    {
        GameFlowManager.Instance?.PauseGame();
    }

    public void ResumeGame()
    {
        GameFlowManager.Instance?.ResumeGame();
    }

    public void RestartGame()
    {
        GameFlowManager.Instance?.RestartGame();
    }

    public void ReturnToMainMenu()
    {
        GameFlowManager.Instance?.ReturnToMainMenu();
    }

    public void QuitGame()
    {
        GameFlowManager.Instance?.QuitGame();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
