using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Builds the Black Knight locomotion/upgrade Animator and applies it to Player.prefab.</summary>
public static class PlayerAnimationSetupTool
{
    private const string SetupRequestPath = "Assets/Editor/PlayerAnimationSetup.request";
    private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";
    private const string ControllerFolder = "Assets/Animations";
    private const string ControllerPath = ControllerFolder + "/PlayerBlackKnight.controller";
    private const string ClipFolder = "Assets/PeterRadiant/2D Pixel Art Black Knight/Animations/Black_Knight";

    [InitializeOnLoadMethod]
    private static void RunRequestedSetupAfterCompile()
    {
        string absoluteRequestPath = Path.Combine(Application.dataPath, "Editor/PlayerAnimationSetup.request");
        if (!File.Exists(absoluteRequestPath)) return;

        EditorApplication.delayCall += () =>
        {
            try
            {
                SetupPlayerAnimation();
                AssetDatabase.DeleteAsset(SetupRequestPath);
                Debug.Log("Player Black Knight animation setup request completed and removed.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Player animation setup is waiting: {exception.Message} " +
                    "The request remains available and will retry after the next script refresh.");
            }
        };
    }

    [MenuItem("Tools/Toxic Barrier/Setup Player Black Knight Animation")]
    public static void SetupPlayerAnimation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up player animation.");

        AnimationClip idleClip = LoadClip("BK_unarmed_idle");
        AnimationClip runClip = LoadClip("BK_unarmed_run");
        AnimationClip attackClip = LoadClip("BK_attack_4");

        if (!AssetDatabase.IsValidFolder(ControllerFolder))
            AssetDatabase.CreateFolder("Assets", "Animations");

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        ConfigureController(controller, idleClip, runClip, attackClip);
        ConfigurePlayerPrefab(controller, idleClip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Player prefab now uses the Black Knight Idle, Run, and Attack_4 animations.");
    }

    private static AnimationClip LoadClip(string clipName)
    {
        string path = $"{ClipFolder}/{clipName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            throw new InvalidOperationException($"Missing animation clip at '{path}'.");
        return clip;
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
        controller.AddParameter("Attack4", AnimatorControllerParameterType.Trigger);

        AnimatorState idle = stateMachine.AddState("Idle", new Vector3(220f, 20f));
        AnimatorState run = stateMachine.AddState("Run", new Vector3(470f, 20f));
        AnimatorState attack = stateMachine.AddState("Attack_4", new Vector3(345f, 170f));
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
        attackTransition.AddCondition(AnimatorConditionMode.If, 0f, "Attack4");

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

    private static void ConfigurePlayerPrefab(AnimatorController controller, AnimationClip idleClip)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (player == null)
            throw new InvalidOperationException($"Missing player prefab at '{PlayerPrefabPath}'.");

        try
        {
            SpriteRenderer spriteRenderer = player.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                throw new InvalidOperationException("Player prefab requires a SpriteRenderer on its root.");

            Animator animator = player.GetComponent<Animator>();
            if (animator == null)
                animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController == null)
                throw new InvalidOperationException("Player prefab requires PlayerController.");
            SerializedObject playerControllerData = new SerializedObject(playerController);
            playerControllerData.FindProperty("animator").objectReferenceValue = animator;
            playerControllerData.ApplyModifiedPropertiesWithoutUndo();

            Sprite idleSprite = GetFirstSprite(idleClip);
            if (idleSprite == null)
                throw new InvalidOperationException("BK_unarmed_idle has no SpriteRenderer keyframes.");
            spriteRenderer.sprite = idleSprite;

            EditorUtility.SetDirty(spriteRenderer);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(playerController);
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    private static Sprite GetFirstSprite(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                continue;

            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keyframes.Length > 0)
                return keyframes[0].value as Sprite;
        }

        return null;
    }
}
