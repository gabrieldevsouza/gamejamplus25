using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class WaypointMover : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 8f;
    public float rotationSpeed = 5f;
    public float stopDistance = 0.1f;
    public float pauseTime = 1.3f;
    public bool randomizeSpeed = true;

    [Header("Feixe de Visão")]
    public float viewRadius = 5f;
    [Range(0, 360)]
    public float viewAngle = 45f;
    public int segments = 20;
    public MeshFilter visionConeMesh; // Arraste o MeshFilter do objeto VisionCone aqui

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
            speed *= Random.Range(2f, 2.5f);

        BuildConeMesh();
    }

    void FixedUpdate()
    {
        MoveAlongWaypoints();
        // Atualiza o mesh caso queira dinâmica (não necessário se cone fixo)
        BuildConeMesh();
    }

    void MoveAlongWaypoints()
    {
        if (waypoints.Length == 0 || isWaiting) return;

        Transform target = waypoints[currentIndex];
        Vector3 direction = target.position - transform.position;
        direction.y = 0;

        if (direction.magnitude < stopDistance)
        {
            StartCoroutine(WaitAtWaypoint());
            currentIndex = (currentIndex + 1) % waypoints.Length;
            return;
        }

        Vector3 move = direction.normalized * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;
        float wait = randomizeSpeed ? pauseTime * Random.Range(0.8f, 1.2f) : pauseTime;
        yield return new WaitForSeconds(wait);
        isWaiting = false;
    }

    void BuildConeMesh()
    {
        if (!visionConeMesh) return;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero; // topo

        float angleStep = viewAngle / segments;
        float halfAngle = viewAngle / 2f;

        for (int i = 0; i <= segments; i++)
        {
            float angle = -halfAngle + angleStep * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            vertices[i + 1] = dir * viewRadius;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        visionConeMesh.mesh = mesh;
    }
}
