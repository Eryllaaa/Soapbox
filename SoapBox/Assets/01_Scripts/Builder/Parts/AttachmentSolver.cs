using System.Collections.Generic;
using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// Pure, stateless geometry and selection helpers for the attachment system:
    /// computing the snapped pose for a mating part and choosing the best socket
    /// match as a part is moved near a vehicle. No allocations, no per-frame state.
    /// </summary>
    public static class AttachmentSolver
    {
        // Sockets mate face-to-face: the incoming socket sits on the target, rotated
        // 180° about the socket up so the two outward directions oppose.
        private static readonly Quaternion FaceToFace = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>
        /// Computes the world position/rotation the incoming part's <paramref name="root"/>
        /// must adopt so that <paramref name="incoming"/> mates perfectly onto
        /// <paramref name="target"/> (coincident position, opposing forward, up aligned).
        /// Solved directly from the socket's fixed local pose. Assumes unit scale on the part.
        /// </summary>
        public static void ComputeSnappedPose(
            Transform root, AttachmentPoint incoming, AttachmentPoint target,
            out Vector3 position, out Quaternion rotation)
        {
            // Desired world pose for the incoming socket.
            Quaternion desired = target.transform.rotation * FaceToFace;

            // Incoming socket expressed in the root's local space (constant for the rigid part).
            Quaternion localRot = Quaternion.Inverse(root.rotation) * incoming.transform.rotation;
            Vector3 localPos = root.InverseTransformPoint(incoming.transform.position);

            // root' such that: root'.rotation * localRot == desired, and the socket lands on target.
            rotation = desired * Quaternion.Inverse(localRot);
            position = target.transform.position - (rotation * localPos);
        }

        /// <summary>
        /// Finds the closest valid match between the incoming part's free sockets and the
        /// supplied candidate sockets, within <paramref name="snapRadius"/>. Sockets owned
        /// by <paramref name="root"/> and occupied sockets are skipped, and a match is only
        /// valid when both sockets accept the other part's category.
        /// Returns an invalid <see cref="AttachmentMatch"/> (IsValid == false) if nothing qualifies.
        /// </summary>
        public static AttachmentMatch FindBestMatch(
            PartAttachments incoming, Transform root,
            IReadOnlyList<AttachmentPoint> candidates, float snapRadius)
        {
            AttachmentMatch best = default; // IsValid == false
            if (incoming == null || root == null || candidates == null) return best;

            float bestSqr = snapRadius * snapRadius;
            IReadOnlyList<AttachmentPoint> incomingPoints = incoming.Points;

            PartInstance incomingPart = incoming.GetComponent<PartInstance>();
            PartCategory incomingCategory = incomingPart != null ? incomingPart.Category : null;

            for (int c = 0; c < candidates.Count; c++)
            {
                AttachmentPoint target = candidates[c];
                if (target == null || target.IsOccupied) continue;
                if (target.transform.IsChildOf(root)) continue;          // never snap to our own sockets
                if (!target.Accepts(incomingCategory)) continue;

                PartCategory targetCategory = target.Owner != null ? target.Owner.Category : null;

                for (int i = 0; i < incomingPoints.Count; i++)
                {
                    AttachmentPoint inc = incomingPoints[i];
                    if (inc.IsOccupied || !inc.Accepts(targetCategory)) continue;

                    float sqr = (inc.transform.position - target.transform.position).sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    ComputeSnappedPose(root, inc, target, out Vector3 pos, out Quaternion rot);
                    best = new AttachmentMatch(inc, target, pos, rot, sqr);
                    bestSqr = sqr;
                }
            }

            return best;
        }
    }
}
