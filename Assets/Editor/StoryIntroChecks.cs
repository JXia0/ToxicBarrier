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

/// <summary>Opt-in preparation and real-time opening-story integration checks.</summary>
[InitializeOnLoad]
internal static class StoryIntroChecks
{
    private const string Key = "ToxicBarrier.StoryChecks";
    private const string Output = ".utmp/story-checks/";
    private static readonly List<string> Results = new List<string>();
    private static IEnumerator _run;
    private static string _errors;
    private static int _frame;
    private static double _deadline;

    static StoryIntroChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                PlaythroughPreferences.Capture(Key);
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                _errors = ""; Results.Clear(); _frame = -1;
                _deadline = EditorApplication.timeSinceStartup + 100;
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

    [MenuItem("Tools/ToxicBarrier/Prepare Story Intro")]
    private static void Prepare()
    {
        foreach (string name in StoryIntroUI.ArtNames)
        {
            string path = "Assets/Resources/StoryIntro/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new Exception("Missing story art: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Presentation/Chinese.asset");
        string copy = string.Concat(StoryIntroUI.Captions) + StoryIntroUI.SkipCaption;
        string missing = new string(copy.Where(c => !char.IsWhiteSpace(c) && !font.HasCharacter(c)).Distinct().ToArray());
        if (missing.Length > 0 && !font.TryAddCharacters(missing, out string unavailable))
            throw new Exception("Missing story glyphs: " + unavailable);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            if (File.Exists("Temp/ToxicBarrierStory.prepare"))
            {
                File.Delete("Temp/ToxicBarrierStory.prepare");
                try { Prepare(); File.WriteAllText("Temp/ToxicBarrierStory.result", "PASS"); }
                catch (Exception e) { File.WriteAllText("Temp/ToxicBarrierStory.result", "FAIL\n" + e); }
            }
            if (File.Exists("Temp/ToxicBarrierStory.test"))
            {
                File.Delete("Temp/ToxicBarrierStory.test");
                if (SceneManager.GetActiveScene().isDirty)
                {
                    Directory.CreateDirectory(Output);
                    File.WriteAllText(Output + "report.txt", "BLOCKED: Save the current scene before running story checks.");
                    return;
                }
                SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
                SessionState.SetBool(Key, true);
                Directory.CreateDirectory(Output);
                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.isPlaying = true;
            }
        }
        if (_run == null || !EditorApplication.isPlaying) return;
        if (!string.IsNullOrEmpty(_errors)) { Finish("FAIL\n" + _errors); return; }
        if (EditorApplication.timeSinceStartup > _deadline) { Finish("FAIL\nStory check timeout"); return; }
        if (_frame == Time.frameCount) return;
        _frame = Time.frameCount;
        try { if (!_run.MoveNext()) Finish("PASS"); }
        catch (Exception e) { Finish("FAIL\n" + e); }
    }

