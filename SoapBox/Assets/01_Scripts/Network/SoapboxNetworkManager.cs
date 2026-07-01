using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Soapbox.Networking
{
    public class SoapboxNetworkManager : NetworkManager
    {
        [Header("Soapbox Settings")]
        [SerializeField] private float fallbackSpawnRadius = 6f;

        private int fallbackSpawnIndex;

        public override void Awake()
        {
            base.Awake();
            
            // On réinitialise les spawns si jamais on relance une scène
            SceneManager.sceneLoaded += (scene, mode) => SoapboxSpawnPoint.ReleaseAll();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            (Vector3 pos, Quaternion rot) = ResolveSpawnPose();

            GameObject player = Instantiate(playerPrefab, pos, rot);
            player.name = $"{playerPrefab.name} [Conn:{conn.connectionId}]";
            
            NetworkServer.AddPlayerForConnection(conn, player);
            UpdateRoster();
        }

        private (Vector3 pos, Quaternion rot) ResolveSpawnPose()
        {
            // 1. Priorité absolue : Notre grille de départ custom
            SoapboxSpawnPoint spawnPoint = SoapboxSpawnPoint.Pick();
            if (spawnPoint != null)
                return (spawnPoint.transform.position, spawnPoint.transform.rotation);

            // 2. Fallback Mirror classique
            Transform mirrorStart = GetStartPosition();
            if (mirrorStart != null)
                return (mirrorStart.position, mirrorStart.rotation);

            // 3. Dernier recours : En cercle (utile pour le Lobby si aucun point n'est posé)
            return (GetFallbackSpawnPosition(), Quaternion.identity);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            UpdateRoster();
        }

        private void UpdateRoster()
        {
            int current = NetworkServer.connections.Count;
            EventManager.EmitLobbyRosterChanged(current, maxConnections, $"Joueurs : {current}");
        }

        private Vector3 GetFallbackSpawnPosition()
        {
            int slot = fallbackSpawnIndex++;
            int slotsPerRing = 8;
            float ring = slot / slotsPerRing;
            float angle = (slot % slotsPerRing) * (360f / slotsPerRing) * Mathf.Deg2Rad;
            float radius = fallbackSpawnRadius * (1f + ring * 0.5f);
            
            return new Vector3(Mathf.Sin(angle) * radius, 0.5f, Mathf.Cos(angle) * radius);
        }
    }
}