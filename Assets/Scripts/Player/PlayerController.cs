using UnityEngine;

/// <summary>
/// Top-down player controller: WASD movement (Rigidbody2D), face mouse cursor,
/// and numeric inventory input (keys 1-5) for equipment/materials.
/// Attach to the player GameObject with a Rigidbody2D (set to Dynamic, freeze rotation Z).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float inputDeadZone = 0.1f;

    [Header("Look")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("Use 0 for sprite facing up (default), 90 for right.")]
    [SerializeField] private float spriteFacingAngleOffset = 0f;

    private Rigidbody2D _rb;

    /// <summary> Fired when player presses 1-5. Parameter is slot index (0-4). </summary>
    public System.Action<int> OnInventorySlotPressed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
            _rb.freezeRotation = true;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_rb == null) return;

        HandleMovementInput();
        FaceMouseCursor();
        HandleInventoryInput();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(horizontal) < inputDeadZone) horizontal = 0f;
        if (Mathf.Abs(vertical) < inputDeadZone) vertical = 0f;

        Vector2 moveDir = new Vector2(horizontal, vertical);
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        _rb.velocity = moveDir * moveSpeed;
    }

    private void FaceMouseCursor()
    {
        if (mainCamera == null) return;

        Vector2 mouseScreen = Input.mousePosition;
        Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(mouseScreen);
        Vector2 dir = (mouseWorld - (Vector2)transform.position).normalized;

        if (dir.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - spriteFacingAngleOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
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
