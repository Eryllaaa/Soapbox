using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Placement
{
    /// <summary>Details of a committed placement, passed to listeners (e.g. undo/redo).</summary>
    public readonly struct PlacementCommit
    {
        /// <summary>The part that was placed.</summary>
        public readonly PartInstance Part;

        /// <summary>The placed part's socket that connected, or null for free placement.</summary>
        public readonly AttachmentPoint Incoming;

        /// <summary>The existing socket connected to, or null for free placement.</summary>
        public readonly AttachmentPoint Target;

        public PlacementCommit(PartInstance part, AttachmentPoint incoming, AttachmentPoint target)
        {
            Part = part;
            Incoming = incoming;
            Target = target;
        }
    }

    /// <summary>Why a candidate placement is valid or not.</summary>
    public enum PlacementValidity
    {
        /// <summary>The part can be placed here.</summary>
        Valid,

        /// <summary>No compatible socket in range and free placement is not allowed.</summary>
        Floating,

        /// <summary>The part would overlap existing geometry.</summary>
        Collision
    }

    /// <summary>Outcome of validating a candidate placement for the current frame.</summary>
    public readonly struct PlacementResult
    {
        /// <summary>The reason code for this result.</summary>
        public readonly PlacementValidity Validity;

        /// <summary>Convenience: true only when <see cref="Validity"/> is <see cref="PlacementValidity.Valid"/>.</summary>
        public bool IsValid => Validity == PlacementValidity.Valid;

        public PlacementResult(PlacementValidity validity) => Validity = validity;
    }
}
