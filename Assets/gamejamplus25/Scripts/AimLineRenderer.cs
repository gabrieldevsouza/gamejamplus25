using UnityEngine;

/// Draws two aim lines during aiming:
///  - maxLine  : always full length (maxDrawDistance) in the current aim direction
///  - fillLine : current length based on cursor distance
///
/// IMPORTANT: This presenter reads positions from LineForce and does NOT raycast.
[DisallowMultipleComponent]
public class AimLineRenderer : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private LineForce lineForce;

    [Header("Lines")]
    [SerializeField] private LineRenderer fillLine;   // variable-length line
    [SerializeField] private LineRenderer maxLine;    // always max length

    [Header("Line Colors")]
    [SerializeField] private Gradient fillGradient = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(new Color(0.0f, 0.9f, 0.3f), 0f),
            new GradientColorKey(new Color(1.0f, 0.9f, 0.3f), 1f)
        },
        alphaKeys = new[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        }
    };

    [SerializeField] private Gradient maxGradient = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        },
        alphaKeys = new[]
        {
            new GradientAlphaKey(0.4f, 0f),
            new GradientAlphaKey(0.4f, 1f)
        }
    };

    [Header("Ghost Settings")]
    [Tooltip("Alpha multiplier applied while the shot isn't locked yet.")]
    [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.3f;
    [Tooltip("Extra alpha multiplier for the outline to make it fainter overall.")]
    [SerializeField, Range(0f, 1f)] private float maxBaseAlpha = 0.5f;

    [Header("Geometry")]
    [Tooltip("Distance offset from the ball center to start drawing lines.")]
    [SerializeField, Range(0f, 1f)] private float startOffset = 0.2f;

    [Header("Timing & Behavior")]
    [SerializeField] private float checkInterval = 0.02f;
    [SerializeField] private bool hideWhenTooShort = true;
    [Tooltip("If the effective visible length is shorter than this, lines are hidden.")]
    [SerializeField] private float minVisibleLength = 0.05f;

    float _nextCheck;
    Vector3 _lastDir = Vector3.forward;

    void Reset()
    {
        if (!lineForce) lineForce = GetComponentInParent<LineForce>();
    }

    void Awake()
    {
        if (!lineForce) lineForce = GetComponentInParent<LineForce>();
        ApplyGradients();
        Enable(false);
        Setup(fillLine);
        Setup(maxLine);
    }

    void OnEnable()
    {
        if (!lineForce) return;
        lineForce.AimStarted      += OnAimStarted;
        lineForce.AimPowerChanged += OnAimPowerChanged;
        lineForce.AimEnded        += OnAimEnded;
    }

    void OnDisable()
    {
        if (!lineForce) return;
        lineForce.AimStarted      -= OnAimStarted;
        lineForce.AimPowerChanged -= OnAimPowerChanged;
        lineForce.AimEnded        -= OnAimEnded;
    }

    void Setup(LineRenderer lr)
    {
        if (!lr) return;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.enabled = false;
    }

    void OnAimStarted()
    {
        Enable(true);
        _nextCheck = 0f;
    }

    void OnAimPowerChanged(float powerT)
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + Mathf.Max(0.005f, checkInterval);
        if (!lineForce) return;

        // ----- READ FROM LINEFORCE (no raycasts) -----
        Vector3 origin = lineForce.CurrentAimOrigin;
        Vector3 cursor = lineForce.CursorWorld;

        Vector3 delta = cursor - origin; delta.y = 0f;
        float rawDist = delta.magnitude;
        if (rawDist > 0.0001f) _lastDir = delta / rawDist;

        float maxLen = lineForce.MaxDrawDistance;
        float clamped = Mathf.Clamp(rawDist, 0f, maxLen);

        // ---- Colors (ghost vs locked) ----
        bool ghost = !lineForce.IsLocked;

        Color fillColor = fillGradient.Evaluate(powerT);
        fillColor.a *= ghost ? ghostAlpha : 1f;

        Color outlineColor = maxGradient.Evaluate(powerT);
        outlineColor.a *= (ghost ? ghostAlpha : 1f) * maxBaseAlpha;

        if (fillLine)
        {
            fillLine.startColor = fillColor;
            fillLine.endColor   = fillColor;
        }
        if (maxLine)
        {
            maxLine.startColor = outlineColor;
            maxLine.endColor   = outlineColor;
        }

        // ---- Geometry with start offset & non-negative effective lengths ----
        Vector3 offsetOrigin = origin + _lastDir * startOffset;

        // Never draw backwards toward the ball.
        float fillEffective    = Mathf.Max(0f, clamped - startOffset);
        float outlineEffective = Mathf.Max(0f, maxLen  - startOffset);

        Vector3 fillEnd = offsetOrigin + _lastDir * fillEffective;
        Vector3 maxEnd  = offsetOrigin + _lastDir * outlineEffective;

        Draw(fillLine, offsetOrigin, fillEnd);
        Draw(maxLine,  offsetOrigin, maxEnd);

        // ---- Visibility gate based on effective visible length ----
        if (hideWhenTooShort)
        {
            bool visible = fillEffective >= Mathf.Max(minVisibleLength, lineForce.MinDrawDistance * 0.25f);
            if (fillLine) fillLine.enabled = visible;
            if (maxLine)  maxLine.enabled  = visible;
        }
    }

    void OnAimEnded() => Enable(false);

    void Enable(bool on)
    {
        if (fillLine) { fillLine.positionCount = 2; fillLine.enabled = on; }
        if (maxLine)  { maxLine.positionCount = 2;  maxLine.enabled  = on; }
    }

    void Draw(LineRenderer lr, Vector3 a, Vector3 b)
    {
        if (!lr) return;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    void ApplyGradients()
    {
        if (fillLine) fillLine.colorGradient = fillGradient;
        if (maxLine)  maxLine.colorGradient  = maxGradient;
    }
}
