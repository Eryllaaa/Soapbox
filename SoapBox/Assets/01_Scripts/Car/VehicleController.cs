using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Soapbox.Race;

[RequireComponent(typeof(Rigidbody))]
public class VehicleController : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Wheel Groups")]
    [Tooltip("Wheels that will receive steering rotation.")]
    [SerializeField] private Wheel[] _steeringWheels;

    [Tooltip("Wheels that will receive braking commands.")]
    [SerializeField] private Wheel[] _brakeWheels;

    [Header("Suspension")]
    [SerializeField] private Suspension[] _suspensions;

    [Header("Steering")]
    [SerializeField] private float _maxSteerAngle = 30f;
    [SerializeField] private float _steerSpeed = 5f;

    [Header("Air Control")]
    [SerializeField, Min(0f)] private float _airPitchTorque = 1500f;
    [SerializeField, Min(0f)] private float _airYawTorque = 1500f;
    [SerializeField, Range(0f, 1f)] private float _airControlDeadZone = 0.05f;

    [Header("Speed Limit")]
    [SerializeField, Min(0f)] private float _maxSpeed = 30f;

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private float _debugAcceleration = 100f;
#endif

    // -------------------------------------------------------------------------
    // Private — input state
    // -------------------------------------------------------------------------

    private float _steerInput;
    private bool _brakeInput;
    private float _pitchInput;
    private float _yawInput;

    // -------------------------------------------------------------------------
    // Private — steering & physics
    // -------------------------------------------------------------------------

    private float _currentSteerAngle;
    private Quaternion[] _steeringNeutralRotations;
    private bool _debugAccelerating;
    private Rigidbody _rb;
    private InputActions _actions;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_rb == null)
            Debug.LogError("[VehicleController] No Rigidbody found on this GameObject.", this);

        _steeringNeutralRotations = new Quaternion[_steeringWheels.Length];
        for (int i = 0; i < _steeringWheels.Length; i++)
        {
            if (_steeringWheels[i] != null)
                _steeringNeutralRotations[i] = _steeringWheels[i].transform.localRotation;
        }

        _actions = new InputActions();
    }

    public override void OnStartClient()
    {
        if (IsOffline()) return;

        SetWheelsEnabled(isOwned);
        SetSuspensionsEnabled(isOwned);
    }

    public override void OnStartAuthority()
    {
        EnableLocalInput();
    }

    public override void OnStopAuthority()
    {
        if (IsOffline()) return;
        DisableLocalInput();
    }

    private void OnDestroy()
    {
        DisableLocalInput();
    }

    private void EnableLocalInput()
    {
        if (_actions == null) return;
        _actions.Enable();

        _actions.Vehicle.Steer.performed += OnSteer;
        _actions.Vehicle.Steer.canceled += OnSteer;

        _actions.Vehicle.Brake.performed += OnBrake;
        _actions.Vehicle.Brake.canceled += OnBrake;

        _actions.Vehicle.Pitch.performed += OnPitch;
        _actions.Vehicle.Pitch.canceled += OnPitch;

        _actions.Vehicle.Yaw.performed += OnYaw;
        _actions.Vehicle.Yaw.canceled += OnYaw;

#if UNITY_EDITOR
        _actions.Vehicle.DebugAcceleration.performed += OnDebugAcceleration;
        _actions.Vehicle.DebugAcceleration.canceled += OnDebugAcceleration;
#endif
    }

    private void DisableLocalInput()
    {
        if (_actions == null) return;

        _actions.Vehicle.Steer.performed -= OnSteer;
        _actions.Vehicle.Steer.canceled -= OnSteer;

        _actions.Vehicle.Brake.performed -= OnBrake;
        _actions.Vehicle.Brake.canceled -= OnBrake;

        _actions.Vehicle.Pitch.performed -= OnPitch;
        _actions.Vehicle.Pitch.canceled -= OnPitch;

        _actions.Vehicle.Yaw.performed -= OnYaw;
        _actions.Vehicle.Yaw.canceled -= OnYaw;

#if UNITY_EDITOR
        _actions.Vehicle.DebugAcceleration.performed -= OnDebugAcceleration;
        _actions.Vehicle.DebugAcceleration.canceled -= OnDebugAcceleration;
#endif

        _actions.Disable();
    }

    private void FixedUpdate()
    {
        if (!isOwned && !IsOffline()) return;

        // =================================================================
        // FIX : Blocage de la voiture pendant le compte à rebours
        // Si le gestionnaire de course existe et qu'on n'est pas "En Course", on bloque
        // =================================================================
        if (RaceManager.Instance != null && RaceManager.Instance.State != RaceManager.RaceState.Racing)
            return;

        ClampSpeed();
        HandleSteering();
        HandleBraking();
        HandleAirControl();
#if UNITY_EDITOR
        HandleDebugAcceleration();
#endif
    }

    // -------------------------------------------------------------------------
    // Input callbacks
    // -------------------------------------------------------------------------

    private void OnSteer(InputAction.CallbackContext ctx) => _steerInput = ctx.ReadValue<float>();
    private void OnBrake(InputAction.CallbackContext ctx) => _brakeInput = ctx.ReadValueAsButton();
    private void OnPitch(InputAction.CallbackContext ctx) => _pitchInput = ctx.ReadValue<float>();
    private void OnYaw(InputAction.CallbackContext ctx) => _yawInput = ctx.ReadValue<float>();

