using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BreakableEnemy))]
public class EnemyPoints : MonoBehaviour
{
    [SerializeField] private int enemyPointsValue = 100;

    BreakableEnemy _breakable;
    bool _awarded;

    void Awake()
    {
        _breakable = GetComponent<BreakableEnemy>();
    }

    void OnEnable()
    {
        if (_breakable != null)
            _breakable.Broken += OnBroken;
    }

    void OnDisable()
    {
        if (_breakable != null)
            _breakable.Broken -= OnBroken;
    }

    void OnBroken()
    {
        if (_awarded) return;
        _awarded = true;

        var sm = ScoreManager.scoreManagerInstance;
        if (sm != null)
            sm.AddScore(enemyPointsValue);
        else
            Debug.LogWarning("[EnemyPoints] ScoreManager instance not found.");
    }
}