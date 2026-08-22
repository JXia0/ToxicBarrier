using UnityEngine;

/// <summary>
/// Top-down player controller: WASD movement (Rigidbody2D), face left/right by movement direction,
/// and numeric inventory input (keys 1-5) for equipment/materials.
/// Default facing: right. Attach to the player GameObject with a Rigidbody2D (set to Dynamic, freeze rotation Z).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float inputDeadZone = 0.1f;

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
    private bool _controlEnabled = true;

    /// <summary> Fired when player presses 1-5. Parameter is slot index (0-4). </summary>
    public System.Action<int> OnInventorySlotPressed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
            _rb.freezeRotation = true;

        transform.rotation = Quaternion.identity;

    }

    private void Update()
    {
        if (_rb == null || !_controlEnabled) return;

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
    }

    private void OnDisable()
    {
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
    }

    private void FaceMovementDirection()
    {
        if (Mathf.Abs(_lastHorizontal) < inputDeadZone) return;
        float scaleX = Mathf.Sign(_lastHorizontal);
        transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
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
