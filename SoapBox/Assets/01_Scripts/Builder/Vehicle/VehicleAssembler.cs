using System.Collections.Generic;
using UnityEngine;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>
    /// Turns a finished build into a vehicle drivable by the existing
    /// <c>VehicleController</c>: validates it, ensures a configured Rigidbody on the
    /// root with the computed centre of mass, enables the driving behaviours on every
    /// part, gathers the steering/brake wheels from each <see cref="WheelRoleProvider"/>,
    /// and initialises the controller. The builder adapts to the controller — the
    /// controller's architecture is not changed (only a backward-compatible
    /// <c>Initialize</c> was added).
    /// </summary>
    public sealed class VehicleAssembler : MonoBehaviour
    {
        [Header("Rigidbody configuration")]
        [SerializeField, Min(0f)] private float _linearDamping = 0.05f;
        [SerializeField, Min(0f)] private float _angularDamping = 0.5f;
        [SerializeField] private RigidbodyInterpolation _interpolation = RigidbodyInterpolation.Interpolate;
        [SerializeField] private CollisionDetectionMode _collisionMode = CollisionDetectionMode.ContinuousDynamic;

        [Header("Validation requirements")]
        [SerializeField, Min(0)] private int _minWheels = 4;
        [SerializeField] private bool _requireChassis = true;
        [SerializeField] private bool _requireSeat = true;

        [Header("Steering")]
        [Tooltip("Restrict steering to the front wheels only (decided by local Z). A wheel must still have IsSteering on its WheelRoleProvider to be eligible. Turn off to honour every provider flag.")]
        [SerializeField] private bool _frontWheelsSteerOnly = true;

        /// <summary>
        /// Assembles the vehicle for driving. Returns false (with a reason) if the build
        /// fails validation.
        /// </summary>
        public bool AssembleForDriving(VehicleRoot vehicle, out string error)
        {
            error = null;

            ValidationResult validation = VehicleValidator.Validate(vehicle, _minWheels, _requireChassis, _requireSeat);
            if (!validation.IsValid)
            {
                error = string.Join("\n", validation.Errors);
                return false;
            }

            ConfigureRigidbody(vehicle);
            InitialiseController(vehicle);
            return true;
        }

        private void ConfigureRigidbody(VehicleRoot vehicle)
        {
            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null) rb = vehicle.gameObject.AddComponent<Rigidbody>();

            VehicleStats stats = VehicleStatsCalculator.Compute(vehicle);

            rb.mass = Mathf.Max(stats.TotalWeight, 0.0001f);
            rb.linearDamping = _linearDamping;
            rb.angularDamping = _angularDamping;
            rb.interpolation = _interpolation;
            rb.collisionDetectionMode = _collisionMode;
            rb.centerOfMass = stats.CenterOfMass;
        }

        private void InitialiseController(VehicleRoot vehicle)
        {
            var brake = new List<Wheel>();
            var steerCandidates = new List<Wheel>();
            var steerLocalZ = new List<float>();

            IReadOnlyList<PartInstance> parts = vehicle.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;

                // Enable driving physics now that the Rigidbody exists (lazily resolved by Wheel/Suspension).
                PartGameplayToggle.SetGameplayEnabled(parts[i].gameObject, true);

                WheelRoleProvider[] providers = parts[i].GetComponentsInChildren<WheelRoleProvider>(true);
                for (int p = 0; p < providers.Length; p++)
                {
                    Wheel wheel = providers[p].Wheel;
                    if (wheel == null) continue;

                    if (providers[p].IsBrake) brake.Add(wheel);
                    if (providers[p].IsSteering)
                    {
                        steerCandidates.Add(wheel);
                        steerLocalZ.Add(vehicle.transform.InverseTransformPoint(wheel.transform.position).z);
                    }
                }
            }

            List<Wheel> steering = SelectSteeringWheels(steerCandidates, steerLocalZ);

            VehicleController controller = vehicle.GetComponent<VehicleController>();
            if (controller == null) controller = vehicle.gameObject.AddComponent<VehicleController>();

            controller.Initialize(steering.ToArray(), brake.ToArray());
        }

        /// <summary>
        /// Restricts steering to the front half of the vehicle (by local Z) so a build using
        /// all-steering wheel prefabs still steers like a car. Honours every eligible wheel
        /// when <see cref="_frontWheelsSteerOnly"/> is off or the wheels share a single axle.
        /// </summary>
        private List<Wheel> SelectSteeringWheels(List<Wheel> candidates, List<float> localZ)
        {
            if (!_frontWheelsSteerOnly || candidates.Count <= 1) return candidates;

            float min = localZ[0], max = localZ[0];
            for (int i = 1; i < localZ.Count; i++)
            {
                if (localZ[i] < min) min = localZ[i];
                if (localZ[i] > max) max = localZ[i];
            }

            // Single axle (no front/rear spread): keep them all steering.
            if (max - min < 0.1f) return candidates;

            float mid = (min + max) * 0.5f;
            var front = new List<Wheel>();
            for (int i = 0; i < candidates.Count; i++)
                if (localZ[i] > mid) front.Add(candidates[i]);

            return front.Count > 0 ? front : candidates;
        }
    }
}
