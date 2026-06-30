using System;
using UnityEngine;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>
    /// Keeps a live <see cref="VehicleStats"/> snapshot in sync with a
    /// <see cref="VehicleRoot"/>, recomputing only when the build changes and raising
    /// <see cref="StatsChanged"/> so UI can update without polling.
    /// </summary>
    [RequireComponent(typeof(VehicleRoot))]
    public sealed class VehicleStatsTracker : MonoBehaviour
    {
        private VehicleRoot _vehicle;

        /// <summary>The most recently computed statistics.</summary>
        public VehicleStats Current { get; private set; }

        /// <summary>Raised whenever the statistics are recomputed.</summary>
        public event Action<VehicleStats> StatsChanged;

        private void Awake() => _vehicle = GetComponent<VehicleRoot>();

        private void OnEnable()
        {
            _vehicle.Changed += Recompute;
            Recompute();
        }

        private void OnDisable() => _vehicle.Changed -= Recompute;

        /// <summary>Forces a recompute (e.g. after painting or transforming a part).</summary>
        public void Recompute()
        {
            Current = VehicleStatsCalculator.Compute(_vehicle);
            StatsChanged?.Invoke(Current);
        }
    }
}
