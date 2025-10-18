using System;
using UnityEngine;

public class LineForce : MonoBehaviour
{
    [Header("Aiming / Movement Gates")]
    [SerializeField] private float velocityToAim = 0.5f;   // can aim at/under this speed
    [SerializeField] private float stopVelocity   = 0.02f;  // considered fully stopped

    [Header("Shot Tuning (Distances in world units)")]
    [SerializeField] private float minDrawDistance = 0.35f; // under this, ignore shot
    [SerializeField] private float maxDrawDistance = 5.0f;  // clamp line length & shot strength
    [SerializeField] private float minShootSpeed   = 4.0f;  // launch speed at min dist
    [SerializeField] private float maxShootSpeed   = 20.0f; // launch speed at max dist

    [Header("UX (visual line only)")]
    [SerializeField] private float showLineDeadzone = 0.08f; // min drag to show preview line
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Camera camera;

    private Rigidbody _rigidbody;

    private bool _isIdle;
    private bool _isAiming;
    private bool _stuckInAim;

    private Vector3 _aimStartWorld;
    private Vector3 _lastWorldPoint;

    public float CurrentPowerT { get; private set; } // [0..1] normalized power for UI

    // ── Events for UI/other systems ──────────────────────────────────────────────
    public event Action AimStarted;
    public event Action<float> AimPowerChanged; // t in [0..1]
    public event Action AimEnded;               // fires on cancel or shoot

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _isAiming = false;
        _isIdle   = true;
        _stuckInAim = false;

        if (!camera) camera = Camera.main;

        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        // Start aiming when allowed and mouse is pressed
        if (_isIdle && Input.GetMouseButtonDown(0))
        {
            Vector3? start = CastMouseClickRay();
            if (start.HasValue)
            {
                _isAiming = true;
                _stuckInAim = true;
                _aimStartWorld = FlattenToBallY(start.Value);
                Stop(); // freeze immediately while aiming

                CurrentPowerT = 0f;
                AimStarted?.Invoke();
                AimPowerChanged?.Invoke(CurrentPowerT);
            }
        }

        if (_isAiming)
        {
            Vector3? worldPoint = CastMouseClickRay();
            if (worldPoint.HasValue)
            {
                _lastWorldPoint = FlattenToBallY(worldPoint.Value);
                UpdatePreviewLine(_aimStartWorld, _lastWorldPoint); // updates CurrentPowerT + event
            }
            else
            {
                if (lineRenderer && lineRenderer.enabled) lineRenderer.enabled = false;
                // still aiming; power stays as last computed
            }

            if (Input.GetMouseButtonUp(0))
            {
                TryShoot(_aimStartWorld, _lastWorldPoint);
            }
        }
        else
        {
            if (lineRenderer && lineRenderer.enabled) lineRenderer.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        float currentSpeed = _rigidbody.linearVelocity.magnitude;

        _isIdle = currentSpeed <= velocityToAim;

        if (currentSpeed < stopVelocity)
        {
            Stop();
        }
    }

    // ── Core actions ─────────────────────────────────────────────────────────────
    private void TryShoot(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float rawDist = delta.magnitude;

        // Cancel if too short
        if (rawDist < minDrawDistance)
        {
            EndAim();
            return;
        }

        float clampedDist = Mathf.Min(rawDist, maxDrawDistance);
        Vector3 direction = (delta.sqrMagnitude > 0.0001f) ? delta.normalized : Vector3.zero;

        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clampedDist);
        float desiredSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, t);

        Shoot(direction, desiredSpeed);
        EndAim();
    }

    private void Shoot(Vector3 direction, float desiredSpeed)
    {
        if (lineRenderer) lineRenderer.enabled = false;
        if (direction == Vector3.zero) return;
        _isIdle = false;

        // Impulse that yields desiredSpeed: J = m * Δv
        _rigidbody.AddForce(direction * (_rigidbody.mass * desiredSpeed), ForceMode.Impulse);
    }

    private void EndAim()
    {
        _isAiming = false;
        _stuckInAim = false;
        if (lineRenderer) lineRenderer.enabled = false;
        AimEnded?.Invoke();
    }

    private void UpdatePreviewLine(Vector3 origin, Vector3 cursor)
    {
        Vector3 delta = cursor - origin;
        float rawDist = delta.magnitude;

        // Compute power t ∈ [0..1] from distances
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, Mathf.Min(rawDist, maxDrawDistance));
        CurrentPowerT = Mathf.Clamp01(t);
        AimPowerChanged?.Invoke(CurrentPowerT);

        // Hide line inside deadzone
        if (!lineRenderer || rawDist < showLineDeadzone)
        {
            if (lineRenderer && lineRenderer.enabled) lineRenderer.enabled = false;
            return;
        }

        Vector3 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward;
        float drawDist = Mathf.Clamp(rawDist, showLineDeadzone, maxDrawDistance);
        Vector3 clampedEnd = origin + dir * drawDist;

        if (lineRenderer.positionCount != 2) lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, clampedEnd);
        lineRenderer.enabled = true;
    }

    private void Stop()
    {
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _isIdle = true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────
    private Vector3 FlattenToBallY(Vector3 v) => new Vector3(v.x, transform.position.y, v.z);

    private Vector3? CastMouseClickRay()
    {
        if (!camera) return null;

        Vector3 screenMousePosFar  = new Vector3(Input.mousePosition.x, Input.mousePosition.y, camera.farClipPlane);
        Vector3 screenMousePosNear = new Vector3(Input.mousePosition.x, Input.mousePosition.y, camera.nearClipPlane);

        Vector3 worldMousePosFar  = camera.ScreenToWorldPoint(screenMousePosFar);
        Vector3 worldMousePosNear = camera.ScreenToWorldPoint(screenMousePosNear);

        if (Physics.Raycast(worldMousePosNear, worldMousePosFar - worldMousePosNear, out RaycastHit hit, float.PositiveInfinity))
            return hit.point;

        return null;
    }
}
