using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// Declares how a wheel part is wired into the existing <see cref="VehicleController"/>
    /// when the vehicle is assembled for driving. Lives on the root of every wheel
    /// prefab, alongside a pre-wired Suspension + Wheel + tire-visual sub-hierarchy.
    ///
    /// The builder never assembles the spring internals; it only reads this
    /// component to learn which <see cref="Wheel"/> to register as steering and/or
    /// brake when calling <c>VehicleController.Initialize</c>.
    /// </summary>
    public sealed class WheelRoleProvider : MonoBehaviour
    {
        [Tooltip("The Wheel component on this part's pivot. Required for driving.")]
        [SerializeField] private Wheel _wheel;

        [Tooltip("Register this wheel with the controller's steering set.")]
        [SerializeField] private bool _isSteering = true;

        [Tooltip("Register this wheel with the controller's brake set.")]
        [SerializeField] private bool _isBrake = true;

        /// <summary>The driving-physics wheel this part owns.</summary>
        public Wheel Wheel => _wheel;

        /// <summary>Whether the controller should steer this wheel.</summary>
        public bool IsSteering => _isSteering;

        /// <summary>Whether the controller should brake with this wheel.</summary>
        public bool IsBrake => _isBrake;
    }
}
