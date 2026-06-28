using UnityEngine;
using UnityEngine.InputSystem;

namespace Soapbox.Vehicles
{
    /// <summary>
    /// Reads Input System actions and pushes the result to
    /// <see cref="VehicleController.SetLocalInput"/>.
    ///
    /// Intentionally a plain <see cref="MonoBehaviour"/>, NOT a
    /// <c>NetworkBehaviour</c>: the input is always local to the player pressing
    /// the keys. The VehicleController handles sending it to the server via Cmd.
    /// Keeping this class free of Mirror dependencies means:
    ///   • It works in solo / offline play without a NetworkIdentity,
    ///   • It works in host / client play as long as the local instance owns
    ///     the vehicle (the controller itself decides whether to act on inputs).
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class SoapboxInputReader : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Optional explicit InputActions asset. If null, falls back to a fresh " +
                 "instance generated from the project's InputActions class.")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Debug")]
        [Tooltip("If true, logs binding success and any non-zero input sample. " +
                 "Set to false once everything works to silence the console.")]
        [SerializeField] private bool _debugLogs = true;

        [Tooltip("Only log when the absolute steering value changes by more than this. " +
                 "Prevents log spam while the wheel is held in one direction.")]
        [SerializeField, Min(0f)] private float _debugLogDelta = 0.05f;

        private VehicleController _controller;
        private InputActions _runtimeActions;
        private InputAction _steerAction;
        private InputAction _brakeAction;
        private bool _bound;

        private float _lastLoggedSteer = 999f;
        private bool _lastLoggedBrake;
        private int _nullSampleStreak;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _controller = GetComponent<VehicleController>();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            if (!_bound)
            {
                TryBind();
                if (!_bound)
                {
                    if (_debugLogs)
                    {
                        _nullSampleStreak++;
                        if (_nullSampleStreak == 60)
                            Debug.LogWarning($"[SoapboxInputReader] '{name}' could not bind inputs after 60 frames. " +
                                             "Check that the InputActionAsset is assigned (or that the " +
                                             "generated InputActions class is present).", this);
                    }
                    return;
                }
            }

            if (_controller == null) return;
            if (_steerAction == null || _brakeAction == null) return;

            float steer = _steerAction.ReadValue<float>();
            bool brake = _brakeAction.IsPressed();

            if (_debugLogs)
            {
                if (Mathf.Abs(steer - _lastLoggedSteer) >= _debugLogDelta || brake != _lastLoggedBrake)
                {
                    Debug.Log($"[SoapboxInputReader] '{name}' input: steer={steer:F2} brake={brake}", this);
                    _lastLoggedSteer = steer;
                    _lastLoggedBrake = brake;
                }
            }

            _controller.SetLocalInput(steer, brake);
        }

        // -------------------------------------------------------------------------
        // Binding helpers
        // -------------------------------------------------------------------------

        private void TryBind()
        {
            if (_bound) return;

            if (_inputActions != null)
            {
                InputActionMap vehicleMap = _inputActions.FindActionMap("Vehicle", throwIfNotFound: false);
                if (vehicleMap == null)
                {
                    Debug.LogError($"[SoapboxInputReader] No 'Vehicle' action map in {_inputActions.name}.", this);
                    return;
                }
                _steerAction = vehicleMap.FindAction("Steer", throwIfNotFound: false);
                _brakeAction = vehicleMap.FindAction("Brake", throwIfNotFound: false);
                if (_steerAction == null || _brakeAction == null)
                {
                    Debug.LogError("[SoapboxInputReader] 'Steer' or 'Brake' action missing in Vehicle map.", this);
                    return;
                }
                _inputActions.Enable();
            }
            else
            {
                _runtimeActions = new InputActions();
                _steerAction = _runtimeActions.Vehicle.Steer;
                _brakeAction = _runtimeActions.Vehicle.Brake;
                _runtimeActions.Enable();
            }

            _bound = true;

            if (_debugLogs)
            {
                Debug.Log(
                    $"[SoapboxInputReader] '{name}' bound successfully. " +
                    $"steerAction='{_steerAction.name}' brakeAction='{_brakeAction.name}' " +
                    $"controls={_steerAction.controls.Count}+{_brakeAction.controls.Count}",
                    this);
            }
        }

        private void Unbind()
        {
            if (!_bound) return;

            if (_inputActions != null)
                _inputActions.Disable();
            else
                DisposeRuntimeActions();

            _steerAction = null;
            _brakeAction = null;
            _bound = false;
        }

        private void DisposeRuntimeActions()
        {
            if (_runtimeActions == null) return;
            _runtimeActions.Disable();
            _runtimeActions.Dispose();
            _runtimeActions = null;
        }
    }
}
