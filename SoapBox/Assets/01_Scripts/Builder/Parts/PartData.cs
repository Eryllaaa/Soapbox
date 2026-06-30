using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// Design-time description of a buildable part. Every buildable object
    /// references exactly one <see cref="PartData"/> asset; gameplay values are
    /// authored here and never hardcoded elsewhere.
    /// </summary>
    [CreateAssetMenu(fileName = "PartData", menuName = "Soapbox/Builder/Part Data")]
    public sealed class PartData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique id used in save files. Never change once shipped.")]
        [SerializeField] private string _id;

        [Tooltip("Name shown in the part browser. Falls back to the asset name if empty.")]
        [SerializeField] private string _displayName;

        [SerializeField, TextArea] private string _description;

        [Tooltip("Which category this part belongs to.")]
        [SerializeField] private PartCategory _category;

        [Header("Economy / Physics")]
        [Tooltip("Build cost, summed into the vehicle's total cost stat.")]
        [SerializeField, Min(0f)] private float _cost;

        [Tooltip("Mass contribution in kilograms. Summed into the Rigidbody mass and the centre-of-mass calculation.")]
        [SerializeField, Min(0f)] private float _weight = 1f;

        [Tooltip("Approximate bounding size in metres, used for stats and browser sizing.")]
        [SerializeField] private Vector3 _size = Vector3.one;

        [Header("References")]
        [Tooltip("Prefab spawned when this part is placed. Its root must carry a PartInstance.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Thumbnail shown in the part browser.")]
        [SerializeField] private Sprite _thumbnail;

        /// <summary>Stable id persisted in save files.</summary>
        public string Id => _id;

        /// <summary>Human-readable name for UI; falls back to the asset name.</summary>
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        /// <summary>Free-text description for the part browser.</summary>
        public string Description => _description;

        /// <summary>The category this part belongs to.</summary>
        public PartCategory Category => _category;

        /// <summary>Build cost.</summary>
        public float Cost => _cost;

        /// <summary>Mass contribution in kilograms.</summary>
        public float Weight => _weight;

        /// <summary>Approximate bounding size in metres.</summary>
        public Vector3 Size => _size;

        /// <summary>Prefab to instantiate when this part is placed.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Browser thumbnail.</summary>
        public Sprite Thumbnail => _thumbnail;
    }
}
