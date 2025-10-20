using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LineForce : MonoBehaviour
{
    [Header("Shot Settings")]
    [SerializeField] private float minDrawDistance = 0.35f;
    [SerializeField] private float maxDrawDistance = 5f;
    [SerializeField] private float minShootSpeed   = 4f;
    [SerializeField] private float maxShootSpeed   = 20f;

    [Header("Draw Hysteresis")]
    [SerializeField] private float unlockHysteresis = 0.05f;

    [Header("Visual Line")]
    [SerializeField] private float showLineDeadzone = 0.08f;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Color ghostLineColor  = new(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color lockedLineColor = new(1f, 1f, 1f, 1f);
    [Tooltip("If false, this component won't render the line (external presenter handles visuals).")]
    [SerializeField] private bool renderLineHere = false;

    [Header("Scene")]
    [SerializeField] private Camera camera;

    [Header("Refs")]
    [SerializeField] private SlopeProbe    slopeProbe;
    [SerializeField] private SettleManager settle;

    // Trajectory preview component (optional)
    [SerializeField] private Projection projection;

    [Header("Debug")]
    [SerializeField] private bool debugState = true;

    Rigidbody _rb;

    // Aim state
    bool _inputHeld;
    bool _isAiming;
    bool _isLocked;
    public float CurrentPowerT { get; private set; }

    // Public read-only accessors for external presenters
    public float MinDrawDistance => minDrawDistance;
    public float MaxDrawDistance => maxDrawDistance;
    public bool  IsLocked        => _isLocked;
    public Vector3 LockOrigin    => _lockOrigin;

    // UI events
    public event Action        AimStarted;
    public event Action<float> AimPowerChanged;
    public event Action        AimEnded;

    // Line points
    Vector3 _cursorWorld;
    Vector3 _lockOrigin;

    float _lastDbg;

    // --- Pointer adapter (mouse or single-touch) ---
    Vector2 _pointerPos;
    bool _pointerDownThisFrame;
    bool _pointerUpThisFrame;
    bool _pointerHeld;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!camera)     camera     = Camera.main;
        if (!slopeProbe) slopeProbe = GetComponent<SlopeProbe>();
        if (!settle)     settle     = GetComponent<SettleManager>();

        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    void Update()
    {
        PollPrimaryPointer(); // unified input

        float speed = _rb.linearVelocity.magnitude;

        if (debugState && Time.time - _lastDbg > 0.1f)
        {
            _lastDbg = Time.time;
            string timers =
                settle.InCooldown ? $"COOLDOWN {settle.InCooldown}" :
                (settle.CanAimFromAuto || settle.CanAimFromInput) ? "IDLE" : "MOVING";
            string where = (slopeProbe && slopeProbe.OnSlopeLatched) ? "SLOPE" : "FLAT";
            string state = _isAiming ? (_isLocked ? "LOCKED" : "AIMING") : $"{where}/{timers}";
            Debug.Log($"[LineForce] v={speed:F3} | {state}");
        }

        // Input edges
        if (_pointerDownThisFrame)
        {
            _inputHeld = true;
            if (!settle.InCooldown && _canAim && !_isAiming)
                BeginAimingStopBall();
        }

        if (_pointerUpThisFrame)
        {
            _inputHeld = false;
            if (_isAiming)
            {
                if (_isLocked) TryShoot(_lockOrigin, _cursorWorld);
                EndAim();
            }
        }

        // Enter aim if permission toggles true while holding
        if (_pointerHeld && !_isAiming && !settle.InCooldown && _canAim)
            BeginAimingStopBall();

        // Aiming
        if (_isAiming)
        {
            Vector3? hit = CastPointerRay(_pointerPos);
            if (hit.HasValue)
            {
                _cursorWorld = FlattenY(hit.Value);
                Vector3 origin = _isLocked ? _lockOrigin : FlattenY(transform.position);

                float raw = (_cursorWorld - origin).magnitude;
                float t   = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, Mathf.Min(raw, maxDrawDistance));
                CurrentPowerT = Mathf.Clamp01(t);
                AimPowerChanged?.Invoke(CurrentPowerT);

                if (!_isLocked && raw >= minDrawDistance)
                {
                    _isLocked   = true;
                    _lockOrigin = FlattenY(transform.position);
                }
                else if (_isLocked && raw <= minDrawDistance - unlockHysteresis)
                {
                    _isLocked = false;
                }

                // Preview trajectory using the same mapping as the real shot
                UpdateTrajectoryPrediction(origin, _cursorWorld, CurrentPowerT);

                // Only draw here if this component owns the visuals
                UpdateLine(origin,
                           _cursorWorld,
                           _isLocked ? lockedLineColor : ghostLineColor);
            }
        }
        else if (renderLineHere && lineRenderer && lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }
    }

    void FixedUpdate()
    {
        float speed = _rb.linearVelocity.magnitude;

        // Update slope probe and settle logic
        if (slopeProbe) slopeProbe.FixedStep();
        bool onSlope = slopeProbe ? slopeProbe.OnSlopeLatched : false;

        if (settle)
            settle.FixedStep(speed, onSlope, _inputHeld, _isAiming);

        // If auto settle completed on slope, LineForce is the one that actually stops (authoritative intent)
        if (!_isAiming && settle && settle.CanAimFromAuto && !settle.InCooldown)
        {
            ForceStop();
            // leave permissions as-is; user can aim any time
        }
    }

    bool _canAim => (settle && (settle.CanAimFromInput || settle.CanAimFromAuto));

    // --- Aim & Shoot ---
    void BeginAimingStopBall()
    {
        ForceStop();
        _isAiming = true;
        _isLocked = false;
        CurrentPowerT = 0f;
        AimStarted?.Invoke();
    }

    void TryShoot(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float raw = delta.magnitude;
        if (raw < minDrawDistance) return;

        float clamped = Mathf.Min(raw, maxDrawDistance);
        Vector3 dir = (origin - target).normalized; // opposite pull
        float   t   = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clamped);
        float launchSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, t);

        _rb.AddForce(dir * (_rb.mass * launchSpeed), ForceMode.Impulse);

        // Reset settle permissions and start cooldown
        if (settle) settle.BeginShotCooldown();
    }

    void EndAim()
    {
        _isAiming = false;
        _isLocked = false;
        if (renderLineHere && lineRenderer) lineRenderer.enabled = false;
        AimEnded?.Invoke();
        if (projection) projection.ClearLine();
    }

    // --- Trajectory Preview ---
    void UpdateTrajectoryPrediction(Vector3 origin, Vector3 cursor, float powerT)
    {
        if (!projection) return;

        Vector3 delta = cursor - origin;
        float raw = delta.magnitude;
        if (raw < showLineDeadzone) return; // too tiny, skip preview

        // Direction is opposite to the pull (same as TryShoot)
        Vector3 dir = (origin - cursor).normalized;

        // Use the same speed mapping used for impulses
        float clamped = Mathf.Min(raw, maxDrawDistance);
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clamped);
        float launchSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, Mathf.Clamp01(t));

        // Estimated initial velocity for the preview (velocity, not impulse)
        Vector3 estimatedVelocity = dir * launchSpeed;

        projection.SimulateTrajectory(origin, estimatedVelocity);
    }

    // --- Rendering & Utils ---
    void UpdateLine(Vector3 origin, Vector3 cursor, Color color)
    {
        if (!renderLineHere || !lineRenderer) return;

        Vector3 delta = cursor - origin;
        float dist = delta.magnitude;
        if (dist < showLineDeadzone)
        {
            if (lineRenderer.enabled) lineRenderer.enabled = false;
            return;
        }

        Vector3 dir = delta.normalized;
        Vector3 end = origin + dir * Mathf.Min(dist, maxDrawDistance);

        if (lineRenderer.positionCount != 2) lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startColor = color;
        lineRenderer.endColor   = color;
        lineRenderer.enabled = true;
    }

    void ForceStop()
    {
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    Vector3 FlattenY(Vector3 v) => new(v.x, transform.position.y, v.z);

    // --- Unified pointer input ---
    void PollPrimaryPointer()
    {
        _pointerDownThisFrame = false;
        _pointerUpThisFrame   = false;

        // TOUCH (mobile)
        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            _pointerPos = t.position;

            switch (t.phase)
            {
                case TouchPhase.Began:
                    _pointerHeld = true;
                    _pointerDownThisFrame = true;
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    _pointerHeld = true;
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    _pointerHeld = false;
                    _pointerUpThisFrame = true;
                    break;
            }
            return;
        }

        // MOUSE (editor/desktop)
        _pointerPos = Input.mousePosition;
        _pointerDownThisFrame = Input.GetMouseButtonDown(0);
        _pointerUpThisFrame   = Input.GetMouseButtonUp(0);
        _pointerHeld          = Input.GetMouseButton(0);
    }

    Vector3? CastPointerRay(Vector2 screenPos)
    {
        if (!camera) return null;
        Vector3 near = new(screenPos.x, screenPos.y, camera.nearClipPlane);
        Vector3 far  = new(screenPos.x, screenPos.y, camera.farClipPlane);
        Vector3 nearW = camera.ScreenToWorldPoint(near);
        Vector3 farW  = camera.ScreenToWorldPoint(far);
        if (Physics.Raycast(nearW, farW - nearW, out RaycastHit hit, float.PositiveInfinity))
            return hit.point;
        return null;
    }
}
