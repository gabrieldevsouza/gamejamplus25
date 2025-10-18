using UnityEngine;

[DisallowMultipleComponent]
public class SlopeProbe : MonoBehaviour
{
    [Header("Slope Detection")]
    [Tooltip("Angle from Up (deg) above which surface counts as a slope.")]
    [SerializeField] private float slopeAngleThresholdDeg = 1.5f;
    [Tooltip("Min gravity component along surface to count as slope (0 = ignore).")]
    [SerializeField] private float minTangentGravity = 0.00f;
    [Tooltip("Layer mask for ground.")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("If true, no ground hit => not on slope.")]
    [SerializeField] private bool requireGroundedForSlope = true;

    [Header("SphereCast Probe")]
    [SerializeField] private float slopeProbeSkin = 0.02f;        // upward offset from center
    [SerializeField] private float slopeProbeExtra = 0.30f;       // extra length beyond radius
    [SerializeField] private float slopeProbeRadiusScale = 0.95f; // % of collider radius

    [Header("Latch (debounce)")]
    [SerializeField] private float slopeLatchTime = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugProbe = false;

    public bool OnSlopeLatched => _onSlopeLatched;

    float _ballRadius = 0.25f;
    bool  _onSlopeLatched;
    float _slopeLatchUntil;

    void Awake()
    {
        var sphere = GetComponent<SphereCollider>();
        if (sphere)
        {
            float s = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            _ballRadius = Mathf.Max(0.01f, sphere.radius * s);
        }
        else if (TryGetComponent<Collider>(out var col))
        {
            _ballRadius = Mathf.Max(0.01f, col.bounds.extents.y);
        }
    }

    public void FixedStep()
    {
        bool raw = IsOnSlopeRaw();
        if (raw)
        {
            _onSlopeLatched  = true;
            _slopeLatchUntil = Time.time + slopeLatchTime;
        }
        else if (_onSlopeLatched && Time.time >= _slopeLatchUntil)
        {
            _onSlopeLatched = false;
        }
    }

    bool IsOnSlopeRaw()
    {
        float castRadius = Mathf.Max(0.01f, _ballRadius * slopeProbeRadiusScale);
        Vector3 origin   = transform.position + Vector3.up * slopeProbeSkin;
        float   castDist = _ballRadius + slopeProbeExtra + slopeProbeSkin;

        bool gotHit = Physics.SphereCast(origin, castRadius, Vector3.down,
            out RaycastHit hit, castDist, groundLayers, QueryTriggerInteraction.Ignore);

        if (!gotHit)
        {
            if (requireGroundedForSlope) return false;
            return false; // conservative default
        }

        float angle = Vector3.Angle(hit.normal, Vector3.up);
        if (angle < slopeAngleThresholdDeg) return false;

        if (minTangentGravity > 0f)
        {
            Vector3 tg = Vector3.ProjectOnPlane(Physics.gravity, hit.normal);
            if (tg.magnitude < minTangentGravity) return false;
        }

        if (debugProbe) Debug.DrawRay(hit.point, hit.normal * 0.4f, Color.cyan, 0.05f, false);
        return true;
    }
}
