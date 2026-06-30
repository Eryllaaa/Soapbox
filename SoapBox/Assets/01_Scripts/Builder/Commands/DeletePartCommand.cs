using System.Collections.Generic;
using UnityEngine;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.Commands
{
    /// <summary>
    /// Deletes a part, capturing enough state (data, pose, paint, connections) to fully
    /// restore it on undo, including reconnecting it to its former neighbours.
    /// </summary>
    public sealed class DeletePartCommand : IBuilderCommand
    {
        private struct Connection
        {
            public int LocalSocket;
            public string OtherInstanceId;
            public int OtherSocket;
        }

        private readonly PartFactory _factory;
        private readonly VehicleRoot _vehicle;
        private readonly PartData _data;
        private readonly string _instanceId;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly Color _paint;
        private readonly List<Connection> _connections = new();

        public DeletePartCommand(PartFactory factory, VehicleRoot vehicle, PartInstance part)
        {
            _factory = factory;
            _vehicle = vehicle;
            _data = part.Data;
            _instanceId = part.InstanceId;
            _position = part.transform.position;
            _rotation = part.transform.rotation;
            _paint = part.PaintColor;

            PartAttachments attachments = part.GetComponent<PartAttachments>();
            if (attachments == null) return;

            var points = attachments.Points;
            for (int i = 0; i < points.Count; i++)
            {
                if (!points[i].IsOccupied) continue;

                AttachmentPoint other = points[i].ConnectedTo;
                PartInstance otherOwner = other.Owner;
                _connections.Add(new Connection
                {
                    LocalSocket = i,
                    OtherInstanceId = otherOwner != null ? otherOwner.InstanceId : null,
                    OtherSocket = PartConnectionUtil.IndexOfSocket(otherOwner, other)
                });
            }
        }

        public void Execute()
        {
            PartInstance part = _vehicle.FindByInstanceId(_instanceId);
            if (part != null) _factory.Remove(part);
        }

        public void Undo()
        {
            PartInstance part = _factory.Create(_data, _position, _rotation, _instanceId);
            if (part == null) return;

            part.SetPaint(_paint);

            for (int i = 0; i < _connections.Count; i++)
            {
                Connection c = _connections[i];
                PartInstance other = _vehicle.FindByInstanceId(c.OtherInstanceId);
                if (other != null)
                    PartConnectionUtil.ConnectByIndex(part, c.LocalSocket, other, c.OtherSocket);
            }
        }
    }
}
