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
    [Range(1f, 180f)] public float viewAngle = 60f; // use half-angle math (<=180)
    public int segments = 20;
    public MeshFilter visionConeMesh;

    [Header("Detecção")]
    [Tooltip("Layers that block vision (walls, props). If empty, any collider blocks.")]
    public LayerMask obstructionMask;
    public Transform player;                 // drag the player/ball here
    public float checkInterval = 0.1f;
    public float eyeHeight = 0.6f;
    public float eyeForwardOffset = 0.15f;
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

        // Flatten to horizontal plane
        Vector3 toPlayer = player.position - transform.position;
        Vector3 toPlayerFlat = new Vector3(toPlayer.x, 0f, toPlayer.z);
        float dist = toPlayerFlat.magnitude;
        if (dist > viewRadius) { DrawRay(Color.gray, dist); return; }

        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 dirFlat = toPlayerFlat.normalized;

        // Angle test via dot product (robust)
        float cosHalf = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        float dot = Vector3.Dot(fwd, dirFlat);
        if (dot < cosHalf) { DrawRay(Color.blue, dist); return; } // outside cone

        // Eye origin slightly offset to avoid self-hit
        Vector3 eye = transform.position + Vector3.up * eyeHeight + transform.forward * eyeForwardOffset;
        Vector3 dir3D = (player.position + Vector3.up * 0.3f - eye).normalized;

        // Raycast for line of sight
        float rayLen = Mathf.Min(viewRadius, Vector3.Distance(eye, player.position));
        if (Physics.Raycast(eye, dir3D, out RaycastHit hit, rayLen, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == player)
            {
                DrawRay(Color.green, hit.distance);
                Debug.Log($"[GameOver] Player seen by {name}!");
            }
            else
            {
                // Blocked? If no mask set, any first hit blocks. If mask set, only those layers block.
                bool blocks = obstructionMask == 0
                    ? true
                    : ((1 << hit.collider.gameObject.layer) & obstructionMask) != 0;

                DrawRay(Color.red, hit.distance);
                if (!blocks)
                {
                    // Try direct ray constrained to mask only (optional fallback)
                    if (!Physics.Raycast(eye, dir3D, rayLen, obstructionMask, QueryTriggerInteraction.Ignore))
                    {
                        DrawRay(Color.green, rayLen);
                        Debug.Log($"[GameOver] Player seen by {name}!");
                    }
                }
            }
        }
    }

    void DrawRay(Color c, float len)
    {
        if (!debugDraw) return;
        Vector3 eye = transform.position + Vector3.up * eyeHeight + transform.forward * eyeForwardOffset;
        Vector3 to = (player ? (player.position + Vector3.up * 0.3f) : (transform.position + transform.forward * len));
        Debug.DrawLine(eye, eye + (to - eye).normalized * len, c, checkInterval);
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

    // Optional: visualize FOV in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 pos = Application.isPlaying ? transform.position : transform.position;
        Gizmos.DrawWireSphere(pos, viewRadius);

        float half = viewAngle * 0.5f;
        Quaternion left  = Quaternion.AngleAxis(-half, Vector3.up);
        Quaternion right = Quaternion.AngleAxis( half, Vector3.up);
        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos + right * fwd * viewRadius);
        Gizmos.DrawLine(pos, pos + left  * fwd * viewRadius);
    }
}
