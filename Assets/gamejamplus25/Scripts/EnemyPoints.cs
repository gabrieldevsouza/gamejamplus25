using UnityEngine;

public class EnemyPoints : MonoBehaviour
{
    public int pointsValue = 100;
    private bool scored = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (scored) return;
        
        if (collision.gameObject.CompareTag("Player"))
        {
            scored = true;
            ScoreManager.instance.AddScore(pointsValue);
            
            Destroy(gameObject);
        }
    }
}
