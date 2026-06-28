using Soapbox.Networking;
using UnityEngine;

/// <summary>
/// Self-contained suspension spring.
///
/// Setup
/// ─────
/// • Place this component on its own GameObject (the "suspension anchor") that
///   sits at the top of the wheel travel range, as a child of the vehicle body.
/// • Assign <see cref="_wheelTransform"/> to the Transform of the separate
///   GameObject that carries the <see cref="Wheel"/> component.
///   Suspension will slide that transform up and down along the spring axis;
///   neither script holds a reference to the other component.
///
/// Networking
/// ──────────
/// In a multiplayer session only the instance that owns the simulation runs
/// the spring. Remote clones are disabled via <see cref="NetworkOwnershipGate"/>.
///
/// Who this script knows about : nobody (only a plain Transform + the gate).
/// </summary>
public class Suspension : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Target")]
    [Tooltip("The transform that will be repositioned by the spring " +
             "(e.g. the GameObject that carries the Wheel component).")]
    [SerializeField] private Transform _wheelTransform;

    [Header("Spring")]
    [SerializeField, Min(0f)] private float _restDistance = 0.5f;
    [SerializeField, Min(0f)] private float _springStrength = 30f;
    [SerializeField, Min(0f)] private float _springDamp = 3f;

    [Header("Layer Mask")]
    [SerializeField] private LayerMask _groundMask = ~0;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private Rigidbody _rb;
    private bool _warnedMissingRigidbody;
    private bool _warnedMissingWheel;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        // Gate runs on enable (not Awake) so Mirror has time to assign
        // isOwned / netId before we decide whether to keep the component.
        if (!NetworkOwnershipGate.KeepLocal(this)) return;

        EnsureRigidbody();
        WarnIfMissingWheel();
    }

    private void FixedUpdate()
    {
        // Lazily re-acquire the Rigidbody in case Mirror's PredictedRigidbody
        // moved the physics components onto a ghost object at runtime.
        if (_rb == null) EnsureRigidbody();
        if (_rb == null) return;

        bool grounded = Physics.Raycast(
            transform.position,
            -transform.up,
            out RaycastHit hit,
            _restDistance,
            _groundMask
        );

        if (grounded)
            ApplySpring(hit);

        PositionWheelTransform(grounded, grounded ? hit.point : Vector3.zero);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void EnsureRigidbody()
    {
        if (_rb != null) return;

        _rb = GetComponentInParent<Rigidbody>();

        if (_rb == null && !_warnedMissingRigidbody)
        {
            Debug.LogError($"[Suspension] No Rigidbody found in parent hierarchy of '{name}'. " +
                           "Suspension will stay inert until one is available.", this);
            _warnedMissingRigidbody = true;
        }
    }

    private void WarnIfMissingWheel()
    {
        if (_wheelTransform == null && !_warnedMissingWheel)
        {
            Debug.LogWarning($"[Suspension] '_wheelTransform' is not assigned on '{name}'. " +
                             "The spring will still apply force but nothing will be repositioned.", this);
            _warnedMissingWheel = true;
        }
    }

    /// <summary>Computes and applies the damped spring force to the rigidbody.</summary>
    private void ApplySpring(RaycastHit hit)
    {
        // Defensive — should not happen because FixedUpdate guards above,
        // but keep the helper safe in case it's called from elsewhere later.
        if (_rb == null) return;

        Vector3 springDir = hit.normal;
        Vector3 pointVelocity = _rb.GetPointVelocity(hit.point);

        float offset = _restDistance - hit.distance;
        float velocity = Vector3.Dot(springDir, pointVelocity);
        float force = (offset * _springStrength * 100f) - (velocity * _springDamp * 100f);

        _rb.AddForceAtPosition(springDir * force, transform.position);
    }

    /// <summary>
    /// Slides <see cref="_wheelTransform"/> to the contact point when grounded,
    /// or to the fully-extended rest position when airborne.
    /// </summary>
    private void PositionWheelTransform(bool grounded, Vector3 contactPoint)
    {
        if (_wheelTransform == null) return;

        _wheelTransform.position = grounded
            ? contactPoint
            : transform.position + (-transform.up * _restDistance);
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        bool hit = Physics.Raycast(transform.position, -transform.up, out RaycastHit info, _restDistance);
        Gizmos.color = hit ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position - transform.up * _restDistance);

        if (_wheelTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_wheelTransform.position, 0.05f);
        }
    }
}
