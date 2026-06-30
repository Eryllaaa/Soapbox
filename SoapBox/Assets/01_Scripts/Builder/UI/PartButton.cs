using System;
using UnityEngine;
using UnityEngine.UI;
using Soapbox.Builder.Parts;

namespace Soapbox.Builder.UI
{
    /// <summary>
    /// A single part-browser entry. Displays a part's thumbnail and name and invokes a
    /// callback (begin placement) when clicked.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PartButton : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _label;

        private PartData _data;
        private Action<PartData> _onClick;

        /// <summary>Binds this button to a part and a click handler.</summary>
        public void Bind(PartData data, Action<PartData> onClick)
        {
            _data = data;
            _onClick = onClick;

            if (_label != null) _label.text = data.DisplayName;
            if (_icon != null && data.Thumbnail != null) _icon.sprite = data.Thumbnail;

            Button button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onClick?.Invoke(_data));
        }
    }
}
