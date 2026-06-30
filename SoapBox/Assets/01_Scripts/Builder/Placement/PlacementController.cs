using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Soapbox.Builder.Parts;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.Placement
{
    /// <summary>
    /// Drives the place-a-part loop: a ghost follows the cursor, snaps to the nearest
    /// compatible socket, shows green/red validity, and commits a connected part on
    /// click. Rotation steps spin the ghost around the mating axis. Input is wired via
    /// <see cref="InputActionReference"/>s so this is decoupled from the generated
    /// input wrapper.
    /// </summary>
    public sealed class PlacementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _buildCamera;
        [SerializeField] private VehicleRoot _vehicle;
        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;

        [Header("Input")]
        [SerializeField] private InputActionReference _pointAction;
        [SerializeField] private InputActionReference _placeAction;
        [SerializeField] private InputActionReference _cancelAction;
        [SerializeField] private InputActionReference _rotateLeftAction;
        [SerializeField] private InputActionReference _rotateRightAction;

        [Header("Placement")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [SerializeField, Min(0f)] private float _snapRadius = 0.5f;
        [SerializeField, Min(0f)] private float _collisionPadding = 0.02f;
        [Tooltip("Distance from the camera used for free placement when the ground ray misses.")]
        [SerializeField, Min(0f)] private float _defaultDistance = 10f;
        [Tooltip("Allow resting on the ground with no socket (only while the vehicle has no free sockets, e.g. the first chassis).")]
        [SerializeField] private bool _allowFreePlacement = true;
        [Tooltip("Degrees per rotate step. Common values: 15, 30, 45, 90.")]
        [SerializeField] private float _rotationStep = 45f;
        [SerializeField] private bool _keepPlacingAfterCommit = true;

        /// <summary>Raised after a part is successfully committed, with its connection info.</summary>
        public event Action<PlacementCommit> PartPlaced;

        private PartPreview _preview;
        private float _rotationOffset;
        private AttachmentMatch _match;
        private PlacementResult _result;
        private readonly List<AttachmentPoint> _freeSockets = new();
        private readonly Collider[] _overlapBuffer = new Collider[32];

        /// <summary>True while a ghost is active.</summary>
        public bool IsPlacing => _preview != null;

        private void Awake()
        {
            if (_buildCamera == null) _buildCamera = Camera.main;
        }

        private void OnEnable()
        {
            Enable(_pointAction);
            Subscribe(_placeAction, OnPlace);
            Subscribe(_cancelAction, OnCancel);
            Subscribe(_rotateLeftAction, OnRotateLeft);
            Subscribe(_rotateRightAction, OnRotateRight);
        }

        private void OnDisable()
        {
            Unsubscribe(_placeAction, OnPlace);
            Unsubscribe(_cancelAction, OnCancel);
            Unsubscribe(_rotateLeftAction, OnRotateLeft);
            Unsubscribe(_rotateRightAction, OnRotateRight);
            CancelPlacement();
        }

        private void Update()
        {
            if (_preview != null) UpdatePreview();
        }

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>Begins placing a ghost for the given part. Replaces any current ghost.</summary>
        public void BeginPlacement(PartData data)
        {
            if (data == null || data.Prefab == null) return;

            CancelPlacement();
            _rotationOffset = 0f;
            _preview = new PartPreview(data, _validMaterial, _invalidMaterial);
        }

        /// <summary>Aborts the current placement and destroys the ghost.</summary>
        public void CancelPlacement()
        {
            _preview?.Dispose();
            _preview = null;
        }

        // ---------------------------------------------------------------------
        // Per-frame preview
        // ---------------------------------------------------------------------

        private void UpdatePreview()
        {
            // 1. Free pose at the cursor, so the solver evaluates from where the user points.
            Vector3 basePos = GetCursorPoint();
            _preview.SetPose(basePos, Quaternion.AngleAxis(_rotationOffset, Vector3.up));

            // 2. Find the closest compatible socket from the cursor pose.
            if (_vehicle != null) _vehicle.CollectFreeSockets(_freeSockets);
            else _freeSockets.Clear();

            _match = AttachmentSolver.FindBestMatch(_preview.Attachments, _preview.Transform, _freeSockets, _snapRadius);

            // 3. Snap (with the rotation step applied about the mating axis) when matched.
            if (_match.IsValid)
            {
                Vector3 pivot = _match.TargetPoint.transform.position;
                Quaternion spin = Quaternion.AngleAxis(_rotationOffset, _match.TargetPoint.transform.forward);
                _preview.SetPose(
                    pivot + spin * (_match.SnappedPosition - pivot),
                    spin * _match.SnappedRotation);
            }

            // 4. Validate and tint. Free placement only while nothing can be snapped to.
            bool freeAllowed = _allowFreePlacement && _freeSockets.Count == 0;
            _result = PlacementValidator.Validate(
                _preview, _match, freeAllowed, _obstacleMask, _collisionPadding, _overlapBuffer);
            _preview.SetValid(_result.IsValid);
        }

        private Vector3 GetCursorPoint()
        {
            Vector2 screen = _pointAction != null && _pointAction.action != null
                ? _pointAction.action.ReadValue<Vector2>()
                : Vector2.zero;

            Ray ray = _buildCamera.ScreenPointToRay(screen);
            return Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(_defaultDistance);
        }

        // ---------------------------------------------------------------------
        // Commit
        // ---------------------------------------------------------------------

        private void TryCommit()
        {
            if (_preview == null || !_result.IsValid) return;

            PartData data = _preview.Data;
            GameObject placed = _preview.Commit();
            _preview = null;

            PartInstance instance = placed.GetComponent<PartInstance>();
            instance?.AssignInstanceId(Guid.NewGuid().ToString("N"));

            if (_vehicle != null)
                _vehicle.RegisterPart(instance);

            AttachmentPoint incoming = _match.IsValid ? _match.IncomingPoint : null;
            AttachmentPoint target = _match.IsValid ? _match.TargetPoint : null;
            if (_match.IsValid)
                incoming.Connect(target);

            PartPlaced?.Invoke(new PlacementCommit(instance, incoming, target));

            if (_keepPlacingAfterCommit)
                BeginPlacement(data);
        }

        // ---------------------------------------------------------------------
        // Input callbacks
        // ---------------------------------------------------------------------

        private void OnPlace(InputAction.CallbackContext _) => TryCommit();
        private void OnCancel(InputAction.CallbackContext _) => CancelPlacement();
        private void OnRotateLeft(InputAction.CallbackContext _) => _rotationOffset -= _rotationStep;
        private void OnRotateRight(InputAction.CallbackContext _) => _rotationOffset += _rotationStep;

        // ---------------------------------------------------------------------
        // Input wiring helpers
        // ---------------------------------------------------------------------

        private static void Enable(InputActionReference reference)
        {
            if (reference != null && reference.action != null) reference.action.Enable();
        }

        private static void Subscribe(InputActionReference reference, Action<InputAction.CallbackContext> callback)
        {
            if (reference == null || reference.action == null) return;
            reference.action.performed += callback;
            reference.action.Enable();
        }

        private static void Unsubscribe(InputActionReference reference, Action<InputAction.CallbackContext> callback)
        {
            if (reference == null || reference.action == null) return;
            reference.action.performed -= callback;
        }
    }
}
