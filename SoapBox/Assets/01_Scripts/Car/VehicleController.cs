using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Wheel Groups")]
    [Tooltip("Wheels that will receive steering rotation.")]
    [SerializeField] private Wheel[] _steeringWheels;

    [Tooltip("Wheels that will receive braking commands.")]
    [SerializeField] private Wheel[] _brakeWheels;

    [Header("Steering")]
    [SerializeField] private float _maxSteerAngle = 30f;
    [SerializeField] private float _steerSpeed = 5f;

    [Header("Speed Limit")]
    [Tooltip("Hard maximum speed (m/s). The Rigidbody's linear velocity is clamped to this every physics step.")]
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

    // -------------------------------------------------------------------------
    // Private — steering
    // -------------------------------------------------------------------------

    private float _currentSteerAngle;
    private Quaternion[] _steeringNeutralRotations;

    // -------------------------------------------------------------------------
    // Private — steering
    // -------------------------------------------------------------------------

    private bool _debugAccelerating;

    // -------------------------------------------------------------------------
    // Private — physics
    // -------------------------------------------------------------------------

    private Rigidbody _rb;

    // -------------------------------------------------------------------------
    // Private — input actions
    // -------------------------------------------------------------------------

    private InputActions _actions;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_rb == null)
            Debug.LogError("[VehicleController] No Rigidbody found on this GameObject.", this);

        // Snapshot neutral rotations before anything moves.
        _steeringNeutralRotations = new Quaternion[_steeringWheels.Length];
        for (int i = 0; i < _steeringWheels.Length; i++)
        {
            if (_steeringWheels[i] != null)
                _steeringNeutralRotations[i] = _steeringWheels[i].transform.localRotation;
        }

        _actions = new InputActions();
    }

    private void OnEnable()
    {
        _actions.Enable();

        _actions.Vehicle.Steer.performed += OnSteer;
        _actions.Vehicle.Steer.canceled += OnSteer;

        _actions.Vehicle.Brake.performed += OnBrake;
        _actions.Vehicle.Brake.canceled += OnBrake;

        #if UNITY_EDITOR
        _actions.Vehicle.DebugAcceleration.performed += OnDebugAcceleration;
        _actions.Vehicle.DebugAcceleration.canceled += OnDebugAcceleration;
        #endif
    }

    private void OnDisable()
    {
        _actions.Vehicle.Steer.performed -= OnSteer;
        _actions.Vehicle.Steer.canceled -= OnSteer;

        _actions.Vehicle.Brake.performed -= OnBrake;
        _actions.Vehicle.Brake.canceled -= OnBrake;

        _actions.Disable();
    }

    private void FixedUpdate()
    {
        ClampSpeed();
        HandleSteering();
        HandleBraking();
        HandleDebugAcceleration();
    }

    // -------------------------------------------------------------------------
    // Input callbacks
    // -------------------------------------------------------------------------

    private void OnSteer(InputAction.CallbackContext ctx)
        => _steerInput = ctx.ReadValue<float>();

    private void OnBrake(InputAction.CallbackContext ctx)
        => _brakeInput = ctx.ReadValueAsButton();

    // -------------------------------------------------------------------------
    // Private physics helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clamps the Rigidbody's linear velocity so it never exceeds <see cref="_maxSpeed"/>.
    /// This is applied directly to the velocity vector, preserving direction.
    /// </summary>
    private void ClampSpeed()
    {
        if (_rb == null) return;

        if (_rb.linearVelocity.magnitude > _maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _maxSpeed;
    }

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

#if UNITY_EDITOR
    private void OnDebugAcceleration(InputAction.CallbackContext ctx)
    {
        _debugAccelerating = ctx.started || ctx.performed;
    }

    private void HandleDebugAcceleration()
    {
        if (_debugAccelerating) 
            _rb.AddForce(transform.forward * _debugAcceleration * 10000f * Time.fixedDeltaTime);
    }
#endif
}