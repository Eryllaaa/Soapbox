using Mirror;
using Steamworks;
using UnityEngine;

namespace Soapbox.Networking
{
    [RequireComponent(typeof(NetworkManager))]
    public class SteamLobbyManager : MonoBehaviour
    {
        // Callbacks Steamworks
        protected Callback<LobbyCreated_t> _lobbyCreated;
        protected Callback<GameLobbyJoinRequested_t> _joinRequested;
        protected Callback<LobbyEnter_t> _lobbyEntered;

        private NetworkManager _networkManager;
        private const string HostAddressKey = "HostAddress";

        private void Start()
        {
            _networkManager = GetComponent<NetworkManager>();

            // Si Steam n'est pas allumé, on ne fait rien
            if (!SteamManager.Initialized)
            {
                Debug.LogWarning("[SteamLobbyManager] Steam n'est pas initialisé ou en cours d'exécution !");
                return;
            }

            // On abonne nos fonctions aux événements de Steam
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        // ========================================================================
        // 1. APPELÉ PAR TON BOUTON UI "HOST" (ou intercepté par le NetworkManager)
        // ========================================================================
        public void HostLobby()
        {
            if (!SteamManager.Initialized) return;

            Debug.Log("[SteamLobbyManager] Création du lobby Steam en cours...");
            // Crée un lobby "Amis Uniquement" avec le nombre max de joueurs défini dans le NetworkManager
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _networkManager.maxConnections);
        }

        // ========================================================================
        // 2. QUAND LE LOBBY EST CRÉÉ SUR LES SERVEURS STEAM
        // ========================================================================
        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError("[SteamLobbyManager] Erreur lors de la création du lobby !");
                return;
            }

            Debug.Log("[SteamLobbyManager] Lobby créé avec succès ! Démarrage du Host Mirror...");

            // On enregistre notre Steam ID dans les données du lobby pour que les clients puissent s'y connecter
            CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());

            // On lance le vrai Host Mirror via le bypass pour éviter la boucle infinie
            if (_networkManager is SoapboxNetworkManager soapboxNM)
            {
                soapboxNM.StartHostBypass();
            }
            else
            {
                _networkManager.StartHost();
            }
        }

        // ========================================================================
        // 3. QUAND UN AMI ACCEPTE TON INVITATION VIA L'OVERLAY STEAM
        // ========================================================================
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            Debug.Log("[SteamLobbyManager] Demande de connexion acceptée via Steam !");
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        // ========================================================================
        // 4. QUAND LE CLIENT ENTRE DANS LE LOBBY
        // ========================================================================
        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            // Si on est le Host, on ne fait rien (on est déjà connecté)
            if (NetworkServer.active) return;

            Debug.Log("[SteamLobbyManager] Entrée dans le lobby Steam réussie. Démarrage du Client Mirror...");

            // On récupère le SteamID de l'hôte stocké dans les données du lobby
            string hostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey);

            // On donne ce SteamID à Mirror pour qu'il sache où se connecter via FizzySteamworks
            _networkManager.networkAddress = hostAddress;
            _networkManager.StartClient();
        }
    }
}