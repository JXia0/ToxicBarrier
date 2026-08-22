using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Top-down player controller: WASD movement (Rigidbody2D), face left/right by movement direction,
/// and numeric inventory input (keys 1-5) for equipment/materials.
/// Default facing: right. Attach to the player GameObject with a Rigidbody2D (set to Dynamic, freeze rotation Z).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
    private static readonly int Attack4Parameter = Animator.StringToHash("Attack4");
    private static readonly int Attack4State = Animator.StringToHash("Attack_4");

    [Header("Movement")]
    [SerializeField] private float inputDeadZone = 0.1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Movement speed by map ring")]
    [Tooltip("Current scene: Map 1 outer radius.")]
    [SerializeField] private float map1Radius = 12.5f;
    [Tooltip("Current scene: Map 2 outer radius.")]
    [SerializeField] private float map2Radius = 22.5f;
    [Tooltip("12.5 units in 6 seconds.")]
    [SerializeField] private float map1MoveSpeed = 2.0833f;
    [Tooltip("10 units in 8.5 seconds.")]
    [SerializeField] private float map2MoveSpeed = 1.1765f;
    [Tooltip("12.5 units in 13 seconds.")]
    [SerializeField] private float map3MoveSpeed = 0.9615f;

    private Rigidbody2D _rb;
    private float _lastHorizontal;
    private float _facingScaleMagnitude = 1f;
    private bool _controlEnabled = true;
    private bool _actionLocked;
    private Coroutine _actionRoutine;

    /// <summary> Fired when player presses 1-5. Parameter is slot index (0-4). </summary>
    public System.Action<int> OnInventorySlotPressed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (_rb != null)
            _rb.freezeRotation = true;

        transform.rotation = Quaternion.identity;
        _facingScaleMagnitude = Mathf.Max(Mathf.Abs(transform.localScale.x), 0.0001f);
        SetMovingAnimation(false);
    }

    private void Update()
    {
        if (_rb == null || !_controlEnabled || _actionLocked)
        {
            SetMovingAnimation(false);
            return;
        }

        HandleMovementInput();
        FaceMovementDirection();
        HandleInventoryInput();
    }

    public void SetControlEnabled(bool enabled)
    {
        _controlEnabled = enabled;
        if (!enabled)
            StopMovement();
    }

    public void StopMovement()
    {
        if (_rb != null)
            _rb.velocity = Vector2.zero;
        SetMovingAnimation(false);
    }

    /// <summary>
    /// Locks movement, interrupts locomotion, and plays the tower-upgrade attack once.
    /// The callback runs only after the attack animation has finished.
    /// </summary>
    public bool PlayTowerUpgradeAnimation(Vector3 targetPosition, Action onAnimationFinished)
    {
        if (_actionLocked || !isActiveAndEnabled || animator == null || animator.runtimeAnimatorController == null)
            return false;

        _actionLocked = true;
        StopMovement();
        FaceTowards(targetPosition);
        animator.ResetTrigger(Attack4Parameter);
        animator.SetTrigger(Attack4Parameter);
        _actionRoutine = StartCoroutine(WaitForTowerUpgradeAnimation(onAnimationFinished));
        return true;
    }

    private void OnDisable()
    {
        if (_actionRoutine != null)
        {
            StopCoroutine(_actionRoutine);
            _actionRoutine = null;
        }
        _actionLocked = false;
        StopMovement();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(horizontal) < inputDeadZone) horizontal = 0f;
        if (Mathf.Abs(vertical) < inputDeadZone) vertical = 0f;

        if (Mathf.Abs(horizontal) >= inputDeadZone)
            _lastHorizontal = horizontal;

        Vector2 moveDir = new Vector2(horizontal, vertical);
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        _rb.velocity = moveDir * GetMoveSpeed();
        SetMovingAnimation(moveDir.sqrMagnitude > 0f);
    }

    private void FaceMovementDirection()
    {
        if (Mathf.Abs(_lastHorizontal) < inputDeadZone) return;
        SetHorizontalFacing(_lastHorizontal);
    }

    private void FaceTowards(Vector3 targetPosition)
    {
        float horizontalDirection = targetPosition.x - transform.position.x;
        if (Mathf.Abs(horizontalDirection) < inputDeadZone) return;

        _lastHorizontal = horizontalDirection;
        SetHorizontalFacing(horizontalDirection);
    }

    private void SetHorizontalFacing(float horizontalDirection)
    {
        float scaleX = Mathf.Sign(horizontalDirection) * _facingScaleMagnitude;
        transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
    }

    private void SetMovingAnimation(bool isMoving)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(IsMovingParameter, isMoving);
    }

    private IEnumerator WaitForTowerUpgradeAnimation(Action onAnimationFinished)
    {
        bool enteredAttackState = false;
        float elapsed = 0f;
        const float maximumWait = 2f;

        while (elapsed < maximumWait)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool isAttackState = state.shortNameHash == Attack4State;
            enteredAttackState |= isAttackState;

            if (enteredAttackState && !isAttackState && !animator.IsInTransition(0))
                break;
            if (isAttackState && state.normalizedTime >= 1f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _actionRoutine = null;
        onAnimationFinished?.Invoke();
        _actionLocked = false;
        SetMovingAnimation(false);
    }

    private void HandleInventoryInput()
    {
        for (int i = 0; i < 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                OnInventorySlotPressed?.Invoke(i);
            }
        }
    }

    public float GetMoveSpeed()
    {
        float distanceFromCenter = ((Vector2)transform.position).magnitude;
        if (distanceFromCenter <= map1Radius) return map1MoveSpeed;
        if (distanceFromCenter <= map2Radius) return map2MoveSpeed;
        return map3MoveSpeed;
    }
}
