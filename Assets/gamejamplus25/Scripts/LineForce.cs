using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LineForce : MonoBehaviour
{
    [Header("Movement Gates")]
    [SerializeField] private float autoStopSpeed = 0.05f;  // starts AUTO settle when speed ≤ this
    [SerializeField] private float inputStopSpeed = 0.2f;  // starts INPUT settle when speed ≤ this

    [Header("Settling (seconds)")]
    [SerializeField] private float autoSettleDelay = 10f;   // after this → force stop + aim allowed
    [SerializeField] private float inputSettleDelay = 1.5f; // after this → aim allowed (ball still free)
    [SerializeField] private float speedHysteresis = 0.01f; // cancel timers when speed > gate + hysteresis

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

    // Runtime
    private Rigidbody _rb;

    // Input / Aim
    private bool _inputHeld;
    private bool _isAiming;     // in ghost/locked aiming mode
    private bool _isLocked;     // line exceeded min distance
    public  float CurrentPowerT { get; private set; }

    // UI/FX events (used by PowerUI)
    public event Action        AimStarted;
    public event Action<float> AimPowerChanged;
    public event Action        AimEnded;

    // Permissions (granted by timers)
    private bool _canAimFromInput; // granted when input settle completes
    private bool _canAimFromAuto;  // granted when auto settle completes

    // Timers
    private bool  _autoSettleActive;
    private float _autoSettleEndTime;
    private bool  _inputSettleActive;
    private float _inputSettleEndTime;

    // Line points
    private Vector3 _cursorWorld;
    private Vector3 _lockOrigin;

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

        // ── Debug ───────────────────────────────────────────────
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

            // If any permission is already granted, enter aim now (and stop the ball at aim start)
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

        // If permission flips true while holding, slide into aiming (stop at aim start)
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
                AimPowerChanged?.Invoke(CurrentPowerT); // 🔔 UI update

                // Lock purely affects visuals now (ball stopped at aim start)
                if (!_isLocked && rawDist >= minDrawDistance)
                {
                    _isLocked = true;
                    _lockOrigin = FlattenToBallY(transform.position);
                    // We previously raised AimStarted on aim begin, not on lock
                }
                else if (_isLocked && rawDist <= minDrawDistance - unlockHysteresis)
                {
                    _isLocked = false;
                }

                UpdatePreviewLine(_isLocked ? _lockOrigin : FlattenToBallY(transform.position),
                                  _cursorWorld,
                                  _isLocked ? lockedLineColor : ghostLineColor);
            }
        }
        else if (lineRenderer && lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        float speed = _rb.linearVelocity.magnitude;

        // ── INPUT settle: auto-starts when speed ≤ input gate (no click needed)
        if (!_isAiming && !_inputSettleActive && !_canAimFromInput && speed <= inputStopSpeed /* && IsOnSlope() later */)
            StartInputSettle();

        // ── Cancel timers if speed rises above their gates (+ hysteresis)
        if (_inputSettleActive && speed > inputStopSpeed + speedHysteresis)
            CancelInputSettle();
        if (_autoSettleActive && speed > autoStopSpeed + speedHysteresis)
            CancelAutoSettle();

        // ── Revoke INPUT permission if speed rises before aiming starts
        if (_canAimFromInput && !_isAiming && speed > inputStopSpeed + speedHysteresis)
        {
            _canAimFromInput = false;
            if (debugSpeed) Debug.Log("[LineForce] INPUT permission revoked (speed rose)");
        }

        // ── INPUT settle completion → aim allowed (ball not stopped yet)
        if (_inputSettleActive && Time.time >= _inputSettleEndTime && speed <= inputStopSpeed)
        {
            _canAimFromInput = true;
            _inputSettleActive = false;
            if (debugSpeed) Debug.Log("[LineForce] INPUT settle complete (aim allowed)");
        }

        // ── AUTO settle: starts at the auto gate
        if (!_isAiming && !_autoSettleActive && !_canAimFromAuto && speed <= autoStopSpeed /* && IsOnSlope() later */)
            StartAutoSettle();

        // ── AUTO settle completion → force stop + aim allowed (authoritative)
        if (_autoSettleActive && Time.time >= _autoSettleEndTime && speed <= autoStopSpeed)
        {
            ForceStop();
            _canAimFromAuto = true;
            _autoSettleActive = false;

            // Auto completion supersedes input settle
            if (_inputSettleActive) CancelInputSettle();
            _canAimFromInput = true; // allow aim regardless

            if (debugSpeed) Debug.Log("[LineForce] AUTO settle complete (ball stopped)");
        }
    }

    // ── Aim start helper (stops the ball when aiming begins) ──
    private void BeginAimingAndStopBall()
    {
        ForceStop();
        _isAiming = true;
        _isLocked = false;
        CurrentPowerT = 0f;
        AimStarted?.Invoke(); // 🔔 UI show
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
        Vector3 dir = (origin - target).normalized; // shoot opposite of pull
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clamped);
        float launchSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, t);

        _rb.AddForce(dir * (_rb.mass * launchSpeed), ForceMode.Impulse);

        // Reset permissions and timers for next roll
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
        AimEnded?.Invoke(); // 🔔 UI hide
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
        _rb.linearVelocity  = Vector3.zero;
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

    // ── Future: only start timers on slopes ────────────────────
    // bool IsOnSlope() { return true; /* compute from ground normal later */ }
}
