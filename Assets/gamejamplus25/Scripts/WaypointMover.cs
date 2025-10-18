using UnityEngine;

public class WaypointMover : MonoBehaviour
{
    public Transform[] waypoints;  // Pontos a seguir
    public float speed = 3f;       // Velocidade de movimento
    public float stopDistance = 0.1f; // Distância para considerar "chegou"
    private int currentIndex = 0;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
            currentIndex = 1 % waypoints.Length;
        }
    }

    void FixedUpdate()
    {
        if (waypoints.Length == 0) return;

        Transform target = waypoints[currentIndex];
        Vector3 direction = (target.position - transform.position);
        direction.y = 0; // Mantém no plano da mesa

        // Move o cilindro suavemente até o waypoint
        if (direction.magnitude < stopDistance)
        {
            // Avança para o próximo waypoint (em loop)
            currentIndex = (currentIndex + 1) % waypoints.Length;
        }
        else
        {
            Vector3 move = direction.normalized * speed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + move);

            // Opcional: rotacionar para frente
            if (move != Vector3.zero)
                transform.forward = direction.normalized;
        }
    }
}
