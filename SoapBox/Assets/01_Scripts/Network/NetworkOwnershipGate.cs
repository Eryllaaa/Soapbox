using Mirror;
using UnityEngine;

namespace Soapbox.Networking
{
    /// <summary>
    /// Helper used by components that must only run on the instance of an
    /// object that "owns" the simulation (host or the client that has authority).
    ///
    /// In single-player (no NetworkIdentity anywhere in the scene) every
    /// component is allowed to run, which keeps the existing test scenes working.
    ///
    /// In multiplayer:
    ///   • owner of the NetworkIdentity  -> component stays enabled
    ///   • everyone else                -> component is disabled
    ///
    /// Use from <c>OnEnable()</c> (NOT <c>Awake()</c>) on any MonoBehaviour
    /// that touches the Rigidbody directly — Mirror's <see cref="NetworkIdentity"/>
    /// may not be fully initialised during Awake on a network spawn, which would
    /// cause the gate to falsely disable an owner-side component.
    ///
    /// The function is safe to call repeatedly: once the gate has disabled a
    /// component it does not touch it again, and re-enabling is the caller's
    /// responsibility.
    /// </summary>
    public static class NetworkOwnershipGate
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="behaviour"/> should keep
        /// running. If it returns <c>false</c> the component has already been
        /// disabled — do not re-enable it manually without calling this again
        /// from a context where Mirror is fully initialised.
        /// </summary>
        public static bool KeepLocal(Behaviour behaviour)
        {
            if (behaviour == null) return false;

            NetworkIdentity identity = behaviour.GetComponentInParent<NetworkIdentity>();

            // Solo / no networking: every component runs.
            if (identity == null) return true;

            // Mirror may not have populated isOwned yet on a freshly spawned
            // network object — give it a chance on the next frame by leaving
            // the component enabled. The next OnEnable cycle will gate it
            // correctly. We detect this by checking whether the NetworkIdentity
            // has been assigned a netId yet (zero means "not spawned").
            if (identity.netId == 0 && !identity.isServer)
            {
                // Stay enabled; re-evaluate later.
                return true;
            }

            // Host (server + client of the same machine) runs everything
            // because it both owns and serves the object.
            if (NetworkServer.active && NetworkClient.active && identity.isOwned) return true;

            // Pure server-side identity (e.g. AI, NPC) runs everywhere on the server.
            if (NetworkServer.active && identity.isServer) return true;

            // Pure client: keep the component only on the locally owned instance.
            if (identity.isOwned) return true;

            behaviour.enabled = false;
            return false;
        }
    }
}
