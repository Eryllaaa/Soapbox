using Mirror;
using Steamworks;
using UnityEngine;

namespace Soapbox.Networking
{
    [RequireComponent(typeof(NetworkManager))]
    public class SteamLobbyManager : MonoBehaviour
    {
        private NetworkManager networkManager;
        private Transport defaultMultiplexTransport;
        
        protected Callback<LobbyCreated_t> lobbyCreated;
        protected Callback<GameLobbyJoinRequested_t> joinRequested;
        protected Callback<LobbyEnter_t> lobbyEntered;

        private const string HostAddressKey = "HostAddress";
        public CSteamID CurrentLobbyId { get; private set; } = CSteamID.Nil;
        public bool HasLobby => CurrentLobbyId.IsValid();

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            defaultMultiplexTransport = networkManager.transport;
        }

        private void Start()
        {
            if (!SteamManager.Initialized) return;

            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        public void HostLobby()
        {
            if (!SteamManager.Initialized) return;

            // 🛠 FIX 🛠
            networkManager.transport = defaultMultiplexTransport;
            Transport.active = defaultMultiplexTransport;
            
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, networkManager.maxConnections);
        }

        public void OpenFriendsOverlay()
        {
            if (SteamManager.Initialized) SteamFriends.ActivateGameOverlay("Friends");
        }

        public void InviteFriend()
        {
            if (HasLobby) SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyId);
        }

        public void DisconnectLobby()
        {
            if (HasLobby) SteamMatchmaking.LeaveLobby(CurrentLobbyId);
            CurrentLobbyId = CSteamID.Nil;
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK) return;

            CurrentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
            
            networkManager.StartHost();
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            CurrentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            if (NetworkServer.active) return; // Si host, Mirror est déjà lancé

            // 🛠 FIX 🛠
            networkManager.transport = defaultMultiplexTransport;
            Transport.active = defaultMultiplexTransport;

            networkManager.networkAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);
            networkManager.StartClient();
        }
    }
}