using System.Collections.Generic;
using UnityEngine;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>
    /// Computes <see cref="VehicleStats"/> from a <see cref="VehicleRoot"/>. Pure and
    /// stateless; intended to be called when the build changes, not every frame.
    /// </summary>
    public static class VehicleStatsCalculator
    {
        /// <summary>Builds a fresh stats snapshot for the given vehicle.</summary>
        public static VehicleStats Compute(VehicleRoot vehicle)
        {
            if (vehicle == null || vehicle.Parts.Count == 0)
                return new VehicleStats(0f, 0f, Vector3.zero, Vector3.zero, 0, 0, 0);

            IReadOnlyList<PartInstance> parts = vehicle.Parts;

            float totalWeight = 0f;
            float totalCost = 0f;
            int wheels = 0;
            int seats = 0;
            Vector3 weightedCenter = Vector3.zero;

            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < parts.Count; i++)
            {
                PartInstance part = parts[i];
                if (part == null) continue;

                PartData data = part.Data;
                float weight = data != null ? data.Weight : 0f;
                totalWeight += weight;
                if (data != null) totalCost += data.Cost;

                VehicleRole role = data != null && data.Category != null ? data.Category.Role : VehicleRole.None;
                if (role == VehicleRole.Wheel) wheels++;
                else if (role == VehicleRole.Seat) seats++;

                Vector3 center = PartCenter(part, out Bounds partBounds, out bool partHasBounds);
                weightedCenter += center * weight;

                if (partHasBounds)
                {
                    if (!hasBounds) { bounds = partBounds; hasBounds = true; }
                    else bounds.Encapsulate(partBounds);
                }
            }

            Vector3 comWorld = totalWeight > 0f ? weightedCenter / totalWeight : vehicle.Root.position;
            Vector3 comLocal = vehicle.Root.InverseTransformPoint(comWorld);
            Vector3 size = hasBounds ? bounds.size : Vector3.zero;

            return new VehicleStats(totalWeight, totalCost, size, comLocal, wheels, seats, parts.Count);
        }

        /// <summary>World centre of a part: its renderer bounds centre, or its transform.</summary>
        private static Vector3 PartCenter(PartInstance part, out Bounds bounds, out bool hasBounds)
        {
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                bounds = new Bounds(part.transform.position, Vector3.zero);
                hasBounds = false;
                return part.transform.position;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            hasBounds = true;
            return bounds.center;
        }
    }
}
