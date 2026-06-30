using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// A single connection socket on a part. The component's Transform defines the
    /// socket's position and orientation, where <c>forward</c> is the outward mating
    /// direction. Authoring a socket is simply placing a child GameObject with this
    /// component; dozens per part are supported.
    /// </summary>
    public sealed class AttachmentPoint : MonoBehaviour
    {
        [Tooltip("Categories this socket will accept mating with. Empty = accepts any category.")]
        [SerializeField] private List<PartCategory> _compatibleCategories = new();

        [Tooltip("Gizmo radius used when authoring sockets in the editor.")]
        [SerializeField, Min(0f)] private float _gizmoSize = 0.05f;

        /// <summary>The part this socket belongs to (cached at Awake).</summary>
        public PartInstance Owner { get; private set; }

        /// <summary>The socket this one is currently connected to, or null when free.</summary>
        public AttachmentPoint ConnectedTo { get; private set; }

        /// <summary>True while this socket is connected to another.</summary>
        public bool IsOccupied => ConnectedTo != null;

        /// <summary>
        /// Raised when this socket connects or disconnects. The argument is the other
        /// socket on connect, or null on disconnect.
        /// </summary>
        public event Action<AttachmentPoint> ConnectionChanged;

        private void Awake() => Owner = GetComponentInParent<PartInstance>();

        /// <summary>
        /// Whether a part of the given category may mate with this socket. An empty
        /// compatibility list means the socket accepts any category.
        /// </summary>
        public bool Accepts(PartCategory category)
        {
            if (_compatibleCategories == null || _compatibleCategories.Count == 0) return true;
            return category != null && _compatibleCategories.Contains(category);
        }

        /// <summary>
        /// Records a logical connection between this socket and <paramref name="other"/>,
        /// updating both sides and raising <see cref="ConnectionChanged"/> on each.
        /// Physical reparenting under the vehicle root is handled separately to keep
        /// the hierarchy clean. No-op if either socket is already occupied.
        /// </summary>
        public void Connect(AttachmentPoint other)
        {
            if (other == null || other == this) return;
            if (IsOccupied || other.IsOccupied) return;

            ConnectedTo = other;
            other.ConnectedTo = this;

            ConnectionChanged?.Invoke(other);
            other.ConnectionChanged?.Invoke(this);
        }

        /// <summary>Breaks this socket's connection (and the mating socket's), if any.</summary>
        public void Disconnect()
        {
            if (!IsOccupied) return;

            AttachmentPoint other = ConnectedTo;
            ConnectedTo = null;
            if (other != null) other.ConnectedTo = null;

            ConnectionChanged?.Invoke(null);
            other?.ConnectionChanged?.Invoke(null);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Application.isPlaying && IsOccupied ? Color.grey : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _gizmoSize);

            // Outward mating direction.
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * _gizmoSize * 3f);
        }
    }
}
