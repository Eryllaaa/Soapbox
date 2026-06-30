using System;
using UnityEngine;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>
    /// Creates and removes part instances and registers them with the vehicle root.
    /// Centralises spawning so the load system and undo/redo commands recreate parts
    /// the same way the placement system does.
    /// </summary>
    public sealed class PartFactory : MonoBehaviour
    {
        [SerializeField] private VehicleRoot _vehicle;

        /// <summary>The vehicle parts are created under.</summary>
        public VehicleRoot Vehicle => _vehicle;

        /// <summary>
        /// Instantiates a part from its data at a world pose, strips any Rigidbody (the
        /// single-Rigidbody invariant), disables gameplay for build mode, assigns an id,
        /// and registers it with the vehicle.
        /// </summary>
        public PartInstance Create(PartData data, Vector3 position, Quaternion rotation, string instanceId = null)
        {
            if (data == null || data.Prefab == null) return null;

            GameObject go = Instantiate(data.Prefab, position, rotation);

            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
                Destroy(rb);
            PartGameplayToggle.SetGameplayEnabled(go, false);

            PartInstance part = go.GetComponent<PartInstance>();
            if (part == null) part = go.AddComponent<PartInstance>();
            part.SetData(data);
            part.AssignInstanceId(string.IsNullOrEmpty(instanceId) ? Guid.NewGuid().ToString("N") : instanceId);

            if (_vehicle != null) _vehicle.RegisterPart(part);
            return part;
        }

        /// <summary>Disconnects every socket on a part, unregisters it, and destroys it.</summary>
        public void Remove(PartInstance part)
        {
            if (part == null) return;

            PartAttachments attachments = part.GetComponent<PartAttachments>();
            if (attachments != null)
            {
                var points = attachments.Points;
                for (int i = 0; i < points.Count; i++)
                    points[i].Disconnect();
            }

            if (_vehicle != null) _vehicle.UnregisterPart(part);
            Destroy(part.gameObject);
        }
    }
}
