using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Soapbox.Networking
{
    [DisallowMultipleComponent]
    public class SoapboxSpawnPoint : MonoBehaviour
    {
        private static readonly List<SoapboxSpawnPoint> Registry = new();

        [Tooltip("Position sur la grille de départ (0 = Pole Position, 1 = 2ème, etc.)")]
        [SerializeField] private int gridIndex = 0;

        [Tooltip("Si vrai, le point est bloqué une fois qu'un joueur y a spawn.")]
        [SerializeField] private bool singleUse = true;

        private bool isConsumed;
        public bool IsAvailable => !isConsumed;

        private void OnEnable()
        {
            Registry.Add(this);
            // Trie automatiquement pour toujours assigner la pole position en premier
            Registry.Sort((a, b) => a.gridIndex.CompareTo(b.gridIndex));
        }

        private void OnDisable() => Registry.Remove(this);

        public static SoapboxSpawnPoint Pick()
        {
            Registry.RemoveAll(sp => sp == null);

            for (int i = 0; i < Registry.Count; i++)
            {
                SoapboxSpawnPoint sp = Registry[i];
                if (!sp.IsAvailable) continue;

                if (sp.singleUse) sp.isConsumed = true;
                return sp;
            }

            return null; // Plus de place sur la grille
        }

        public static void ReleaseAll()
        {
            for (int i = 0; i < Registry.Count; i++)
            {
                if (Registry[i] != null) Registry[i].isConsumed = false;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isConsumed ? new Color(1f, 0.2f, 0.2f, 0.5f) : new Color(0.2f, 1f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Flèche de direction
            Vector3 fwd = transform.forward * 1.5f;
            Gizmos.DrawRay(transform.position, fwd);
            
            // Affiche le numéro sur la grille direct dans la scène
            Handles.Label(transform.position + Vector3.up * 1f, $"Grid: {gridIndex}");
        }
#endif
    }
}