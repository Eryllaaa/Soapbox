using UnityEngine;

namespace Soapbox.Race
{
    public class StartingGate : MonoBehaviour
    {
        public Collider BlockingCollider;
        public float DropHeight = -3f;
        
        private Vector3 _startPosition;

        private void Awake()
        {
            _startPosition = transform.position;
        }

        private void OnEnable()
        {
            EventManager.OnRaceCountdownGo += OpenGate;
            EventManager.OnRaceRestart += CloseGate;
        }

        private void OnDisable()
        {
            EventManager.OnRaceCountdownGo -= OpenGate;
            EventManager.OnRaceRestart -= CloseGate;
        }

        private void OpenGate()
        {
            if (BlockingCollider) BlockingCollider.enabled = false;
            transform.position = _startPosition + Vector3.up * DropHeight;
        }

        private void CloseGate()
        {
            if (BlockingCollider) BlockingCollider.enabled = true;
            transform.position = _startPosition;
        }
    }
}