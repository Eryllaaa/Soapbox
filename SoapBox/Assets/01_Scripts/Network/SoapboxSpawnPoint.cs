using System.Collections.Generic;
using UnityEngine;

namespace Soapbox.Networking
{
    /// <summary>
    /// Marker component placed on scene Transforms where vehicles should spawn.
    ///
    /// Why a custom marker instead of Mirror's <c>NetworkStartPosition</c>?
    ///   • We can group spawn points per team / per lobby without forcing the
    ///     user to drop empty GOs around the level.
    ///   • We can draw a gizmo (arrow) so level designers see facing direction.
    ///   • The NetworkManager auto-registers/unregisters them via
    ///     <c>OnEnable</c> / <c>OnDisable</c>, no manual wiring required.
    ///
    /// Set <see cref="_team"/> to a non-empty value to gate spawn usage to a
    /// matching key on the connection. Leave empty for "any team".
    /// </summary>
    [DisallowMultipleComponent]
    public class SoapboxSpawnPoint : MonoBehaviour
    {
        private static readonly List<SoapboxSpawnPoint> Registry = new();

        [Tooltip("Optional tag. Only connections that pass the same tag will spawn here. " +
                 "Leave empty to allow any connection.")]
        [SerializeField] private string _team = "";

        [Tooltip("If true, the spawn is consumed once a player has used it. " +
                 "Useful for race starts where each player gets a unique slot.")]
        [SerializeField] private bool _singleUse;

        private bool _consumed;

        public string Team => _team;
        public bool IsAvailable => !_consumed;

        // -------------------------------------------------------------------------
        // Registry
        // -------------------------------------------------------------------------

        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        public static IReadOnlyList<SoapboxSpawnPoint> All => Registry;

        // -------------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------------

        /// <summary>
        /// Picks the first available spawn point. <paramref name="team"/> can be
        /// null/empty to ignore the team filter.
        /// Returns null if nothing matches.
        /// </summary>
        public static SoapboxSpawnPoint Pick(string team = null)
        {
            // Drop any destroyed entries without mutating the list during iteration.
            Registry.RemoveAll(sp => sp == null);

            for (int i = 0; i < Registry.Count; i++)
            {
                SoapboxSpawnPoint sp = Registry[i];
                if (sp == null || !sp.IsAvailable) continue;
                if (!string.IsNullOrEmpty(team) && sp._team != team) continue;

                if (sp._singleUse) sp._consumed = true;
                return sp;
            }

            return null;
        }

        /// <summary>
        /// Releases a previously consumed single-use spawn point so it can be
        /// reused (e.g. after a round restart).
        /// </summary>
        public static void ReleaseAll()
        {
            for (int i = 0; i < Registry.Count; i++)
            {
                if (Registry[i] != null) Registry[i]._consumed = false;
            }
        }

        // -------------------------------------------------------------------------
        // Editor
        // -------------------------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _consumed ? new Color(1f, 0.4f, 0.4f, 0.5f) : new Color(0.3f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            Vector3 fwd = transform.forward * 1.2f;
            Gizmos.DrawLine(transform.position, transform.position + fwd);
            Gizmos.DrawLine(transform.position + fwd, transform.position + fwd - transform.right * 0.3f + transform.forward * 0.3f);
            Gizmos.DrawLine(transform.position + fwd, transform.position + fwd + transform.right * 0.3f + transform.forward * 0.3f);
        }
#endif
    }
}
