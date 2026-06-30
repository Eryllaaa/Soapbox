using System.Collections.Generic;
using UnityEngine;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.SaveSystem
{
    /// <summary>
    /// A catalog of all available parts, used by the part browser to list parts and by
    /// the load system to resolve a saved part id back to its <see cref="PartData"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "PartCatalog", menuName = "Soapbox/Builder/Part Catalog")]
    public sealed class PartCatalog : ScriptableObject
    {
        [SerializeField] private List<PartData> _parts = new();

        private Dictionary<string, PartData> _byId;

        /// <summary>All parts in the catalog.</summary>
        public IReadOnlyList<PartData> Parts => _parts;

        /// <summary>Resolves a part by its stable id, or null if not present.</summary>
        public PartData GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_byId == null)
            {
                _byId = new Dictionary<string, PartData>();
                for (int i = 0; i < _parts.Count; i++)
                    if (_parts[i] != null && !string.IsNullOrEmpty(_parts[i].Id))
                        _byId[_parts[i].Id] = _parts[i];
            }

            return _byId.TryGetValue(id, out PartData data) ? data : null;
        }
    }
}
