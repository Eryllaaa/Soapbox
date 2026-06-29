using UnityEngine;

/// <summary>
/// Trigger zone that gives a forward boost to any vehicle (Rigidbody) entering it.
///
/// Setup
/// ─────
/// • Attach to a GameObject with a Collider set to <c>isTrigger = true</c>.
/// • Orient the GameObject so its forward (transform.forward) points along the
///   desired boost direction.
/// • The pad optionally re-arms itself after <see cref="_cooldown"/> seconds, or
///   stays single-use if <see cref="_singleUse"/> is true.
///
/// Who this script knows about : the Rigidbody on the entering vehicle. It does
/// NOT know about Wheel / Suspension / VehicleController — it just nudges a
/// velocity. This keeps the trigger fully decoupled from vehicle internals,
/// matching the project's separation-of-responsibilities rule.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpeedPad : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Boost")]
    [Tooltip("Impulse magnitude added to the vehicle's velocity along the pad's forward axis.")]
    [SerializeField, Min(0f)] private float _boostForce = 15f;

    [Tooltip("If true, the pad's velocity is SET to this magnitude instead of ADDED to the current one. " +
             "Useful for jump pads / loops where you want a deterministic launch speed.")]
    [SerializeField] private bool _setVelocity = false;

    [Tooltip("Multiplier applied to the vehicle's mass when adding the impulse. " +
             "1 = respects mass (heavier vehicles get the same Δv as light ones, which feels right for a soapbox).")]
    [SerializeField, Min(0f)] private float _massMultiplier = 1f;

    [Header("Arming")]
    [Tooltip("If true, the pad can only fire once and then stays disabled.")]
    [SerializeField] private bool _singleUse = false;

    [Tooltip("Seconds before the pad re-arms. Ignored if single-use.")]
    [SerializeField, Min(0f)] private float _cooldown = 2f;

    [Header("Filtering")]
    [Tooltip("Only Rigidbodies on these layers will be boosted. Defaults to everything.")]
    [SerializeField] private LayerMask _vehicleMask = ~0;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private bool _armed = true;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Reset()
    {
        // Make sure the collider is set up correctly when first added in the editor.
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        if ((_vehicleMask.value & (1 << rb.gameObject.layer)) == 0) return;

        ApplyBoost(rb);
        Consume();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds (or sets) the vehicle's velocity along the pad's forward axis.
    /// Works in the pad's local space so the designer just rotates the pad to
    /// aim the boost — no extra "direction" field to tune.
    /// </summary>
    private void ApplyBoost(Rigidbody rb)
    {
        Vector3 boostDir = transform.forward;

        if (_setVelocity)
        {
            Vector3 lateral = Vector3.ProjectOnPlane(rb.linearVelocity, boostDir);
            rb.linearVelocity = lateral + boostDir * _boostForce;
        }
        else
        {
            rb.AddForce(boostDir * _boostForce * rb.mass * _massMultiplier, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Disarms the pad and schedules a re-arm if not single-use.
    /// </summary>
    private void Consume()
    {
        _armed = false;

        if (!_singleUse)
            Invoke(nameof(ReArm), _cooldown);
    }

    private void ReArm() => _armed = true;

    // -------------------------------------------------------------------------
    // Editor gizmos — so designers can see boost direction & orientation in scene view
    // -------------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _armed ? Color.green : Color.gray;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
        Gizmos.DrawSphere(transform.position + transform.forward * 2f, 0.15f);
    }
}