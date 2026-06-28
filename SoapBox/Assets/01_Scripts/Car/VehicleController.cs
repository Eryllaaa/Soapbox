using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-level vehicle controller for a soapbox racer.
/// The vehicle has no engine — it is driven purely by gravity.
///
/// Pipeline (intentionally identical to the original single-player version):
///   • Input System callbacks (OnSteer / OnBrake) fill local fields.
///   • FixedUpdate runs ClampSpeed, HandleSteering, HandleBraking in that order.
///   • HandleSteering applies the rotation directly to the wheel transforms in
///     the same physics step, so Wheel.cs reads the correct transform.right
///     when it computes side friction in its own FixedUpdate.
///
/// Networking (added on top of the original pipeline, non-invasive):
///   • Owner-auth model: the local client that owns the vehicle predicts
///     locally (using the same HandleSteering / HandleBraking), and sends
///     inputs to the server via a throttled Command. The server keeps its own
///     mirror of the latest received input and uses it for its own simulation
///     when it does not own the car (e.g. dedicated server).
///   • A single SyncVar replicates the steering angle to all clients so they
///     can apply the same rotation visually (Wheel physics is disabled on
///     non-authoritative clients via NetworkOwnershipGate).
///   • In solo / offline play (no Mirror session), everything behaves exactly
///     like the original single-player script: the InputActions asset is
///     driven directly from the callbacks wired in OnEnable.
///
/// Who this script knows about : <see cref="Wheel"/> (public API only).
///   - It NEVER reads Wheel's serialized values (grip, braking power, etc.).
///   - It NEVER references Suspension at all.
///   - Wheel registration is done via plain arrays in the inspector.
/// </summary>
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

    [Header("Steering")]
    [Tooltip("Max angle (degrees) the steering wheels reach at full input.")]
    [SerializeField, Min(0f)] private float _maxSteerAngle = 30f;

    [Tooltip("Steering interpolation speed, multiplied by _maxSteerAngle to " +
             "get degrees per second. Higher = more direct response.")]
    [SerializeField, Min(0f)] private float _steerSpeed = 5f;

    [Header("Speed Limit")]
    [Tooltip("Hard maximum speed (m/s). The Rigidbody's linear velocity is clamped to this every physics step.")]
    [SerializeField, Min(0f)] private float _maxSpeed = 30f;

    [Header("Network")]
    [Tooltip("Minimum steering delta to relay a new Cmd. Keeps bandwidth low while idle.")]
    [SerializeField, Min(0f)] private float _steerSendThreshold = 0.01f;

    // -------------------------------------------------------------------------
    // Replicated state
    // -------------------------------------------------------------------------

    /// <summary>
    /// Latest steering value applied to the wheels, replicated to all clients.
    /// On non-authoritative clients, the hook is the source of the visual angle.
    /// On the authority, the local HandleSteering is the source; we publish
    /// into this SyncVar in the same FixedUpdate so the value stays in sync.
    /// </summary>
    [SyncVar(hook = nameof(OnSyncedSteerChanged))]
    private float _syncedSteer;

    // -------------------------------------------------------------------------
    // Private — input state (owner)
    // -------------------------------------------------------------------------

    private float _steerInput;
    private bool _brakeInput;

    // -------------------------------------------------------------------------
    // Private — input state (server mirror, used by dedicated server only)
    // -------------------------------------------------------------------------

    private float _serverSteerInput;
    private bool _serverBrakeInput;

    // -------------------------------------------------------------------------
    // Private — steering
    // -------------------------------------------------------------------------

    private float _currentSteerAngle;
    private Quaternion[] _steeringNeutralRotations;

    // -------------------------------------------------------------------------
    // Private — physics
    // -------------------------------------------------------------------------

    private Rigidbody _rb;

    // -------------------------------------------------------------------------
    // Private — input actions (original Input System wiring)
    // -------------------------------------------------------------------------

    private InputActions _actions;

    // -------------------------------------------------------------------------
    // Cmd throttle
    // -------------------------------------------------------------------------

    private float _lastSentSteer;
    private bool _lastSentBrake;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_rb == null)
            Debug.LogError("[VehicleController] No Rigidbody found on this GameObject.", this);

        // Snapshot neutral rotations before anything moves. Done in Awake
        // (not in OnStartLocalPlayer) so it works in solo play too, where
        // Mirror never fires that callback.
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
        // -- Networking routing (the only thing added vs. the original) -----
        //
        // Decide which input sample feeds the simulation this tick. In solo /
        // offline play we keep the original behaviour untouched: _steerInput
        // and _brakeInput come straight from the input callbacks.
        //
        // In a network session:
        //   • Host (server + client)   : same — no round-trip needed.
        //   • Pure server (not owner)  : mirror populated by Cmd_SetInput.
        //   • Owner client (not host)  : originals, AND publish a Cmd so the
        //                                 server can validate / replicate.
        //   • Non-owner client         : skip; visual comes from SyncVar hook.
        if (!IsOffline())
        {
            if (isServer && !isOwned)
            {
                // Pure server (dedicated or host whose player is someone else).
                _steerInput = _serverSteerInput;
                _brakeInput = _serverBrakeInput;
            }
            else if (isOwned && !isServer)
            {
                // Pure owner client — relay inputs to the server.
                if (Mathf.Abs(_steerInput - _lastSentSteer) >= _steerSendThreshold
                    || _brakeInput != _lastSentBrake)
                {
                    _lastSentSteer = _steerInput;
                    _lastSentBrake = _brakeInput;
                    Cmd_SetInput(_steerInput, _brakeInput);
                }
            }
            else if (!isServer && !isOwned)
            {
                // Non-owner, non-server client: nothing to simulate here.
                return;
            }
        }

        // -- Original pipeline, unchanged ------------------------------------
        ClampSpeed();
        HandleSteering();
        HandleBraking();

        // -- Publish SyncVar (server / host only) -----------------------------
        // The local HandleSteering already applied the rotation to the wheels,
        // so we only need to push the value out for remote clients to mirror.
        if (isServer)
            _syncedSteer = _steerInput;
    }

    // -------------------------------------------------------------------------
    // Input callbacks (unchanged from the original)
    // -------------------------------------------------------------------------

    private void OnSteer(InputAction.CallbackContext ctx)
        => _steerInput = ctx.ReadValue<float>();

    private void OnBrake(InputAction.CallbackContext ctx)
        => _brakeInput = ctx.ReadValueAsButton();

    // -------------------------------------------------------------------------
    // Command — owner → server
    // -------------------------------------------------------------------------

    [Command]
    private void Cmd_SetInput(float steer, bool brake)
    {
        _serverSteerInput = Mathf.Clamp(steer, -1f, 1f);
        _serverBrakeInput = brake;
    }

    // -------------------------------------------------------------------------
    // SyncVar hook — non-authoritative clients apply the replicated angle
    // -------------------------------------------------------------------------

    private void OnSyncedSteerChanged(float _, float newValue)
    {
        // Only act on instances that are not running the simulation themselves.
        if (IsOffline() || isServer || isOwned) return;

        // Visual: same math as the original HandleSteering, but driven by the
        // replicated value instead of the local _steerInput. We use a
        // MoveTowards so the rotation interpolates smoothly toward the target,
        // which is what the original does on the owner side.
        float targetAngle = newValue * _maxSteerAngle;
        _currentSteerAngle = Mathf.MoveTowards(
            _currentSteerAngle,
            targetAngle,
            _steerSpeed * _maxSteerAngle * Time.fixedDeltaTime
        );

        ApplySteeringRotation(_currentSteerAngle);
    }

    // -------------------------------------------------------------------------
    // Private physics helpers (unchanged from the original)
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

    /// <summary>Applies <paramref name="angle"/> to every steering wheel transform.</summary>
    private void ApplySteeringRotation(float angle)
    {
        for (int i = 0; i < _steeringWheels.Length; i++)
        {
            if (_steeringWheels[i] == null) continue;

            _steeringWheels[i].transform.localRotation =
                _steeringNeutralRotations[i] * Quaternion.Euler(0f, angle, 0f);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// True when no Mirror session is active (solo / offline play).
    /// Used to keep the original behaviour intact when no NetworkIdentity
    /// is in the hierarchy.
    /// </summary>
    private bool IsOffline() => !NetworkServer.active && !NetworkClient.active;
}
