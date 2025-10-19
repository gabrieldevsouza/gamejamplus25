using UnityEngine;

public class EnemyPoints : MonoBehaviour
{
    [SerializeField] int enemyPointsValue = 100;
    private bool scored = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (scored) return;
        
        if (collision.gameObject.CompareTag("Player"))
        {
            scored = true;
            ScoreManager.scoreManagerInstance.AddScore(enemyPointsValue);
            
            //Destroy(gameObject);
        }
    }
}
