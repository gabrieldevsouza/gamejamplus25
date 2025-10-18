using UnityEngine;

/// Draws two aim lines during aiming:
///  - maxLine  : always full length (maxDrawDistance) in the current aim direction
///  - fillLine : current length based on cursor distance
///
/// Handles color, ghost transparency, power gradient, and start offset dynamically.
[DisallowMultipleComponent]
public class AimLineRenderer : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private LineForce lineForce;
    [SerializeField] private new Camera camera;

    [Header("Lines")]
    [SerializeField] private LineRenderer fillLine;
    [SerializeField] private LineRenderer maxLine;

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
    [Tooltip("World Y where lines are drawn (table plane).")]
    [SerializeField] private float flattenToY = 0f;

    [Header("Timing & Behavior")]
    [SerializeField] private float checkInterval = 0.02f;
    [SerializeField] private bool hideWhenTooShort = true;
    [Tooltip("If the effective visible length is shorter than this, lines are hidden.")]
    [SerializeField] private float minVisibleLength = 0.05f;

    float _nextCheck;
    Vector3 _lastDir;

    void Reset()
    {
        if (!lineForce) lineForce = GetComponent<LineForce>();
        camera = Camera.main;
    }

    void Awake()
    {
        if (!camera) camera = Camera.main;
        ApplyGradients();
        Enable(false);
    }

    void OnEnable()
    {
        if (!lineForce) return;
        lineForce.AimStarted += OnAimStarted;
        lineForce.AimPowerChanged += OnAimPowerChanged;
        lineForce.AimEnded += OnAimEnded;
    }

    void OnDisable()
    {
        if (!lineForce) return;
        lineForce.AimStarted -= OnAimStarted;
        lineForce.AimPowerChanged -= OnAimPowerChanged;
        lineForce.AimEnded -= OnAimEnded;
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

        Vector3 origin = FlattenToPlane(lineForce.IsLocked ? lineForce.LockOrigin : transform.position);
        Vector3? hit = CastMouseRay();
        if (!hit.HasValue) return;

        Vector3 cursor = FlattenToPlane(hit.Value);
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

        // Ensure we never draw "backwards" toward the ball.
        float fillEffective   = Mathf.Max(0f, clamped - startOffset);
        float outlineEffective = Mathf.Max(0f, maxLen  - startOffset);

        Vector3 fillEnd = offsetOrigin + _lastDir * fillEffective;
        Vector3 maxEnd  = offsetOrigin + _lastDir * outlineEffective;

        Draw(fillLine, offsetOrigin, fillEnd);
        Draw(maxLine, offsetOrigin, maxEnd);

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
        if (maxLine)  { maxLine.positionCount = 2;  maxLine.enabled = on;  }
    }

    void Draw(LineRenderer lr, Vector3 a, Vector3 b)
    {
        if (!lr) return;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    Vector3 FlattenToPlane(Vector3 v) => new(v.x, flattenToY, v.z);

    Vector3? CastMouseRay()
    {
        if (!camera) camera = Camera.main;
        if (!camera) return null;

        Vector3 near = new(Input.mousePosition.x, Input.mousePosition.y, camera.nearClipPlane);
        Vector3 far  = new(Input.mousePosition.x, Input.mousePosition.y, camera.farClipPlane);
        Vector3 nearW = camera.ScreenToWorldPoint(near);
        Vector3 dir   = camera.ScreenToWorldPoint(far) - nearW;

        if (Physics.Raycast(nearW, dir, out RaycastHit hit, float.PositiveInfinity))
            return hit.point;

        return null;
    }

    void ApplyGradients()
    {
        if (fillLine) fillLine.colorGradient = fillGradient;
        if (maxLine)  maxLine.colorGradient  = maxGradient;
    }
}
