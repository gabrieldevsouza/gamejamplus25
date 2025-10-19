using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LevelGoal : MonoBehaviour
{
    [Header("Next Level Settings")]
    [Tooltip("Name of the next scene to load when all enemies are destroyed.")]
    [SerializeField] private string nextSceneName;

    [Tooltip("Delay in seconds before loading the next scene.")]
    [SerializeField] private float loadDelay = 1.5f;

    [Tooltip("Optional win panel or effect to show before switching scenes.")]
    [SerializeField] private GameObject winPanel;

    BreakableEnemy[] _targets;
    int _remaining;

    void Start()
    {
        _targets = FindObjectsOfType<BreakableEnemy>(true);
        _remaining = _targets.Length;

        foreach (var t in _targets)
            if (t != null) t.Broken += OnEnemyBroken;

        Debug.Log($"[LevelGoal] Started with {_remaining} targets.");
    }

    void OnEnemyBroken()
    {
        _remaining = Mathf.Max(0, _remaining - 1);
        Debug.Log($"[LevelGoal] Target broken. Remaining: {_remaining}");

        if (_remaining == 0)
        {
            Debug.Log("[LevelGoal] All enemies broken! Level complete!");
            if (winPanel) winPanel.SetActive(true);

            if (!string.IsNullOrEmpty(nextSceneName))
                StartCoroutine(LoadNextLevelAfterDelay());
            else
                Debug.LogWarning("[LevelGoal] No nextSceneName defined — staying in current scene.");
        }
    }

    IEnumerator LoadNextLevelAfterDelay()
    {
        yield return new WaitForSeconds(loadDelay);
        Debug.Log($"[LevelGoal] Loading next scene: {nextSceneName}");
        SceneManager.LoadScene(nextSceneName);
    }
}