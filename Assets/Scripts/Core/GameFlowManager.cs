using System;
using System.Collections;
using System.Collections.Generic;
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
    private StoryIntroUI _storyIntro;
    private Tower _tower;
    private HealthSystem _towerHealth;
    private EnemySpawner[] _spawners = new EnemySpawner[0];
    public float SurvivalSeconds { get; private set; }
    public string EndReason { get; private set; } = string.Empty;
    private readonly HashSet<EnemySpawner> _rewardedRegions = new HashSet<EnemySpawner>();
    public string LatestSupplyNotice { get; private set; } = string.Empty;
    private float _supplyNoticeUntil;
    public bool ShowSupplyNotice => SurvivalSeconds < _supplyNoticeUntil && !string.IsNullOrEmpty(LatestSupplyNotice);
    private readonly List<RegionSupplyCache> _supplyCaches = new List<RegionSupplyCache>();
    public RegionSupplyCache ActiveSupplyCache
    {
        get { for (int i = 0; i < _supplyCaches.Count; i++) if (_supplyCaches[i] != null) return _supplyCaches[i]; return null; }
    }
    private sealed class Notice
    {
        public string Text;
        public float Remaining;
        public bool Urgent;
    }
    private Notice _notice;
    private readonly List<Notice> _pendingNotices = new List<Notice>();
    public string GameplayNotice => _notice?.Text ?? string.Empty;
    public bool ShowNotice => _notice != null;
    public bool ShowDangerNotice => _notice != null && _notice.Urgent;
    public void ShowGameplayNotice(string text, float seconds, bool urgent = false)
    {
        if (string.IsNullOrEmpty(text) || seconds <= 0) return;
        if (_notice != null && _notice.Text == text) return;
        foreach (var pending in _pendingNotices) if (pending.Text == text) return;
        var next = new Notice { Text = text, Remaining = seconds, Urgent = urgent };
        if (_notice == null) { _notice = next; return; }
        if (urgent && !_notice.Urgent)
        {
            _pendingNotices.Insert(0, _notice);
            _notice = next;
        }
        else if (urgent)
        {
            int index = _pendingNotices.FindIndex(n => !n.Urgent);
            _pendingNotices.Insert(index < 0 ? _pendingNotices.Count : index, next);
        }
        else _pendingNotices.Add(next);
        if (_pendingNotices.Count > 8) _pendingNotices.RemoveAt(_pendingNotices.Count - 1);
    }
    private void TickNotices(float deltaTime)
    {
        if (_notice == null || (!_notice.Urgent && SurvivalSeconds < 18)) return;
        _notice.Remaining -= deltaTime;
        if (_notice.Remaining > 0) return;
        _notice = _pendingNotices.Count > 0 ? _pendingNotices[0] : null;
        if (_pendingNotices.Count > 0) _pendingNotices.RemoveAt(0);
    }
    public bool IsRegionCleared(int region)
    { foreach (var spawner in _spawners) if (spawner != null && spawner.RegionLevel == region) return spawner.IsCleared; return false; }
    public int DefeatedEnemies
    {
        get
        {
            int total = 0;
            foreach (EnemySpawner spawner in _spawners)
                if (spawner != null) total += spawner.DefeatedCount;
            return total;
        }
    }
    public string RunSummary => $"用时 {Mathf.FloorToInt(SurvivalSeconds / 60):00}:{Mathf.FloorToInt(SurvivalSeconds % 60):00}    |    击败 {DefeatedEnemies}    |    塔 {(_tower != null ? _tower.CurrentLevel : 0)} 级";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (GetComponent<GameAudio>() == null) gameObject.AddComponent<GameAudio>();
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
        if (IsPlaying)
        {
            SurvivalSeconds += Time.deltaTime;
            TickNotices(Time.deltaTime);
            AwardRegionSupplies();
            CheckVictory();
        }
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (CurrentState == GameState.StoryIntro)
        {
            _storyIntro?.Skip();
            return;
        }
        if (GameSettingsUI.CloseIfOpen()) return;
        if (ExplorationMapUI.Instance != null && ExplorationMapUI.Instance.Close()) return;

        if (CurrentState == GameState.Playing)
            PauseGame();
        else if (CurrentState == GameState.Paused)
            ResumeGame();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && IsPlaying) PauseGame();
    }

    public void StartNewGame()
    {
        if (CurrentState != GameState.MainMenu) return;
        SetPlayerControl(false);
        Time.timeScale = 0f;
        SetState(GameState.StoryIntro);
        if (_storyIntro == null) _storyIntro = gameObject.AddComponent<StoryIntroUI>();
        _storyIntro.Play(() =>
        {
            if (CurrentState == GameState.StoryIntro) BeginSceneTransition(GameplaySceneName);
        });
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
        if (CurrentState == GameState.MainMenu || CurrentState == GameState.Transitioning || CurrentState == GameState.StoryIntro) return;
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
        EndRun(GameState.Dead, "幸存者倒下了。留意血量，及时返塔恢复。");
    }

    private void HandleTowerDeath()
    {
        EndRun(GameState.Dead, "净化塔已被摧毁。升级可以恢复塔的生命。");
    }

    private void CheckVictory()
    {
        if (_tower == null || !_tower.IsMaxLevel || _spawners.Length != 3) return;
        foreach (EnemySpawner spawner in _spawners)
            if (spawner == null || !spawner.IsCleared) return;
        EndRun(GameState.Won, "三区已净化，屏障重新亮起。");
    }

    // Finite early waves cannot produce all later costs (region 1 has no stone).
    // A physical cache guarantees missing supplies, but requires an actual collection trip.
    private void AwardRegionSupplies()
    {
        if (_tower == null || _playerController == null) return;
        PlayerInventory inventory = _playerController.GetComponent<PlayerInventory>();
        if (inventory == null) return;
        foreach (EnemySpawner spawner in _spawners)
        {
            if (spawner == null || !spawner.IsCleared || spawner.RegionLevel < 1 ||
                spawner.RegionLevel >= 3 || _rewardedRegions.Contains(spawner)) continue;
            _rewardedRegions.Add(spawner);
            _supplyCaches.Add(RegionSupplyCache.Spawn(_tower, spawner.RegionLevel));
            LatestSupplyNotice = $"第 {spawner.RegionLevel} 区已清空！前往金色标记领取补给。";
            _supplyNoticeUntil = SurvivalSeconds + 12f;
        }
    }

    public void NotifySupplyCollected(int region)
    {
        LatestSupplyNotice = $"已领取第 {region} 区补给并恢复 25 生命，返塔升级！";
        _supplyNoticeUntil = SurvivalSeconds + 12f;
    }

    private void EndRun(GameState result, string reason)
    {
        if (!IsPlaying) return;
        EndReason = reason;
        GameAudio.Play(result == GameState.Won ? "Victory" : "Defeat");
        if (result == GameState.Won)
        {
            float best = PlayerPrefs.GetFloat("BestClearSeconds", float.MaxValue);
            if (SurvivalSeconds < best) PlayerPrefs.SetFloat("BestClearSeconds", SurvivalSeconds);
            PlayerPrefs.SetInt("CompletedRuns", PlayerPrefs.GetInt("CompletedRuns", 0) + 1);
            PlayerPrefs.Save();
        }
        SetPlayerControl(false);
        Time.timeScale = 0f;
        SetState(result);
    }

    private void BeginSceneTransition(string sceneName)
    {
        if (CurrentState == GameState.Transitioning) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"GameFlowManager cannot load scene '{sceneName}'. Add it to Build Settings.", this);
            if (CurrentState == GameState.StoryIntro)
            {
                _storyIntro?.Cancel();
                Time.timeScale = 1f;
                SetState(GameState.MainMenu);
            }
            return;
        }

        _storyIntro?.Cancel();
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
        SurvivalSeconds = 0f;
        EndReason = string.Empty;
        LatestSupplyNotice = string.Empty;
        _supplyNoticeUntil = 0f;

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
        _tower = FindObjectOfType<Tower>();
        _towerHealth = _tower != null ? _tower.GetComponent<HealthSystem>() : null;
        if (_towerHealth != null)
            _towerHealth.OnDeath.AddListener(HandleTowerDeath);
        _spawners = FindObjectsOfType<EnemySpawner>();
        Array.Sort(_spawners, (a, b) => string.CompareOrdinal(a.name, b.name));

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
        if (_towerHealth != null)
            _towerHealth.OnDeath.RemoveListener(HandleTowerDeath);
        _towerHealth = null;
        _tower = null;
        _spawners = new EnemySpawner[0];
        _rewardedRegions.Clear();
        _supplyCaches.Clear();
        _notice = null; _pendingNotices.Clear();
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
