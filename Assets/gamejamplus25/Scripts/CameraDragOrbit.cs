using UnityEngine;
using Unity.Cinemachine; // Cinemachine v3 namespace

/// Drag left/right to orbit by rotating ONLY the FollowOffset on a CinemachineFollow.
/// When the player is aiming (LineForce.IsAiming), input is ignored (no rotation).
[DisallowMultipleComponent]
public class CameraDragOrbit : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Cinemachine Follow on this virtual camera.")]
    [SerializeField] private CinemachineFollow cinemachineFollow;

    [Tooltip("Usually the player. If left empty, uses cinemachineFollow.FollowTarget.")]
    [SerializeField] private Transform target;

    [Tooltip("Optional: when provided, camera rotation is disabled while IsAiming is true.")]
    [SerializeField] private LineForce lineForce;

    [Header("Behavior")]
    [Tooltip("If true, the camera will not rotate while the player is aiming.")]
    [SerializeField] private bool blockWhenAiming = true;

    [Header("Input")]
    [Tooltip("Yaw degrees added per screen pixel dragged horizontally.")]
    [SerializeField] private float yawDegreesPerPixel = 0.18f;

    [Tooltip("Minimum pixels moved to start rotating (avoids micro jitter).")]
    [SerializeField] private float dragThresholdPx = 1.5f;

    [Header("Smoothing")]
    [Tooltip("How quickly the yaw reaches the target angle. 0 = instant.")]
    [Range(0f, 20f)] [SerializeField] private float yawLerpSpeed = 10f;

    // runtime
    Vector3 _initialOffset; // (x,z) distance & y height
    float _yawTarget;
    float _yawCurrent;

    // pointer state
    bool _dragging;
    Vector2 _lastPos;

    void Awake()
    {
        if (!cinemachineFollow)
            cinemachineFollow = GetComponent<CinemachineFollow>();

        if (!target && cinemachineFollow && cinemachineFollow.FollowTarget != null)
            target = cinemachineFollow.FollowTarget;

        if (!lineForce && target)
            lineForce = target.GetComponent<LineForce>(); // optional

        if (cinemachineFollow)
            _initialOffset = cinemachineFollow.FollowOffset;

        // derive initial yaw from offset
        if (_initialOffset.sqrMagnitude > 0.0001f)
            _yawTarget = _yawCurrent = Mathf.Atan2(_initialOffset.x, _initialOffset.z) * Mathf.Rad2Deg;
    }

    void Update()
    {
        // If aiming is active and blocking is enabled, ignore input this frame.
        if (blockWhenAiming && lineForce && lineForce.IsAiming)
        {
            _dragging = false; // cancel any ongoing drag
            return;
        }

        PollPointer(); // sets _yawTarget from input
    }

    void LateUpdate()
    {
        if (!cinemachineFollow || !target) return;

        // Smooth to target yaw
        if (yawLerpSpeed > 0f)
            _yawCurrent = Mathf.LerpAngle(_yawCurrent, _yawTarget, 1f - Mathf.Exp(-yawLerpSpeed * Time.deltaTime));
        else
            _yawCurrent = _yawTarget;

        // Rebuild offset from current yaw, preserving height & distance
        float distance = new Vector2(_initialOffset.x, _initialOffset.z).magnitude;
        float height   = _initialOffset.y;
        float rad      = _yawCurrent * Mathf.Deg2Rad;

        cinemachineFollow.FollowOffset = new Vector3(
            Mathf.Sin(rad) * distance,
            height,
            Mathf.Cos(rad) * distance
        );
    }

    // --- Input (mouse + single touch) ---
    void PollPointer()
    {
        bool down, up, held;
        Vector2 pos;

        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            pos  = t.position;
            down = t.phase == TouchPhase.Began;
            up   = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
        }
        else
        {
            pos  = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            up   = Input.GetMouseButtonUp(0);
            held = Input.GetMouseButton(0);
        }

        if (down)
        {
            _dragging = true;
            _lastPos = pos;
        }
        else if (up)
        {
            _dragging = false;
        }
        else if (held && _dragging)
        {
            Vector2 delta = pos - _lastPos;
            _lastPos = pos;

            if (Mathf.Abs(delta.x) >= dragThresholdPx)
            {
                _yawTarget += delta.x * yawDegreesPerPixel;

                // keep bounded to avoid drift
                if (_yawTarget > 180f) _yawTarget -= 360f;
                if (_yawTarget < -180f) _yawTarget += 360f;
            }
        }
    }
}
