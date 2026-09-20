using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in end-to-end smoke checks; fixtures change only the temporary Play Mode scene.</summary>
[InitializeOnLoad]
public static class ReleaseUIPlayModeChecks
{
    private const string ActiveKey = "ToxicBarrier.UI.Checks";
    private static IEnumerator _run;
    private static double _deadline;
    private static readonly List<string> Passed = new List<string>();
    private static readonly List<string> Errors = new List<string>();
    private static int _frames;
    private static int _lastPlayFrame = -1;

    static ReleaseUIPlayModeChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += StateChanged;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.isPaused = false;
            PlaythroughPreferences.Capture(ActiveKey);
            Application.runInBackground = true;
            Passed.Clear(); Errors.Clear();
            _lastPlayFrame = -1;
            Application.logMessageReceived += Log;
            _deadline = EditorApplication.timeSinceStartup + 240;
            _run = Run();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            PlaythroughPreferences.Restore(ActiveKey);
            Application.runInBackground = false;
            SessionState.SetBool(ActiveKey, false);
            string original = SessionState.GetString(ActiveKey + ".Scene", "Assets/Scenes/GameScene.unity");
            if (File.Exists(original)) EditorSceneManager.OpenScene(original);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && !File.Exists("Temp/ToxicBarrierUI.setup") && File.Exists("Temp/ToxicBarrierUI.test"))
        {
            File.Delete("Temp/ToxicBarrierUI.test");
            EditorSceneManager.SaveOpenScenes();
            SessionState.SetString(ActiveKey + ".Scene", SceneManager.GetActiveScene().path);
            SessionState.SetBool(ActiveKey, true);
            Directory.CreateDirectory(".utmp/ui-checks");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            if (EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType().Name == "GameView")
                EditorWindow.focusedWindow.maximized = true;
            EditorApplication.isPlaying = true;
        }
        if (_run == null || !EditorApplication.isPlaying) return;
        if (Errors.Count > 0) { Finish("FAIL\n" + string.Join("\n", Errors) + "\n" + string.Join("\n", Passed)); return; }
        if (EditorApplication.timeSinceStartup > _deadline) { Finish("FAIL\nPlay Mode checks timed out\n" + string.Join("\n", Passed)); return; }
        // Editor update can run several times between player frames. Wait for actual lifecycle/render work.
        if (_lastPlayFrame == Time.frameCount) return;
        _lastPlayFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("Play Mode checks timed out");
            if (_run.MoveNext()) return;
            Check(Errors.Count == 0, "No runtime exceptions or errors");
            Finish("PASS\n" + string.Join("\n", Passed));
        }
        catch (Exception e) { Finish("FAIL\n" + e + "\n" + string.Join("\n", Passed) + "\n" + string.Join("\n", Errors)); }
    }

    private static void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(text + "\n" + stack);
    }

    private static void Finish(string report)
    {
        _run = null;
        Application.logMessageReceived -= Log;
        File.WriteAllText(".utmp/ui-checks/report.txt", report);
        Debug.Log(report);
        Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator Run()
    {
        yield return null;
        GameText.SetEnglish(true);
        Check(GameFlowManager.Instance.CurrentState == GameState.MainMenu, "Main menu entry state");
        Check(UnityEngine.Object.FindObjectsOfType<EventSystem>().Length == 1, "One menu EventSystem");
        Capture("01-main-menu");
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        var menuButtons = GameObject.Find("MainMenuPanel").GetComponentsInChildren<Button>();
        Check(menuButtons.Length == 3 && menuButtons.Any(b => b.name == "StartGameButton") && menuButtons.Any(b => b.name == "QuitButton") && menuButtons.Any(b => b.name == "LanguageButton"), "Main menu contains Start Game, Quit and the language shortcut");
        var quitButton = menuButtons.Single(b => b.name == "QuitButton");
        Check(quitButton.onClick.GetPersistentEventCount() == 1 && quitButton.onClick.GetPersistentMethodName(0) == nameof(GameFlowUI.QuitGame), "Quit button is bound to game exit");
        Click("MainMenuPanel", "LanguageButton");
        Check(GameObject.Find("StartGameButton").GetComponentInChildren<TextMeshProUGUI>().text == "开始游戏", "Simplified menu retains Chinese localization");
        Click("MainMenuPanel", "LanguageButton");
        Check(GameObject.Find("StartGameButton").GetComponentInChildren<TextMeshProUGUI>().text == "START GAME" && quitButton.GetComponentInChildren<TextMeshProUGUI>().text == "QUIT", "English Start Game and Quit labels");
        Check(Resources.Load<TMP_FontAsset>("Presentation/Chinese").HasCharacter('塔'), "Bundled Chinese font contains game glyphs");
        Check(UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(l => l.enabled) == 1, "Main menu has one active audio listener");
        Check(GameAudio.Instance.GetComponents<AudioSource>().Any(a => a.clip != null && a.isPlaying), "Background music is connected and playing");
        Click("MainMenuPanel", "StartGameButton");
        while (GameFlowManager.Instance.CurrentState != GameState.Playing) yield return null;
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        Check(UnityEngine.Object.FindObjectsOfType<GameFlowManager>().Length == 1, "Persistent flow manager has no duplicates");
        Check(UnityEngine.Object.FindObjectsOfType<EventSystem>().Length == 1, "One gameplay EventSystem");
        Canvas.ForceUpdateCanvases();
        Button pauseTarget = GameObject.Find("PauseButton").GetComponent<Button>();
        RectTransform pauseRect = pauseTarget.GetComponent<RectTransform>();
        PointerEventData pointer = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(null, pauseRect.TransformPoint(pauseRect.rect.center)) };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == pauseTarget,
            "Pause button is the topmost pointer raycast target");
        StopCombat();
        HealthSystem player = UnityEngine.Object.FindObjectsOfType<HealthSystem>().Single(h => h.IsPlayer);
        Check(player.GetComponent<CircleCollider2D>().bounds.extents.x <= .226f, "Player foot collider fits the navigable clearance");
        Tower tower = UnityEngine.Object.FindObjectOfType<Tower>();
        Check(Tower.FindClosestLivingTower(player.transform.position) == tower, "Enemy target registry finds the living tower");
        int authoredTowerLevel = tower.CurrentLevel;
        Check(authoredTowerLevel == Tower.InitialLevel, "New game begins at tower level zero");
        Check(Mathf.Approximately(tower.AttackRange, 4.75f), "Tower uses the reduced 4.75 metre defense range");
        Check(GameObject.Find("RunTimer").transform.parent.name == "RunStatusBar", "Timer and defeated count share the compact HUD bar");
        IEnumerator routeChecks = WorldLayoutPlayChecks.Run(player, tower, Check);
        while (routeChecks.MoveNext()) yield return routeChecks.Current;
        IEnumerator edgeChecks = GameplayEdgePlayChecks.Run(player, tower, Check);
        while (edgeChecks.MoveNext()) yield return edgeChecks.Current;
        IEnumerator pursuitChecks = RouteGameplayPlayChecks.CheckPursuit(player, tower, Check);
        while (pursuitChecks.MoveNext()) yield return pursuitChecks.Current;
        IEnumerator balanceChecks = GameplayBalancePlayChecks.Run(player, tower, Check);
        while (balanceChecks.MoveNext()) yield return balanceChecks.Current;
        // Scene authors can preview any tier; the upgrade fixture always starts at level zero.
        tower.SetLevel(Tower.InitialLevel);
        float towerBase = tower.GetComponent<SpriteRenderer>().sprite.bounds.min.y;
        for (int level = 0; level <= 3; level++)
            Check(AssetDatabase.GetAssetPath(Ref<Sprite>(tower, "level" + level + "Sprite")) ==
                "Assets/Sprites/Sprites_tower" + (level == 1 ? "" : level.ToString()) + ".png", "Uses assigned tower sprite for level " + level);
        TowerParticleEffects fx = tower.GetComponent<TowerParticleEffects>();
        Check(fx != null, "Tower has configured particle effects");
        Check(fx.PlayAttack(tower.transform.position + Vector3.right * 2, 0) && fx.LastAttackPrefab == "FX_Fire_06", "Level zero uses first attack effect");
        var emittedEffect = UnityEngine.Object.FindObjectsOfType<ParticleSystem>().First(p => p.name == "FX_Fire_06(Clone)");
        Check(tower.GetComponent<SpriteRenderer>().bounds.Contains(emittedEffect.transform.position), "Attack effect originates inside the tower sprite, not at the target");
        PlayerInventory bag = player.GetComponent<PlayerInventory>();
        var pickupFixture = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Wood.prefab"), Vector3.one * 100, Quaternion.identity).GetComponent<ResourcePickup>();
        int woodCount = bag.GetCount(LootManager.ResourceType.Wood);
        var playerCollider = player.GetComponent<Collider2D>();
        pickupFixture.SendMessage("OnTriggerEnter2D", playerCollider);
        pickupFixture.SendMessage("OnTriggerEnter2D", playerCollider);
        Check(bag.GetCount(LootManager.ResourceType.Wood) == woodCount + 2, "Repeated pickup collision awards one two-unit resource only once");
        IEnumerator warningChecks = CheckParasiteWarning(player, bag, playerCollider);
        while (warningChecks.MoveNext()) yield return warningChecks.Current;
        player.TakeDamage(20, null);
        HealthControl bar = UnityEngine.Object.FindObjectsOfType<HealthControl>().Single(c => Ref<HealthSystem>(c, "health") == player);
        Check(Mathf.Approximately(Ref<Image>(bar, "fillImage").fillAmount, .8f), "Player damage updates filled health bar");
        Check(Ref<TextMeshProUGUI>(bar, "valueText").text == "80 / 100", "Health value text updates");
        Capture("02-game-hud");
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        Click("GameFlowCanvas", "PauseButton");
        Check(GameFlowManager.Instance.CurrentState == GameState.Paused && Time.timeScale == 0, "Pause button freezes game");
        var enemyFixture = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Bug.prefab"), player.transform.position, Quaternion.identity);
        Check(enemyFixture.GetComponent<BoxCollider2D>().bounds.extents.magnitude <= .251f, "Enemy physics footprint fits the baked navigation radius");
        float beforePausedAttack = player.CurrentHp;
        enemyFixture.GetComponent<EnemyAttackBehavior>().SendMessage("Update");
        Check(player.CurrentHp == beforePausedAttack, "Ready enemy attack cannot deal damage while paused");
        UnityEngine.Object.Destroy(enemyFixture);
        float hp = player.CurrentHp;
        float elapsed = GameFlowManager.Instance.SurvivalSeconds;
        Capture("03-pause");
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        Check(player.CurrentHp == hp && GameFlowManager.Instance.SurvivalSeconds == elapsed, "Pause preserves HP and survival timer");
        Click("PauseMenuPanel", "ResumeButton");
        Check(GameFlowManager.Instance.IsPlaying && Time.timeScale == 1, "Resume returns to play");
        GameFlowManager.Instance.PauseGame();
        Check(GameObject.Find("PauseMenuPanel").GetComponentsInChildren<Button>().Length == 4, "Simple pause has three actions and a language shortcut");
        Click("PauseMenuPanel", "LanguageButton");
        Check(!GameText.English && GameObject.Find("ResumeButton").GetComponentInChildren<TextMeshProUGUI>().text == "继续游戏" && Time.timeScale == 0, "Pause language switch updates Chinese labels without resuming");
        Click("PauseMenuPanel", "LanguageButton");
        Check(GameText.English && GameFlowManager.Instance.CurrentState == GameState.Paused && Time.timeScale == 0, "Pause language switch restores English without resuming");
        GameFlowManager.Instance.ResumeGame();
        GameFlowManager.Instance.SendMessage("OnApplicationFocus", false);
        Check(GameFlowManager.Instance.CurrentState == GameState.Paused, "Losing application focus pauses gameplay");
        GameFlowManager.Instance.ResumeGame();
        player.GetComponent<Rigidbody2D>().position = (Vector2)tower.transform.position + Vector2.down * .5f;
        player.transform.position = tower.transform.position + Vector3.down * .5f;
        Physics2D.SyncTransforms();
        while (!tower.IsPlayerInUpgradeRange) yield return null;
        player.RestoreFullHealth();
        var contact = new GameObject("Contact damage fixture");
        player.TakeDamage(18, contact);
        player.TakeDamage(18, contact);
        Check(Mathf.Approximately(player.CurrentHp, 82), "Contact protection rejects repeated hits inside 0.75 seconds");
        float recoverUntil = Time.time + 4;
        while (Time.time < recoverUntil) yield return null;
        Check(player.CurrentHp > 82, "Player recovers after three damage-free seconds beside the tower");
        player.RestoreFullHealth();
        UnityEngine.Object.Destroy(contact);
        TowerUpgradePanelUI upgrade = UnityEngine.Object.FindObjectOfType<TowerUpgradePanelUI>();
        Button upgradeButton = Ref<Button>(upgrade, "upgradeButton");
        Check(Ref<RectTransform>(upgrade, "panelRoot").gameObject.activeSelf, "Approaching tower opens saved upgrade panel");
        foreach (LootManager.ResourceType type in Enum.GetValues(typeof(LootManager.ResourceType))) bag.TryConsume(type, bag.GetCount(type));
        Check(!upgradeButton.interactable, "Insufficient resources disables upgrade");
        EnemySpawner[] spawners = UnityEngine.Object.FindObjectsOfType<EnemySpawner>().OrderBy(s => s.RegionLevel).ToArray();
        tower.GetComponent<TowerAttackBehavior>().enabled = false;
        StartShortWave(spawners[0]);
        while (!spawners[0].HasFinishedSpawning) yield return null;
        Enemy attackTarget = UnityEngine.Object.FindObjectOfType<Enemy>();
        attackTarget.GetComponent<EnemyAttackBehavior>().enabled = false;
        Vector3 originalEnemyPosition = attackTarget.transform.position;
        attackTarget.transform.position = tower.transform.position + Vector3.left;
        attackTarget.GetComponent<HealthSystem>().SetStats(0, 1, 100000);
        attackTarget.GetComponent<HealthSystem>().RestoreFullHealth();
        int attacksBefore = fx.AttackPlayCount;
        tower.GetComponent<TowerAttackBehavior>().enabled = true;
        while (fx.AttackPlayCount == attacksBefore) yield return null;
        Check(fx.LastAttackPrefab == "FX_Fire_06", "Real tower attack triggers configured particles");
        // Restore the fixture before defeat so random drops/parasites cannot land under the upgrade tester.
        attackTarget.transform.position = originalEnemyPosition;
        DefeatVisibleEnemies();
        var collectFirst = CollectCache(player, tower, bag);
        while (collectFirst.MoveNext()) yield return null;
        while (bag.GetCount(LootManager.ResourceType.Stone) < tower.GetUpgradeCostToLevel(2).stone) yield return null;
        Check(bag.GetCount(LootManager.ResourceType.Gel) >= tower.GetUpgradeCostToLevel(1).gel + tower.GetUpgradeCostToLevel(2).gel,
            "Region 1 clear guarantees cumulative supplies through tower level 2");
        Capture("08-tower-level-0");
        for (_frames = 0; _frames < 4; _frames++) yield return null;
        tower.GetComponent<HealthSystem>().TakeDamage(100, null);
        for (int level = 1; level <= 3; level++)
        {
            if (level == 3)
            {
                StartShortWave(spawners[1]);
                while (!spawners[1].HasFinishedSpawning) yield return null;
                DefeatVisibleEnemies();
                var collectSecond = CollectCache(player, tower, bag);
                while (collectSecond.MoveNext()) yield return null;
                while (bag.GetCount(LootManager.ResourceType.Teeth) < tower.GetUpgradeCostToLevel(3).teeth) yield return null;
                Check(bag.GetCount(LootManager.ResourceType.Gel) >= tower.GetUpgradeCostToLevel(3).gel,
                    "Region 2 clear guarantees remaining level 3 supplies");
            }
            Tower.TowerUpgradeCost cost = tower.GetUpgradeCostForNextLevel();
            int woodBefore = bag.GetCount(LootManager.ResourceType.Wood);
            int gelBefore = bag.GetCount(LootManager.ResourceType.Gel);
            Check(upgradeButton.interactable, "Upgrade enabled when materials ready: " + level);
            if (level == 1)
            {
                Capture("04-upgrade");
                for (_frames = 0; _frames < 12; _frames++) yield return null;
            }
            upgradeButton.onClick.Invoke();
            if (level == 1)
            {
                GameFlowManager.Instance.PauseGame();
                Check(!Ref<RectTransform>(upgrade, "panelRoot").gameObject.activeSelf, "Pause hides upgrade panel");
                for (_frames = 0; _frames < 12; _frames++) yield return null;
                Check(tower.CurrentLevel == 0 && bag.GetCount(LootManager.ResourceType.Wood) == woodBefore, "Pause holds upgrade animation and material payment");
                GameFlowManager.Instance.ResumeGame();
            }
            while (tower.CurrentLevel < level) yield return null;
            for (_frames = 0; _frames < 4; _frames++) yield return null;
            Check(fx.ActiveUpgradeCount == 2, "Both upgrade particles play at tower foot: " + level);
            var towerRenderer = tower.GetComponent<SpriteRenderer>();
            Check(Mathf.Abs(towerRenderer.sprite.bounds.min.y - towerBase) < .002f,
                "Tower sprite keeps its base aligned after upgrade: " + level);
            string expected = new[] { "FX_Fire_06", "FX_Fire_06 1", "FX_Fire_06_2" }[level - 1];
            fx.PlayAttack(tower.transform.position + Vector3.right * 2, level);
            Check(fx.LastAttackPrefab == expected, "Attack prefab matches tower level: " + level);
            float captureAt = Time.time + .35f;
            while (Time.time < captureAt) yield return null;
            string particleInfo = string.Join("\n", UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Select(p => {
                var r = p.GetComponent<ParticleSystemRenderer>();
                return $"{p.name}: count={p.particleCount} time={p.time} enabled={r.enabled} bounds={r.bounds} material={r.sharedMaterial.name} shader={r.sharedMaterial.shader.name} supported={r.sharedMaterial.shader.isSupported}";
            }));
            File.WriteAllText(".utmp/ui-checks/particles-level-" + level + ".txt", particleInfo);
            Capture("07-fx-level-" + level);
            for (_frames = 0; _frames < 4; _frames++) yield return null;
            Check(bag.GetCount(LootManager.ResourceType.Wood) == woodBefore - cost.wood && bag.GetCount(LootManager.ResourceType.Gel) == gelBefore - cost.gel,
                "Upgrade consumes materials exactly once: " + level);
            Check(player.CurrentHp == player.MaxHp && tower.Health.CurrentHp == tower.Health.MaxHp, "Upgrade restores player and tower: " + level);
            if (level >= 2) Check(MapController.Instance.IsMapUnlocked(level), "Upgrade unlocks region " + level);
        }
        Check(!upgradeButton.interactable && GameFlowManager.Instance.IsPlaying, "Max tower alone does not trigger victory");
        for (int i = 0; i < 24; i++) fx.PlayAttack(tower.transform.position + Vector3.right * 2, 3);
        Check(fx.ActiveAttackCount <= 8, "Rapid attacks have a bounded particle pool");
        var visibleParticle = UnityEngine.Object.FindObjectsOfType<ParticleSystem>().First(p => p.isPlaying);
        GameFlowManager.Instance.PauseGame();
        float particleTime = visibleParticle.time;
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        Check(Mathf.Approximately(visibleParticle.time, particleTime), "Pause freezes particle simulation");
        GameFlowManager.Instance.ResumeGame();
        float clearAt = Time.time + 2.2f;
        tower.GetComponent<TowerAttackBehavior>().enabled = false;
        while (Time.time < clearAt) yield return null;
        Check(fx.ActiveAttackCount == 0 && fx.ActiveUpgradeCount == 0, "Attack and upgrade particles expire automatically");
        IEnumerator outerChecks = GameplayBalancePlayChecks.CheckOuterRegions(player, Check);
        while (outerChecks.MoveNext()) yield return outerChecks.Current;
        IEnumerator repairChecks = RouteGameplayPlayChecks.CheckRepair(player, tower, Check);
        while (repairChecks.MoveNext()) yield return repairChecks.Current;
        StartShortWave(spawners[2]);
        while (spawners.Any(s => !s.HasFinishedSpawning)) yield return null;
        Check(GameFlowManager.Instance.IsPlaying && spawners.Sum(s => s.AliveEnemies) > 0, "Spawn completion waits for remaining enemies");
        Enemy[] finalEnemies = UnityEngine.Object.FindObjectsOfType<Enemy>();
        for (int i = 0; i < finalEnemies.Length - 1; i++) finalEnemies[i].GetComponent<HealthSystem>().TakeDamage(100000, null);
        yield return null;
        Check(GameFlowManager.Instance.IsPlaying, "One remaining enemy prevents premature victory");
        finalEnemies[finalEnemies.Length - 1].GetComponent<HealthSystem>().TakeDamage(100000, null);
        while (GameFlowManager.Instance.CurrentState != GameState.Won) yield return null;
        Check(Time.timeScale == 0, "Full clear enters frozen victory result");
        GameFlowManager.Instance.ResumeGame();
        Check(GameFlowManager.Instance.CurrentState == GameState.Won, "Victory cannot resume as gameplay");
        Capture("05-victory");
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        Click("VictoryPanel", "RestartButton");
        while (!GameFlowManager.Instance.IsPlaying) yield return null;
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        StopCombat();
        tower = UnityEngine.Object.FindObjectOfType<Tower>();
        Check(tower.CurrentLevel == authoredTowerLevel && Time.timeScale == 1, "Restart restores scene tower level and time scale");
        tower.Health.TakeDamage(100000, null);
        Check(GameFlowManager.Instance.CurrentState == GameState.Dead && GameFlowManager.Instance.EndReason.Contains("净化塔"), "Tower destruction triggers defeat");
        Check(Tower.FindClosestLivingTower(Vector2.zero) == null, "Destroyed tower leaves the enemy target registry");
        Capture("06-tower-defeat");
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        Click("DeathPanel", "RestartButton");
        while (!GameFlowManager.Instance.IsPlaying) yield return null;
        for (_frames = 0; _frames < 12; _frames++) yield return null;
        StopCombat();
        player = UnityEngine.Object.FindObjectsOfType<HealthSystem>().Single(h => h.IsPlayer);
        player.TakeDamage(100000, null);
        Check(GameFlowManager.Instance.CurrentState == GameState.Dead && Time.timeScale == 0, "Player death triggers defeat");
        GameFlowManager.Instance.ResumeGame();
        Check(GameFlowManager.Instance.CurrentState == GameState.Dead, "Death cannot be resumed");
        Click("DeathPanel", "MainMenuButton");
        while (GameFlowManager.Instance.CurrentState != GameState.MainMenu) yield return null;
        Check(Time.timeScale == 1 && UnityEngine.Object.FindObjectsOfType<GameFlowManager>().Length == 1, "Return to menu restores time and single manager");
        for (_frames = 0; _frames < 12; _frames++) yield return null;
    }

    private static IEnumerator CheckParasiteWarning(HealthSystem player, PlayerInventory bag, Collider2D playerCollider)
    {
        var existing = UnityEngine.Object.FindObjectsOfType<DisguisedParasite>();
        foreach (var parasite in existing) parasite.enabled = false;
        var disguise = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Wood.prefab"), Vector3.one * 100, Quaternion.identity);
        var pickup = disguise.GetComponent<ResourcePickup>();
        pickup.enabled = false;
        int wood = bag.GetCount(LootManager.ResourceType.Wood);
        pickup.SendMessage("OnTriggerEnter2D", playerCollider);
        Check(bag.GetCount(LootManager.ResourceType.Wood) == wood, "Disabled parasite disguise cannot award a resource");
        var trap = disguise.AddComponent<DisguisedParasite>();
        trap.Configure(0, .7f, 3.5f);
        var warning = UnityEngine.Object.FindObjectOfType<ParasiteWarningUI>();
        var group = warning.GetComponent<CanvasGroup>();
        var graphic = warning.GetComponentInChildren<ParasiteVignetteGraphic>();
        Canvas.ForceUpdateCanvases();
        Mesh mesh = graphic.canvasRenderer.GetMesh();
        var centre = mesh.vertices.Select((p, i) => new { p, i }).OrderBy(v => v.p.sqrMagnitude).First();
        Check(mesh.colors32[centre.i].a == 0 && mesh.colors32.Any(c => c.a > 200), "Warning has a transparent centre and feathered visible edges");
        Check(!graphic.raycastTarget && !group.blocksRaycasts && warning.GetComponent<Canvas>().sortingOrder < GameObject.Find("HUDCanvas").GetComponent<Canvas>().sortingOrder,
            "Warning sits below HUD and never blocks clicks");
        Check(DisguisedParasite.GetStrongestWarning(player.transform.position) == 0, "Distant hidden enemy produces no warning");
        disguise.transform.position = player.transform.position + Vector3.right * .6f;
        float rawNear = DisguisedParasite.GetStrongestWarning(player.transform.position);
        Check(rawNear > .8f, "Nearby hidden enemy increases warning strength");
        float until = Time.realtimeSinceStartup + 1.3f;
        while (Time.realtimeSinceStartup < until) { if (!GameFlowManager.Instance.IsPlaying) GameFlowManager.Instance.ResumeGame(); yield return null; }
        Check(group.alpha > .25f && group.alpha <= .68f, "Near warning fades in with capped opacity");
        Capture("10-parasite-warning-near");
        for (int i = 0; i < 4; i++) yield return null;
        disguise.transform.position = player.transform.position + Vector3.right * 20;
        until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until) { if (!GameFlowManager.Instance.IsPlaying) GameFlowManager.Instance.ResumeGame(); yield return null; }
        Check(group.alpha < .01f, "Active warning fades away after leaving danger without pausing");
        GameFlowManager.Instance.PauseGame();
        trap.Configure(10, .7f, 3.5f);
        float hp = player.CurrentHp;
        trap.SendMessage("OnTriggerEnter2D", playerCollider);
        yield return null;
        Check(player.CurrentHp == hp && group.alpha == 0, "Pause hides warning and blocks hidden enemy damage");
        trap.Configure(0, .7f, 3.5f);
        GameFlowManager.Instance.ResumeGame();
        disguise.transform.position = player.transform.position + Vector3.right * 20;
        until = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < until) { if (!GameFlowManager.Instance.IsPlaying) GameFlowManager.Instance.ResumeGame(); yield return null; }
        Check(group.alpha < .01f, "Warning clears when player leaves the danger radius");
        UnityEngine.Object.Destroy(disguise);
        foreach (var parasite in existing) if (parasite != null) parasite.enabled = true;
    }

    private static void StopCombat()
    {
        foreach (EnemySpawner spawner in UnityEngine.Object.FindObjectsOfType<EnemySpawner>()) spawner.StopSpawning();
        DefeatVisibleEnemies();
    }
    private static IEnumerator CollectCache(HealthSystem player, Tower tower, PlayerInventory bag)
    {
        while (GameFlowManager.Instance.ActiveSupplyCache == null) yield return null;
        var cache = GameFlowManager.Instance.ActiveSupplyCache;
        int before = bag.GetCount(LootManager.ResourceType.Stone);
        for (int i = 0; i < 10; i++) yield return null;
        Check(bag.GetCount(LootManager.ResourceType.Stone) == before, "Cleared region does not deposit supplies remotely");
        Check(Vector2.Distance(cache.transform.position, tower.transform.position) > 7, "Supply reward creates an actual collection destination");
        player.transform.position = cache.transform.position;
        player.GetComponent<Rigidbody2D>().position = cache.transform.position; Physics2D.SyncTransforms();
        while (cache != null) yield return null;
        player.transform.position = tower.transform.position + Vector3.down * .5f;
        player.GetComponent<Rigidbody2D>().position = player.transform.position; Physics2D.SyncTransforms();
        while (!tower.IsPlayerInUpgradeRange) yield return null;
    }
    private static void DefeatVisibleEnemies()
    {
        foreach (Enemy enemy in UnityEngine.Object.FindObjectsOfType<Enemy>()) enemy.GetComponent<HealthSystem>().TakeDamage(100000, null);
    }
    private static void StartShortWave(EnemySpawner spawner)
    {
        LevelWavesConfig fixture = ScriptableObject.CreateInstance<LevelWavesConfig>();
        fixture.waves = new[] { new LevelWavesConfig.Wave { budget = 50, minDelayBeforeNext = 0, maxDelayBeforeNext = 0 } };
        SerializedObject data = new SerializedObject(spawner);
        data.FindProperty("levelWaves").objectReferenceValue = fixture;
        data.ApplyModifiedPropertiesWithoutUndo();
        spawner.StartSpawning();
    }
    private static void Click(string panelName, string buttonName)
    {
        GameObject panel = GameObject.Find(panelName);
        if (panel == null) throw new Exception("Visible panel missing: " + panelName);
        Button button = panel.GetComponentsInChildren<Button>().Single(b => b.name == buttonName);
        if (!button.interactable) throw new Exception("Button disabled: " + buttonName);
        button.onClick.Invoke();
    }
    private static T Ref<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object
    { return new SerializedObject(target).FindProperty(field).objectReferenceValue as T; }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Passed.Add("OK  " + message); }
    private static void Capture(string name)
    { ScreenCapture.CaptureScreenshot(".utmp/ui-checks/" + name + ".png"); }
}

