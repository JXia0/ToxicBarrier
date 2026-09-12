using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in full authored-wave playthrough. No damage, resource, wave or invincibility cheats.</summary>
[InitializeOnLoad]
public static class FullRunReviewChecks
{
    private const string Key = "ToxicBarrier.FullReview";
    private static IEnumerator _run;
    private static int _frame;
    private static double _deadline;
    private static string _errors;
    static FullRunReviewChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.isPaused = false;
                PlaythroughPreferences.Capture(Key);
                Application.runInBackground = true;
                _errors = ""; _frame = -1; _deadline = EditorApplication.timeSinceStartup + 360;
                Application.logMessageReceived += Log;
                _run = Run();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                PlaythroughPreferences.Restore(Key);
                Application.runInBackground = false;
                SessionState.SetBool(Key, false);
                EditorSceneManager.OpenScene(SessionState.GetString(Key + ".Scene", "Assets/Scenes/GameScene.unity"));
            }
        };
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) _errors += message + "\n";
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            if (File.Exists("Temp/ToxicBarrierFlow.polish"))
            {
                File.Delete("Temp/ToxicBarrierFlow.polish");
                PolishCopy();
            }
            if (File.Exists("Temp/ToxicBarrierFlow.fulltest"))
            {
                File.Delete("Temp/ToxicBarrierFlow.fulltest");
                EditorSceneManager.SaveOpenScenes();
                SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
                SessionState.SetBool(Key, true);
                Directory.CreateDirectory(".utmp/full-review");
                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                EditorApplication.isPlaying = true;
            }
        }
        if (_run == null || !EditorApplication.isPlaying) return;
        if (!string.IsNullOrEmpty(_errors)) { Finish("FAIL\n" + _errors); return; }
        if (EditorApplication.timeSinceStartup > _deadline) { Finish("FAIL\nFull run timed out. See progress for remaining enemies and paths."); return; }
        if (_frame == Time.frameCount) return;
        _frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("Full run timed out. See progress for remaining enemies and paths.");
            if (!_run.MoveNext()) Finish("PASS\n" + File.ReadAllText(".utmp/full-review/progress.txt"));
        }
        catch (Exception e) { Finish("FAIL\n" + e + "\n" + _errors); }
    }
    private static void Finish(string result)
    {
        _run = null;
        Application.logMessageReceived -= Log;
        File.WriteAllText(".utmp/full-review/report.txt", result);
        Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }
    private static IEnumerator Run()
    {
        for (int i = 0; i < 10; i++) yield return null;
        UnityEngine.Random.InitState(20260912);
        GameObject.Find("StartGameButton").GetComponent<Button>().onClick.Invoke();
        while (!GameFlowManager.Instance.IsPlaying) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        var tower = UnityEngine.Object.FindObjectOfType<Tower>();
        var player = MapController.Instance.Player;
        // One initial placement defines the stationary defense route; all subsequent combat is real.
        player.transform.position = tower.transform.position + Vector3.down * .5f;
        player.GetComponent<Rigidbody2D>().position = player.transform.position;
        Physics2D.SyncTransforms();
        var inventory = player.GetComponent<PlayerInventory>();
        var spawners = UnityEngine.Object.FindObjectsOfType<EnemySpawner>().OrderBy(s => s.RegionLevel).ToArray();
        var panel = UnityEngine.Object.FindObjectOfType<TowerUpgradePanelUI>();
        var button = (Button)new SerializedObject(panel).FindProperty("upgradeButton").objectReferenceValue;
        float nextLog = 0;
        Vector3 home = player.transform.position;
        Vector3 lastGoal = Vector3.one * 10000;
        NavMeshPath route = new NavMeshPath();
        int corner = 0;
        int collectedCaches = 0;
        bool travellingToCache = false;
        while (GameFlowManager.Instance.IsPlaying || GameFlowManager.Instance.CurrentState == GameState.Paused)
        {
            // Keep this unattended test running if the editor loses focus; production remains paused.
            if (GameFlowManager.Instance.CurrentState == GameState.Paused) GameFlowManager.Instance.ResumeGame();
            Time.timeScale = 4; // Accelerates the clock only; authored wave budgets/delays and combat stats stay intact.
            var cache = GameFlowManager.Instance.ActiveSupplyCache;
            Vector3 goal = cache != null ? cache.transform.position : home;
            if (travellingToCache && cache == null) collectedCaches++;
            travellingToCache = cache != null;
            if (Vector2.Distance(goal, lastGoal) > .1f)
            {
                if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit start, 2, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(goal, out NavMeshHit end, 2, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, route) || route.status != NavMeshPathStatus.PathComplete)
                    throw new Exception("Cannot walk to supply/home at " + goal);
                corner = 1; lastGoal = goal;
            }
            while (corner < route.corners.Length && Vector2.Distance(player.transform.position, route.corners[corner]) < .3f) corner++;
            Vector3 waypoint = corner < route.corners.Length ? route.corners[corner] : goal;
            player.SetReviewMovement(Vector2.Distance(player.transform.position, goal) < .2f ? Vector2.zero : ((Vector2)(waypoint - player.transform.position)).normalized);
            if (tower.CanUpgrade(inventory) && button.interactable) button.onClick.Invoke();
            if (Time.time >= nextLog)
            {
                nextLog = Time.time + 10;
                var enemies = UnityEngine.Object.FindObjectsOfType<Enemy>();
                string progress = $"Clock x4; route: walk to clear caches then return to tower; seed 20260912; caches collected {collectedCaches}; player {player.transform.position}; goal {goal}\n{GameFlowManager.Instance.RunSummary}\nPlayer {MapController.Instance.PlayerHealth.CurrentHp} Tower {tower.Health.CurrentHp}\n" +
                    string.Join("\n", spawners.Select(s => $"Region {s.RegionLevel}: {s.CurrentWaveNumber}/{s.TotalWaves}, alive {s.AliveEnemies}, finished {s.HasFinishedSpawning}")) + "\n" +
                    string.Join("\n", enemies.Take(12).Select(e => { var a=e.GetComponent<NavMeshAgent>(); return $"{e.name} at {e.transform.position}, nav={a.isOnNavMesh}, path={(a.isOnNavMesh ? a.pathStatus.ToString() : "off mesh")}"; }));
                File.WriteAllText(".utmp/full-review/progress.txt", progress);
            }
            if (GameFlowManager.Instance.SurvivalSeconds > 1400) throw new Exception("Authored run stalled beyond 1400 game seconds.");
            yield return null;
        }
        if (GameFlowManager.Instance.CurrentState != GameState.Won)
            throw new Exception("Defense route ended: " + GameFlowManager.Instance.EndReason + " / " + GameFlowManager.Instance.RunSummary);
        if (collectedCaches != 2) throw new Exception("Expected two physically collected region caches, got " + collectedCaches);
        File.WriteAllText(".utmp/full-review/progress.txt", "Clock x4; walked to both physical region caches and returned to tower; no direct damage, resource injection, invincibility, or wave replacement.\n" + GameFlowManager.Instance.RunSummary + "\n" +
            string.Join("\n", spawners.Select(s => $"Region {s.RegionLevel}: {s.CurrentWaveNumber}/{s.TotalWaves}, alive {s.AliveEnemies}, cleared {s.IsCleared}")));
        ScreenCapture.CaptureScreenshot(".utmp/full-review/victory.png");
        for (int i = 0; i < 8; i++) yield return null;
        if (!string.IsNullOrEmpty(_errors)) throw new Exception(_errors);
    }

    private static void PolishCopy()
    {
        EditorSceneManager.SaveOpenScenes();
        string original = SceneManager.GetActiveScene().path;
        foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/GameScene.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMeshProUGUI>(true)))
            {
                if (text.name == "HowToPlay") { text.text = "WASD / ARROWS Move | ESC Pause\nYou gather and dodge; the tower auto-attacks\nClear regions for upgrade supplies\nE near tower: upgrade and restore health"; text.fontSize = 23; }
                if (text.name == "Hint" && text.transform.parent.name == "PauseCard") text.text = "WASD Move | E Upgrade near tower | ESC Resume";
                if (text.name == "Waves") { text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 24; }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        File.WriteAllText("Temp/ToxicBarrierFlow.polish-result", "PASS");
    }
}
