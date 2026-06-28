using Mirror;
using UnityEngine;

namespace Soapbox.Vehicles
{
    /// <summary>
    /// Top-level networked controller for a soapbox racer.
    ///
    /// Responsibilities (strictly separated):
    ///   • Inputs   — receive owner inputs (via <see cref="SoapboxInputReader"/>)
    ///                and relay them to the server with a throttled Command.
    ///   • Sim      — steer the visual wheels and toggle brake on Wheel when this
    ///                instance owns the simulation (host, owning client, or solo).
    ///   • Present  — drive cosmetic steering rotation from a <c>SyncVar</c> so
    ///                every client sees the wheels turn, even when this instance
    ///                does not own the car.
    ///
    /// Authority model: owner-authoritative.
    /// The owning client predicts locally (zero latency feel) and sends inputs
    /// to the server. Physics are reconciled by <c>PredictedRigidbody</c>.
    ///
    /// Solo / offline play: when no Mirror session is active and the
    /// <see cref="_treatAsLocalOwnerWhenOffline"/> toggle is on, the vehicle
    /// behaves as its own authority. Inputs come from
    /// <see cref="SetLocalInput"/> as usual.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : NetworkBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector — wheel groups
        // -------------------------------------------------------------------------

        [Header("Wheel Groups")]
        [Tooltip("Wheels that will receive steering rotation.")]
        [SerializeField] private Wheel[] _steeringWheels;

        [Tooltip("Wheels that will receive braking commands.")]
        [SerializeField] private Wheel[] _brakeWheels;

        // -------------------------------------------------------------------------
        // Inspector — steering
        // -------------------------------------------------------------------------

        [Header("Steering")]
        [Tooltip("Max angle the steering wheels reach at full input. " +
                 "Higher = tighter turning radius but more twitchy.")]
        [SerializeField, Min(0f)] private float _maxSteerAngle = 10f;

        [Tooltip("Steering interpolation speed, multiplied by _maxSteerAngle " +
                 "to get degrees-per-second. Higher = more direct response, " +
                 "lower = more progressive/understeery feel.")]
        [SerializeField, Min(0f)] private float _steerSpeed = 3f;

        // -------------------------------------------------------------------------
        // Inspector — physics
        // -------------------------------------------------------------------------

        [Header("Speed Limit")]
        [Tooltip("Hard maximum speed (m/s). Clamped on whichever instance owns the simulation.")]
        [SerializeField, Min(0f)] private float _maxSpeed = 30f;

        // -------------------------------------------------------------------------
        // Inspector — network
        // -------------------------------------------------------------------------

        [Header("Network")]
        [Tooltip("Minimum delta on the steering axis to relay a new Cmd. Keeps bandwidth low while idle.")]
        [SerializeField, Min(0f)] private float _steerSendThreshold = 0.01f;

        [Tooltip("If true, the vehicle treats itself as the local authority whenever " +
                 "no network session is active (solo / offline play). Lets the " +
                 "controller drive the vehicle without Mirror assigning authority.")]
        [SerializeField] private bool _treatAsLocalOwnerWhenOffline = true;

        [Header("Debug")]
        [Tooltip("If true, logs a line every FixedUpdate with the current input / " +
                 "angle / authority state. Useful to figure out why steering is dead. " +
                 "Also enables an on-screen HUD via OnGUI.")]
        [SerializeField] private bool _debugLogs = true;

        [Tooltip("Throttles the debug log: only one line every N FixedUpdate ticks. " +
                 "Set to 1 to log every tick, 60 to log roughly once per second @ 50Hz.")]
        [SerializeField, Min(1)] private int _debugLogEveryNthTick = 30;

        // -------------------------------------------------------------------------
        // Replicated state (SyncVars) — used for cosmetic steering on remote clients
        // -------------------------------------------------------------------------

        [SyncVar(hook = nameof(OnSteerChanged))]
        private float _syncedSteer;

        [SyncVar(hook = nameof(OnBrakeChanged))]
        private bool _syncedBrake;

        // -------------------------------------------------------------------------
        // Inputs — local (owner / solo / host)
        // -------------------------------------------------------------------------

        private float _localSteerInput;
        private bool _localBrakeInput;

        // -------------------------------------------------------------------------
        // Inputs — server-side mirror of the latest received Cmd (non-host server only)
        // -------------------------------------------------------------------------

        private float _serverSteerInput;
        private bool _serverBrakeInput;

