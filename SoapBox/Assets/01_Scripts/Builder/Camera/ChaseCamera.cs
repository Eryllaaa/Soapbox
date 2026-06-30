using UnityEngine;

namespace Soapbox.Builder.CameraRig
{
    /// <summary>
    /// A simple chase camera for the test drive. Stays dormant (its Camera disabled) while
    /// building and automatically takes over once the vehicle gains a Rigidbody (added by the
    /// assembler at "Test"): it disables the build camera and follows the vehicle from behind.
    /// No coupling to BuilderController is required — activation is detected from the Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [Tooltip("The VehicleRoot to follow.")]
        [SerializeField] private Transform _target;

        [Tooltip("The build camera's Camera, disabled when this chase camera activates.")]
        [SerializeField] private Camera _buildCamera;

        [Header("Framing")]
        [Tooltip("Camera offset behind/above the target, expressed in the target's local space.")]
        [SerializeField] private Vector3 _localOffset = new Vector3(0f, 4f, -9f);
        [SerializeField] private float _lookHeight = 1.5f;
        [SerializeField, Min(0f)] private float _positionLerp = 6f;
        [SerializeField, Min(0f)] private float _rotationLerp = 6f;

        private Camera _camera;
        private Rigidbody _body;
        private bool _active;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.enabled = false;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            if (!_active)
            {
                if (_body == null) _body = _target.GetComponent<Rigidbody>();
                if (_body == null) return;   // still building — vehicle not assembled yet
                Activate();
            }

            Vector3 desiredPos = _target.TransformPoint(_localOffset);
            transform.position = Vector3.Lerp(
                transform.position, desiredPos, 1f - Mathf.Exp(-_positionLerp * Time.deltaTime));

            Vector3 lookAt = _target.position + Vector3.up * _lookHeight;
            Quaternion desiredRot = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRot, 1f - Mathf.Exp(-_rotationLerp * Time.deltaTime));
        }

        private void Activate()
        {
            _active = true;
            if (_buildCamera != null) _buildCamera.enabled = false;
            _camera.enabled = true;

            transform.position = _target.TransformPoint(_localOffset);
            transform.LookAt(_target.position + Vector3.up * _lookHeight, Vector3.up);
        }
    }
}
