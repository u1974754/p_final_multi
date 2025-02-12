using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_movement : MonoBehaviour
{
    public float speed = 5f;        // Velocidad del personaje
    public float jumpForce = 10f;   // Fuerza del salto
    private bool isGrounded;        // ¿Está tocando el suelo?

    private Rigidbody2D rb;
    private Collider2D col;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        // Movimiento horizontal
        float moveInput = Input.GetAxis("Horizontal"); // Usa "A/D" o "?/?"
        rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

        // Salto
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }
    }

    // Detectar si el jugador está en el suelo
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}
