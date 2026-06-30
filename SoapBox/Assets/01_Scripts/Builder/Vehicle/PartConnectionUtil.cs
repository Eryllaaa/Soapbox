using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>
    /// Helpers for referencing and re-establishing socket connections by index, used by
    /// the save/load and undo/redo systems where live socket references aren't stable.
    /// Socket indices are positions within a part's <see cref="PartAttachments.Points"/>.
    /// </summary>
    public static class PartConnectionUtil
    {
        /// <summary>Index of a socket within its part, or -1 if not found.</summary>
        public static int IndexOfSocket(PartInstance part, AttachmentPoint socket)
        {
            if (part == null || socket == null) return -1;

            PartAttachments attachments = part.GetComponent<PartAttachments>();
            if (attachments == null) return -1;

            var points = attachments.Points;
            for (int i = 0; i < points.Count; i++)
                if (points[i] == socket) return i;

            return -1;
        }

        /// <summary>The socket at the given index on a part, or null.</summary>
        public static AttachmentPoint GetSocket(PartInstance part, int index)
        {
            if (part == null || index < 0) return null;

            PartAttachments attachments = part.GetComponent<PartAttachments>();
            if (attachments == null) return null;

            var points = attachments.Points;
            return index < points.Count ? points[index] : null;
        }

        /// <summary>Connects two sockets identified by their part + index.</summary>
        public static void ConnectByIndex(PartInstance a, int socketA, PartInstance b, int socketB)
        {
            AttachmentPoint pa = GetSocket(a, socketA);
            AttachmentPoint pb = GetSocket(b, socketB);
            if (pa != null && pb != null) pa.Connect(pb);
        }
    }
}
