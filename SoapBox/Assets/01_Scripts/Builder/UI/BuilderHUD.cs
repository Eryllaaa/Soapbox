using UnityEngine;
using UnityEngine.UI;
using Soapbox.Builder.Core;

namespace Soapbox.Builder.UI
{
    /// <summary>
    /// Wires the builder toolbar buttons (save, load, delete, duplicate, paint, test,
    /// undo, redo) to the <see cref="BuilderController"/>. All references are optional so
    /// a HUD can expose only the buttons it needs.
    /// </summary>
    public sealed class BuilderHUD : MonoBehaviour
    {
        [SerializeField] private BuilderController _builder;

        [Header("Buttons")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _duplicateButton;
        [SerializeField] private Button _paintButton;
        [SerializeField] private Button _testButton;
        [SerializeField] private Button _undoButton;
        [SerializeField] private Button _redoButton;

        [Header("Fields")]
        [SerializeField] private InputField _nameField;
        [SerializeField] private Text _statusText;
        [SerializeField] private Color _paintColor = Color.red;

        private void Start()
        {
            Wire(_saveButton, OnSave);
            Wire(_loadButton, OnLoad);
            Wire(_deleteButton, () => _builder.DeleteSelected());
            Wire(_duplicateButton, () => _builder.DuplicateSelected());
            Wire(_paintButton, () => _builder.PaintSelected(_paintColor));
            Wire(_testButton, OnTest);
            Wire(_undoButton, () => _builder.Undo());
            Wire(_redoButton, () => _builder.Redo());

            if (_builder != null)
            {
                _builder.History.Changed += RefreshUndoRedo;
                RefreshUndoRedo();
            }
        }

        private void OnDestroy()
        {
            if (_builder != null) _builder.History.Changed -= RefreshUndoRedo;
        }

        /// <summary>Sets the colour applied by the Paint button (e.g. from a colour picker).</summary>
        public void SetPaintColor(Color color) => _paintColor = color;

        private void OnSave()
        {
            _builder.Save(VehicleName());
            SetStatus($"Saved '{VehicleName()}'.");
        }

        private void OnLoad()
        {
            _builder.Load(VehicleName());
            SetStatus($"Loaded '{VehicleName()}'.");
        }

        private void OnTest()
        {
            SetStatus(_builder.TestVehicle(out string error) ? "Driving!" : error);
        }

        private string VehicleName() =>
            _nameField != null && !string.IsNullOrWhiteSpace(_nameField.text) ? _nameField.text : "New Vehicle";

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
        }

        private void RefreshUndoRedo()
        {
            if (_undoButton != null) _undoButton.interactable = _builder.History.CanUndo;
            if (_redoButton != null) _redoButton.interactable = _builder.History.CanRedo;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }
    }
}