    private static IEnumerator Run()
    {
        for (int i = 0; i < 5; i++) yield return null;
        GameText.SetEnglish(true);
        var flow = GameFlowManager.Instance;
        GameObject.Find("StartGameButton").GetComponent<Button>().onClick.Invoke();
        var intro = flow.GetComponent<StoryIntroUI>();
        Check(flow.CurrentState == GameState.StoryIntro && intro.IsVisible, "Start Game opens the story");
        Check(Time.timeScale == 0 && SceneManager.GetActiveScene().name == "MainMenu", "Gameplay has not loaded and simulation is frozen");
        Check(UnityEngine.Object.FindObjectOfType<EnemySpawner>() == null && flow.SurvivalSeconds == 0, "No waves or survival time advance during the story");
        flow.StartNewGame(); flow.RestartGame(); flow.PauseGame();
        Check(flow.CurrentState == GameState.StoryIntro && intro.SlideIndex == 0, "Repeated start/restart/pause cannot interrupt the intro");
        double started = EditorApplication.timeSinceStartup;
        double[] appeared = new double[4];
        bool[] captured = new bool[4];
        int previous = -1;
        while (flow.CurrentState == GameState.StoryIntro)
        {
            int index = intro.SlideIndex;
            if (index != previous)
            {
                appeared[index] = EditorApplication.timeSinceStartup;
                if (index > 0) Check(appeared[index] - appeared[index - 1] >= 4.8 && appeared[index] - appeared[index - 1] < 5.7, "Image " + index + " stays approximately five seconds");
                previous = index;
            }
            if (intro.SlideElapsed > 1f && !captured[index])
            {
                captured[index] = true;
                RawImage art = GameObject.Find("StoryIntroCanvas").GetComponentInChildren<RawImage>();
                Check(art.texture != null && art.texture.width >= 1600, "Image " + (index + 1) + " uses the finished illustration");
                var caption = GameObject.Find("Caption").GetComponent<TextMeshProUGUI>();
                caption.ForceMeshUpdate();
                Check(caption.text == GameText.Translate(StoryIntroUI.Captions[index]) && !caption.isTextOverflowing, "English caption " + (index + 1) + " is localized and fits");
                ScreenCapture.CaptureScreenshot(Output + "0" + (index + 1) + "-english.png");
            }
            CheckQuiet(flow.SurvivalSeconds == 0 && Time.timeScale == 0, "Game advanced during the intro");
            yield return null;
        }
        double duration = EditorApplication.timeSinceStartup - started;
        Check(captured.All(x => x) && duration >= 19.8 && duration < 22, "All four images complete automatically in about 20 seconds (" + duration.ToString("F2") + "s)");
        while (!flow.IsPlaying) yield return null;
        Check(!intro.IsVisible && Time.timeScale == 1 && flow.SurvivalSeconds < .5f, "Story ends in a fresh playable run with a reset clock");
        Check(UnityEngine.Object.FindObjectOfType<Tower>().CurrentLevel == Tower.InitialLevel, "Tower still starts at level zero");
        flow.PauseGame();
        for (int i = 0; i < 5; i++) yield return null;
        Check(flow.CurrentState == GameState.Paused && Time.timeScale == 0, "Pause remains functional after the intro");
        flow.RestartGame();
        while (!flow.IsPlaying) yield return null;
        Check(!intro.IsVisible, "Restart retries immediately without replaying the opening");
        flow.ReturnToMainMenu();
        while (flow.CurrentState != GameState.MainMenu) yield return null;
        for (int i = 0; i < 5; i++) yield return null;
        GameText.SetEnglish(false);
        flow.StartNewGame();
        while (intro.SlideElapsed < 1f) yield return null;
        var chinese = GameObject.Find("Caption").GetComponent<TextMeshProUGUI>();
        chinese.ForceMeshUpdate();
        Check(chinese.text == StoryIntroUI.Captions[0] && !chinese.isTextOverflowing, "New Start Game replays the opening in selected Chinese");
        Check((string.Concat(StoryIntroUI.Captions) + StoryIntroUI.SkipCaption).Where(c => !char.IsWhiteSpace(c)).All(c => chinese.font.HasCharacter(c)), "All Chinese story characters are baked into the shipped font");
        ScreenCapture.CaptureScreenshot(Output + "05-chinese.png");
        for (int i = 0; i < 5; i++) yield return null;
        Button skip = GameObject.Find("SkipStory").GetComponent<Button>();
        Canvas.ForceUpdateCanvases();
        RectTransform rect = skip.GetComponent<RectTransform>();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == skip, "Skip is the topmost clickable UI target");
        skip.onClick.Invoke(); intro.Skip();
        while (!flow.IsPlaying) yield return null;
        Check(!intro.IsVisible && !GameText.English && UnityEngine.Object.FindObjectsOfType<GameFlowManager>().Length == 1, "Skip loads gameplay once and preserves language");
        flow.ReturnToMainMenu();
        while (flow.CurrentState != GameState.MainMenu) yield return null;
        flow.StartNewGame();
        yield return null;
        flow.ReturnToMainMenu();
        while (flow.CurrentState != GameState.MainMenu) yield return null;
        Check(!intro.IsVisible && Time.timeScale == 1, "Leaving an intro cancels it cleanly without a delayed scene load");
        Check(string.IsNullOrEmpty(_errors), "No runtime errors or missing-glyph warnings");
    }

    private static void CheckQuiet(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Check(bool condition, string message)
    {
        CheckQuiet(condition, message);
        Results.Add("PASS " + message);
        File.WriteAllText(Output + "progress.txt", string.Join("\n", Results));
    }
    private static void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || (type == LogType.Warning && text.Contains("was not found in")))
            _errors += text + "\n" + stack + "\n";
    }
    private static void Finish(string status)
    {
        _run = null; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "report.txt", status + "\n" + string.Join("\n", Results));
        EditorApplication.isPlaying = false;
    }
}
