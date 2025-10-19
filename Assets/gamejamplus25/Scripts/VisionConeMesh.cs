using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VisionConeMesh : MonoBehaviour
{
    public float viewRadius = 5f;       // Alcance do feixe
    [Range(0, 360)]
    public float viewAngle = 45f;       // Ângulo do feixe
    public int segments = 20;           // Quanto mais segmentos, mais suave

    private MeshFilter meshFilter;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        BuildConeMesh();
    }

    void BuildConeMesh()
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2]; // topo + base
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero; // topo do cone (na posição do objeto)

        float angleStep = viewAngle / segments;
        float halfAngle = viewAngle / 2f;

        // Base do cone
        for (int i = 0; i <= segments; i++)
        {
            float angle = -halfAngle + angleStep * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            vertices[i + 1] = dir * viewRadius;
        }

        // Triângulos do cone
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }
}
