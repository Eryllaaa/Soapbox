using UnityEngine;

public class CameraRig : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _followSpeed;

    void Update()
    {
        transform.position = ExpDecay(transform.position, _target.position, _followSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, _target.rotation.eulerAngles.y, 0f), _followSpeed * Time.deltaTime);
    }

    private Vector3 ExpDecay(Vector3 a, Vector3 b, float decay)
    {
        return b + (a - b) * Mathf.Exp(-decay);
    }

    private float ExpDecay(float a, float b, float decay)
    {
        return b + (a - b) * Mathf.Exp(-decay);
    }
}
