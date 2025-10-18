using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("UX")]
    [SerializeField] private float showLineDeadzone = 0.08f; // min drag to show preview line
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Camera camera;

    [Header("Power UI (HUD)")]
    [Tooltip("UI Image set to 'Filled' (Horizontal).")]
    [SerializeField] private Image powerFill;
    [Tooltip("Optional percentage text (TMP).")]
    [SerializeField] private TextMeshProUGUI powerText;
    [Tooltip("Optional color gradient from low to high power.")]
    [SerializeField] private Gradient powerGradient;

    private Rigidbody _rigidbody;

    private bool _isIdle;
    private bool _isAiming;
    private bool _stuckInAim; // preserved for your flow

    private Vector3 _aimStartWorld; // press-begin point (planar)
    private Vector3 _lastWorldPoint; // last valid aim point

    // cache the current normalized power [0..1] for UI
    private float _currentPowerT;

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

        HidePowerUI();
    }

    private void Update()
    {
        // Begin aiming when allowed and mouse is pressed
        if (_isIdle && Input.GetMouseButtonDown(0))
        {
            Vector3? start = CastMouseClickRay();
            if (start.HasValue)
            {
                _isAiming = true;
                _stuckInAim = true;
                _aimStartWorld = FlattenToBallY(start.Value);
                Stop(); // freeze immediately while aiming
                ShowPowerUI(0f);
            }
        }

        if (_isAiming)
        {
            Vector3? worldPoint = CastMouseClickRay();
            if (worldPoint.HasValue)
            {
                _lastWorldPoint = FlattenToBallY(worldPoint.Value);
                UpdatePreviewLine(_aimStartWorld, _lastWorldPoint);   // also updates UI power
            }
            else
            {
                if (lineRenderer && lineRenderer.enabled)
                    lineRenderer.enabled = false;
                ShowPowerUI(0f);
            }

            // Release → attempt to shoot (with thresholds)
            if (Input.GetMouseButtonUp(0))
            {
                TryShoot(_aimStartWorld, _lastWorldPoint);
                HidePowerUI();
            }
        }
        else
        {
            if (lineRenderer && lineRenderer.enabled)
                lineRenderer.enabled = false;
            HidePowerUI();
        }
    }

    private void FixedUpdate()
    {
        float currentSpeed = _rigidbody.linearVelocity.magnitude;

        // Allow aiming when speed is below threshold
        _isIdle = currentSpeed <= velocityToAim;

        // If almost stopped, ensure fully stopped (helps stability)
        if (currentSpeed < stopVelocity)
        {
            Stop();
        }
    }

    // --- Core actions ---

    private void TryShoot(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float rawDist = delta.magnitude;

        // Ignore tiny shots
        if (rawDist < minDrawDistance)
        {
            _isAiming = false;
            _stuckInAim = false;
            if (lineRenderer) lineRenderer.enabled = false;
            return;
        }

        float clampedDist = Mathf.Min(rawDist, maxDrawDistance);
        Vector3 direction = (delta.sqrMagnitude > 0.0001f) ? delta.normalized : Vector3.zero;

        // Map distance → speed
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, clampedDist);
        float desiredSpeed = Mathf.Lerp(minShootSpeed, maxShootSpeed, t);

        Shoot(direction, desiredSpeed);
    }

    private void Shoot(Vector3 direction, float desiredSpeed)
    {
        _isAiming = false;
        _stuckInAim = false;
        if (lineRenderer) lineRenderer.enabled = false;

        if (direction == Vector3.zero) return;

        _isIdle = false;

        // J = m * Δv  → impulse that yields desiredSpeed
        _rigidbody.AddForce(direction * (_rigidbody.mass * desiredSpeed), ForceMode.Impulse);
    }

    private void UpdatePreviewLine(Vector3 origin, Vector3 cursor)
    {
        if (!lineRenderer) return;

        Vector3 delta = cursor - origin;
        float rawDist = delta.magnitude;

        // Power "t" from 0..1 using min/max draw distance
        float t = Mathf.InverseLerp(minDrawDistance, maxDrawDistance, Mathf.Min(rawDist, maxDrawDistance));
        _currentPowerT = Mathf.Clamp01(t);

        // Update power UI even if we don't show the line yet (feels responsive)
        ShowPowerUI(_currentPowerT);

        // Hide line for tiny drag to avoid flicker
        if (rawDist < showLineDeadzone)
        {
            if (lineRenderer.enabled) lineRenderer.enabled = false;
            return;
        }

        Vector3 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward;

        // Clamp the visual length to max
        float drawDist = Mathf.Clamp(rawDist, showLineDeadzone, maxDrawDistance);
        Vector3 clampedEnd = origin + dir * drawDist;

        if (lineRenderer.positionCount != 2) lineRenderer.positionCount = 2;

        lineRenderer.SetPosition(0, transform.position); // from the ball
        lineRenderer.SetPosition(1, clampedEnd);         // to clamped end
        lineRenderer.enabled = true;
    }

    private void Stop()
    {
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _isIdle = true;
    }

    // --- Power UI helpers ---

    private void ShowPowerUI(float t01)
    {
        if (powerFill)
        {
            powerFill.fillAmount = t01; // Image Type must be Filled
            if (powerGradient.colorKeys.Length > 0 || powerGradient.alphaKeys.Length > 0)
                powerFill.color = powerGradient.Evaluate(t01);
            else
                powerFill.color = Color.white;
            if (!powerFill.gameObject.activeSelf) powerFill.gameObject.SetActive(true);
        }

        if (powerText)
        {
            int pct = Mathf.RoundToInt(t01 * 100f);
            powerText.text = pct + "%";
            if (!powerText.gameObject.activeSelf) powerText.gameObject.SetActive(true);
        }
    }

    private void HidePowerUI()
    {
        if (powerFill && powerFill.gameObject.activeSelf) powerFill.gameObject.SetActive(false);
        if (powerText && powerText.gameObject.activeSelf) powerText.gameObject.SetActive(false);
        _currentPowerT = 0f;
        if (powerFill) powerFill.fillAmount = 0f;
        if (powerText) powerText.text = "0%";
    }

    // --- Helpers ---

    private Vector3 FlattenToBallY(Vector3 v)
    {
        return new Vector3(v.x, transform.position.y, v.z);
    }

    private Vector3? CastMouseClickRay()
    {
        if (!camera) return null;

        Vector3 screenMousePosFar = new Vector3(
            Input.mousePosition.x,
            Input.mousePosition.y,
            camera.farClipPlane);

        Vector3 screenMousePosNear = new Vector3(
            Input.mousePosition.x,
            Input.mousePosition.y,
            camera.nearClipPlane);

        Vector3 worldMousePosFar = camera.ScreenToWorldPoint(screenMousePosFar);
        Vector3 worldMousePosNear = camera.ScreenToWorldPoint(screenMousePosNear);

        if (Physics.Raycast(worldMousePosNear, worldMousePosFar - worldMousePosNear, out RaycastHit hit, float.PositiveInfinity))
            return hit.point;

        return null;
    }
}
