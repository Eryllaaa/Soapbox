using UnityEngine;
using UnityEngine.InputSystem;
using Soapbox.Builder.Selection;

namespace Soapbox.Builder.CameraRig
{
    /// <summary>
    /// Orbit / pan / zoom / focus camera for the builder. Orbits a pivot point while the
    /// Orbit button is held (with Pan held it pans the pivot instead); the scroll wheel
    /// zooms; Focus frames the current selection. Input is via InputActionReferences.
    /// </summary>
    public sealed class BuilderCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SelectionController _selection;

        [Header("Input")]
        [SerializeField] private InputActionReference _look;
        [SerializeField] private InputActionReference _zoom;
        [SerializeField] private InputActionReference _orbitButton;
        [SerializeField] private InputActionReference _panButton;
        [SerializeField] private InputActionReference _focusButton;

        [Header("Tuning")]
        [SerializeField] private float _orbitSpeed = 0.2f;
        [SerializeField] private float _panSpeed = 0.002f;
        [SerializeField] private float _zoomSpeed = 0.01f;
        [SerializeField] private float _distance = 10f;
        [SerializeField] private float _minDistance = 2f;
        [SerializeField] private float _maxDistance = 60f;
        [SerializeField] private Vector2 _pitchClamp = new(-80f, 80f);
        [SerializeField] private Vector3 _initialPivot = Vector3.zero;

        private float _yaw;
        private float _pitch;
        private Vector3 _pivot;

        private void OnEnable()
        {
            Enable(_look); Enable(_zoom); Enable(_orbitButton); Enable(_panButton);
            if (_focusButton?.action != null)
            {
                _focusButton.action.performed += OnFocus;
                _focusButton.action.Enable();
            }

            Vector3 euler = transform.eulerAngles;
            _pitch = euler.x;
            _yaw = euler.y;
            _pivot = _initialPivot;
            Apply();
        }

        private void OnDisable()
        {
            if (_focusButton?.action != null) _focusButton.action.performed -= OnFocus;
        }

        private void Update()
        {
            bool orbiting = IsPressed(_orbitButton);
            if (orbiting)
            {
                Vector2 look = Read2(_look);
                if (IsPressed(_panButton)) Pan(look);
                else Orbit(look);
            }

            float zoom = ReadAxis(_zoom);
            if (Mathf.Abs(zoom) > 0.0001f)
                _distance = Mathf.Clamp(_distance - zoom * _zoomSpeed, _minDistance, _maxDistance);

            Apply();
        }

        private void Orbit(Vector2 look)
        {
            _yaw += look.x * _orbitSpeed;
            _pitch = Mathf.Clamp(_pitch - look.y * _orbitSpeed, _pitchClamp.x, _pitchClamp.y);
        }

        private void Pan(Vector2 look)
        {
            Vector3 move = (-transform.right * look.x - transform.up * look.y) * (_panSpeed * _distance);
            _pivot += move;
        }

        private void OnFocus(InputAction.CallbackContext _)
        {
            if (_selection == null || _selection.Selected == null) return;

            var renderers = _selection.Selected.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                _pivot = _selection.Selected.transform.position;
                return;
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            _pivot = b.center;
            _distance = Mathf.Clamp(b.extents.magnitude * 2.5f, _minDistance, _maxDistance);
        }

        private void Apply()
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(_pivot - rot * Vector3.forward * _distance, rot);
        }

        private static void Enable(InputActionReference r)
        {
            if (r?.action != null) r.action.Enable();
        }

        private static bool IsPressed(InputActionReference r) => r?.action != null && r.action.IsPressed();
        private static Vector2 Read2(InputActionReference r) => r?.action != null ? r.action.ReadValue<Vector2>() : Vector2.zero;
        private static float ReadAxis(InputActionReference r) => r?.action != null ? r.action.ReadValue<float>() : 0f;
    }
}
