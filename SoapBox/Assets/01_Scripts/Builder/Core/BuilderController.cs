using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Soapbox.Builder.Commands;
using Soapbox.Builder.Painting;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Placement;
using Soapbox.Builder.SaveSystem;
using Soapbox.Builder.Selection;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.Core
{
    /// <summary>
    /// Thin coordinator that ties the builder subsystems together and exposes the
    /// high-level operations the UI and input call: place, delete, duplicate, undo, redo,
    /// paint, save, load and test. Delegates all real work to the subsystems; holds the
    /// undo/redo history.
    /// </summary>
    public sealed class BuilderController : MonoBehaviour
    {
        [Header("Subsystems")]
        [SerializeField] private VehicleRoot _vehicle;
        [SerializeField] private PartFactory _factory;
        [SerializeField] private PlacementController _placement;
        [SerializeField] private SelectionController _selection;
        [SerializeField] private PaintController _paint;
        [SerializeField] private VehicleAssembler _assembler;
        [SerializeField] private VehicleStatsTracker _stats;
        [SerializeField] private PartCatalog _catalog;

        [Tooltip("Behaviours disabled while test-driving (placement, selection, builder camera, paint, etc.).")]
        [SerializeField] private MonoBehaviour[] _buildModeBehaviours;

        [Header("Input (editing)")]
        [SerializeField] private InputActionReference _deleteAction;
        [SerializeField] private InputActionReference _duplicateAction;
        [SerializeField] private InputActionReference _undoAction;
        [SerializeField] private InputActionReference _redoAction;

        [SerializeField] private float _duplicateOffset = 0.5f;

        private readonly CommandHistory _history = new();

        /// <summary>Undo/redo history (for binding UI button interactability).</summary>
        public CommandHistory History => _history;

        /// <summary>True once the vehicle has been assembled for a test drive.</summary>
        public bool IsTesting { get; private set; }

        private void OnEnable()
        {
            if (_placement != null) _placement.PartPlaced += OnPartPlaced;
            _history.Changed += OnHistoryChanged;

            Subscribe(_deleteAction, OnDeleteInput);
            Subscribe(_duplicateAction, OnDuplicateInput);
            Subscribe(_undoAction, OnUndoInput);
            Subscribe(_redoAction, OnRedoInput);
        }

        private void OnDisable()
        {
            if (_placement != null) _placement.PartPlaced -= OnPartPlaced;
            _history.Changed -= OnHistoryChanged;

            Unsubscribe(_deleteAction, OnDeleteInput);
            Unsubscribe(_duplicateAction, OnDuplicateInput);
            Unsubscribe(_undoAction, OnUndoInput);
            Unsubscribe(_redoAction, OnRedoInput);
        }

        // ---------------------------------------------------------------------
        // High-level operations (also callable from UI buttons)
        // ---------------------------------------------------------------------

        /// <summary>Starts placing a new part of the given type.</summary>
        public void BeginPlace(PartData data)
        {
            if (!IsTesting) _placement.BeginPlacement(data);
        }

        /// <summary>Deletes the selected part (undoable).</summary>
        public void DeleteSelected()
        {
            PartInstance selected = _selection != null ? _selection.Selected : null;
            if (selected == null || IsTesting) return;

            _selection.Deselect();
            _history.Execute(new DeletePartCommand(_factory, _vehicle, selected));
        }

        /// <summary>Duplicates the selected part (undoable).</summary>
        public void DuplicateSelected()
        {
            PartInstance selected = _selection != null ? _selection.Selected : null;
            if (selected == null || IsTesting) return;

            _history.Execute(new DuplicatePartCommand(_factory, _vehicle, selected, Vector3.up * _duplicateOffset));
        }

        /// <summary>Rotates the selected part by an angle about the world up axis (undoable).</summary>
        public void RotateSelected(float degrees)
        {
            PartInstance selected = _selection != null ? _selection.Selected : null;
            if (selected == null || IsTesting) return;

            Quaternion newRot = Quaternion.AngleAxis(degrees, Vector3.up) * selected.transform.rotation;
            _history.Execute(new TransformPartCommand(_vehicle, selected, selected.transform.position, newRot));
        }

        /// <summary>Moves the selected part by a world-space delta (undoable).</summary>
        public void MoveSelected(Vector3 delta)
        {
            PartInstance selected = _selection != null ? _selection.Selected : null;
            if (selected == null || IsTesting) return;

            Vector3 newPos = selected.transform.position + delta;
            _history.Execute(new TransformPartCommand(_vehicle, selected, newPos, selected.transform.rotation));
        }

        /// <summary>Undoes the last action.</summary>
        public void Undo() => _history.Undo();

        /// <summary>Redoes the last undone action.</summary>
        public void Redo() => _history.Redo();

        /// <summary>Paints the selected part with the given colour.</summary>
        public void PaintSelected(Color color)
        {
            if (_paint != null) _paint.PaintSelected(color);
        }

        /// <summary>Saves the current build under the given name.</summary>
        public void Save(string vehicleName)
        {
            _vehicle.VehicleName = vehicleName;
            VehicleSaveData data = VehicleSerializer.Capture(_vehicle);
            VehicleSaveSystem.Save(data, vehicleName);
        }

        /// <summary>Clears the build and loads the saved vehicle with the given name.</summary>
        public void Load(string vehicleName)
        {
            VehicleSaveData data = VehicleSaveSystem.Load(vehicleName);
            if (data == null) return;

            ClearVehicle();
            VehicleSerializer.Restore(data, _vehicle, _factory, _catalog);
            _history.Clear();
        }

        /// <summary>Validates and assembles the vehicle so it can be driven by the controller.</summary>
        public bool TestVehicle(out string error)
        {
            error = null;
            if (IsTesting) return true;

            _placement.CancelPlacement();
            _selection?.Deselect();

            if (!_assembler.AssembleForDriving(_vehicle, out error)) return false;

            SetBuildModeEnabled(false);
            IsTesting = true;
            return true;
        }

        // ---------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------

        private void OnPartPlaced(PlacementCommit commit)
            => _history.Record(new PlacePartCommand(_factory, _vehicle, commit));

        private void OnHistoryChanged()
        {
            if (_stats != null) _stats.Recompute();
        }

        private void ClearVehicle()
        {
            var snapshot = new List<PartInstance>(_vehicle.Parts);
            for (int i = 0; i < snapshot.Count; i++)
                _factory.Remove(snapshot[i]);
        }

        private void SetBuildModeEnabled(bool enabled)
        {
            if (_buildModeBehaviours == null) return;
            for (int i = 0; i < _buildModeBehaviours.Length; i++)
                if (_buildModeBehaviours[i] != null) _buildModeBehaviours[i].enabled = enabled;
        }

        private void OnDeleteInput(InputAction.CallbackContext _) => DeleteSelected();
        private void OnDuplicateInput(InputAction.CallbackContext _) => DuplicateSelected();
        private void OnUndoInput(InputAction.CallbackContext _) => Undo();
        private void OnRedoInput(InputAction.CallbackContext _) => Redo();

        private static void Subscribe(InputActionReference r, System.Action<InputAction.CallbackContext> cb)
        {
            if (r?.action == null) return;
            r.action.performed += cb;
            r.action.Enable();
        }

        private static void Unsubscribe(InputActionReference r, System.Action<InputAction.CallbackContext> cb)
        {
            if (r?.action != null) r.action.performed -= cb;
        }
    }
}