/// <summary>Fixtures must not overwrite the user's settings or personal best, including on cancellation.</summary>
internal static class PlaythroughPreferences
{
    private static readonly string[] FloatKeys = { "BestClearSeconds", "MusicVolume", "EffectsVolume" };
    private static readonly string[] IntKeys = { "CompletedRuns", "ResolutionIndex", "Fullscreen", "English" };
    public static void Capture(string prefix)
    {
        foreach (string key in FloatKeys.Concat(IntKeys)) SessionState.SetBool(prefix + key, PlayerPrefs.HasKey(key));
        foreach (string key in FloatKeys) SessionState.SetFloat(prefix + key + "value", PlayerPrefs.GetFloat(key));
        foreach (string key in IntKeys) SessionState.SetInt(prefix + key + "value", PlayerPrefs.GetInt(key));
    }
    public static void Restore(string prefix)
    {
        foreach (string key in FloatKeys)
            if (SessionState.GetBool(prefix + key, false)) PlayerPrefs.SetFloat(key, SessionState.GetFloat(prefix + key + "value", 0)); else PlayerPrefs.DeleteKey(key);
        foreach (string key in IntKeys)
            if (SessionState.GetBool(prefix + key, false)) PlayerPrefs.SetInt(key, SessionState.GetInt(prefix + key + "value", 0)); else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
