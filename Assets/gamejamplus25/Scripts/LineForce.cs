using System;
using UnityEngine;

public class LineForce : MonoBehaviour
{
    [Header("Shot Settings")]
    [SerializeField] private float shotPower = 10f;
    [SerializeField] private float stopVelocity = 0.02f;     // When considered fully stopped
    [SerializeField] private float velocityToAim = 0.5f;     // Can aim at/under this speed

    [Header("Components")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Camera camera;

    private Rigidbody _rigidbody;

    private bool _isIdle;
    private bool _isAiming;
    private bool _stuckInAim;

    private Vector3 _lastWorldPoint;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        _isAiming = false;
        _isIdle = true;
        _stuckInAim = false;

        if (!camera)
            camera = Camera.main;

        if (lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        // Start aiming when mouse is pressed and velocity is low enough
        if (_isIdle && Input.GetMouseButtonDown(0))
        {
            _isAiming = true;
            _stuckInAim = true;
            Stop(); // 🔸 stop immediately when aiming begins
        }

        // While aiming, update line and shoot when mouse released
        if (_isAiming)
        {
            Vector3? worldPoint = CastMouseClickRay();
            if (worldPoint.HasValue)
            {
                _lastWorldPoint = worldPoint.Value;
                DrawLine(_lastWorldPoint);
            }
            else
            {
                if (lineRenderer && lineRenderer.enabled)
                    lineRenderer.enabled = false;
            }

            if (Input.GetMouseButtonUp(0))
            {
                Shoot(_lastWorldPoint);
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
        float currentSpeed = _rigidbody.linearVelocity.magnitude;

        // Allow aiming when below velocityToAim
        _isIdle = currentSpeed <= velocityToAim;

        // If almost stopped, ensure full stop
        if (currentSpeed < stopVelocity)
        {
            Stop();
        }
    }

    private void Shoot(Vector3 worldPoint)
    {
        _isAiming = false;
        _stuckInAim = false;

        if (lineRenderer) lineRenderer.enabled = false;

        // Flatten aim to horizontal plane
        var horizontalWorldPoint = new Vector3(worldPoint.x, transform.position.y, worldPoint.z);

        Vector3 direction = (horizontalWorldPoint - transform.position).normalized;
        float strength = Vector3.Distance(transform.position, horizontalWorldPoint);

        _isIdle = false;

        _rigidbody.AddForce(direction * (strength * shotPower), ForceMode.Impulse);
    }

    private void DrawLine(Vector3 worldPoint)
    {
        if (!lineRenderer) return;

        if (lineRenderer.positionCount != 2)
            lineRenderer.positionCount = 2;

        Vector3[] positions = {
            transform.position,
            worldPoint
        };

        lineRenderer.SetPositions(positions);
        lineRenderer.enabled = true;
    }

    private void Stop()
    {
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _isIdle = true;
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

        RaycastHit hit;
        if (Physics.Raycast(worldMousePosNear, worldMousePosFar - worldMousePosNear, out hit, float.PositiveInfinity))
        {
            return hit.point;
        }
        else
        {
            return null;
        }
    }
}
