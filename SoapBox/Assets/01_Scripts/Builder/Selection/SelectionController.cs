using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Placement;

namespace Soapbox.Builder.Selection
{
    /// <summary>
    /// Click-to-select existing parts. Raycasts on the Select action (ignored while the
    /// placement system is active so a click that places a part doesn't also select one),
    /// highlights the selection by swapping to a highlight material, and exposes the
    /// current selection for the move/rotate/duplicate/delete and paint systems.
    /// </summary>
    public sealed class SelectionController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private PlacementController _placement;
        [SerializeField] private InputActionReference _selectAction;
        [SerializeField] private InputActionReference _pointAction;
        [SerializeField] private Material _highlightMaterial;
        [SerializeField] private LayerMask _selectableMask = ~0;

        /// <summary>The currently selected part, or null.</summary>
        public PartInstance Selected { get; private set; }

        /// <summary>Raised when the selection changes (argument may be null).</summary>
        public event Action<PartInstance> SelectionChanged;

        private Renderer[] _highlighted;
        private Material[] _savedMaterials;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            if (_pointAction?.action != null) _pointAction.action.Enable();
            if (_selectAction?.action != null)
            {
                _selectAction.action.performed += OnSelect;
                _selectAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_selectAction?.action != null) _selectAction.action.performed -= OnSelect;
        }

        private void OnSelect(InputAction.CallbackContext _)
        {
            if (_placement != null && _placement.IsPlacing) return;

            Vector2 screen = _pointAction?.action != null ? _pointAction.action.ReadValue<Vector2>() : Vector2.zero;
            Ray ray = _camera.ScreenPointToRay(screen);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _selectableMask, QueryTriggerInteraction.Ignore))
                Select(hit.collider.GetComponentInParent<PartInstance>());
            else
                Select(null);
        }

        /// <summary>Selects a part (or clears selection when null).</summary>
        public void Select(PartInstance part)
        {
            if (part == Selected) return;

            ClearHighlight();
            Selected = part;
            ApplyHighlight();
            SelectionChanged?.Invoke(Selected);
        }

        /// <summary>Clears the current selection.</summary>
        public void Deselect() => Select(null);

        private void ApplyHighlight()
        {
            if (Selected == null || _highlightMaterial == null) return;

            _highlighted = Selected.GetComponentsInChildren<Renderer>(includeInactive: true);
            _savedMaterials = new Material[_highlighted.Length];
            for (int i = 0; i < _highlighted.Length; i++)
            {
                _savedMaterials[i] = _highlighted[i].sharedMaterial;
                _highlighted[i].sharedMaterial = _highlightMaterial;
            }
        }

        private void ClearHighlight()
        {
            if (_highlighted == null) return;

            for (int i = 0; i < _highlighted.Length; i++)
                if (_highlighted[i] != null) _highlighted[i].sharedMaterial = _savedMaterials[i];

            _highlighted = null;
            _savedMaterials = null;
        }
    }
}
