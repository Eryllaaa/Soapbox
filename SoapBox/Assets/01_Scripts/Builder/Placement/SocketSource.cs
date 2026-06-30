using System.Collections.Generic;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Placement
{
    /// <summary>
    /// Supplies the set of free attachment sockets a part can snap onto. Abstracted so
    /// the placement controller does not depend on how sockets are tracked; the vehicle
    /// root implements this without the placement code knowing the details.
    /// </summary>
    public interface ISocketSource
    {
        /// <summary>Fills <paramref name="buffer"/> (cleared first) with the currently free sockets.</summary>
        void CollectFreeSockets(List<AttachmentPoint> buffer);
    }
}
