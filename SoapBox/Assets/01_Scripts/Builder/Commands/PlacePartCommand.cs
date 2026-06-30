using UnityEngine;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Placement;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.Commands
{
    /// <summary>
    /// Undoable record of a placed part. The placement system performs the placement, so
    /// this is created already-executed (use <c>CommandHistory.Record</c>): Undo removes
    /// the part, Redo recreates it (with its connection) from the captured descriptor.
    /// Parts are referenced by instance id so the command survives undo/redo recreation.
    /// </summary>
    public sealed class PlacePartCommand : IBuilderCommand
    {
        private readonly PartFactory _factory;
        private readonly VehicleRoot _vehicle;
        private readonly PartData _data;
        private readonly string _instanceId;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly Color _paint;

        private readonly bool _hasConnection;
        private readonly int _localSocket;
        private readonly string _targetInstanceId;
        private readonly int _targetSocket;

        public PlacePartCommand(PartFactory factory, VehicleRoot vehicle, PlacementCommit commit)
        {
            _factory = factory;
            _vehicle = vehicle;

            PartInstance part = commit.Part;
            _data = part.Data;
            _instanceId = part.InstanceId;
            _position = part.transform.position;
            _rotation = part.transform.rotation;
            _paint = part.PaintColor;

            if (commit.Incoming != null && commit.Target != null && commit.Target.Owner != null)
            {
                _hasConnection = true;
                _localSocket = PartConnectionUtil.IndexOfSocket(part, commit.Incoming);
                _targetInstanceId = commit.Target.Owner.InstanceId;
                _targetSocket = PartConnectionUtil.IndexOfSocket(commit.Target.Owner, commit.Target);
            }
        }

        public void Execute()
        {
            PartInstance part = _factory.Create(_data, _position, _rotation, _instanceId);
            if (part == null) return;

            part.SetPaint(_paint);

            if (_hasConnection)
            {
                PartInstance target = _vehicle.FindByInstanceId(_targetInstanceId);
                if (target != null)
                    PartConnectionUtil.ConnectByIndex(part, _localSocket, target, _targetSocket);
            }
        }

        public void Undo()
        {
            PartInstance part = _vehicle.FindByInstanceId(_instanceId);
            if (part != null) _factory.Remove(part);
        }
    }
}
