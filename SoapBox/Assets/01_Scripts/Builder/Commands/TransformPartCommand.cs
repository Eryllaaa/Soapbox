using UnityEngine;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.Commands
{
    /// <summary>
    /// Moves and/or rotates a part, remembering its previous pose so the change can be
    /// undone. Covers both the "Move" and "Rotate" builder operations.
    /// </summary>
    public sealed class TransformPartCommand : IBuilderCommand
    {
        private readonly VehicleRoot _vehicle;
        private readonly string _instanceId;
        private readonly Vector3 _oldPosition;
        private readonly Quaternion _oldRotation;
        private readonly Vector3 _newPosition;
        private readonly Quaternion _newRotation;

        public TransformPartCommand(VehicleRoot vehicle, PartInstance part, Vector3 newPosition, Quaternion newRotation)
        {
            _vehicle = vehicle;
            _instanceId = part.InstanceId;
            _oldPosition = part.transform.position;
            _oldRotation = part.transform.rotation;
            _newPosition = newPosition;
            _newRotation = newRotation;
        }

        public void Execute() => Apply(_newPosition, _newRotation);

        public void Undo() => Apply(_oldPosition, _oldRotation);

        private void Apply(Vector3 position, Quaternion rotation)
        {
            PartInstance part = _vehicle.FindByInstanceId(_instanceId);
            if (part != null) part.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
