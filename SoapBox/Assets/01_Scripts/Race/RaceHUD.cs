using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace Soapbox.Race
{
    public class RaceHUD : MonoBehaviour
    {
        public Text CountdownText;
        public Text TimerText;
        public Text RankText; // <--- NOUVEAU: Le texte pour afficher "Pos: 1/4"
        public Text LeaderboardText;
        public GameObject FinishPanel;

        private void OnEnable()
        {
            EventManager.OnRaceCountdownTick += HandleCountdownTick;
            EventManager.OnRaceCountdownGo += HandleCountdownGo;
            EventManager.OnRaceFinished += HandleRaceFinished;
            EventManager.OnRaceRestart += HandleRaceRestart;
            EventManager.OnLeaderboardUpdated += HandleLeaderboardUpdated;
        }

        private void OnDisable()
        {
            EventManager.OnRaceCountdownTick -= HandleCountdownTick;
            EventManager.OnRaceCountdownGo -= HandleCountdownGo;
            EventManager.OnRaceFinished -= HandleRaceFinished;
            EventManager.OnRaceRestart -= HandleRaceRestart;
            EventManager.OnLeaderboardUpdated -= HandleLeaderboardUpdated;
        }

        private void Start()
        {
            HandleRaceRestart();
        }

        private void Update()
        {
            if (RaceManager.Instance == null) return;

            // Mise à jour de l'UI en direct uniquement pendant la course
            if (RaceManager.Instance.State == RaceManager.RaceState.Racing)
            {
                // On lit le chrono localement (pas de réseau utilisé)
                TimerText.text = $"Temps: {RaceManager.Instance.GetElapsedTime():F1}s";

                // On cherche notre propre joueur local pour lire son rank
                if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out VehicleCheckpointTracker tracker))
                {
                    int totalPlayers = FindObjectsByType<VehicleCheckpointTracker>(FindObjectsSortMode.None).Length;
                    
                    if (RankText != null)
                    {
                        RankText.gameObject.SetActive(true);
                        RankText.text = $"Position: {tracker.RacePosition} / {totalPlayers}";
                    }
                }
            }
        }

        private void HandleCountdownTick(int time)
        {
            CountdownText.gameObject.SetActive(true);
            CountdownText.text = time.ToString();
        }

        private void HandleCountdownGo()
        {
            CountdownText.text = "GO !";
            Invoke(nameof(HideCountdown), 1f);
        }

        private void HideCountdown() => CountdownText.gameObject.SetActive(false);

        private void HandleRaceFinished()
        {
            FinishPanel.SetActive(true);
        }

        private void HandleRaceRestart()
        {
            FinishPanel.SetActive(false);
            CountdownText.gameObject.SetActive(false);
            TimerText.text = "En attente...";
            if (RankText != null) RankText.gameObject.SetActive(false);
        }

        private void HandleLeaderboardUpdated(string board, string time)
        {
            LeaderboardText.text = board;
            TimerText.text = time;
        }
    }
}