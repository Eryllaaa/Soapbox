using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// Runtime identity for a single placed part. Lives on the root of every part
    /// prefab and links the spawned <see cref="GameObject"/> back to its
    /// <see cref="PartData"/>.
    ///
    /// Pure identity only — placement, snapping, painting, stats and saving are
    /// handled by dedicated systems that query this component.
    /// </summary>
    public sealed class PartInstance : MonoBehaviour
    {
        [Tooltip("The design-time data this prefab represents.")]
        [SerializeField] private PartData _data;

        /// <summary>Design-time data describing this part.</summary>
        public PartData Data => _data;

        /// <summary>Convenience accessor for the part's category (null-safe).</summary>
        public PartCategory Category => _data != null ? _data.Category : null;

        /// <summary>
        /// Unique id for this specific placed instance within a vehicle, assigned at
        /// spawn or load time. The save system uses it to reference connections.
        /// </summary>
        public string InstanceId { get; private set; }

        /// <summary>
        /// Assigns this instance's runtime id. Intended to be called once by the
        /// builder when the part is spawned or loaded; ignored if already set.
        /// </summary>
        public void AssignInstanceId(string id)
        {
            if (string.IsNullOrEmpty(InstanceId))
                InstanceId = id;
        }

        /// <summary>
        /// Overrides the data reference. Used by the load system when reconstructing
        /// a part from saved data.
        /// </summary>
        public void SetData(PartData data) => _data = data;

        // ---------------------------------------------------------------------
        // Painting
        // ---------------------------------------------------------------------

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color _paintColor = Color.white;
        private MaterialPropertyBlock _propertyBlock;

        /// <summary>The current paint colour applied to this part.</summary>
        public Color PaintColor => _paintColor;

        /// <summary>
        /// Applies a paint colour to every renderer on this part via a
        /// MaterialPropertyBlock, so no material is instanced and the colour survives
        /// temporary material swaps (e.g. selection highlight).
        /// </summary>
        public void SetPaint(Color color)
        {
            _paintColor = color;
            ApplyPaint();
        }

        /// <summary>Re-applies the stored paint colour. Call after a material swap.</summary>
        public void ApplyPaint()
        {
            _propertyBlock ??= new MaterialPropertyBlock();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, _paintColor);
                _propertyBlock.SetColor(ColorId, _paintColor);
                renderers[i].SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
