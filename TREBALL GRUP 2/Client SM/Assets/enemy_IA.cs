using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public Transform player;      // Referencia al jugador
    public float speed = 3f;      // Velocidad del enemigo
    public float detectionRange = 5f; // Distancia a la que detecta al jugador

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // Calcular la distancia al jugador
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Si el jugador está en el rango, perseguirlo
        if (distanceToPlayer < detectionRange)
        {
            MoveTowardsPlayer();
        }
        else
        {
            rb.velocity = Vector2.zero; // Se queda quieto si el jugador está fuera de rango
        }
    }

    void MoveTowardsPlayer()
    {
        // Dirección hacia el jugador
        Vector2 direction = (player.position - transform.position).normalized;

        // Aplicar velocidad
        rb.velocity = new Vector2(direction.x * speed, rb.velocity.y);

        // Voltear el sprite si es necesario
        if (direction.x > 0)
        {
            spriteRenderer.flipX = false; // Mirando a la derecha
        }
        else if (direction.x < 0)
        {
            spriteRenderer.flipX = true; // Mirando a la izquierda
        }
    }
}
