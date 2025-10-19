using UnityEngine;

public class LevelGoal : MonoBehaviour
{
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
            Debug.Log("[LevelGoal] All enemies broken! Level complete!");
    }
}