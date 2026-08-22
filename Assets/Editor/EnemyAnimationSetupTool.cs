using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Creates enemy clips/controllers from the Monsters Creatures Fantasy sprite sheets.</summary>
public static class EnemyAnimationSetupTool
{
    private const string SetupRequestPath = "Assets/Editor/EnemyAnimationSetup.request";
    private const string SourceRoot = "Assets/Monsters Creatures Fantasy/Sprites";
    private const string OutputFolder = "Assets/Animations/Enemies";

    private static readonly EnemyAnimationDefinition[] Definitions =
    {
        new EnemyAnimationDefinition(
            "Goblin",
            "Assets/Prefab/Bug.prefab",
            SourceRoot + "/Goblin/Idle.png",
            SourceRoot + "/Goblin/Run.png",
            SourceRoot + "/Goblin/Attack1.png",
            10f,
            12f),
        new EnemyAnimationDefinition(
            "FlyingEye",
            "Assets/Prefab/Leader.prefab",
            SourceRoot + "/Flying eye/Flight.png",
            SourceRoot + "/Flying eye/Flight.png",
            SourceRoot + "/Flying eye/Attack1.png",
            10f,
            14f),
        new EnemyAnimationDefinition(
            "Skeleton",
            "Assets/Prefab/Boss.prefab",
            SourceRoot + "/Skeleton/Idle.png",
            SourceRoot + "/Skeleton/Walk.png",
            SourceRoot + "/Skeleton/Attack1.png",
            10f,
            12f)
    };

    [InitializeOnLoadMethod]
    private static void RunRequestedSetupAfterCompile()
    {
        string absoluteRequestPath = Path.Combine(Application.dataPath, "Editor/EnemyAnimationSetup.request");
        if (!File.Exists(absoluteRequestPath)) return;

        EditorApplication.delayCall += () =>
        {
            try
            {
                SetupEnemyAnimations();
                AssetDatabase.DeleteAsset(SetupRequestPath);
                Debug.Log("Enemy animation setup request completed and removed.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Enemy animation setup is waiting: {exception.Message} " +
                    "The request remains available and will retry after the next script refresh.");
            }
        };
    }

    [MenuItem("Tools/Toxic Barrier/Setup Enemy Animations")]
    public static void SetupEnemyAnimations()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up enemy animations.");

