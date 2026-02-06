using UnityEngine;

/// <summary>
/// Legacy/alternative top-down movement (WASD + flip sprite). Use PlayerController for full 3C + inventory.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float inputDeadZone = 0.1f;

    private Rigidbody2D rb2d;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d == null)
            Debug.LogError("Rigidbody2D component missing on Player! Add it to the moving object.");
    }

    void Update()
    {
        if (rb2d == null) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        horizontal = Mathf.Abs(horizontal) < inputDeadZone ? 0f : horizontal;
        vertical = Mathf.Abs(vertical) < inputDeadZone ? 0f : vertical;

        Vector2 moveDir = transform.right * horizontal + transform.up * vertical;
        if (moveDir.magnitude > 1f) moveDir.Normalize();

        rb2d.velocity = new Vector2(moveDir.x * moveSpeed, moveDir.y * moveSpeed);
        FlipSprite(horizontal);
    }

    public void SetMoveSpeed(float tierMoveScale)
    {
        moveSpeed = Mathf.Max(0f, tierMoveScale);
    }

    private void FlipSprite(float horizontalInput)
    {
        if (horizontalInput != 0)
            transform.localScale = new Vector3(Mathf.Sign(horizontalInput), transform.localScale.y, transform.localScale.z);
    }
}
