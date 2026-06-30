using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// The role a category plays in vehicle validation and statistics. Lets the
    /// validator and stats counter recognise chassis/wheels/seats without hardcoding
    /// category names, while keeping categories fully data-driven.
    /// </summary>
    public enum VehicleRole
    {
        /// <summary>Structural, decorative or otherwise uncounted parts.</summary>
        None,
        Chassis,
        Wheel,
        Seat
    }

    /// <summary>
    /// Data-driven definition of a part category (Chassis, Wheel, Seat, ...).
    /// Categories are <see cref="ScriptableObject"/> assets rather than an enum so
    /// new ones can be added in the editor without touching or recompiling code.
    /// </summary>
    [CreateAssetMenu(fileName = "PartCategory", menuName = "Soapbox/Builder/Part Category")]
    public sealed class PartCategory : ScriptableObject
    {
        [Tooltip("Role this category plays in validation/stats (chassis, wheel, seat, or none).")]
        [SerializeField] private VehicleRole _role = VehicleRole.None;
        [Tooltip("Stable identifier persisted in save files. Must be unique and never change once shipped.")]
        [SerializeField] private string _id;

        [Tooltip("Human-readable name shown in the part browser. Falls back to the asset name if empty.")]
        [SerializeField] private string _displayName;

        [Tooltip("Optional icon for category tabs in the UI.")]
        [SerializeField] private Sprite _icon;

        [Tooltip("Accent colour used to group this category in the UI.")]
        [SerializeField] private Color _tint = Color.white;

        /// <summary>Stable identifier persisted in save files.</summary>
        public string Id => _id;

        /// <summary>Role this category plays in validation and statistics.</summary>
        public VehicleRole Role => _role;

        /// <summary>Human-readable name for UI; falls back to the asset name.</summary>
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        /// <summary>Optional UI icon.</summary>
        public Sprite Icon => _icon;

        /// <summary>Accent colour for UI grouping.</summary>
        public Color Tint => _tint;
    }
}
