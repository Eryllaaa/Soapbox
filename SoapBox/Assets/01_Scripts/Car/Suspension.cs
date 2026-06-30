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
/// Who this script knows about : nobody (only a plain Transform).
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

    /// <summary>
    /// The owning Rigidbody, resolved lazily so the modular builder can add the
    /// Rigidbody to the vehicle root and enable this suspension afterwards.
    /// </summary>
    private Rigidbody Rb => _rb != null ? _rb : (_rb = GetComponentInParent<Rigidbody>());

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (_wheelTransform == null)
            Debug.LogWarning($"[Suspension] '_wheelTransform' is not assigned on '{name}'. " +
                             "The spring will still apply force but nothing will be repositioned.", this);
    }

    private void FixedUpdate()
    {
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

    /// <summary>Computes and applies the damped spring force to the rigidbody.</summary>
    private void ApplySpring(RaycastHit hit)
    {
        Vector3 springDir = hit.normal;
        Vector3 pointVelocity = Rb.GetPointVelocity(hit.point);

        float offset = _restDistance - hit.distance;
        float velocity = Vector3.Dot(springDir, pointVelocity);
        float force = (offset * _springStrength * 100f) - (velocity * _springDamp * 100f);

        Rb.AddForceAtPosition(springDir * force, transform.position);
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