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

    [Header("Scene")]
    [SerializeField] private Camera camera;

    [Header("Refs")]
    [SerializeField] private SlopeProbe   slopeProbe;
    [SerializeField] private SettleManager settle;

    [Header("Debug")]
    [SerializeField] private bool debugState = true;

    Rigidbody _rb;

    // Aim state
    bool _inputHeld;
    bool _isAiming;
    bool _isLocked;
    public float CurrentPowerT { get; private set; }

    // UI events
    public event Action        AimStarted;
    public event Action<float> AimPowerChanged;
    public event Action        AimEnded;

    // Line points
    Vector3 _cursorWorld;
    Vector3 _lockOrigin;

    float _lastDbg;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!camera) camera = Camera.main;
        if (!slopeProbe)   slopeProbe = GetComponent<SlopeProbe>();
        if (!settle)       settle     = GetComponent<SettleManager>();

        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    void Update()
    {
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
        if (Input.GetMouseButtonDown(0))
        {
            _inputHeld = true;
            if (!settle.InCooldown && (_canAim) && !_isAiming)
                BeginAimingStopBall();
        }

        if (Input.GetMouseButtonUp(0))
        {
            _inputHeld = false;
            if (_isAiming)
            {
                if (_isLocked) TryShoot(_lockOrigin, _cursorWorld);
                EndAim();
            }
        }

        // Enter aim if permission toggles true while holding
        if (_inputHeld && !_isAiming && !settle.InCooldown && _canAim)
            BeginAimingStopBall();

        // Aiming
        if (_isAiming)
        {
            Vector3? hit = CastMouseClickRay();
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

                UpdateLine(_isLocked ? _lockOrigin : FlattenY(transform.position),
                           _cursorWorld,
                           _isLocked ? lockedLineColor : ghostLineColor);
            }
        }
        else if (lineRenderer && lineRenderer.enabled)
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
        if (lineRenderer) lineRenderer.enabled = false;
        AimEnded?.Invoke();
    }

    // --- Rendering & Utils ---
    void UpdateLine(Vector3 origin, Vector3 cursor, Color color)
    {
        if (!lineRenderer) return;

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

    Vector3? CastMouseClickRay()
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
