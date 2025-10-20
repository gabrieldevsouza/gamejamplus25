using System;
using UnityEngine;

public class MoneyPoint : MonoBehaviour
{
    [SerializeField] int enemyPointsValue = 100;
    private bool scored = false;

    private void OnTriggerEnter(Collider other)
    {
        if (scored) return;
        
        if (other.gameObject.CompareTag("Player"))
        {
            scored = true;
            ScoreManager.scoreManagerInstance.AddScore(enemyPointsValue);
            
            Destroy(gameObject);
        }
    }
}
