using System;
using System.Collections.Generic;
using UnityEngine;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.SaveSystem
{
    /// <summary>
    /// Converts a live <see cref="VehicleRoot"/> to/from <see cref="VehicleSaveData"/>.
    /// Part transforms are stored in the vehicle root's local space so a saved vehicle can
    /// be loaded at any position. Connections are de-duplicated by ordering instance ids.
    /// </summary>
    public static class VehicleSerializer
    {
        /// <summary>Captures the current build into serializable data.</summary>
        public static VehicleSaveData Capture(VehicleRoot vehicle)
        {
            var data = new VehicleSaveData
            {
                vehicleName = vehicle.VehicleName,
                dateIso = DateTime.UtcNow.ToString("o")
            };

            Transform root = vehicle.Root;
            Quaternion invRoot = Quaternion.Inverse(root.rotation);

            IReadOnlyList<PartInstance> parts = vehicle.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                PartInstance part = parts[i];
                if (part == null || part.Data == null) continue;

                data.parts.Add(new PartSaveData
                {
                    partId = part.Data.Id,
                    instanceId = part.InstanceId,
                    position = root.InverseTransformPoint(part.transform.position),
                    rotation = invRoot * part.transform.rotation,
                    paint = part.PaintColor
                });

                CaptureConnections(part, data.connections);
            }

            return data;
        }

        private static void CaptureConnections(PartInstance part, List<ConnectionSaveData> into)
        {
            PartAttachments attachments = part.GetComponent<PartAttachments>();
            if (attachments == null) return;

            var points = attachments.Points;
            for (int i = 0; i < points.Count; i++)
            {
                if (!points[i].IsOccupied) continue;

                PartInstance other = points[i].ConnectedTo.Owner;
                if (other == null) continue;

                // Record each connection once: only from the part with the smaller id.
                if (string.CompareOrdinal(part.InstanceId, other.InstanceId) >= 0) continue;

                into.Add(new ConnectionSaveData
                {
                    aInstanceId = part.InstanceId,
                    aSocket = i,
                    bInstanceId = other.InstanceId,
                    bSocket = PartConnectionUtil.IndexOfSocket(other, points[i].ConnectedTo)
                });
            }
        }

        /// <summary>
        /// Rebuilds a vehicle from saved data. The caller is expected to have cleared the
        /// existing build first; parts are created through <paramref name="factory"/> and
        /// resolved against <paramref name="catalog"/>.
        /// </summary>
        public static void Restore(VehicleSaveData data, VehicleRoot vehicle, PartFactory factory, PartCatalog catalog)
        {
            if (data == null) return;

            vehicle.VehicleName = data.vehicleName;
            Transform root = vehicle.Root;

            for (int i = 0; i < data.parts.Count; i++)
            {
                PartSaveData ps = data.parts[i];
                PartData partData = catalog.GetById(ps.partId);
                if (partData == null)
                {
                    Debug.LogWarning($"[VehicleSerializer] Unknown part id '{ps.partId}' skipped during load.");
                    continue;
                }

                Vector3 worldPos = root.TransformPoint(ps.position);
                Quaternion worldRot = root.rotation * ps.rotation;

                PartInstance part = factory.Create(partData, worldPos, worldRot, ps.instanceId);
                part?.SetPaint(ps.paint);
            }

            for (int i = 0; i < data.connections.Count; i++)
            {
                ConnectionSaveData c = data.connections[i];
                PartInstance a = vehicle.FindByInstanceId(c.aInstanceId);
                PartInstance b = vehicle.FindByInstanceId(c.bInstanceId);
                if (a != null && b != null)
                    PartConnectionUtil.ConnectByIndex(a, c.aSocket, b, c.bSocket);
            }
        }
    }
}
