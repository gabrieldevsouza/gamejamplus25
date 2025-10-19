using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class WaypointMover : MonoBehaviour
{
    [Header("Path")]
    public Transform[] waypoints;
    public float speed = 8f;
    public float rotationSpeed = 5f;
    public float stopDistance = 0.1f;
    public float pauseTime = 1.3f;
    public bool randomizeSpeed = true;

    [Header("Feixe de Visão")]
    public float viewRadius = 5f;
    [Range(0, 360)] public float viewAngle = 45f;
    public int segments = 20;
    public MeshFilter visionConeMesh;

    [Header("Detecção")]
    [Tooltip("Layers that block vision (walls, props). If empty, ANY collider blocks.")]
    public LayerMask obstructionMask;
    public Transform player;                 // arraste o player aqui
    public float checkInterval = 0.1f;
    [Tooltip("Ray starts a bit above and in front to avoid hitting own collider.")]
    public float eyeHeight = 0.6f;
    public float eyeForwardOffset = 0.2f;
    public bool debugDraw = true;

    int currentIndex = 0;
    Rigidbody rb;
    bool isWaiting = false;
    float _nextCheck;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
            currentIndex = 1 % waypoints.Length;
        }

        if (randomizeSpeed) speed *= Random.Range(2f, 2.5f);

        BuildConeMesh();
    }

    void FixedUpdate()
    {
        MoveAlongWaypoints();
        BuildConeMesh();

        if (Time.time >= _nextCheck)
        {
            _nextCheck = Time.time + Mathf.Max(0.02f, checkInterval);
            CheckPlayerInView();
        }
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

    void CheckPlayerInView()
    {
        if (!player) return;

        // Distance & angle (flattened)
        Vector3 toPlayer = player.position - transform.position;
        Vector3 toPlayerFlat = new Vector3(toPlayer.x, 0f, toPlayer.z);
        float dist = toPlayerFlat.magnitude;
        if (dist > viewRadius) return;

        float angle = Vector3.Angle(transform.forward, toPlayerFlat.normalized);
        if (angle > viewAngle * 0.5f) return;

        // Eye origin slightly offset to avoid self-hit
        Vector3 eye = transform.position + Vector3.up * eyeHeight + transform.forward * eyeForwardOffset;
        Vector3 dir = (player.position + Vector3.up * 0.5f - eye).normalized;

        // Raycast: treat any collider in front as blocking (unless it's the player)
        if (Physics.Raycast(eye, dir, out RaycastHit hit, viewRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            bool blockedByMask = obstructionMask != 0 &&
                                 ((1 << hit.collider.gameObject.layer) & obstructionMask) != 0;

            if (hit.transform == player)
            {
                if (debugDraw) Debug.DrawRay(eye, dir * dist, Color.green, checkInterval);
                Debug.Log($"[GameOver] Player seen by {name}!");
            }
            else
            {
                // If mask set → only those layers block. If mask empty → anything blocks.
                bool blocks = (obstructionMask == 0) ? true : blockedByMask;
                if (debugDraw) Debug.DrawRay(eye, dir * Mathf.Min(viewRadius, (player.position - eye).magnitude), Color.red, checkInterval);
                if (!blocks)
                {
                    // try see past the hit (rare): cast to player directly and check clear
                    float toPlayerDist = Vector3.Distance(eye, player.position);
                    if (!Physics.Raycast(eye, dir, toPlayerDist, obstructionMask, QueryTriggerInteraction.Ignore))
                    {
                        if (debugDraw) Debug.DrawRay(eye, dir * dist, Color.green, checkInterval);
                        Debug.Log($"[GameOver] Player seen by {name}!");
                    }
                }
            }
        }
    }

    void BuildConeMesh()
    {
        if (!visionConeMesh) return;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
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
