using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Start is called before the first frame update
    private Rigidbody2D rb2d;
    [SerializeField] private float moveSpeed;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 moveDir = transform.right * horizontal + transform.up * vertical;
        rb2d.velocity = new Vector3(moveDir.x * moveSpeed, moveDir.y * moveSpeed, moveDir.z * moveSpeed);
    }
    void SetMoveSpeed(float tierMoveScale)
    {
        moveSpeed = tierMoveScale;
    }
}
