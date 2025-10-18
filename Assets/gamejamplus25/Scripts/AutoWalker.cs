using UnityEngine;

public class AutoWalker : MonoBehaviour
{
    public float speed = 3f;
    public float changeDirTime = 2f;
    private Vector3 direction;
    private Rigidbody rb;
    private float timer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ChooseNewDirection();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= changeDirTime)
        {
            ChooseNewDirection();
            timer = 0;
        }
    }

    void FixedUpdate()
    {
        // Move o cilindro na direção atual
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    void ChooseNewDirection()
    {
        // Escolhe direção aleatória no plano XZ
        float angle = Random.Range(0f, 360f);
        direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
    }

    void OnCollisionEnter(Collision col)
    {
        // Ao colidir com bordas, muda de direção
        if (col.gameObject.name.Contains("Borda") || col.gameObject.name.Contains("Mesa"))
        {
            ChooseNewDirection();
        }
    }
}
