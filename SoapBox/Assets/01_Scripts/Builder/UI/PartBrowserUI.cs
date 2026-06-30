using UnityEngine;
using UnityEngine.UI;
using Soapbox.Builder.Core;
using Soapbox.Builder.Parts;
using Soapbox.Builder.SaveSystem;

namespace Soapbox.Builder.UI
{
    /// <summary>
    /// Populates a scrollable list of part buttons from the catalog, filtered by an
    /// optional category and search text, and starts placement when a button is clicked.
    /// </summary>
    public sealed class PartBrowserUI : MonoBehaviour
    {
        [SerializeField] private PartCatalog _catalog;
        [SerializeField] private BuilderController _builder;
        [SerializeField] private Transform _content;
        [SerializeField] private PartButton _buttonPrefab;
        [SerializeField] private InputField _searchField;

        private PartCategory _categoryFilter;
        private string _search = "";

        private void Start()
        {
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(OnSearchChanged);

            Rebuild();
        }

        /// <summary>Shows only parts in the given category (null clears the filter).</summary>
        public void SetCategoryFilter(PartCategory category)
        {
            _categoryFilter = category;
            Rebuild();
        }

        /// <summary>Clears the category filter.</summary>
        public void ClearCategoryFilter() => SetCategoryFilter(null);

        private void OnSearchChanged(string text)
        {
            _search = text;
            Rebuild();
        }

        private void Rebuild()
        {
            if (_catalog == null || _content == null || _buttonPrefab == null) return;

            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            string search = _search?.ToLowerInvariant() ?? "";

            var parts = _catalog.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                PartData data = parts[i];
                if (data == null) continue;
                if (_categoryFilter != null && data.Category != _categoryFilter) continue;
                if (search.Length > 0 && !data.DisplayName.ToLowerInvariant().Contains(search)) continue;

                PartButton button = Instantiate(_buttonPrefab, _content);
                button.Bind(data, d => _builder.BeginPlace(d));
            }
        }
    }
}