        // -------------------------------------------------------------------------
        // Sim state — local (owner / host / solo)
        // -------------------------------------------------------------------------

        private float _currentSteerAngle;
        private Quaternion[] _steeringNeutralRotations;

        // -------------------------------------------------------------------------
        // Present state — all instances (cosmetic)
        // -------------------------------------------------------------------------

        private float _presentSteerAngle;

        // -------------------------------------------------------------------------
        // Cmd throttle
        // -------------------------------------------------------------------------

        private float _lastSentSteer;
        private bool _lastSentBrake;

        // -------------------------------------------------------------------------
        // Debug
        // -------------------------------------------------------------------------

        private int _debugTickCounter;
        private bool _hasLoggedAwake;

        // -------------------------------------------------------------------------
        // Cached components
        // -------------------------------------------------------------------------

        private Rigidbody _rb;
        private SoapboxInputReader _inputReader;

        // -------------------------------------------------------------------------
        // Runtime flags
        // -------------------------------------------------------------------------

        private bool _runningOffline;

        // -------------------------------------------------------------------------
        // Public API (called by SoapboxInputReader)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Push the latest local input sample from the input system into the
        /// controller. Only meaningful on the owning instance (or in solo play
        /// with <see cref="_treatAsLocalOwnerWhenOffline"/> enabled).
        /// </summary>
        public void SetLocalInput(float steer, bool brake)
        {
            if (_debugLogs && !_hasLoggedAwake)
            {
                // One-shot boot diagnostic so we can see exactly why input is being rejected.
                Debug.Log(
                    $"[VehicleController] Boot diagnostic on '{name}': " +
                    $"runningOffline={_runningOffline} treatAsLocalOwner={_treatAsLocalOwnerWhenOffline} " +
                    $"isServer={isServer} isClient={NetworkClient.active} isOwned={isOwned} " +
                    $"=> IsLocalAuthority={IsLocalAuthority}",
                    this);
                _hasLoggedAwake = true;
            }

            if (!IsLocalAuthority)
            {
                if (_debugLogs)
                    Debug.LogWarning($"[VehicleController] SetLocalInput('{name}') rejected — " +
                                     $"not the local authority. isServer={isServer} isOwned={isOwned} " +
                                     $"runningOffline={_runningOffline}", this);
                return;
            }

            _localSteerInput = Mathf.Clamp(steer, -1f, 1f);
            _localBrakeInput = brake;
        }

        // -------------------------------------------------------------------------
        // NetworkBehaviour lifecycle
        // -------------------------------------------------------------------------

        public override void OnStartLocalPlayer() => CacheNeutralRotations();
        public override void OnStartClient() => CacheNeutralRotations();

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
                Debug.LogError("[VehicleController] No Rigidbody found on this GameObject.", this);

            // Detect solo play at boot. If Mirror is not running and the user
            // asked for offline-as-owner, we behave as the local authority.
            _runningOffline = !NetworkServer.active && !NetworkClient.active;

            // Auto-wire input: if no SoapboxInputReader is present in the scene
            // / prefab, add one. This avoids the "I edited the script but the
            // prefab still has nothing bound" footgun.
            _inputReader = GetComponent<SoapboxInputReader>();
            if (_inputReader == null)
            {
                _inputReader = gameObject.AddComponent<SoapboxInputReader>();
                if (_debugLogs)
                    Debug.LogWarning(
                        $"[VehicleController] No SoapboxInputReader found on '{name}' — " +
                        "auto-added one at runtime. Add it explicitly to the prefab " +
                        "to silence this warning.", this);
            }

