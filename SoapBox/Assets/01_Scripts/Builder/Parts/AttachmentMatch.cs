using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// Result of matching an incoming part's socket against an existing free socket,
    /// including the world pose the incoming part's root must adopt to mate perfectly.
    /// A default-constructed value has <see cref="IsValid"/> == false (no match).
    /// </summary>
    public readonly struct AttachmentMatch
    {
        /// <summary>True when this represents a real, valid match.</summary>
        public readonly bool IsValid;

        /// <summary>The incoming part's socket that will mate.</summary>
        public readonly AttachmentPoint IncomingPoint;

        /// <summary>The existing free socket to mate onto.</summary>
        public readonly AttachmentPoint TargetPoint;

        /// <summary>World position the incoming part's root must adopt to snap.</summary>
        public readonly Vector3 SnappedPosition;

        /// <summary>World rotation the incoming part's root must adopt to snap.</summary>
        public readonly Quaternion SnappedRotation;

        /// <summary>Squared distance between the two sockets before snapping (used for ranking).</summary>
        public readonly float SqrDistance;

        public AttachmentMatch(
            AttachmentPoint incoming,
            AttachmentPoint target,
            Vector3 snappedPosition,
            Quaternion snappedRotation,
            float sqrDistance)
        {
            IsValid = true;
            IncomingPoint = incoming;
            TargetPoint = target;
            SnappedPosition = snappedPosition;
            SnappedRotation = snappedRotation;
            SqrDistance = sqrDistance;
        }
    }
}
