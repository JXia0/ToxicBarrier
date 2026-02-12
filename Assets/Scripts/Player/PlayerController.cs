using UnityEngine;

/// <summary>
/// Top-down player controller: WASD movement (Rigidbody2D), face left/right by movement direction,
/// and numeric inventory input (keys 1-5) for equipment/materials.
/// Default facing: right. Attach to the player GameObject with a Rigidbody2D (set to Dynamic, freeze rotation Z).
/// Ensures player sprite renders in front of map zones when crossing edges (sorting order).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float inputDeadZone = 0.1f;

    [Header("Rendering (crossing map edge)")]
    [Tooltip("Sorting order for player sprite so it stays in front of map zone sprites when crossing edges. Map zones use 0.")]
    [SerializeField] private int playerSortingOrder = 10;

    private Rigidbody2D _rb;
    private float _lastHorizontal;

    /// <summary> Fired when player presses 1-5. Parameter is slot index (0-4). </summary>
    public System.Action<int> OnInventorySlotPressed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
            _rb.freezeRotation = true;

        transform.rotation = Quaternion.identity;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sortingOrder = playerSortingOrder;
    }

    private void Update()
    {
        if (_rb == null) return;

        HandleMovementInput();
        FaceMovementDirection();
        HandleInventoryInput();
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

        _rb.velocity = moveDir * moveSpeed;
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

    public void SetMoveSpeed(float speed)
    {
        if (speed < 0f) speed = 0f;
        moveSpeed = speed;
    }

    public float GetMoveSpeed() => moveSpeed;
}
