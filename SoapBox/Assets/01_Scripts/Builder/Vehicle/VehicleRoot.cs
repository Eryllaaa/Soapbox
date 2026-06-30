using System;
using System.Collections.Generic;
using UnityEngine;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Placement;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>
    /// The single root every placed part belongs to. Owns the authoritative list of
    /// parts, keeps the hierarchy clean by reparenting parts under itself, and acts as
    /// the <see cref="ISocketSource"/> for the placement system. Holds no driving
    /// behaviour — assembling for "Test" is the assembler's job.
    /// </summary>
    public sealed class VehicleRoot : MonoBehaviour, ISocketSource
    {
        [Tooltip("Display name used when saving this vehicle.")]
        [SerializeField] private string _vehicleName = "New Vehicle";

        private readonly List<PartInstance> _parts = new();

        /// <summary>Display name of the current build.</summary>
        public string VehicleName
        {
            get => _vehicleName;
            set => _vehicleName = value;
        }

        /// <summary>The transform parts live under (this object).</summary>
        public Transform Root => transform;

        /// <summary>All parts currently in the build.</summary>
        public IReadOnlyList<PartInstance> Parts => _parts;

        /// <summary>Raised whenever a part is added or removed.</summary>
        public event Action Changed;

        /// <summary>Adds a part to the build, parenting it under the root.</summary>
        public void RegisterPart(PartInstance part)
        {
            if (part == null || _parts.Contains(part)) return;

            _parts.Add(part);
            part.transform.SetParent(transform, worldPositionStays: true);
            Changed?.Invoke();
        }

        /// <summary>Removes a part from the build (does not destroy it).</summary>
        public void UnregisterPart(PartInstance part)
        {
            if (_parts.Remove(part))
                Changed?.Invoke();
        }

        /// <summary>Finds a registered part by its runtime instance id, or null.</summary>
        public PartInstance FindByInstanceId(string instanceId)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i] != null && _parts[i].InstanceId == instanceId)
                    return _parts[i];
            }
            return null;
        }

        /// <inheritdoc />
        public void CollectFreeSockets(List<AttachmentPoint> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i] == null) continue;

                PartAttachments attachments = _parts[i].GetComponent<PartAttachments>();
                if (attachments != null) attachments.CollectFree(buffer);
            }
        }
    }
}
