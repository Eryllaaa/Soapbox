using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soapbox.Race
{
    public class VehicleCheckpointTracker : NetworkBehaviour
    {
        [SyncVar] public int LastCheckpoint = -1;
        [SyncVar] public bool IsFinished = false;
        [SyncVar] public float FinishTime = 0f;
        [SyncVar] public int RacePosition = 1; 

        private void Update()
        {
            // Seul le joueur local peut déclencher son propre respawn
            if (!isLocalPlayer) return;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                CmdRequestRespawn();
            }

            if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame)
            {
                CmdRequestRespawn();
            }
        }

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

        [Command]
        private void CmdRequestRespawn()
        {
            // Recherche de tous les checkpoints de la scène
            Checkpoint[] checkpoints = FindObjectsOfType<Checkpoint>();
            Checkpoint targetCheckpoint = null;

            // Recherche du checkpoint correspondant au dernier validé
            foreach (var cp in checkpoints)
            {
                if (cp.Index == LastCheckpoint)
                {
                    targetCheckpoint = cp;
                    break;
                }
            }

            // Si aucun checkpoint n'a encore été franchi, on cherche le départ (Index 0)
            if (targetCheckpoint == null)
            {
                foreach (var cp in checkpoints)
                {
                    if (cp.Index == 0)
                    {
                        targetCheckpoint = cp;
                        break;
                    }
                }
            }

            // Si un checkpoint de destination valide est trouvé
            if (targetCheckpoint != null)
            {
                // On téléporte le véhicule sur le SERVEUR
                TeleportAndReset(targetCheckpoint.transform.position, targetCheckpoint.transform.rotation);

                // On ordonne au client de faire de même
                TargetRespawn(targetCheckpoint.transform.position, targetCheckpoint.transform.rotation);
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

        // Appel ciblé du serveur vers le client propriétaire pour se téléporter
        [TargetRpc]
        public void TargetRespawn(Vector3 position, Quaternion rotation)
        {
            TeleportAndReset(position, rotation);
        }

        /// <summary>
        /// Méthode robuste pour téléporter un véhicule physique sans conflit avec Unity ni Mirror.
        /// </summary>
        private void TeleportAndReset(Vector3 position, Quaternion rotation)
        {
            // Étape 1 : Gérer la physique Unity et l'interpolation du Rigidbody
            if (TryGetComponent(out Rigidbody rb))
            {
                // On désactive temporairement l'interpolation pour éviter le bug de gel en l'air de Unity
                RigidbodyInterpolation tempInterpolation = rb.interpolation;
                rb.interpolation = RigidbodyInterpolation.None;

                // On stoppe toutes les forces accumulées
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                // On déplace le Rigidbody physiquement et visuellement
                rb.position = position;
                rb.rotation = rotation;
                transform.position = position;
                transform.rotation = rotation;

                // On remet l'interpolation d'origine
                rb.interpolation = tempInterpolation;
            }
            else
            {
                transform.position = position;
                transform.rotation = rotation;
            }

            // Étape 2 : Forcer la mise à jour immédiate de la physique pour éviter les décalages de colliders
            Physics.SyncTransforms();

            // Étape 3 : Réinitialiser la mémoire tampon d'interpolation de Mirror (Snapshot Buffer)
            // Pour éviter les erreurs de compilation dues aux fichiers d'Assembly Definition (.asmdef), 
            // on accède à la méthode OnTeleport de NetworkTransform par Réflexion.
            Component nt = GetComponent("NetworkTransform");
            if (nt != null)
            {
                System.Type type = nt.GetType();
                
                // On cherche d'abord la méthode OnTeleport(Vector3, Quaternion)
                var methodWithRot = type.GetMethod("OnTeleport", new System.Type[] { typeof(Vector3), typeof(Quaternion) });
                if (methodWithRot != null)
                {
                    methodWithRot.Invoke(nt, new object[] { position, rotation });
                }
                else
                {
                    // Si indisponible, on cherche OnTeleport(Vector3)
                    var methodNoRot = type.GetMethod("OnTeleport", new System.Type[] { typeof(Vector3) });
                    if (methodNoRot != null)
                    {
                        methodNoRot.Invoke(nt, new object[] { position });
                    }
                }
            }
        }
    }
}