using UnityEngine;

/// <summary>
/// Handles wheel-level physics for a gravity-driven soapbox racer:
/// side friction (grip / cornering) and braking only.
/// Acceleration has been removed — the vehicle is pushed solely by gravity.
///
/// Setup
/// ─────
/// • Place this on its own GameObject (the "wheel pivot") that is a child of
///   the vehicle body.
/// • The Suspension component lives on a *sibling* GameObject and will slide
///   this transform's position along the spring axis — Wheel never references
///   Suspension.
/// • The VehicleController (or any other system) drives this component through
///   <see cref="Brake"/> and <see cref="StopBraking"/>.
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

    /// <summary>
    /// The owning Rigidbody, resolved lazily. Lazy resolution lets the modular builder
    /// add the Rigidbody to the vehicle root and then enable this wheel afterwards
    /// (Awake may have run while no Rigidbody existed yet).
    /// </summary>
    private Rigidbody Rb => _rb != null ? _rb : (_rb = GetComponentInParent<Rigidbody>());

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _activeGrip = _grip;
    }

    private void FixedUpdate()
    {
        _isGrounded = Physics.Raycast(
            transform.position + _groundedRaycastPadding,
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
        Vector3 tireWorldVel = Rb.GetPointVelocity(transform.position);

        float steeringVel = Vector3.Dot(steeringDir, tireWorldVel);
        float desiredVelChange = -steeringVel * _activeGrip;
        float desiredAccel = desiredVelChange / Time.fixedDeltaTime;

        Rb.AddForceAtPosition(steeringDir * desiredAccel * _frictionStrength, transform.position);
    }

    /// <summary>Applies a force opposing the current forward velocity.</summary>
    private void ApplyBraking()
    {
        float brakingFactor = Vector3.Dot(transform.forward, Rb.linearVelocity);

        if (Mathf.Abs(brakingFactor) < 0.01f) return;

        Vector3 brakeDir = -Mathf.Sign(brakingFactor) * transform.forward;
        Rb.AddForceAtPosition(brakeDir * _brakingPower, transform.position);
    }

    /// <summary>Spins the optional tire mesh based on the vehicle's forward speed.</summary>
    private void RollTireVisual()
    {
        if (_tireVisual == null || Rb == null) return;

        float forwardSpeed = Vector3.Dot(Rb.linearVelocity, transform.forward);
        float rollDeg = forwardSpeed * (180f / Mathf.PI) * Time.fixedDeltaTime;

        _tireVisual.localRotation *= Quaternion.Euler(rollDeg, 0f, 0f);
    }
}