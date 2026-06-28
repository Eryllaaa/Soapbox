using UnityEngine;

/// <summary>
/// Handles all wheel-level physics: side friction, acceleration, and braking.
///
/// Setup
/// ?????
/// • Place this on its own GameObject (the "wheel pivot") that is a child of
///   the vehicle body.
/// • The Suspension component lives on a *sibling* GameObject and will slide
///   this transform's position along the spring axis — Wheel never references
///   Suspension.
/// • The VehicleController (or any other system) drives this component through
///   the three public members: <see cref="AccelInput"/>, <see cref="TopSpeed"/>,
///   <see cref="Brake"/>, and <see cref="StopBraking"/>.
///
/// Who this script knows about : nobody.
/// </summary>
public class Wheel : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Friction")]
    [SerializeField, Range(0f, 1f)] private float _grip = 0.8f;
    [SerializeField, Range(0f, 1f)] private float _gripWhenBraking = 0.4f;
    [SerializeField, Min(0f)] private float _frictionStrength = 1f;

    [Header("Acceleration")]
    [SerializeField, Min(0f)] private float _power = 500f;

    [Header("Braking")]
    [SerializeField, Min(0f)] private float _brakingPower = 800f;

    [Header("Grounding")]
    [Tooltip("Should match the Suspension rest distance on the sibling Suspension object.")]
    [SerializeField, Min(0f)] private float _groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask _groundMask = ~0;

    [Header("Visual Tire (optional)")]
    [Tooltip("If assigned, rotated to match forward rolling speed.")]
    [SerializeField] private Transform _tireVisual;

    // -------------------------------------------------------------------------
    // Public API — driven by an external controller
    // -------------------------------------------------------------------------

    /// <summary>Normalised throttle input [0 … 1].</summary>
    public float AccelInput { get; set; }

    /// <summary>Maximum forward speed used to shape the torque curve (m/s).</summary>
    public float TopSpeed { get; set; } = 30f;

    /// <summary>Tell the wheel to apply braking force this physics step.</summary>
    public void Brake() => _isBraking = true;

    /// <summary>Release the brakes.</summary>
    public void StopBraking() => _isBraking = false;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private Rigidbody _rb;
    private bool _isBraking;
    private float _activeGrip;
    private bool _isGrounded;
    private Vector3 _groundedRaycastPadding => transform.up * 0.1f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponentInParent<Rigidbody>();
        _activeGrip = _grip;

        if (_rb == null)
            Debug.LogError($"[Wheel] No Rigidbody found in parent hierarchy of '{name}'.", this);
    }

    private void FixedUpdate()
    {
        _isGrounded = Physics.Raycast(
            transform.position + _groundedRaycastPadding, // avoid raycasting below the ground when the wheel is right on the surface
            -transform.up,
            _groundCheckDistance,
            _groundMask
        );

        if (_isGrounded)
        {
            _activeGrip = _isBraking ? _gripWhenBraking : _grip;

            ApplySideFriction();

            if (_isBraking)
                ApplyBraking();
            else
                ApplyAcceleration();
        }

        RollTireVisual();
    }

    // -------------------------------------------------------------------------
    // Private physics helpers
    // -------------------------------------------------------------------------

    /// <summary>Resists lateral sliding (cornering / steering grip).</summary>
    private void ApplySideFriction()
    {
        Vector3 steeringDir = transform.right;
        Vector3 tireWorldVel = _rb.GetPointVelocity(transform.position);

        float steeringVel = Vector3.Dot(steeringDir, tireWorldVel);
        float desiredVelChange = -steeringVel * _activeGrip;
        float desiredAccel = desiredVelChange / Time.fixedDeltaTime;

        _rb.AddForceAtPosition(steeringDir * desiredAccel * _frictionStrength, transform.position);
    }

    /// <summary>Drives the wheel forward, torque falling off near <see cref="TopSpeed"/>.</summary>
    private void ApplyAcceleration()
    {
        if (AccelInput <= 0f || _power <= 0f) return;

        Vector3 accelDir = transform.forward;
        float carSpeed = Vector3.Dot(_rb.transform.forward, _rb.linearVelocity);
        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(carSpeed) / TopSpeed);
        float availableTorque = _power * AccelInput * (1f - normalizedSpeed);

        _rb.AddForceAtPosition(accelDir * availableTorque, transform.position, ForceMode.Force);
    }

    /// <summary>Applies a force opposing the current forward velocity.</summary>
    private void ApplyBraking()
    {
        float brakingFactor = Vector3.Dot(transform.forward, _rb.linearVelocity);

        if (Mathf.Abs(brakingFactor) < 0.01f) return;

        Vector3 brakeDir = -Mathf.Sign(brakingFactor) * transform.forward;
        _rb.AddForceAtPosition(brakeDir * _brakingPower, transform.position);
    }

    /// <summary>Spins the optional tire mesh based on the vehicle's forward speed.</summary>
    private void RollTireVisual()
    {
        if (_tireVisual == null || _rb == null) return;

        float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
        float rollDeg = forwardSpeed * (180f / Mathf.PI) * Time.fixedDeltaTime;

        _tireVisual.localRotation *= Quaternion.Euler(rollDeg, 0f, 0f);
    }
}