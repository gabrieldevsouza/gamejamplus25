using UnityEngine;
using System.Collections;

public class WaypointMover : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 5f;
    public float rotationSpeed = 5f;       // Suaviza a rotação
    public float stopDistance = 0.1f;
    public float pauseTime = 1.5f;         // Tempo de pausa nos waypoints
    public bool randomizeSpeed = true;     // Velocidade levemente variável

    private int currentIndex = 0;
    private Rigidbody rb;
    private bool isWaiting = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
            currentIndex = 1 % waypoints.Length;
        }

        if (randomizeSpeed)
            speed *= Random.Range(1.0f, 1.3f);
    }

    void FixedUpdate()
    {
        if (waypoints.Length == 0 || isWaiting) return;

        Transform target = waypoints[currentIndex];
        Vector3 direction = (target.position - transform.position);
        direction.y = 0;

        if (direction.magnitude < stopDistance)
        {
            StartCoroutine(WaitAtWaypoint());
            currentIndex = (currentIndex + 1) % waypoints.Length;
            return;
        }

        // Move suavemente
        Vector3 move = direction.normalized * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        // Rotaciona suavemente em direção ao movimento
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;

        // Pequena variação de pausa para não ficarem sincronizados
        float wait = randomizeSpeed ? pauseTime * Random.Range(0.8f, 1.2f) : pauseTime;
        yield return new WaitForSeconds(wait);

        isWaiting = false;
    }
}