#if UNITY_EDITOR
    private void OnDebugAcceleration(InputAction.CallbackContext ctx)
    {
        _debugAccelerating = ctx.started || ctx.performed;
    }
#endif

    // -------------------------------------------------------------------------
    // Private physics helpers
    // -------------------------------------------------------------------------

    private void ClampSpeed()
    {
        if (_rb == null) return;

        if (_rb.linearVelocity.magnitude > _maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _maxSpeed;
    }

    private void HandleSteering()
    {
        float targetAngle = _steerInput * _maxSteerAngle;
        _currentSteerAngle = Mathf.MoveTowards(
            _currentSteerAngle,
            targetAngle,
            _steerSpeed * _maxSteerAngle * Time.fixedDeltaTime
        );

        for (int i = 0; i < _steeringWheels.Length; i++)
        {
            if (_steeringWheels[i] == null) continue;

            _steeringWheels[i].transform.localRotation =
                _steeringNeutralRotations[i] * Quaternion.Euler(0f, _currentSteerAngle, 0f);
        }
    }

    private void HandleBraking()
    {
        foreach (Wheel wheel in _brakeWheels)
        {
            if (wheel == null) continue;

            if (_brakeInput) wheel.Brake();
            else wheel.StopBraking();
        }
    }

    private void HandleAirControl()
    {
        if (_rb == null) return;
        if (IsAnyWheelGrounded()) return;

        if (Mathf.Abs(_pitchInput) > _airControlDeadZone)
            _rb.AddTorque(transform.right * (_pitchInput * _airPitchTorque), ForceMode.Force);

        if (Mathf.Abs(_yawInput) > _airControlDeadZone)
            _rb.AddTorque(transform.up * (_yawInput * _airYawTorque), ForceMode.Force);
    }

    private bool IsAnyWheelGrounded()
    {
        for (int i = 0; i < _steeringWheels.Length; i++)
            if (_steeringWheels[i] != null && _steeringWheels[i].IsGrounded) return true;

        for (int i = 0; i < _brakeWheels.Length; i++)
            if (_brakeWheels[i] != null && _brakeWheels[i].IsGrounded) return true;

        return false;
    }

#if UNITY_EDITOR
    private void HandleDebugAcceleration()
    {
        if (_rb == null) return;
        if (_debugAccelerating)
            _rb.AddForce(transform.forward * _debugAcceleration * 10000f * Time.fixedDeltaTime);
    }
#endif

    // -------------------------------------------------------------------------
    // Private network helpers
    // -------------------------------------------------------------------------

    private void SetWheelsEnabled(bool enabled)
    {
        foreach (Wheel wheel in _steeringWheels)
            if (wheel != null) wheel.enabled = enabled;

        foreach (Wheel wheel in _brakeWheels)
            if (wheel != null) wheel.enabled = enabled;
    }

    private void SetSuspensionsEnabled(bool enabled)
    {
        foreach (Suspension suspension in _suspensions)
            if (suspension != null) suspension.enabled = enabled;
    }

    private bool IsOffline() => !NetworkServer.active && !NetworkClient.active;
}
