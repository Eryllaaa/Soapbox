using UnityEngine;
using UnityEngine.UI;
using Mirror;
using Soapbox.Networking;

namespace Soapbox.Menu
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Steam UI")]
        [SerializeField] private GameObject steamPanel;
        [SerializeField] private Button hostSteamButton;
        [SerializeField] private Button joinSteamButton;

        [Header("LAN UI")]
        [SerializeField] private GameObject lanPanel;
        [SerializeField] private Button hostLanButton;
        [SerializeField] private Button joinLanButton;
        [SerializeField] private InputField ipInputField; 
        [SerializeField] private Button quitButton;

        private SteamLobbyManager steamLobbyManager;
        private Transport defaultMultiplexTransport;

        private void Start()
        {
            steamLobbyManager = FindFirstObjectByType<SteamLobbyManager>();

            // On mémorise le transport de base (Multiplex)
            defaultMultiplexTransport = NetworkManager.singleton.transport;

            // --- Bindings Steam ---
            hostSteamButton.onClick.AddListener(() => steamLobbyManager.HostLobby());
            joinSteamButton.onClick.AddListener(() => steamLobbyManager.OpenFriendsOverlay());

            // --- Bindings LAN ---
            hostLanButton.onClick.AddListener(() => 
            {
                // On remet le Multiplex pour le Host LAN (pour que Steam puisse écouter en background)
                NetworkManager.singleton.transport = defaultMultiplexTransport;
                Transport.active = defaultMultiplexTransport; // 🛠 FIX 🛠
                NetworkManager.singleton.StartHost();
            });

            joinLanButton.onClick.AddListener(() => 
            {
                string ip = string.IsNullOrEmpty(ipInputField.text) ? "localhost" : ipInputField.text;
                
                // On isole le transport LAN pour éviter l'erreur SteamID
                foreach (var t in NetworkManager.singleton.GetComponents<Transport>())
                {
                    if (t != defaultMultiplexTransport && t.GetType().Name != "FizzySteamworks")
                    {
                        NetworkManager.singleton.transport = t; 
                        Transport.active = t; // 🛠 FIX : On force Mirror à l'utiliser ! 🛠
                        break;
                    }
                }

                NetworkManager.singleton.networkAddress = ip;
                NetworkManager.singleton.StartClient();
            });

            // --- Quitter ---
            if (quitButton != null) 
                quitButton.onClick.AddListener(() => {
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #else
                    Application.Quit();
                    #endif
                });

            bool steamReady = SteamManager.Initialized;
            if (steamPanel != null) steamPanel.SetActive(steamReady);
        }
    }
}