using Mirror;
using UnityEngine;

namespace Soapbox.Race
{
    public class VehicleCheckpointTracker : NetworkBehaviour
    {
        [SyncVar] public int LastCheckpoint = -1;
        [SyncVar] public bool IsFinished = false;
        [SyncVar] public float FinishTime = 0f;
        [SyncVar] public int RacePosition = 1; 

        private void OnTriggerEnter(Collider other)
        {
            if (!isLocalPlayer) return;

            if (other.TryGetComponent(out Checkpoint cp) && cp.Index > LastCheckpoint)
            {
                CmdCrossCheckpoint(cp.Index, cp.IsFinishLine);
            }
        }

        [Command]
        private void CmdCrossCheckpoint(int index, bool isFinish)
        {
            if (index <= LastCheckpoint || IsFinished) return;

            LastCheckpoint = index;

            if (isFinish)
            {
                IsFinished = true;
                FinishTime = RaceManager.Instance.GetElapsedTime();
                RaceManager.Instance.PlayerFinishedRace(netId, FinishTime);
            }
        }

        [Server]
        public void ResetProgress()
        {
            LastCheckpoint = -1;
            IsFinished = false;
            FinishTime = 0f;
            RacePosition = 1;
        }

        // NOUVEAU : Appel ciblé du serveur vers le client propriétaire pour se téléporter
        [TargetRpc]
        public void TargetRespawn(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;

            // On stoppe totalement la voiture (pour éviter qu'elle continue de rouler / voler)
            if (TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}