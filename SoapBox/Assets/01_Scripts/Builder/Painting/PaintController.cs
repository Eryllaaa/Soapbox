using UnityEngine;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Selection;

namespace Soapbox.Builder.Painting
{
    /// <summary>
    /// Applies the current paint colour to the selected part. Painting is stored on the
    /// part via a MaterialPropertyBlock (see <see cref="PartInstance.SetPaint"/>), so it
    /// is non-destructive and is captured by the save system.
    /// </summary>
    public sealed class PaintController : MonoBehaviour
    {
        [SerializeField] private SelectionController _selection;
        [SerializeField] private Color _currentColor = Color.red;

        /// <summary>The colour applied when painting.</summary>
        public Color CurrentColor
        {
            get => _currentColor;
            set => _currentColor = value;
        }

        /// <summary>Paints the currently selected part with the current colour.</summary>
        public void PaintSelected()
        {
            PartInstance selected = _selection != null ? _selection.Selected : null;
            if (selected != null) selected.SetPaint(_currentColor);
        }

        /// <summary>Sets the current colour and paints the selected part.</summary>
        public void PaintSelected(Color color)
        {
            _currentColor = color;
            PaintSelected();
        }
    }
}
