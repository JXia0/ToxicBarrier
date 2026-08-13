using UnityEngine;

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
        SetActive(mainMenuPanel, state == GameState.MainMenu);
        SetActive(pausePanel, state == GameState.Paused);
        SetActive(deathPanel, state == GameState.Dead);
        SetActive(loadingPanel, state == GameState.Transitioning);
        SetActive(pauseButton, state == GameState.Playing);
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
