using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LineForce : MonoBehaviour
{
    [Header("Movement Gates")]
    [SerializeField] private float autoStopSpeed  = 0.05f; // AUTO gate (authoritative)
    [SerializeField] private float inputStopSpeed = 0.20f; // INPUT gate (permission)

    [Header("Settling (seconds) — used ONLY on slopes")]
    [SerializeField] private float autoSettleDelay  = 10f;  // stay ≤ auto gate → force stop + aim
    [SerializeField] private float inputSettleDelay = 1.5f; // stay ≤ input gate → aim allowed (no stop)
    [SerializeField] private float speedHysteresis  = 0.02f; // cancel timers when speed > gate + hysteresis

    [Header("Post-shot")]
    [Tooltip("Block aim/timers for this long after a shot to avoid instant re-aim while speed ramps up.")]
    [SerializeField] private float postShotAimCooldown = 0.35f;

    [Header("Shot Settings")]
    [SerializeField] private float minDrawDistance = 0.35f;
    [SerializeField] private float maxDrawDistance = 5f;
    [SerializeField] private float minShootSpeed   = 4f;
    [SerializeField] private float maxShootSpeed   = 20f;

    [Header("Threshold Hysteresis (draw)")]
    [SerializeField] private float unlockHysteresis = 0.05f;

    [Header("Visual Line")]
    [SerializeField] private float showLineDeadzone = 0.08f;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Color ghostLineColor  = new(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color lockedLineColor = new(1f, 1f, 1f, 1f);

    [Header("Scene")]
    [SerializeField] private Camera camera;

    [Header("Slope Detection")]
    [SerializeField] private float slopeAngleThresholdDeg = 1.5f;
    [SerializeField] private float minTangentGravity = 0.00f;
    [SerializeField] private float groundProbeOffset = 0.2f;
    [SerializeField] private float groundProbeDistance = 1.0f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private bool requireGroundedForSlope = true;

    [Header("Slope Probe (spherecast)")]
    [SerializeField] private float slopeProbeSkin = 0.02f;
    [SerializeField] private float slopeProbeExtra = 0.30f;
    [SerializeField] private float slopeProbeRadiusScale = 0.95f;

    [Header("Slope Latch (debounce)")]
    [SerializeField] private float slopeLatchTime = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugSpeed = true;
    [SerializeField] private float debugThrottle = 0.10f;

    // Runtime
    private Rigidbody _rb;
    private float _ballRadius = 0.25f;

    // Input / Aim
    private bool _inputHeld;
    private bool _isAiming;     // ghost/locked
    private bool _isLocked;     // exceeded minDrawDistance
    public  float CurrentPowerT { get; private set; }

    // UI/FX events (PowerUI hooks)
    public event Action        AimStarted;
    public event Action<float> AimPowerChanged;
    public event Action        AimEnded;

    // Permissions (granted by timers OR flat rules)
    private bool _canAimFromInput; // input permission
    private bool _canAimFromAuto;  // auto permission (authoritative)

    // Timers (only on slopes)
    private bool  _autoSettleActive;
    private float _autoSettleEndTime;
    private bool  _inputSettleActive;
    private float _inputSettleEndTime;

    // Line points
    private Vector3 _cursorWorld;
    private Vector3 _lockOrigin;

    // Slope debounce
    private bool  _onSlopeLatched;
    private float _slopeLatchUntil;

    // Post-shot cooldown
    private float _noAimUntil; // absolute time after which aim/timers can resume

    // Debug throttles
    private float _lastDebugStateTime;
    private float _lastReasonLogTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!camera) camera = Camera.main;

        var sphere = GetComponent<SphereCollider>();
        if (sphere)
        {
            float maxScale = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            _ballRadius = sphere.radius * maxScale;
        }
        else if (TryGetComponent<Collider>(out var col))
        {
            _ballRadius = Mathf.Max(0.01f, col.bounds.extents.y);
        }

        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        float speed = _rb.linearVelocity.magnitude;

        // ── Debug state (throttled) ────────────────────────────
        if (debugSpeed && Time.time - _lastDebugStateTime > debugThrottle)
        {
            _lastDebugStateTime = Time.time;
            string timers =
                _autoSettleActive  ? $"SETTLING_AUTO<th={autoStopSpeed:F2}, rem={Mathf.Max(0f, _autoSettleEndTime - Time.time):F2}s>" :
                _inputSettleActive ? $"SETTLING_INPUT<th={inputStopSpeed:F2}, rem={Mathf.Max(0f, _inputSettleEndTime - Time.time):F2}s>" :
                (_canAimFromAuto || _canAimFromInput) ? "IDLE" : "MOVING";
            string where = _onSlopeLatched ? "SLOPE" : "FLAT";
            string cd   = Time.time < _noAimUntil ? $" COOLDOWN {(_noAimUntil - Time.time):F2}s" : "";
            string state = _isAiming ? (_isLocked ? "LOCKED" : "AIMING") : $"{where}/{timers}{cd}";
            Debug.Log($"[LineForce] v={speed:F3} | {state}");
        }

        // ── Input edges ────────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            _inputHeld = true;

            // If permission granted and not in cooldown, enter aim (stop ball)
            if (Time.time >= _noAimUntil && (_canAimFromInput || _canAimFromAuto) && !_isAiming)
                BeginAimingAndStopBall();
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

        // If permission flips true while holding and not in cooldown, slide into aim
        if (_inputHeld && Time.time >= _noAimUntil && (_canAimFromInput || _canAimFromAuto) && !_isAiming)
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
                AimPowerChanged?.Invoke(CurrentPowerT); // 🔔 UI

                if (!_isLocked && rawDist >= minDrawDistance)
                {
                    _isLocked = true;
                    _lockOrigin = FlattenToBallY(transform.position);
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

        // Update slope latch
        bool onSlopeRaw = IsOnSlopeRaw();
        if (onSlopeRaw)
        {
            _onSlopeLatched  = true;
            _slopeLatchUntil = Time.time + slopeLatchTime;
        }
        else if (_onSlopeLatched && Time.time >= _slopeLatchUntil)
        {
            _onSlopeLatched = false;
        }

        bool inCooldown = Time.time < _noAimUntil;

        if (_onSlopeLatched)
        {
            // ───────────── Timers run ONLY on slopes ─────────────

            // INPUT settle auto-starts at input gate (not during cooldown)
            if (!inCooldown && !_isAiming && !_inputSettleActive && !_canAimFromInput && speed <= inputStopSpeed)
            {
                StartInputSettle();
            }

            // Cancel timers if we leave their windows
            if (_inputSettleActive && speed > inputStopSpeed + speedHysteresis)
                CancelInputSettle();
            if (_autoSettleActive && speed > autoStopSpeed + speedHysteresis)
                CancelAutoSettle();

            // Revoke INPUT permission if speed rises before aiming starts
            if (_canAimFromInput && !_isAiming && speed > inputStopSpeed + speedHysteresis)
            {
                _canAimFromInput = false;
                LogReason("INPUT permission revoked (speed rose).");
            }

            // INPUT settle completion → aim allowed (not during cooldown)
            if (_inputSettleActive && Time.time >= _inputSettleEndTime && speed <= inputStopSpeed)
            {
                if (!inCooldown)
                {
                    _canAimFromInput = true;
                    LogReason("INPUT settle complete (aim allowed).");
                }
                _inputSettleActive = false; // complete regardless
            }

            // AUTO settle start & completion (authoritative; not during cooldown)
            if (!inCooldown && !_isAiming && !_autoSettleActive && !_canAimFromAuto && speed <= autoStopSpeed)
                StartAutoSettle();

            if (_autoSettleActive && Time.time >= _autoSettleEndTime && speed <= autoStopSpeed)
            {
                if (!inCooldown)
                {
                    ForceStop();
                    _canAimFromAuto = true;
                    _canAimFromInput = true; // allow aim regardless
                    LogReason("AUTO settle complete (ball stopped).");
                }
                _autoSettleActive = false;
                // Cancel input timer if running
                if (_inputSettleActive) CancelInputSettle();
            }
        }
        else
        {
            // ───────────── Flat ground: no timers ─────────────

            // Cancel any running slope timers
            if (_autoSettleActive)  CancelAutoSettle();
            if (_inputSettleActive) CancelInputSettle();

            if (!inCooldown)
            {
                // FLAT: auto-stop immediately at auto gate
                if (!_isAiming && speed <= autoStopSpeed)
                {
                    ForceStop();
                    _canAimFromAuto  = true;
                    _canAimFromInput = true; // allow aim
                }
                else
                {
                    // FLAT: input permission is immediate at input gate (no timer, no stop yet)
                    if (speed <= inputStopSpeed)
                    {
                        _canAimFromInput = true;
                    }
                    else if (!_isAiming && speed > inputStopSpeed + speedHysteresis)
                    {
                        if (_canAimFromInput)
                        {
                            _canAimFromInput = false;
                            LogReason("FLAT: input permission revoked (speed rose).");
                        }
                    }
                }
            }
            else
            {
                // In cooldown: do not grant permissions on flat either
                _canAimFromInput = false;
                _canAimFromAuto  = false;
            }
        }
    }

    // ── Aim start helper (stops the ball when aiming begins) ──
    private void BeginAimingAndStopBall()
    {
        // Guard against cooldown (double safety)
        if (Time.time < _noAimUntil) return;

        ForceStop();
        _isAiming   = true;
        _isLocked   = false;
        CurrentPowerT = 0f;
        AimStarted?.Invoke(); // 🔔 UI show
    }

    // ── Timer helpers ──────────────────────────────────────────
    private void StartAutoSettle()
    {
        _autoSettleActive  = true;
        _autoSettleEndTime = Time.time + Mathf.Max(0f, autoSettleDelay);
        LogReason($"AUTO settle started (th={autoStopSpeed:F2}, delay={autoSettleDelay:F2}s).");
    }

    private void CancelAutoSettle()
    {
        if (_autoSettleActive)
        {
            _autoSettleActive = false;
            LogReason("AUTO settle canceled (speed rose or left slope).");
        }
    }

    private void StartInputSettle()
    {
        _inputSettleActive  = true;
        _inputSettleEndTime = Time.time + Mathf.Max(0f, inputSettleDelay);
        LogReason($"INPUT settle started (th={inputStopSpeed:F2}, delay={inputSettleDelay:F2}s).");
    }

    private void CancelInputSettle()
    {
        if (_inputSettleActive)
        {
            _inputSettleActive = false;
            LogReason("INPUT settle canceled (speed rose or left slope).");
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

        // Start post-shot cooldown to prevent instant re-aim
        _noAimUntil = Time.time + Mathf.Max(0f, postShotAimCooldown);
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

    // ── Slope detection (spherecast raw) + debounce ────────────
    private bool IsOnSlopeRaw()
    {
        float castRadius = Mathf.Max(0.01f, _ballRadius * slopeProbeRadiusScale);
        Vector3 origin   = transform.position + Vector3.up * slopeProbeSkin;
        float   castDist = _ballRadius + slopeProbeExtra + slopeProbeSkin;

        bool gotHit = Physics.SphereCast(
            origin,
            castRadius,
            Vector3.down,
            out RaycastHit hit,
            castDist,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        if (!gotHit)
        {
            if (requireGroundedForSlope) return false;

            // Fallback ray (optional)
            Vector3 rayOrigin = transform.position + Vector3.up * groundProbeOffset;
            gotHit = Physics.Raycast(
                rayOrigin, Vector3.down, out hit,
                groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore
            );
            if (!gotHit) return false;
        }

        float angle = Vector3.Angle(hit.normal, Vector3.up);
        if (angle < slopeAngleThresholdDeg) return false;

        if (minTangentGravity > 0f)
        {
            Vector3 tangentG = Vector3.ProjectOnPlane(Physics.gravity, hit.normal);
            if (tangentG.magnitude < minTangentGravity) return false;
        }
        return true;
    }

    private void LogReason(string msg)
    {
        if (!debugSpeed) return;
        if (Time.time - _lastReasonLogTime > 0.2f)
        {
            _lastReasonLogTime = Time.time;
            Debug.Log($"[LineForce] {msg}");
        }
    }
}
