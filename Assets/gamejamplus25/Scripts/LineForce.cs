using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LineForce : MonoBehaviour
{
    [Header("Movement Gates")]
    [Tooltip("Natural settle gate. When speed ≤ this, AUTO settle can start.")]
    [SerializeField] private float autoStopSpeed = 0.05f;

    [Tooltip("Input gate. When speed ≤ this, INPUT settle can start (no click needed).")]
    [SerializeField] private float inputStopSpeed = 0.2f;

    [Header("Settling (seconds)")]
    [Tooltip("Stay under auto gate for this long → force stop + aim allowed.")]
    [SerializeField] private float autoSettleDelay = 10f;

    [Tooltip("Stay under input gate for this long → aim allowed (ball still moving until aim starts).")]
    [SerializeField] private float inputSettleDelay = 1.5f;

    [Tooltip("Extra margin to cancel timers when speed rises above a gate.")]
    [SerializeField] private float speedHysteresis = 0.01f;

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
    [SerializeField] private bool debugSpeed = true;

    // --- Internal state ---
    private Rigidbody _rb;

    // Input / aim state
    private bool _inputHeld;
    private bool _isAiming;
    private bool _isLocked;

    // Permission flags (either may grant aiming)
    private bool _canAimFromInput; // granted when input timer completes
    private bool _canAimFromAuto;  // granted when auto timer completes

    // Auto settle timer
    private bool  _autoSettleActive;
    private float _autoSettleEndTime;

    // Input settle timer
    private bool  _inputSettleActive;
    private float _inputSettleEndTime;

    // Points
    private Vector3 _cursorWorld;
    private Vector3 _lockOrigin;

    // UI/Event
    public float CurrentPowerT { get; private set; }
    public event Action AimStarted;
    public event Action<float> AimPowerChanged;
    public event Action AimEnded;

    private float _lastDebugTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!camera) camera = Camera.main;

        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        float speed = _rb.linearVelocity.magnitude;

        // ── DEBUG ───────────────────────────────────────────────
        if (debugSpeed && Time.time - _lastDebugTime > 0.1f)
        {
            _lastDebugTime = Time.time;

            string timer =
                _autoSettleActive  ? $"SETTLING_AUTO<th={autoStopSpeed:F2}, rem={Mathf.Max(0f, _autoSettleEndTime - Time.time):F2}s>" :
                _inputSettleActive ? $"SETTLING_INPUT<th={inputStopSpeed:F2}, rem={Mathf.Max(0f, _inputSettleEndTime - Time.time):F2}s>" :
                (_canAimFromAuto || _canAimFromInput) ? "IDLE" : "MOVING";

            string state = _isAiming ? (_isLocked ? "LOCKED" : "AIMING") : timer;
            Debug.Log($"[LineForce] v={speed:F3} | {state}");
        }

        // ── Input edges ────────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            _inputHeld = true;

            // If permission already granted by either timer, enter aim immediately and stop the ball now
            if ((_canAimFromInput || _canAimFromAuto) && !_isAiming)
                BeginAimingAndStopBall();
        }

        if (Input.GetMouseButtonUp(0))
        {
            _inputHeld = false;

            if (_isAiming)
            {
                if (_isLocked)
                    TryShoot(_lockOrigin, _cursorWorld);
                EndAim();
            }
        }

        // If permission becomes true while holding, slide into aim and stop the ball at aim start
        if (_inputHeld && (_canAimFromInput || _canAimFromAuto) && !_isAiming)
            BeginAimingAndStopBall();

        // ── Aiming update ──────────────────────────────────────
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

                // Lock purely controls visuals now (ball already stopped at aim start)
                if (!_isLocked && rawDist >= minDrawDistance)
                {
                    _isLocked = true;
                    _lockOrigin = FlattenToBallY(transform.position);
                    AimStarted?.Invoke();
                }
                else if (_isLocked && rawDist <= minDrawDistance - unlockHysteresis)
                {
                    _isLocked = false;
                    AimEnded?.Invoke();
                }

                UpdatePreviewLine(_isLocked ? _lockOrigin : FlattenToBallY(transform.position),
                                  _cursorWorld,
                                  _isLocked ? lockedLineColor : ghostLineColor);
            }
        }
        else
        {
            if (lineRenderer && lineRenderer.enabled)
                lineRenderer.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        float speed = _rb.linearVelocity.magnitude;

        // ── INPUT settle: starts automatically at the input gate (no click needed)
        if (!_isAiming && !_inputSettleActive && !_canAimFromInput && speed <= inputStopSpeed /* && IsOnSlope() later */)
            StartInputSettle();

        // cancel input settle if we leave its window
        if (_inputSettleActive && speed > inputStopSpeed + speedHysteresis)
            CancelInputSettle();

        // complete input settle → allow aiming (ball not stopped yet)
        if (_inputSettleActive && Time.time >= _inputSettleEndTime && speed <= inputStopSpeed)
        {
            _canAimFromInput = true;
            _inputSettleActive = false;
        }

        // ── AUTO settle: starts at the auto gate
        if (!_isAiming && !_autoSettleActive && !_canAimFromAuto && speed <= autoStopSpeed /* && IsOnSlope() later */)
            StartAutoSettle();

        // cancel auto settle if we leave its window
        if (_autoSettleActive && speed > autoStopSpeed + speedHysteresis)
            CancelAutoSettle();

        // complete auto settle → force stop + allow aiming (authoritative)
        if (_autoSettleActive && Time.time >= _autoSettleEndTime && speed <= autoStopSpeed)
        {
            ForceStop();
            _canAimFromAuto = true;
            _autoSettleActive = false;

            // Auto completion supersedes input settle
            if (_inputSettleActive) CancelInputSettle();
            _canAimFromInput = true; // aiming allowed in any case
        }
    }

    // ── Aim start helper (stops the ball on enter) ─────────────
    private void BeginAimingAndStopBall()
    {
        ForceStop(); // stop immediately when aiming begins per your spec
        _isAiming = true;
        _isLocked = false;
        CurrentPowerT = 0f;
    }

    // ── Timer helpers ──────────────────────────────────────────
    private void StartAutoSettle()
    {
        _autoSettleActive  = true;
        _autoSettleEndTime = Time.time + Mathf.Max(0f, autoSettleDelay);
        if (debugSpeed) Debug.Log($"[LineForce] AUTO settle started (th={autoStopSpeed:F2}, delay={autoSettleDelay:F2}s)");
    }

    private void CancelAutoSettle()
    {
        if (_autoSettleActive)
        {
            _autoSettleActive = false;
            if (debugSpeed) Debug.Log("[LineForce] AUTO settle canceled");
        }
    }

    private void StartInputSettle()
    {
        _inputSettleActive  = true;
        _inputSettleEndTime = Time.time + Mathf.Max(0f, inputSettleDelay);
        if (debugSpeed) Debug.Log($"[LineForce] INPUT settle started (th={inputStopSpeed:F2}, delay={inputSettleDelay:F2}s)");
    }

    private void CancelInputSettle()
    {
        if (_inputSettleActive)
        {
            _inputSettleActive = false;
            if (debugSpeed) Debug.Log("[LineForce] INPUT settle canceled");
        }
    }

    // ── Shooting / Aiming helpers ──────────────────────────────
    private void TryShoot(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float rawDist = delta.magnitude;
        if (rawDist < minDrawDistance) return;

        float clamped = Mathf.Min(rawDist, maxDrawDistance);
        Vector3 dir = (origin - target).normalized; // opposite of pull
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clamped);
        float launchSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, t);

        _rb.AddForce(dir * (_rb.mass * launchSpeed), ForceMode.Impulse);

        // Reset permissions/timers for next roll
        _canAimFromInput = false;
        _canAimFromAuto  = false;
        CancelAutoSettle();
        CancelInputSettle();
    }

    private void EndAim()
    {
        _isAiming = false;
        _isLocked = false;
        if (lineRenderer) lineRenderer.enabled = false;
        AimEnded?.Invoke();
    }

    // ── Rendering ──────────────────────────────────────────────
    private void UpdatePreviewLine(Vector3 origin, Vector3 cursor, Color color)
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

    // ── Util ───────────────────────────────────────────────────
    private void ForceStop()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
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

    // ── Future hook: start timers only on slopes ───────────────
    // bool IsOnSlope() { return true; } // later: check ground normal or gravity tangent
}
