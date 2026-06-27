using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-level vehicle controller.
///
/// Who this script knows about : <see cref="Wheel"/> (public API only).
///   - It NEVER reads Wheel's serialized values (grip, power, etc.).
///   - It NEVER references Suspension at all.
///   - Wheel registration is done via plain arrays in the inspector.
///
/// Input is driven by the generated <see cref="InputsActions"/> C# class.
/// Subscribe/unsubscribe is handled via OnEnable/OnDisable so the action map
/// is active only while this component is enabled.
/// </summary>
public class VehicleController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Wheel Groups")]
    [Tooltip("Wheels that will receive steering rotation.")]
    [SerializeField] private Wheel[] _steeringWheels;

    [Tooltip("Wheels that will receive acceleration commands.")]
    [SerializeField] private Wheel[] _driveWheels;

    [Tooltip("Wheels that will receive braking commands (can overlap with drive/steering).")]
    [SerializeField] private Wheel[] _brakeWheels;

    [Header("Steering")]
    [SerializeField] private float _maxSteerAngle = 30f;
    [SerializeField] private float _steerSpeed = 5f;

    [Header("Acceleration")]
    [SerializeField] private float _topSpeed = 30f;

    // -------------------------------------------------------------------------
    // Private — input state
    // -------------------------------------------------------------------------

    private float _steerInput;
    private float _throttleInput;
    private bool _brakeInput;

    // -------------------------------------------------------------------------
    // Private — steering
    // -------------------------------------------------------------------------

    private float _currentSteerAngle;
    private Quaternion[] _steeringNeutralRotations;

    // -------------------------------------------------------------------------
    // Private — input actions
    // -------------------------------------------------------------------------

    private InputActions _actions;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Snapshot neutral rotations before anything moves.
        _steeringNeutralRotations = new Quaternion[_steeringWheels.Length];
        for (int i = 0; i < _steeringWheels.Length; i++)
        {
            if (_steeringWheels[i] != null)
                _steeringNeutralRotations[i] = _steeringWheels[i].transform.localRotation;
        }

        _actions = new InputActions();
    }

    private void Start()
    {
        foreach (Wheel wheel in _driveWheels)
            if (wheel != null)
                wheel.TopSpeed = _topSpeed;
    }

    private void OnEnable()
    {
        _actions.Enable();

        _actions.Vehicle.Steer.performed += OnSteer;
        _actions.Vehicle.Steer.canceled += OnSteer;

        _actions.Vehicle.Accelerate.performed += OnAccelerate;
        _actions.Vehicle.Accelerate.canceled += OnAccelerate;

        _actions.Vehicle.Brake.performed += OnBrake;
        _actions.Vehicle.Brake.canceled += OnBrake;
    }

    private void OnDisable()
    {
        _actions.Vehicle.Steer.performed -= OnSteer;
        _actions.Vehicle.Steer.canceled -= OnSteer;

        _actions.Vehicle.Accelerate.performed -= OnAccelerate;
        _actions.Vehicle.Accelerate.canceled -= OnAccelerate;

        _actions.Vehicle.Brake.performed -= OnBrake;
        _actions.Vehicle.Brake.canceled -= OnBrake;

        _actions.Disable();
    }

    private void FixedUpdate()
    {
        HandleSteering();
        HandleAcceleration();
        HandleBraking();
    }

    // -------------------------------------------------------------------------
    // Input callbacks
    // -------------------------------------------------------------------------

    private void OnSteer(InputAction.CallbackContext ctx)
        => _steerInput = ctx.ReadValue<float>();

    private void OnAccelerate(InputAction.CallbackContext ctx)
        => _throttleInput = ctx.ReadValue<float>();

    private void OnBrake(InputAction.CallbackContext ctx)
        => _brakeInput = ctx.ReadValueAsButton();

    // -------------------------------------------------------------------------
    // Private physics helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Smoothly rotates each steering wheel around its own local Y axis,
    /// offset from the rotation it had when the scene started.
    /// </summary>
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

    /// <summary>Passes the throttle value to every drive wheel.</summary>
    private void HandleAcceleration()
    {
        foreach (Wheel wheel in _driveWheels)
        {
            if (wheel != null)
                wheel.AccelInput = _throttleInput;
        }
    }

    /// <summary>Tells brake wheels to start or stop braking.</summary>
    private void HandleBraking()
    {
        foreach (Wheel wheel in _brakeWheels)
        {
            if (wheel == null) continue;

            if (_brakeInput) wheel.Brake();
            else wheel.StopBraking();
        }
    }
}