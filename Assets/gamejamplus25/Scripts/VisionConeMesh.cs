using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VisionConeMesh : MonoBehaviour
{
    [Header("Cone Shape (also drives the mesh)")]
    public float viewRadius = 5f;
    [Range(1f, 360f)] public float viewAngle = 60f;
    public int segments = 20;

    [Header("Origin")]
    [Tooltip("Where the cone tip actually is. If null, uses this object’s transform.")]
    public Transform originTransform;
    [Tooltip("Local-space offset from originTransform to the cone tip (e.g., (0,0.1,0) to lift, (0,0,0.15) to push forward).")]
    public Vector3 localTipOffset = Vector3.zero;

    [Header("Detection")]
    [Tooltip("Player target to detect. If empty, tries tag 'Player' at Start().")]
    public Transform player;
    [Tooltip("Layers that block vision (walls/props). If 0, ANY first hit blocks.")]
    public LayerMask obstructionMask = 0;
    public float checkInterval = 0.1f;
    public bool debugDraw = true;

    MeshFilter _meshFilter;
    float _nextCheck;

    void Start()
    {
        _meshFilter = GetComponent<MeshFilter>();
        BuildConeMesh();

        if (!originTransform) originTransform = transform;

        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
        }
    }

    void Update()
    {
        if (Time.time >= _nextCheck)
        {
            _nextCheck = Time.time + Mathf.Max(0.02f, checkInterval);
            CheckPlayerInView();
        }
    }

    // -------- Detection --------
    void CheckPlayerInView()
    {
        if (!player || !originTransform) return;

        // cone tip in world space, from explicit origin + local offset
        Vector3 tip = originTransform.position + originTransform.rotation * localTipOffset;

        Vector3 target = player.position; // center of ball is fine for now
        Vector3 toTarget = target - tip;
        float dist = toTarget.magnitude;

        // 1) Strict radius
        if (dist > viewRadius) { DrawRay(tip, toTarget.normalized * viewRadius, Color.gray); return; }

        // 2) Angle (flattened) using the cone's forward (this object's forward)
        Vector3 fwdFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 dirFlat = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
        float cosHalf = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        if (Vector3.Dot(fwdFlat, dirFlat) < cosHalf) { DrawRay(tip, toTarget.normalized * dist, Color.blue); return; }

        // 3) Line of sight
        Vector3 dir = toTarget.normalized;
        float rayLen = Mathf.Min(viewRadius, dist);

        if (obstructionMask == 0)
        {
            // First hit must be the player
            if (Physics.Raycast(tip, dir, out RaycastHit hit, rayLen, ~0, QueryTriggerInteraction.Ignore)
                && hit.transform == player)
            {
                DrawRay(tip, dir * hit.distance, Color.green);
                Debug.Log("[GameOver] Player seen!");
            }
            else
            {
                DrawRay(tip, dir * rayLen, Color.red);
            }
        }
        else
        {
            // Any collider on mask before player blocks
            bool blocked = Physics.Raycast(tip, dir, rayLen, obstructionMask, QueryTriggerInteraction.Ignore);
            if (blocked) { DrawRay(tip, dir * rayLen, Color.red); return; }

            if (Physics.Raycast(tip, dir, out RaycastHit hit2, rayLen, ~0, QueryTriggerInteraction.Ignore)
                && hit2.transform == player)
            {
                DrawRay(tip, dir * hit2.distance, Color.green);
                Debug.Log("[GameOver] Player seen!");
            }
            else
            {
                DrawRay(tip, dir * rayLen, Color.red);
            }
        }
    }

    void DrawRay(Vector3 from, Vector3 vec, Color c)
    {
        if (!debugDraw) return;
        Debug.DrawLine(from, from + vec, c, checkInterval);
    }

    // -------- Mesh --------
    void BuildConeMesh()
    {
        if (!_meshFilter) _meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2]; // tip + ring
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero; // local tip at (0,0,0) of THIS object

        float angleStep = viewAngle / segments;
        float halfAngle = viewAngle * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float ang = -halfAngle + i * angleStep;
            Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
            vertices[i + 1] = dir * viewRadius;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        _meshFilter.mesh = mesh;
    }
}
