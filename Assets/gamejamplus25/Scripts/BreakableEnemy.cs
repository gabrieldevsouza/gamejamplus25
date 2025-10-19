using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BreakableEnemy : MonoBehaviour
{
    [Tooltip("Minimum collision relative speed required to break this enemy.")]
    [SerializeField] private float impulseThreshold = 7.5f;

    public event Action Broken;
    public bool IsBroken { get; private set; }

    void OnCollisionEnter(Collision c)
    {
        if (IsBroken) return;

        if (c.relativeVelocity.magnitude >= impulseThreshold)
        {
            IsBroken = true;
            Broken?.Invoke();
            Destroy(gameObject);
        }
    }
}