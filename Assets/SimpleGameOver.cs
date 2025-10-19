using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleGameOver : MonoBehaviour
{
    public static SimpleGameOver Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject scoreUI; // 👈 assign your Score UI Canvas or TMP object here

    [Header("Behavior")]
    [SerializeField] private bool pauseOnGameOver = true;
    [SerializeField] private bool allowRestartKey = true;
    [SerializeField] private KeyCode restartKey = KeyCode.R;

    bool _fired;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (losePanel) losePanel.SetActive(false);
    }

    void Update()
    {
        if (_fired && allowRestartKey && Input.GetKeyDown(restartKey))
        {
            if (pauseOnGameOver) Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void Trigger()
    {
        if (_fired) return;
        _fired = true;

        // ✅ Hide Score UI
        if (scoreUI) scoreUI.SetActive(false);

        // ✅ Show Lose Panel
        if (losePanel) losePanel.SetActive(true);
        else Debug.LogWarning("[GameOver] No Lose Panel assigned to SimpleGameOver.");

        //if (pauseOnGameOver) Time.timeScale = 0f;

        Debug.Log("[GameOver] Player seen! Game Over triggered.");
    }
}