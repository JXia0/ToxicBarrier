using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float inputDeadZone = 0.1f;

    private Rigidbody2D rb2d;

    // Start is called before the first frame update
    void Start()
    {
        rb2d = this.GetComponent<Rigidbody2D>();
        if (rb2d == null)
        {
            Debug.LogError("Rigidbody2D component missing on Player! Add it to the moving object.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (rb2d == null) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        horizontal = Mathf.Abs(horizontal) < inputDeadZone ? 0f : horizontal;
        vertical = Mathf.Abs(vertical) < inputDeadZone ? 0f : vertical;

        Vector2 moveDir = transform.right * horizontal + transform.up * vertical;

        if (moveDir.magnitude > 1f)
        {
            moveDir.Normalize();
        }

        UpdatePlayerVelocity(moveDir);

        FlipSprite(horizontal);
    }

    private void FixedUpdate()
    {

    }

    private void UpdatePlayerVelocity(Vector2 moveDir)
    {
        rb2d.velocity = new Vector2(moveDir.x * moveSpeed, moveDir.y * moveSpeed);
    }

    public void SetMoveSpeed(float tierMoveScale)
    {
        if (tierMoveScale < 0f)
        {
            Debug.LogWarning("Attempted to set negative move speed! Clamping to 0.");
            tierMoveScale = 0f;
        }
        moveSpeed = tierMoveScale;
        Debug.Log($"Player move speed updated to: {moveSpeed}");
    }

    private void FlipSprite(float horizontalInput)
    {
        if (horizontalInput != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(horizontalInput), transform.localScale.y, transform.localScale.z);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(transform.right * moveSpeed * 0.5f));
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(transform.up * moveSpeed * 0.5f));
    }
}