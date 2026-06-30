using UnityEngine;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.Commands
{
    /// <summary>
    /// Duplicates a part at an offset as a new, free-floating part (the user re-snaps it).
    /// The duplicate's instance id is captured on first execution so undo/redo stay consistent.
    /// </summary>
    public sealed class DuplicatePartCommand : IBuilderCommand
    {
        private readonly PartFactory _factory;
        private readonly VehicleRoot _vehicle;
        private readonly PartData _data;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly Color _paint;
        private string _duplicateId;

        public DuplicatePartCommand(PartFactory factory, VehicleRoot vehicle, PartInstance source, Vector3 offset)
        {
            _factory = factory;
            _vehicle = vehicle;
            _data = source.Data;
            _position = source.transform.position + offset;
            _rotation = source.transform.rotation;
            _paint = source.PaintColor;
        }

        public void Execute()
        {
            PartInstance part = _factory.Create(_data, _position, _rotation, _duplicateId);
            if (part == null) return;

            if (string.IsNullOrEmpty(_duplicateId))
                _duplicateId = part.InstanceId;

            part.SetPaint(_paint);
        }

        public void Undo()
        {
            PartInstance part = _vehicle.FindByInstanceId(_duplicateId);
            if (part != null) _factory.Remove(part);
        }
    }
}
