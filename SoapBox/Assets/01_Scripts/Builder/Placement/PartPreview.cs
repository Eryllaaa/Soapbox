using UnityEngine;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.Placement
{
    /// <summary>
    /// A translucent ghost of a part shown while placing it. Owns the spawned
    /// preview GameObject and its visual valid/invalid state. Gameplay behaviours
    /// and the Rigidbody are removed so the ghost never participates in physics;
    /// attachment sockets are kept so the placement solver can query them.
    /// </summary>
    public sealed class PartPreview
    {
        private readonly GameObject _instance;
        private readonly Renderer[] _renderers;
        private readonly Material[] _originalMaterials;
        private readonly Material _validMaterial;
        private readonly Material _invalidMaterial;
        private bool _released;

        /// <summary>The ghost's transform.</summary>
        public Transform Transform => _instance.transform;

        /// <summary>The part data this ghost represents.</summary>
        public PartData Data { get; }

        /// <summary>The ghost's cached attachment sockets.</summary>
        public PartAttachments Attachments { get; }

        /// <summary>Colliders on the ghost, used so collision tests can ignore itself.</summary>
        public Collider[] Colliders { get; }

        /// <summary>
        /// Spawns a ghost for <paramref name="data"/>. The prefab is instantiated under a
        /// temporary inactive parent so no gameplay Awake runs before stripping, then the
        /// Rigidbody is removed and driving behaviours disabled.
        /// </summary>
        public PartPreview(PartData data, Material validMaterial, Material invalidMaterial)
        {
            Data = data;
            _validMaterial = validMaterial;
            _invalidMaterial = invalidMaterial;

            // Instantiate inactive so Wheel/Suspension Awake doesn't fire before we strip.
            var holder = new GameObject("PreviewHolder");
            holder.SetActive(false);
            _instance = Object.Instantiate(data.Prefab, holder.transform);

            // Remove physics participation.
            foreach (Rigidbody rb in _instance.GetComponentsInChildren<Rigidbody>(true))
                Object.Destroy(rb);
            PartGameplayToggle.SetGameplayEnabled(_instance, false);

            // Move out of the holder and activate.
            _instance.transform.SetParent(null, worldPositionStays: false);
            Object.Destroy(holder);
            _instance.SetActive(true);

            Attachments = _instance.GetComponent<PartAttachments>();
            _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            Colliders = _instance.GetComponentsInChildren<Collider>(true);

            // Remember the real materials so a committed ghost can become the placed part.
            _originalMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalMaterials[i] = _renderers[i].sharedMaterial;
        }

        /// <summary>Places the ghost at the given world pose.</summary>
        public void SetPose(Vector3 position, Quaternion rotation)
            => _instance.transform.SetPositionAndRotation(position, rotation);

        /// <summary>Tints the ghost green (valid) or red (invalid).</summary>
        public void SetValid(bool valid)
        {
            Material mat = valid ? _validMaterial : _invalidMaterial;
            if (mat == null) return;

            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].sharedMaterial = mat;
        }

        /// <summary>World-space bounds of the ghost's renderers (empty if none).</summary>
        public Bounds WorldBounds()
        {
            if (_renderers.Length == 0)
                return new Bounds(_instance.transform.position, Vector3.zero);

            Bounds b = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Length; i++)
                b.Encapsulate(_renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Promotes the ghost into the final placed part: restores its real materials and
        /// releases ownership so <see cref="Dispose"/> will no longer destroy it. The
        /// caller takes responsibility for the returned GameObject. Gameplay behaviours
        /// remain disabled until the assembler enables them at "Test".
        /// </summary>
        public GameObject Commit()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].sharedMaterial = _originalMaterials[i];

            _released = true;
            return _instance;
        }

        /// <summary>Destroys the ghost GameObject, unless it has been committed.</summary>
        public void Dispose()
        {
            if (!_released && _instance != null)
                Object.Destroy(_instance);
        }
    }
}
