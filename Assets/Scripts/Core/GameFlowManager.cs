using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns top-level game state, scene transitions, pause time, player death, and flow UI.
/// Gameplay systems expose capabilities to this manager but never load scenes themselves.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameFlowManager : MonoBehaviour
{
    public const string MainMenuSceneName = "MainMenu";
    public const string GameplaySceneName = "GameScene";

    public static GameFlowManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Transitioning;
    public bool IsPlaying => CurrentState == GameState.Playing;

    public event Action<GameState, GameState> OnStateChanged;

    private PlayerController _playerController;
    private HealthSystem _playerHealth;
    private GameFlowUI _flowUI;
    private int _initializedSceneHandle = -1;
    private bool _isQuitting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded)
            InitializeScene(activeScene);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (CurrentState == GameState.Playing)
            PauseGame();
        else if (CurrentState == GameState.Paused)
            ResumeGame();
    }

    public void StartNewGame()
    {
        if (CurrentState != GameState.MainMenu) return;
        BeginSceneTransition(GameplaySceneName);
    }

    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;

        SetPlayerControl(false);
        SetState(GameState.Paused);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;

        Time.timeScale = 1f;
        SetPlayerControl(true);
        SetState(GameState.Playing);
    }

    public void RestartGame()
    {
        if (CurrentState == GameState.MainMenu || CurrentState == GameState.Transitioning) return;
        BeginSceneTransition(GameplaySceneName);
    }

    public void ReturnToMainMenu()
    {
        if (CurrentState == GameState.MainMenu || CurrentState == GameState.Transitioning) return;
        BeginSceneTransition(MainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RegisterUI(GameFlowUI flowUI)
    {
        if (flowUI == null) return;

        _flowUI = flowUI;
        _flowUI.ShowState(CurrentState);
    }

    public void UnregisterUI(GameFlowUI flowUI)
    {
        if (_flowUI == flowUI)
            _flowUI = null;
    }

    private void HandlePlayerDeath()
    {
        if (CurrentState != GameState.Playing) return;

        SetPlayerControl(false);
        SetState(GameState.Dead);
        Time.timeScale = 0f;
    }

    private void BeginSceneTransition(string sceneName)
    {
        if (CurrentState == GameState.Transitioning) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"GameFlowManager cannot load scene '{sceneName}'. Add it to Build Settings.", this);
            return;
        }

        SetPlayerControl(false);
        UnbindPlayer();
        Time.timeScale = 1f;
        SetState(GameState.Transitioning);
        StartCoroutine(LoadScene(sceneName));
    }

    private static IEnumerator LoadScene(string sceneName)
    {
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (loadOperation != null && !loadOperation.isDone)
            yield return null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeScene(scene);
    }

    private void InitializeScene(Scene scene)
    {
        if (_initializedSceneHandle == scene.handle) return;
        _initializedSceneHandle = scene.handle;

        Time.timeScale = 1f;
        UnbindPlayer();

        if (scene.name == MainMenuSceneName)
        {
            SetState(GameState.MainMenu);
            return;
        }

        BindPlayer();
        SetPlayerControl(true);
        SetState(GameState.Playing);
    }

    private void BindPlayer()
    {
        _playerController = FindObjectOfType<PlayerController>();

        HealthSystem[] healthSystems = FindObjectsOfType<HealthSystem>();
        foreach (HealthSystem health in healthSystems)
        {
            if (health != null && health.IsPlayer)
            {
                _playerHealth = health;
                break;
            }
        }

        if (_playerHealth != null)
            _playerHealth.OnDeath.AddListener(HandlePlayerDeath);
        else
            Debug.LogWarning("GameFlowManager did not find a player HealthSystem in the gameplay scene.", this);
    }

    private void UnbindPlayer()
    {
        if (_playerHealth != null)
            _playerHealth.OnDeath.RemoveListener(HandlePlayerDeath);

        _playerHealth = null;
        _playerController = null;
    }

    private void SetPlayerControl(bool enabled)
    {
        if (_playerController == null)
            _playerController = FindObjectOfType<PlayerController>();

        if (_playerController != null)
            _playerController.SetControlEnabled(enabled);
    }

    private void SetState(GameState nextState)
    {
        if (CurrentState == nextState) return;

        GameState previousState = CurrentState;
        CurrentState = nextState;
        _flowUI?.ShowState(nextState);
        OnStateChanged?.Invoke(previousState, nextState);
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindPlayer();
        if (!_isQuitting)
            Time.timeScale = 1f;
        Instance = null;
    }
}
