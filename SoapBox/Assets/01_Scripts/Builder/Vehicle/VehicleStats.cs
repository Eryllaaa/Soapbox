using UnityEngine;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>Immutable snapshot of a vehicle's computed statistics.</summary>
    public readonly struct VehicleStats
    {
        /// <summary>Sum of every part's weight (kg).</summary>
        public readonly float TotalWeight;

        /// <summary>Sum of every part's cost.</summary>
        public readonly float TotalCost;

        /// <summary>Bounding size in metres: x = width, y = height, z = length.</summary>
        public readonly Vector3 Size;

        /// <summary>Weight-weighted centre of mass, in the vehicle root's local space.</summary>
        public readonly Vector3 CenterOfMass;

        /// <summary>Number of wheel parts.</summary>
        public readonly int WheelCount;

        /// <summary>Number of seat parts.</summary>
        public readonly int SeatCount;

        /// <summary>Total number of parts.</summary>
        public readonly int PartCount;

        public VehicleStats(float totalWeight, float totalCost, Vector3 size,
            Vector3 centerOfMass, int wheelCount, int seatCount, int partCount)
        {
            TotalWeight = totalWeight;
            TotalCost = totalCost;
            Size = size;
            CenterOfMass = centerOfMass;
            WheelCount = wheelCount;
            SeatCount = seatCount;
            PartCount = partCount;
        }

        /// <summary>Width (X extent) in metres.</summary>
        public float Width => Size.x;

        /// <summary>Height (Y extent) in metres.</summary>
        public float Height => Size.y;

        /// <summary>Length (Z extent) in metres.</summary>
        public float Length => Size.z;
    }
}