            // Last-resort fallback: read keyboard directly if no input action
            // ever produces a value. Useful when the InputActionAsset / generated
            // wrapper fails to bind for any reason. Driven from Update().
        }

        // No Update() anymore: steering rotation is written in the physics
        // step (FixedUpdate) on the authority, and via the SyncVar hook on
        // non-authoritative clients. Both paths use ApplySteeringRotation,
        // so the wheels stay perfectly in sync with the simulation.

        // -------------------------------------------------------------------------
        // Debug HUD
        // -------------------------------------------------------------------------

        private void OnGUI()
        {
            if (!_debugLogs) return;

            const int w = 380;
            const int h = 192;
            const int pad = 8;
            GUI.Box(new Rect(pad, pad, w, h), "VehicleController (debug)");

            float y = pad + 22;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"runningOffline={_runningOffline}  treatOffline={_treatAsLocalOwnerWhenOffline}");
            y += 18;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"isServer={isServer}  isClient={NetworkClient.active}  isOwned={isOwned}");
            y += 18;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"IsLocalAuthority={IsLocalAuthority}  netId={(netIdentity != null ? netIdentity.netId : 0)}");
            y += 18;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"localSteer={_localSteerInput:F2}  localBrake={_localBrakeInput}");
            y += 18;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"currentAngle={_currentSteerAngle:F1}°  presentAngle={_presentSteerAngle:F1}°");
            y += 18;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"steerWheels={(_steeringWheels != null ? _steeringWheels.Length : 0)}  " +
                $"brakeWheels={(_brakeWheels != null ? _brakeWheels.Length : 0)}");
            y += 18;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"neutralRot={(_steeringNeutralRotations != null ? _steeringNeutralRotations.Length : 0)}");
            y += 18;
            GUI.Label(new Rect(pad + 8, y, w, 18),
                $"inputReader={(_inputReader != null ? _inputReader.GetType().Name : "<null>")} " +
                $"enabled={(_inputReader != null && _inputReader.enabled)}");
        }

        private void FixedUpdate()
        {
            // --- 0. Solo / offline mode: drive from local inputs directly. ---
            if (_runningOffline && _treatAsLocalOwnerWhenOffline)
            {
                ClampSpeed();
                ApplySimulation(_localSteerInput, _localBrakeInput);
                // Keep _presentSteerAngle in sync for HUD / external systems
                // that might read it. The wheel transforms themselves are
                // already up-to-date from inside ApplySimulation.
                _presentSteerAngle = _currentSteerAngle;

                if (_debugLogs)
                {
                    _debugTickCounter++;
                    if (_debugTickCounter >= _debugLogEveryNthTick)
                    {
                        _debugTickCounter = 0;
                        Debug.Log(
                            $"[VehicleController] SOLO tick '{name}': " +
                            $"localSteer={_localSteerInput:F2} localBrake={_localBrakeInput} " +
                            $"currentAngle={_currentSteerAngle:F1}° presentAngle={_presentSteerAngle:F1}° " +
                            $"steerWheels={(_steeringWheels != null ? _steeringWheels.Length : 0)}",
                            this);
                    }
                }
                return;
            }

            // --- 1. Decide the latest authoritative input sample for this tick ---
            //
            // • Host (server + client of the same machine, isServer && isOwned):
            //     reads from local input directly. No Cmd round-trip.
            // • Pure server (isServer && !isOwned):
            //     reads from the Cmd buffer populated by Cmd_SetInput.
            // • Pure client owner (isOwned && !isServer):
            //     reads from local input directly (predicted locally) AND sends
            //     a Cmd so the server can validate / re-broadcast.
            // • Non-owner clients:
            //     skip — they only consume the SyncVars for cosmetics.

            float simSteer;
            bool simBrake;

            if (isServer && isOwned)
            {
                simSteer = _localSteerInput;
                simBrake = _localBrakeInput;
            }
            else if (isServer)
            {
                simSteer = _serverSteerInput;
                simBrake = _serverBrakeInput;
            }
            else if (isOwned)
            {
                simSteer = _localSteerInput;
                simBrake = _localBrakeInput;

                // Throttled Cmd toward the server.
                if (Mathf.Abs(simSteer - _lastSentSteer) >= _steerSendThreshold || simBrake != _lastSentBrake)
                {
                    _lastSentSteer = simSteer;
                    _lastSentBrake = simBrake;
                    Cmd_SetInput(simSteer, simBrake);
                }
            }
            else
            {
                if (_debugLogs)
                {
                    _debugTickCounter++;
                    if (_debugTickCounter >= _debugLogEveryNthTick)
                    {
                        _debugTickCounter = 0;
                        Debug.LogWarning(
                            $"[VehicleController] MULTI tick '{name}' is in the dead branch: " +
                            $"not server, not owner, not solo — so the sim is skipped. " +
                            $"isServer={isServer} isClient={NetworkClient.active} isOwned={isOwned} " +
                            $"runningOffline={_runningOffline} netId={(netIdentity != null ? netIdentity.netId : 0)}",
                            this);
                    }
                }
                return;
            }

            // --- 2. Publish to SyncVars (server / host only). On non-authority
            //        clients, OnSteerChanged / OnBrakeChanged fire from the
            //        network layer instead. ---
            if (isServer)
            {
                _syncedSteer = simSteer;
                _syncedBrake = simBrake;
            }

            // --- 3. Drive the local simulation on whichever instance is authority. ---
            if (IsLocalAuthority)
            {
                ClampSpeed();
                ApplySimulation(simSteer, simBrake);
            }
        }

        // -------------------------------------------------------------------------
        // Command — owner -> server
        // -------------------------------------------------------------------------

        [Command]
        private void Cmd_SetInput(float steer, bool brake)
        {
            _serverSteerInput = Mathf.Clamp(steer, -1f, 1f);
            _serverBrakeInput = brake;
        }

        // -------------------------------------------------------------------------
        // SyncVar hooks — drive cosmetic state on every client
        // -------------------------------------------------------------------------

        private void OnSteerChanged(float _, float newValue)
        {
            // Non-authoritative clients: apply the replicated angle to the
            // wheel transforms immediately. Wheel physics is disabled for them
            // via NetworkOwnershipGate, so this is purely visual — but it must
            // happen here (not in Update) so the visual stays in sync with
            // the network tick that delivered the value.
            _presentSteerAngle = newValue;
            if (!IsLocalAuthority)
                ApplySteeringRotation(newValue);
        }

        private void OnBrakeChanged(bool _, bool newValue)
        {
            // Cosmetic only — non-authoritative clients must NOT push Brake()
            // into Wheel physics, because Wheel is disabled for them via
            // NetworkOwnershipGate. Hook is here for future UI/audio cues.
            _ = newValue;
        }

        // -------------------------------------------------------------------------
        // Authority helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// True when this instance is allowed to act on local inputs.
        /// Covers all roles:
        ///   • Solo / offline (when the toggle is on),
        ///   • Host (isServer && isOwned),
        ///   • Dedicated server (isServer),
        ///   • Owner client (isOwned && !isServer).
        /// </summary>
        private bool IsLocalAuthority =>
            (_runningOffline && _treatAsLocalOwnerWhenOffline) || isServer || isOwned;

        // -------------------------------------------------------------------------
        // Sim (authority only)
        // -------------------------------------------------------------------------

        private void ApplySimulation(float sourceSteer, bool sourceBrake)
        {
            // Smooth steering interpolation toward target.
            float targetAngle = sourceSteer * _maxSteerAngle;
            _currentSteerAngle = Mathf.MoveTowards(
                _currentSteerAngle,
                targetAngle,
                _steerSpeed * _maxSteerAngle * Time.fixedDeltaTime
            );

            // Apply rotation to wheel transforms HERE (not in Update).
            // Wheel.cs reads transform.right in its own FixedUpdate to compute
            // side friction — applying the rotation in the same physics step
            // keeps the steering input and the resulting lateral force in lockstep,
            // which is what the original controller did and what makes the
            // vehicle feel responsive.
            ApplySteeringRotation(_currentSteerAngle);

            // Brake wheels.
            for (int i = 0; i < _brakeWheels.Length; i++)
            {
                Wheel w = _brakeWheels[i];
                if (w == null) continue;
                if (sourceBrake) w.Brake();
                else w.StopBraking();
            }
        }

        private void ClampSpeed()
        {
            if (_rb == null || _rb.isKinematic) return;

            Vector3 v = _rb.linearVelocity;
            if (v.sqrMagnitude > _maxSpeed * _maxSpeed)
                _rb.linearVelocity = v.normalized * _maxSpeed;
        }

        // -------------------------------------------------------------------------
        // Steering rotation (single source of truth)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Writes <paramref name="angle"/> into the local rotation of every
        /// steering wheel, relative to the cached neutral rotation. Called from
        /// the physics step on the authority, and from the SyncVar hook on
        /// non-authoritative clients — never from Update.
        /// </summary>
        private void ApplySteeringRotation(float angle)
        {
            if (_steeringWheels == null || _steeringNeutralRotations == null) return;
            if (_steeringWheels.Length != _steeringNeutralRotations.Length) return;

            for (int i = 0; i < _steeringWheels.Length; i++)
            {
                Wheel w = _steeringWheels[i];
                if (w == null) continue;

                w.transform.localRotation =
                    _steeringNeutralRotations[i] * Quaternion.Euler(0f, angle, 0f);
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private void CacheNeutralRotations()
        {
            if (_steeringWheels == null) return;

            _steeringNeutralRotations = new Quaternion[_steeringWheels.Length];
            for (int i = 0; i < _steeringWheels.Length; i++)
            {
                if (_steeringWheels[i] != null)
                    _steeringNeutralRotations[i] = _steeringWheels[i].transform.localRotation;
            }
        }
    }
}
