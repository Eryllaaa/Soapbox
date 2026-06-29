using Mirror;
using Mirror.FizzySteam;
using System.Collections;
using UnityEngine;

namespace Soapbox.Networking
{
    /// <summary>
    /// Soapbox-specific NetworkManager.
    /// Gère l'apparition des joueurs, l'attachement de la caméra et intègre Steam.
    /// </summary>
    public class SoapboxNetworkManager : NetworkManager
    {
        [Header("Camera")]
        [Tooltip("CameraRig present in the scene that will follow the local player's vehicle. " +
                 "If null, the rig is auto-found via FindFirstObjectByType.")]
        [SerializeField] private Soapbox.CameraSystem.CameraRig _cameraRig;

        [Header("Spawn Fallback")]
        [Tooltip("Used when the scene has no SoapboxSpawnPoint. Spawns are then " +
                 "spread on a circle of this radius around the world origin.")]
        [SerializeField, Min(0f)] private float _fallbackSpawnRadius = 6f;

        [Tooltip("If true, scene SoapboxSpawnPoints are released for reuse when a new round starts. " +
                 "Hook SoapboxSpawnPoint.ReleaseAll() from your round manager.")]
        [SerializeField] private bool _autoReleaseSpawnsOnStartServer;

        private int _fallbackSpawnIndex;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        public override void Awake()
        {
            base.Awake();
            EnsureFizzyTransport();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_autoReleaseSpawnsOnStartServer) SoapboxSpawnPoint.ReleaseAll();
        }

        // =========================================================================
        // STEAM INTERCEPTION (Pour le HUD par défaut de Mirror)
        // =========================================================================

        /// <summary>
        /// Masque le StartHost d'origine (Mirror l'ayant rendu non-virtuel dans ses versions récentes).
        /// On intercepte l'appel pour créer le lobby Steam en premier.
        /// </summary>
        public new void StartHost()
        {
            SteamLobbyManager steamLobby = GetComponent<SteamLobbyManager>();
            
            // Si Steam est là, on lui demande de créer le lobby d'abord
            if (steamLobby != null && SteamManager.Initialized)
            {
                Debug.Log("[SoapboxNetworkManager] Interception de StartHost pour créer le Lobby Steam...");
                steamLobby.HostLobby();
            }
            else
            {
                // Si Steam n'est pas lancé, on lance normalement via base.StartHost()
                base.StartHost();
            }
        }

        /// <summary>
        /// Appelé par le SteamLobbyManager UNE FOIS que le lobby Steam est bel et bien créé.
        /// Évite de rappeler StartHost() en boucle.
        /// </summary>
        public void StartHostBypass()
        {
            base.StartHost();
        }

        // -------------------------------------------------------------------------
        // Spawn — server side
        // -------------------------------------------------------------------------

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[SoapboxNetworkManager] playerPrefab is not assigned.", this);
                return;
            }

            // 1. Try our own scene markers first.
            // 2. Fall back to Mirror's NetworkStartPosition (uses round-robin).
            // 3. Last resort: circle-of-spawns.
            (Vector3 pos, Quaternion rot) = ResolveSpawnPose();

            GameObject player = Instantiate(playerPrefab, pos, rot);
            player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
            NetworkServer.AddPlayerForConnection(conn, player);
        }

        private (Vector3 pos, Quaternion rot) ResolveSpawnPose()
        {
            SoapboxSpawnPoint sp = SoapboxSpawnPoint.Pick();
            if (sp != null)
                return (sp.transform.position, sp.transform.rotation);

            Transform mirrorStart = GetStartPosition();
            if (mirrorStart != null)
                return (mirrorStart.position, mirrorStart.rotation);

            return (GetFallbackSpawnPosition(), Quaternion.identity);
        }

        private Vector3 GetFallbackSpawnPosition()
        {
            // Spread players evenly around a circle, just above ground.
            int slot = _fallbackSpawnIndex++;
            const int slotsPerRing = 8;
            float ring = slot / slotsPerRing;
            float angle = (slot % slotsPerRing) * (360f / slotsPerRing) * Mathf.Deg2Rad;
            float radius = _fallbackSpawnRadius * (1f + ring * 0.5f);

            return new Vector3(Mathf.Sin(angle) * radius, 0.5f, Mathf.Cos(angle) * radius);
        }

        // -------------------------------------------------------------------------
        // Camera — client side (local player only)
        // -------------------------------------------------------------------------

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            // Begin polling: the local player object is spawned by Mirror
            // asynchronously after this callback. We try every frame for a
            // short window, then give up silently.
            StartCoroutine(AttachCameraWhenLocalPlayerReady());
        }

        private IEnumerator AttachCameraWhenLocalPlayerReady()
        {
            const float timeoutSeconds = 5f;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (TryAttachCamera()) yield break;
                yield return null;
            }

            Debug.LogWarning("[SoapboxNetworkManager] Timed out waiting for a local player to spawn. " +
                             "Camera will not follow anyone.", this);
        }

        private bool TryAttachCamera()
        {
            Soapbox.CameraSystem.CameraRig rig = _cameraRig != null
                ? _cameraRig
                : FindFirstObjectByType<Soapbox.CameraSystem.CameraRig>();

            if (rig == null) return false;

            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null) return false;

            rig.SetTarget(localPlayer.transform);
            return true;
        }

        // -------------------------------------------------------------------------
        // Transport — auto-pick FizzySteamworks if nothing else is configured
        // -------------------------------------------------------------------------

        private void EnsureFizzyTransport()
        {
            if (transport != null) return;

            FizzySteamworks found = GetComponent<FizzySteamworks>();
            if (found == null) found = FindFirstObjectByType<FizzySteamworks>();

            if (found != null)
            {
                transport = found;
            }
            else
            {
                Debug.LogWarning(
                    "[SoapboxNetworkManager] No FizzySteamworks transport found. " +
                    "Assign one on the NetworkManager component or via this manager's auto-pick.",
                    this);
            }
        }
    }
}