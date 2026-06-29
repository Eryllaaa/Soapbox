using Mirror;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soapbox.Race
{
    public class RaceManager : NetworkBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public enum RaceState { Lobby, Countdown, Racing, Finished }
        [SyncVar] public RaceState State = RaceState.Lobby;
        [SyncVar] public double RaceStartTime; 

        [Header("Settings")]
        public float RestartDelay = 10f;

        // NOUVEAU : Glisse tes points d'apparition (des GameObject vides) ici dans l'inspecteur
        [Tooltip("Points d'apparition sur la ligne de départ")]
        public Transform[] SpawnPoints;
        
        private Checkpoint[] _checkpoints;
        private float _rankUpdateTimer;

        private void Awake()
        {
            Instance = this;
            _checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (isServer && State == RaceState.Lobby && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartCoroutine(RaceRoutine());
            }

            if (isServer && State == RaceState.Racing)
            {
                _rankUpdateTimer += Time.deltaTime;
                if (_rankUpdateTimer >= 0.25f)
                {
                    _rankUpdateTimer = 0f;
                    UpdateLiveRankings();
                }
            }
        }

        public float GetElapsedTime() => State == RaceState.Racing ? (float)(NetworkTime.time - RaceStartTime) : 0f;

        // ================= LOGIQUE SERVEUR =================

        [Server]
        private IEnumerator RaceRoutine()
        {
            State = RaceState.Countdown;
            
            for (int i = 3; i > 0; i--)
            {
                RpcEmitCountdownTick(i);
                yield return new WaitForSeconds(1f);
            }
            
            RpcEmitCountdownGo();
            
            RaceStartTime = NetworkTime.time;
            State = RaceState.Racing;
            RpcEmitRaceStarted();
        }

        [Server]
        private void UpdateLiveRankings()
        {
            var players = FindObjectsByType<VehicleCheckpointTracker>(FindObjectsSortMode.None).ToList();

            players.Sort((a, b) =>
            {
                if (a.IsFinished != b.IsFinished) return a.IsFinished ? -1 : 1;
                if (a.IsFinished) return a.FinishTime.CompareTo(b.FinishTime);
                if (a.LastCheckpoint != b.LastCheckpoint) return b.LastCheckpoint.CompareTo(a.LastCheckpoint); 

                Checkpoint nextA = _checkpoints.FirstOrDefault(c => c.Index == a.LastCheckpoint + 1);
                Checkpoint nextB = _checkpoints.FirstOrDefault(c => c.Index == b.LastCheckpoint + 1);

                float distA = nextA != null ? Vector3.Distance(a.transform.position, nextA.transform.position) : float.MaxValue;
                float distB = nextB != null ? Vector3.Distance(b.transform.position, nextB.transform.position) : float.MaxValue;

                return distA.CompareTo(distB);
            });

            for (int i = 0; i < players.Count; i++)
            {
                players[i].RacePosition = i + 1; 
            }
        }

        [Server]
        public void PlayerFinishedRace(uint netId, float finishTime)
        {
            if (State == RaceState.Finished) return;

            State = RaceState.Finished;
            RpcEmitRaceFinished();
            UpdateFinalLeaderboard(); 
            StartCoroutine(RestartRoutine());
        }

        [Server]
        private IEnumerator RestartRoutine()
        {
            yield return new WaitForSeconds(RestartDelay);

            var trackers = FindObjectsByType<VehicleCheckpointTracker>(FindObjectsSortMode.None);
            
            int spawnIndex = 0;

            foreach (var t in trackers)
            {
                t.ResetProgress();

                // NOUVEAU : Trouver la position de respawn
                Vector3 pos = t.transform.position;
                Quaternion rot = t.transform.rotation;

                // On utilise les points définis dans l'inspecteur, s'il y en a
                if (SpawnPoints != null && SpawnPoints.Length > 0)
                {
                    Transform sp = SpawnPoints[spawnIndex % SpawnPoints.Length];
                    pos = sp.position;
                    rot = sp.rotation;
                    spawnIndex++;
                }

                // On ordonne au client propriétaire de se téléporter
                t.TargetRespawn(pos, rot);
            }

            State = RaceState.Lobby;
            RpcEmitRaceRestart();
        }

        [Server]
        private void UpdateFinalLeaderboard()
        {
            UpdateLiveRankings();
            var players = FindObjectsByType<VehicleCheckpointTracker>(FindObjectsSortMode.None).OrderBy(p => p.RacePosition).ToList();

            string board = "CLASSEMENT\n";
            foreach (var p in players)
            {
                string status = p.IsFinished ? $"{p.FinishTime:F1}s" : "DNF";
                board += $"{p.RacePosition}. Joueur {p.netId} - {status}\n";
            }
            
            string timeStr = $"Temps total: {GetElapsedTime():F1}s";
            RpcEmitLeaderboard(board, timeStr);
        }

        // ================= PONTS RESEAU -> EVENT MANAGER =================

        [ClientRpc] private void RpcEmitCountdownTick(int time) => EventManager.EmitRaceCountdownTick(time);
        [ClientRpc] private void RpcEmitCountdownGo() => EventManager.EmitRaceCountdownGo();
        [ClientRpc] private void RpcEmitRaceStarted() => EventManager.EmitRaceStarted();
        [ClientRpc] private void RpcEmitRaceFinished() => EventManager.EmitRaceFinished();
        [ClientRpc] private void RpcEmitRaceRestart() => EventManager.EmitRaceRestart();
        [ClientRpc] private void RpcEmitLeaderboard(string board, string time) => EventManager.EmitLeaderboardUpdated(board, time);
    }
}