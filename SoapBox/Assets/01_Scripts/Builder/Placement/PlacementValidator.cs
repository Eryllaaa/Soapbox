using UnityEngine;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Placement
{
    /// <summary>
    /// Stateless validation for a candidate placement: whether the ghost is connected
    /// (or allowed to float), and whether it overlaps existing geometry.
    /// </summary>
    public static class PlacementValidator
    {
        /// <summary>
        /// Resolves the validity of the current ghost pose.
        /// </summary>
        /// <param name="preview">The ghost being placed.</param>
        /// <param name="match">The best socket match this frame (may be invalid).</param>
        /// <param name="freePlacementAllowed">True when the part may rest on the ground with no socket (e.g. the first chassis).</param>
        /// <param name="obstacleMask">Layers treated as blocking geometry.</param>
        /// <param name="collisionPadding">Box shrink applied so touching mating faces aren't counted as collisions.</param>
        /// <param name="overlapBuffer">Caller-owned buffer for the non-alloc overlap test.</param>
        public static PlacementResult Validate(
            PartPreview preview,
            in AttachmentMatch match,
            bool freePlacementAllowed,
            LayerMask obstacleMask,
            float collisionPadding,
            Collider[] overlapBuffer)
        {
            if (!match.IsValid && !freePlacementAllowed)
                return new PlacementResult(PlacementValidity.Floating);

            if (HasCollision(preview, in match, obstacleMask, collisionPadding, overlapBuffer))
                return new PlacementResult(PlacementValidity.Collision);

            return new PlacementResult(PlacementValidity.Valid);
        }

        /// <summary>
        /// Overlap test over the ghost's bounds, ignoring the ghost's own colliders and
        /// the colliders of the part it is mating onto (their faces touch by design).
        /// </summary>
        private static bool HasCollision(
            PartPreview preview,
            in AttachmentMatch match,
            LayerMask obstacleMask,
            float collisionPadding,
            Collider[] overlapBuffer)
        {
            Bounds bounds = preview.WorldBounds();
            Vector3 halfExtents = Vector3.Max(bounds.extents - Vector3.one * collisionPadding, Vector3.zero);

            // Renderer bounds are axis-aligned, so the overlap box must be too.
            int count = Physics.OverlapBoxNonAlloc(
                bounds.center, halfExtents, overlapBuffer,
                Quaternion.identity, obstacleMask, QueryTriggerInteraction.Ignore);

            Transform mateRoot = match.IsValid && match.TargetPoint.Owner != null
                ? match.TargetPoint.Owner.transform
                : null;

            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (IsOwnedBy(hit, preview.Colliders)) continue;          // our own ghost
                if (mateRoot != null && hit.transform.IsChildOf(mateRoot)) continue; // the part we snap to
                return true;
            }

            return false;
        }

        private static bool IsOwnedBy(Collider hit, Collider[] own)
        {
            for (int i = 0; i < own.Length; i++)
                if (hit == own[i]) return true;
            return false;
        }
    }
}
