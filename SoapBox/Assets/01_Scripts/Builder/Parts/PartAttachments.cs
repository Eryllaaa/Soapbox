using System.Collections.Generic;
using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// Caches the <see cref="AttachmentPoint"/>s belonging to a single part so callers
    /// never run <c>GetComponentsInChildren</c> every frame. Place on the part root,
    /// alongside the <see cref="PartInstance"/>.
    /// </summary>
    public sealed class PartAttachments : MonoBehaviour
    {
        private AttachmentPoint[] _points;

        /// <summary>All attachment points on this part (cached, lazily built).</summary>
        public IReadOnlyList<AttachmentPoint> Points
        {
            get
            {
                if (_points == null) Rebuild();
                return _points;
            }
        }

        private void Awake() => Rebuild();

        /// <summary>
        /// Re-scans child attachment points. Call after adding or removing sockets at
        /// runtime (otherwise the cache from Awake is used).
        /// </summary>
        public void Rebuild() => _points = GetComponentsInChildren<AttachmentPoint>(includeInactive: true);

        /// <summary>Appends this part's currently free sockets to <paramref name="results"/>.</summary>
        public void CollectFree(List<AttachmentPoint> results)
        {
            IReadOnlyList<AttachmentPoint> pts = Points;
            for (int i = 0; i < pts.Count; i++)
            {
                if (!pts[i].IsOccupied) results.Add(pts[i]);
            }
        }
    }
}
