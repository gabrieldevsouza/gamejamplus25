using UnityEngine;

/// Draws two aim lines during aiming:
///  - maxLine  : always full length (maxDrawDistance) in the current aim direction
///  - fillLine : current length based on cursor distance
///
/// Handles color, ghost transparency, and power gradient dynamically.
/// Listens only to LineForce events; does not change physics.
[DisallowMultipleComponent]
public class AimLineRenderer : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private LineForce lineForce;     // required
    [SerializeField] private new Camera camera;       // optional, defaults to Camera.main

    [Header("Lines")]
    [SerializeField] private LineRenderer fillLine;   // actual power
    [SerializeField] private LineRenderer maxLine;    // outline / full range

    [Header("Line Colors")]
    [Tooltip("Gradient for the main (fill) line.")]
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

    [Tooltip("Gradient for the max (outline) line.")]
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

    [Header("Tuning")]
    [Tooltip("World Y where lines are drawn (table plane).")]
    [SerializeField] private float flattenToY = 0f;
    [SerializeField] private float checkInterval = 0.02f;

    [Header("Behavior")]
    [SerializeField] private bool hideWhenTooShort = true;
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
        _nextCheck = 0f; // update immediately
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

        float dist = delta.magnitude;
        if (dist > 0.0001f) _lastDir = delta / dist;

        float maxLen = lineForce.MaxDrawDistance;
        float clamped = Mathf.Clamp(dist, 0f, maxLen);

        // --- Color selection ---
        bool ghost = !lineForce.IsLocked;

        // Fill line: dynamic hue + ghost fade
        Color fillColor = fillGradient.Evaluate(powerT);
        fillColor.a *= ghost ? ghostAlpha : 1f;

        // Outline line: its own gradient + ghost fade + base fade
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

        // --- Geometry ---
        Draw(fillLine, origin, origin + _lastDir * clamped);
        Draw(maxLine, origin, origin + _lastDir * maxLen);

        if (hideWhenTooShort)
        {
            bool visible = clamped >= Mathf.Max(minVisibleLength, lineForce.MinDrawDistance * 0.25f);
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
        Vector3 dir   = Camera.main.ScreenToWorldPoint(far) - nearW;

        if (Physics.Raycast(nearW, dir, out RaycastHit hit, float.PositiveInfinity))
            return hit.point;

        return null;
    }

    void ApplyGradients()
    {
        if (fillLine)
        {
            fillLine.colorGradient = fillGradient;
        }
        if (maxLine)
        {
            maxLine.colorGradient = maxGradient;
        }
    }
}
