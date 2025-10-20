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

    [Header("Aiming Space (no colliders used)")]
    [SerializeField] private float aimPlaneYOffset = 0f;
    [SerializeField] private float parallelRayFallbackDistance = 20f;

    [Header("Aim Start Gate")]
    [Tooltip("Press must begin within this screen distance (pixels) from the ball to start aiming.")]
    [SerializeField] private float aimStartRadiusScreenPx = 120f;
    [Tooltip("If disabled, any press can start aiming (old behavior).")]
    [SerializeField] private bool requireNearPressToStartAim = true;

    [Header("Scene")]
    [SerializeField] private Camera camera;

    [Header("Refs")]
    [SerializeField] private SlopeProbe    slopeProbe;
    [SerializeField] private SettleManager settle;
    [SerializeField] private Projection    projection;

    [Header("Debug")]
    [SerializeField] private bool debugState = true;

    Rigidbody _rb;

    // Aim state
    bool _inputHeld;
    bool _isAiming;
    bool _isLocked;
    public float CurrentPowerT { get; private set; }

    // --- Public data access for external presenters/UI ---
    public bool   IsAiming          => _isAiming;
    public bool   IsLocked          => _isLocked;
    public Vector3 CursorWorld      => _cursorWorld;
    public Vector3 CurrentAimOrigin => _isLocked ? _lockOrigin : FlattenY(transform.position);
    public float  MinDrawDistance   => minDrawDistance;
    public float  MaxDrawDistance   => maxDrawDistance;
    public Vector3 LockOrigin       => _lockOrigin;

    // UI events
    public event Action        AimStarted;
    public event Action<float> AimPowerChanged;
    public event Action        AimEnded;

    // Line points
    Vector3 _cursorWorld;
    Vector3 _lockOrigin;

    float _lastDbg;

    // Pointer adapter
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
        PollPrimaryPointer();

        if (debugState && Time.time - _lastDbg > 0.1f)
        {
            _lastDbg = Time.time;
            float speed = _rb.linearVelocity.magnitude;
            string timers = settle && settle.InCooldown ? $"COOLDOWN {settle.InCooldown}" :
                             (settle && (settle.CanAimFromAuto || settle.CanAimFromInput)) ? "IDLE" : "MOVING";
            string where = (slopeProbe && slopeProbe.OnSlopeLatched) ? "SLOPE" : "FLAT";
            string state = _isAiming ? (_isLocked ? "LOCKED" : "AIMING") : $"{where}/{timers}";
            Debug.Log($"[LineForce] v={speed:F3} | {state}");
        }

        // edges
        if (_pointerDownThisFrame)
        {
            _inputHeld = true;
            if (!settle.InCooldown && _canAim && !_isAiming)
            {
                if (!requireNearPressToStartAim || IsPointerNearBall(_pointerPos))
                    BeginAimingStopBall();
            }
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

        // Permission toggled true while holding
        if (_pointerHeld && !_isAiming && !settle.InCooldown && _canAim)
        {
            if (!requireNearPressToStartAim || IsPointerNearBall(_pointerPos))
                BeginAimingStopBall();
        }

        // aiming
        if (_isAiming)
        {
            Vector3 aimPoint = GetAimPointOnPlane(_pointerPos);
            _cursorWorld = FlattenY(aimPoint);
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

            UpdateTrajectoryPrediction(origin, _cursorWorld, CurrentPowerT);

            UpdateLine(origin,
                       _cursorWorld,
                       _isLocked ? lockedLineColor : ghostLineColor);
        }
        else if (renderLineHere && lineRenderer && lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }
    }

    void FixedUpdate()
    {
        float speed = _rb.linearVelocity.magnitude;
        if (slopeProbe) slopeProbe.FixedStep();
        bool onSlope = slopeProbe ? slopeProbe.OnSlopeLatched : false;
        if (settle) settle.FixedStep(speed, onSlope, _inputHeld, _isAiming);

        if (!_isAiming && settle && settle.CanAimFromAuto && !settle.InCooldown)
            ForceStop();
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
        Vector3 dir = (origin - target).normalized;
        float   t   = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clamped);
        float launchSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, t);

        _rb.AddForce(dir * (_rb.mass * launchSpeed), ForceMode.Impulse);

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
        if (raw < showLineDeadzone) return;

        Vector3 dir = (origin - cursor).normalized;

        float clamped = Mathf.Min(raw, maxDrawDistance);
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clamped);
        float launchSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, Mathf.Clamp01(t));

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

    // --- Input ---
    void PollPrimaryPointer()
    {
        _pointerDownThisFrame = false;
        _pointerUpThisFrame   = false;

        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            _pointerPos = t.position;
            switch (t.phase)
            {
                case TouchPhase.Began: _pointerHeld = true; _pointerDownThisFrame = true; break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary: _pointerHeld = true; break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled: _pointerHeld = false; _pointerUpThisFrame = true; break;
            }
            return;
        }

        _pointerPos = Input.mousePosition;
        _pointerDownThisFrame = Input.GetMouseButtonDown(0);
        _pointerUpThisFrame   = Input.GetMouseButtonUp(0);
        _pointerHeld          = Input.GetMouseButton(0);
    }

    // --- Strict plane aim (NO Physics) ---
    Vector3 GetAimPointOnPlane(Vector2 screenPos)
    {
        if (!camera) camera = Camera.main;
        if (!camera) return transform.position;

        float planeY = transform.position.y + aimPlaneYOffset;
        Ray ray = camera.ScreenPointToRay(screenPos);

        float denom = ray.direction.y;
        if (Mathf.Abs(denom) >= 1e-4f)
        {
            float t = (planeY - ray.origin.y) / denom;
            if (t < 0f) t = 0.01f;
            return ray.origin + ray.direction * t;
        }
        else
        {
            Vector3 p = ray.origin + ray.direction * Mathf.Max(0.5f, parallelRayFallbackDistance);
            p.y = planeY;
            return p;
        }
    }

    // --- Gate helper ---
    bool IsPointerNearBall(Vector2 screenPos)
    {
        if (!camera) camera = Camera.main;
        if (!camera) return true; // if we can't evaluate, don't block

        Vector3 ballSS = camera.WorldToScreenPoint(transform.position);
        if (ballSS.z < 0f) return false; // behind camera

        float dist = Vector2.Distance(new Vector2(ballSS.x, ballSS.y), screenPos);
        return dist <= aimStartRadiusScreenPx;
    }
}
