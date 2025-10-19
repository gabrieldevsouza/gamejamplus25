using UnityEngine;

[DisallowMultipleComponent]
public class SettleManager : MonoBehaviour
{
    [Header("Movement Gates")]
    [SerializeField] private float autoStopSpeed  = 0.05f;
    [SerializeField] private float inputStopSpeed = 0.20f;

    [Header("Settling (used ONLY on slopes)")]
    [SerializeField] private float autoSettleDelay  = 5f;
    [SerializeField] private float inputSettleDelay = 3f;
    [SerializeField] private float speedHysteresis  = 0.02f;

    [Header("Post-shot")]
    [SerializeField] private float postShotAimCooldown = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool  debugLogs = true;

    // Outputs
    public bool CanAimFromInput { get; private set; }
    public bool CanAimFromAuto  { get; private set; }
    public bool InCooldown      => Time.time < _noAimUntil;

    // Timers
    bool  _autoSettleActive;
    float _autoSettleEndTime;
    bool  _inputSettleActive;
    float _inputSettleEndTime;

    float _noAimUntil;   // cooldown
    float _lastLogTime;  // throttle

    public void ResetAll()
    {
        CanAimFromInput = false;
        CanAimFromAuto  = false;
        _autoSettleActive  = false;
        _inputSettleActive = false;
        _noAimUntil = 0f;
    }

    public void BeginShotCooldown()
    {
        _noAimUntil = Time.time + Mathf.Max(0f, postShotAimCooldown);
        CanAimFromInput = false;
        CanAimFromAuto  = false;
        _autoSettleActive  = false;
        _inputSettleActive = false;
    }

    public void FixedStep(float speed, bool onSlope, bool inputHeld, bool isAiming)
    {
        bool inCd = InCooldown;

        if (onSlope)
        {
            // --- Input settle (permission only) ---
            if (!inCd && !isAiming && !_inputSettleActive && !CanAimFromInput && speed <= inputStopSpeed)
                StartInputSettle();
            if (_inputSettleActive && speed > inputStopSpeed + speedHysteresis)
                CancelInputSettle();
            if (CanAimFromInput && !isAiming && speed > inputStopSpeed + speedHysteresis)
            {
                CanAimFromInput = false;
                Log("INPUT permission revoked (speed rose).");
            }
            if (_inputSettleActive && Time.time >= _inputSettleEndTime && speed <= inputStopSpeed)
            {
                if (!inCd)
                {
                    CanAimFromInput = true;
                    Log("INPUT settle complete (aim allowed).");
                }
                _inputSettleActive = false;
            }

            // --- Auto settle (authoritative stop) ---
            if (!inCd && !isAiming && !_autoSettleActive && !CanAimFromAuto && speed <= autoStopSpeed)
                StartAutoSettle();
            if (_autoSettleActive && speed > autoStopSpeed + speedHysteresis)
                CancelAutoSettle();
            if (_autoSettleActive && Time.time >= _autoSettleEndTime && speed <= autoStopSpeed)
            {
                if (!inCd)
                {
                    // caller should ForceStop() when consuming this signal
                    CanAimFromAuto  = true;
                    CanAimFromInput = true; // global allow
                    Log("AUTO settle complete (ball should stop).");
                }
                _autoSettleActive = false;
                if (_inputSettleActive) CancelInputSettle();
            }
        }
        else
        {
            // --- Flat ground: no timers ---
            if (_autoSettleActive)  CancelAutoSettle();
            if (_inputSettleActive) CancelInputSettle();

            if (!inCd)
            {
                if (!isAiming && speed <= autoStopSpeed)
                {
                    CanAimFromAuto  = true;
                    CanAimFromInput = true;
                }
                else
                {
                    if (speed <= inputStopSpeed)
                    {
                        CanAimFromInput = true;
                    }
                    else if (!isAiming && speed > inputStopSpeed + speedHysteresis)
                    {
                        if (CanAimFromInput)
                        {
                            CanAimFromInput = false;
                            Log("FLAT: input permission revoked (speed rose).");
                        }
                    }
                }
            }
            else
            {
                CanAimFromInput = false;
                CanAimFromAuto  = false;
            }
        }
    }

    // Timer helpers
    void StartAutoSettle()
    {
        _autoSettleActive  = true;
        _autoSettleEndTime = Time.time + Mathf.Max(0f, autoSettleDelay);
        Log($"AUTO settle started (th={autoStopSpeed:F2}, delay={autoSettleDelay:F2}s).");
    }
    void CancelAutoSettle()
    {
        if (_autoSettleActive)
        {
            _autoSettleActive = false;
            Log("AUTO settle canceled.");
        }
    }
    void StartInputSettle()
    {
        _inputSettleActive  = true;
        _inputSettleEndTime = Time.time + Mathf.Max(0f, inputSettleDelay);
        Log($"INPUT settle started (th={inputStopSpeed:F2}, delay={inputSettleDelay:F2}s).");
    }
    void CancelInputSettle()
    {
        if (_inputSettleActive)
        {
            _inputSettleActive = false;
            Log("INPUT settle canceled.");
        }
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        if (Time.time - _lastLogTime > 0.2f)
        {
            _lastLogTime = Time.time;
            Debug.Log($"[SettleManager] {msg}");
        }
    }

    // Expose config to LineForce (optional convenience)
    public float AutoStopSpeed  => autoStopSpeed;
    public float InputStopSpeed => inputStopSpeed;
}
