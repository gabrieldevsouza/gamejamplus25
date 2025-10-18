using System;
using UnityEngine;

public class LineForce : MonoBehaviour
{
    [Header("Movement Gates")]
    [Tooltip("Ball stops automatically when velocity ≤ this (no input).")]
    [SerializeField] private float autoStopSpeed = 0.05f;

    [Tooltip("Ball stops immediately if player tries to aim and velocity ≤ this.")]
    [SerializeField] private float inputStopSpeed = 0.2f;

    [Header("Shot Settings")]
    [SerializeField] private float minDrawDistance = 0.35f;
    [SerializeField] private float maxDrawDistance = 5f;
    [SerializeField] private float minShootSpeed = 4f;
    [SerializeField] private float maxShootSpeed = 20f;

    [Header("Threshold Hysteresis")]
    [SerializeField] private float unlockHysteresis = 0.05f;

    [Header("Visual Line")]
    [SerializeField] private float showLineDeadzone = 0.08f;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Color ghostLineColor = new(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color lockedLineColor = new(1f, 1f, 1f, 1f);

    [Header("Scene")]
    [SerializeField] private Camera camera;

    [Header("Debug")]
    [SerializeField] private bool debugSpeed = true; // ✅ toggle this in inspector

    private Rigidbody _rigidbody;
    private bool _isAiming;
    private bool _isLocked;
    private bool _canAim;

    private Vector3 _cursorWorld;
    private Vector3 _lockOrigin;
    private float _lastDebugTime;

    public float CurrentPowerT { get; private set; }

    public event Action AimStarted;
    public event Action<float> AimPowerChanged;
    public event Action AimEnded;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (!camera) camera = Camera.main;

        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        float speed = _rigidbody.linearVelocity.magnitude;

        // 🔍 DEBUG SECTION
        if (debugSpeed && Time.time - _lastDebugTime > 0.1f)
        {
            _lastDebugTime = Time.time;
            string state =
                _isAiming ? (_isLocked ? "LOCKED" : "AIMING") :
                (_canAim ? "IDLE" : "MOVING");
            Debug.Log($"[LineForce] Speed={speed:F3} | State={state} | autoStop={autoStopSpeed:F2} | inputStop={inputStopSpeed:F2}");
        }

        // ---------- INPUT START ----------
        if (Input.GetMouseButtonDown(0))
        {
            if (speed <= inputStopSpeed)
            {
                ForceStop();
                _canAim = true;
            }
            else
            {
                _canAim = false;
            }

            if (_canAim)
            {
                _isAiming = true;
                _isLocked = false;
                CurrentPowerT = 0f;
            }
        }

        // ---------- AIMING ----------
        if (_isAiming)
        {
            Vector3? hit = CastMouseClickRay();
            if (hit.HasValue)
            {
                _cursorWorld = FlattenToBallY(hit.Value);
                Vector3 origin = _isLocked ? _lockOrigin : FlattenToBallY(transform.position);

                float rawDist = (_cursorWorld - origin).magnitude;
                float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, Mathf.Min(rawDist, maxDrawDistance));
                CurrentPowerT = Mathf.Clamp01(t);
                AimPowerChanged?.Invoke(CurrentPowerT);

                if (!_isLocked && rawDist >= minDrawDistance)
                {
                    _isLocked = true;
                    _lockOrigin = FlattenToBallY(transform.position);
                    AimStarted?.Invoke();
                    origin = _lockOrigin;
                }
                else if (_isLocked && rawDist <= minDrawDistance - unlockHysteresis)
                {
                    _isLocked = false;
                    AimEnded?.Invoke();
                    origin = FlattenToBallY(transform.position);
                }

                UpdatePreviewLine(origin, _cursorWorld, _isLocked ? lockedLineColor : ghostLineColor);
            }

            // ---------- RELEASE ----------
            if (Input.GetMouseButtonUp(0))
            {
                if (_isLocked)
                    TryShoot(_lockOrigin, _cursorWorld);
                EndAim();
            }
        }
    }

    private void FixedUpdate()
    {
        float speed = _rigidbody.linearVelocity.magnitude;

        // ---------- AUTO STOP ----------
        if (!_isAiming && speed <= autoStopSpeed)
        {
            ForceStop();
            _canAim = true;
        }
    }

    // ────────────────────────────────────────────────
    private void TryShoot(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float rawDist = delta.magnitude;
        if (rawDist < minDrawDistance) return;

        float clamped = Mathf.Min(rawDist, maxDrawDistance);
        Vector3 direction = (origin - target).normalized; // opposite
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clamped);
        float launchSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, t);

        _rigidbody.AddForce(direction * (_rigidbody.mass * launchSpeed), ForceMode.Impulse);
        _canAim = false;
    }

    private void EndAim()
    {
        _isAiming = false;
        _isLocked = false;
        if (lineRenderer) lineRenderer.enabled = false;
        AimEnded?.Invoke();
    }

    private void UpdatePreviewLine(Vector3 origin, Vector3 cursor, Color color)
    {
        if (!lineRenderer) return;
        Vector3 delta = cursor - origin;
        if (delta.magnitude < showLineDeadzone)
        {
            if (lineRenderer.enabled) lineRenderer.enabled = false;
            return;
        }

        Vector3 dir = delta.normalized;
        Vector3 end = origin + dir * Mathf.Min(delta.magnitude, maxDrawDistance);
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.enabled = true;
    }

    private void ForceStop()
    {
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private Vector3 FlattenToBallY(Vector3 v) => new(v.x, transform.position.y, v.z);

    private Vector3? CastMouseClickRay()
    {
        if (!camera) return null;
        Vector3 near = new(Input.mousePosition.x, Input.mousePosition.y, camera.nearClipPlane);
        Vector3 far  = new(Input.mousePosition.x, Input.mousePosition.y, camera.farClipPlane);
        Vector3 nearW = camera.ScreenToWorldPoint(near);
        Vector3 farW  = camera.ScreenToWorldPoint(far);
        if (Physics.Raycast(nearW, farW - nearW, out RaycastHit hit, float.PositiveInfinity))
            return hit.point;
        return null;
    }
}
