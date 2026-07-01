using UnityEngine;
using UnityEngine.UI;
using Mirror;
using Soapbox.Networking;

namespace Soapbox.Menu
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button startRaceButton;
        [SerializeField] private Button inviteFriendButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Text statusText;

        [Header("Settings")]
        [SerializeField] private string raceSceneName = "JhiderScene";

        private SteamLobbyManager steamLobbyManager;

        private void Start()
        {
            steamLobbyManager = FindFirstObjectByType<SteamLobbyManager>();

            // Le Host charge la scène pour tout le monde
            startRaceButton.onClick.AddListener(() => 
            {
                if (NetworkServer.active) NetworkManager.singleton.ServerChangeScene(raceSceneName);
            });

            inviteFriendButton.onClick.AddListener(() => steamLobbyManager.InviteFriend());
            
            leaveButton.onClick.AddListener(() => 
            {
                steamLobbyManager.DisconnectLobby();
                if (NetworkServer.active && NetworkClient.isConnected) NetworkManager.singleton.StopHost();
                else if (NetworkClient.isConnected) NetworkManager.singleton.StopClient();
                else if (NetworkServer.active) NetworkManager.singleton.StopServer();
            });
        }

        private void Update()
        {
            // Seul l'hôte peut lancer la course
            if (startRaceButton != null && startRaceButton.gameObject.activeSelf != NetworkServer.active)
                startRaceButton.gameObject.SetActive(NetworkServer.active);
        }

        private void OnEnable()
        {
            EventManager.OnSteamLobbyAvailabilityChanged += UpdateInviteButton;
            EventManager.OnLobbyRosterChanged += UpdateRosterDisplay;
        }

        private void OnDisable()
        {
            EventManager.OnSteamLobbyAvailabilityChanged -= UpdateInviteButton;
            EventManager.OnLobbyRosterChanged -= UpdateRosterDisplay;
        }

        private void UpdateInviteButton(bool hasLobby)
        {
            if (inviteFriendButton != null) inviteFriendButton.gameObject.SetActive(hasLobby);
        }

        private void UpdateRosterDisplay(int current, int max, string roster)
        {
            if (statusText != null) statusText.text = $"Joueurs : {current}/{max}\n{roster}";
            if (startRaceButton != null) startRaceButton.interactable = current >= 1; // Ajuster selon tes besoins
        }
    }
}