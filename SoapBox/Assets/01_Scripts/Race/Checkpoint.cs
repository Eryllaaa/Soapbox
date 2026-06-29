using UnityEngine;

namespace Soapbox.Race
{
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        public int Index;
        public bool IsFinishLine;

        private void Reset()
        {
            if (TryGetComponent(out Collider col)) col.isTrigger = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = IsFinishLine ? new Color(1f, 0.85f, 0.2f, 0.5f) : new Color(0.3f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
#endif
    }
}