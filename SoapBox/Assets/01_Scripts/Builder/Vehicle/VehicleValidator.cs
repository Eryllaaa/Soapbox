using System.Collections.Generic;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>
    /// Validates whether a build is drivable: enough chassis/wheels/seats and no
    /// disconnected (floating) parts. Stateless.
    /// </summary>
    public static class VehicleValidator
    {
        /// <summary>
        /// Validates <paramref name="vehicle"/> against the test requirements.
        /// </summary>
        public static ValidationResult Validate(
            VehicleRoot vehicle, int minWheels = 4, bool requireChassis = true, bool requireSeat = true)
        {
            var errors = new List<string>();

            if (vehicle == null || vehicle.Parts.Count == 0)
            {
                errors.Add("The vehicle has no parts.");
                return new ValidationResult(errors);
            }

            int chassis = 0, wheels = 0, seats = 0;
            CountRoles(vehicle, ref chassis, ref wheels, ref seats);

            if (requireChassis && chassis < 1)
                errors.Add("At least one chassis is required.");

            if (wheels < minWheels)
                errors.Add($"At least {minWheels} wheels are required (found {wheels}).");

            if (requireSeat && seats < 1)
                errors.Add("At least one seat is required.");

            if (vehicle.Parts.Count > 1 && !IsFullyConnected(vehicle))
                errors.Add("Some parts are disconnected or floating.");

            return new ValidationResult(errors);
        }

        private static void CountRoles(VehicleRoot vehicle, ref int chassis, ref int wheels, ref int seats)
        {
            IReadOnlyList<PartInstance> parts = vehicle.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                PartCategory category = parts[i] != null ? parts[i].Category : null;
                if (category == null) continue;

                switch (category.Role)
                {
                    case VehicleRole.Chassis: chassis++; break;
                    case VehicleRole.Wheel: wheels++; break;
                    case VehicleRole.Seat: seats++; break;
                }
            }
        }

        /// <summary>Breadth-first traversal over socket connections to detect floating parts.</summary>
        private static bool IsFullyConnected(VehicleRoot vehicle)
        {
            IReadOnlyList<PartInstance> parts = vehicle.Parts;
            var visited = new HashSet<PartInstance>();
            var queue = new Queue<PartInstance>();

            queue.Enqueue(parts[0]);
            visited.Add(parts[0]);

            while (queue.Count > 0)
            {
                PartInstance current = queue.Dequeue();
                PartAttachments attachments = current.GetComponent<PartAttachments>();
                if (attachments == null) continue;

                IReadOnlyList<AttachmentPoint> points = attachments.Points;
                for (int i = 0; i < points.Count; i++)
                {
                    if (!points[i].IsOccupied) continue;

                    PartInstance neighbor = points[i].ConnectedTo.Owner;
                    if (neighbor != null && visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return visited.Count == parts.Count;
        }
    }
}