        EnsureOutputFolder();
        foreach (EnemyAnimationDefinition definition in Definitions)
            ConfigureEnemy(definition);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Bug, Leader, and Boss now use Goblin, Flying Eye, and Skeleton animations.");
    }

    private static void ConfigureEnemy(EnemyAnimationDefinition definition)
    {
        AnimationClip idleClip = CreateOrUpdateClip(
            $"{OutputFolder}/{definition.Name}_Idle.anim",
            definition.IdleSheetPath,
            definition.IdleFrameRate,
            true);
        AnimationClip runClip = CreateOrUpdateClip(
            $"{OutputFolder}/{definition.Name}_Run.anim",
            definition.RunSheetPath,
            definition.RunFrameRate,
            true);
        AnimationClip attackClip = CreateOrUpdateClip(
            $"{OutputFolder}/{definition.Name}_Attack1.anim",
            definition.AttackSheetPath,
            12f,
            false);

        string controllerPath = $"{OutputFolder}/{definition.Name}Enemy.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        ConfigureController(controller, idleClip, runClip, attackClip);
        ConfigurePrefab(definition.PrefabPath, controller, idleClip);
    }

    private static AnimationClip CreateOrUpdateClip(
        string clipPath,
        string spriteSheetPath,
        float frameRate,
        bool loop)
    {
        Sprite[] frames = LoadSortedSprites(spriteSheetPath);
        if (frames.Length == 0)
            throw new InvalidOperationException($"No sprites found at '{spriteSheetPath}'.");

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.name = Path.GetFileNameWithoutExtension(clipPath);
        clip.frameRate = frameRate;

        var keyframes = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = frames[i]
            };
        }
        keyframes[frames.Length] = new ObjectReferenceKeyframe
        {
            time = frames.Length / frameRate,
            value = loop ? frames[0] : frames[frames.Length - 1]
        };

        EditorCurveBinding spriteBinding = EditorCurveBinding.PPtrCurve(
            string.Empty,
            typeof(SpriteRenderer),
            "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        SetLoopTime(clip, loop);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetLoopTime(AnimationClip clip, bool loop)
    {
        SerializedObject clipData = new SerializedObject(clip);
        SerializedProperty settings = clipData.FindProperty("m_AnimationClipSettings");
        if (settings == null)
            throw new InvalidOperationException($"Could not edit loop settings for '{clip.name}'.");
        settings.FindPropertyRelative("m_LoopTime").boolValue = loop;
        clipData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite[] LoadSortedSprites(string spriteSheetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(spriteSheetPath);
        var sprites = new List<Sprite>();
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
                sprites.Add(sprite);
        }

        sprites.Sort((left, right) => GetFrameIndex(left.name).CompareTo(GetFrameIndex(right.name)));
        return sprites.ToArray();
    }

    private static int GetFrameIndex(string spriteName)
    {
        int separator = spriteName.LastIndexOf('_');
        if (separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), out int index))
            return index;
        return int.MaxValue;
    }

    private static void ConfigureController(
        AnimatorController controller,
        AnimationClip idleClip,
        AnimationClip runClip,
        AnimationClip attackClip)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
            stateMachine.RemoveState(childState.state);
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            stateMachine.RemoveAnyStateTransition(transition);

        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorState idle = stateMachine.AddState("Idle", new Vector3(220f, 20f));
        AnimatorState run = stateMachine.AddState("Run", new Vector3(470f, 20f));
        AnimatorState attack = stateMachine.AddState("Attack", new Vector3(345f, 170f));
        idle.motion = idleClip;
        run.motion = runClip;
        attack.motion = attackClip;
        stateMachine.defaultState = idle;

        ConfigureImmediateTransition(idle.AddTransition(run), "IsMoving", true);
        ConfigureImmediateTransition(run.AddTransition(idle), "IsMoving", false);

        AnimatorStateTransition attackTransition = stateMachine.AddAnyStateTransition(attack);
        attackTransition.hasExitTime = false;
        attackTransition.duration = 0f;
        attackTransition.canTransitionToSelf = false;
        attackTransition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

        ConfigureAttackExit(attack.AddTransition(idle), "IsMoving", false);
        ConfigureAttackExit(attack.AddTransition(run), "IsMoving", true);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(stateMachine);
    }

    private static void ConfigureImmediateTransition(
        AnimatorStateTransition transition,
        string parameter,
        bool expectedValue)
    {
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(
            expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            parameter);
    }

    private static void ConfigureAttackExit(
        AnimatorStateTransition transition,
        string parameter,
        bool expectedValue)
    {
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.duration = 0f;
        transition.AddCondition(
            expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            parameter);
    }

    private static void ConfigurePrefab(
        string prefabPath,
        AnimatorController controller,
        AnimationClip idleClip)
    {
        GameObject enemy = PrefabUtility.LoadPrefabContents(prefabPath);
        if (enemy == null)
            throw new InvalidOperationException($"Missing enemy prefab at '{prefabPath}'.");

        try
        {
            SpriteRenderer spriteRenderer = enemy.GetComponent<SpriteRenderer>();
            EnemyAttackBehavior behavior = enemy.GetComponent<EnemyAttackBehavior>();
            if (spriteRenderer == null || behavior == null)
                throw new InvalidOperationException($"'{prefabPath}' requires SpriteRenderer and EnemyAttackBehavior.");

            Animator animator = enemy.GetComponent<Animator>();
            if (animator == null)
                animator = enemy.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            SerializedObject behaviorData = new SerializedObject(behavior);
            behaviorData.FindProperty("animator").objectReferenceValue = animator;
            behaviorData.ApplyModifiedPropertiesWithoutUndo();

            Sprite[] idleSprites = GetClipSprites(idleClip);
            if (idleSprites.Length == 0)
                throw new InvalidOperationException($"'{idleClip.name}' has no sprite frames.");
            spriteRenderer.sprite = idleSprites[0];
            spriteRenderer.color = Color.white;
            spriteRenderer.flipX = false;

            EditorUtility.SetDirty(spriteRenderer);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(behavior);
            PrefabUtility.SaveAsPrefabAsset(enemy, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(enemy);
        }
    }

    private static Sprite[] GetClipSprites(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                continue;

            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            var sprites = new List<Sprite>();
            foreach (ObjectReferenceKeyframe keyframe in keyframes)
            {
                if (keyframe.value is Sprite sprite && !sprites.Contains(sprite))
                    sprites.Add(sprite);
            }
            return sprites.ToArray();
        }

        return Array.Empty<Sprite>();
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Animations", "Enemies");
    }

    private sealed class EnemyAnimationDefinition
    {
        public readonly string Name;
        public readonly string PrefabPath;
        public readonly string IdleSheetPath;
        public readonly string RunSheetPath;
        public readonly string AttackSheetPath;
        public readonly float IdleFrameRate;
        public readonly float RunFrameRate;

        public EnemyAnimationDefinition(
            string name,
            string prefabPath,
            string idleSheetPath,
            string runSheetPath,
            string attackSheetPath,
            float idleFrameRate,
            float runFrameRate)
        {
            Name = name;
            PrefabPath = prefabPath;
            IdleSheetPath = idleSheetPath;
            RunSheetPath = runSheetPath;
            AttackSheetPath = attackSheetPath;
            IdleFrameRate = idleFrameRate;
            RunFrameRate = runFrameRate;
        }
    }
}
