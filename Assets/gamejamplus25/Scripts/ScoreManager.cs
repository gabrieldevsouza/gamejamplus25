using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager scoreManagerInstance;
    private int currentScore = 0;
    [SerializeField] TextMeshProUGUI scoreText;

    private void Awake()
    {
        if (scoreManagerInstance == null)
            scoreManagerInstance = this;
        else
            Destroy(gameObject);
    }

    public void AddScore(int points)
    {
        currentScore += points;
        if (scoreText != null)
            scoreText.text = currentScore.ToString();
    }
}
