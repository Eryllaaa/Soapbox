using Mirror;
using UnityEngine;

namespace Soapbox.CameraSystem
{
    /// <summary>
    /// Smooth follow camera for the local player's vehicle.
    ///
    /// Designed for multiplayer:
    ///   • In a network session, the rig follows the <c>NetworkClient.localPlayer</c>.
    ///   • If a target is already assigned (or assigned later via <see cref="SetTarget"/>),
    ///     the rig uses that.
    ///   • In single-player / editor, the rig simply follows whatever target
    ///     is set in the inspector.
    ///
    /// The rig <b>polls each frame</b> for the local player so it never misses
    /// a respawn — no event subscription required, no race conditions with
    /// Mirror's spawn timing.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to follow. If left empty, the rig will look for " +
                 "the local player each frame (network) or stay still (offline).")]
        [SerializeField] private Transform _target;

        [Header("Follow")]
        [Tooltip("Higher = snappier follow. Applied as exponential decay coefficient.")]
        [SerializeField, Min(0f)] private float _followSpeed = 8f;

        [Tooltip("Higher = snappier rotation follow. Applied as lerp coefficient.")]
        [SerializeField, Min(0f)] private float _rotationSpeed = 6f;

        [Header("Editor")]
        [Tooltip("In single-player, follow this Transform even if NetworkClient.localPlayer is null. " +
                 "Useful for the test scenes that have a vehicle placed by hand.")]
        [SerializeField] private Transform _editorFollowTarget;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Assign (or clear) the follow target at runtime. Use this from any
        /// system that knows the right target earlier than the rig does
        /// (e.g. a respawn manager).
        /// </summary>
        public void SetTarget(Transform target) => _target = target;

        public Transform Target => _target;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void LateUpdate()
        {
            ResolveTarget();
            if (_target == null) return;

            transform.position = ExpDecay(transform.position, _target.position, _followSpeed * Time.deltaTime);

            Quaternion desired = Quaternion.Euler(0f, _target.rotation.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, desired, _rotationSpeed * Time.deltaTime);
        }

        // -------------------------------------------------------------------------
        // Target resolution
        // -------------------------------------------------------------------------

        private void ResolveTarget()
        {
            // Highest priority: explicitly-set target that still exists.
            if (_target != null) return;

            // Network session: find the local player.
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                _target = NetworkClient.localPlayer.transform;
                return;
            }

            // Offline / editor: use the inspector-assigned fallback if any.
#if UNITY_EDITOR
            if (_editorFollowTarget != null)
            {
                _target = _editorFollowTarget;
            }
#endif
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static Vector3 ExpDecay(Vector3 a, Vector3 b, float decay)
            => b + (a - b) * Mathf.Exp(-decay);
    }
}
