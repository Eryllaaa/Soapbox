using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(fileName = "WheelData", menuName = "Scriptable Objects/WheelData")]
public class WheelData : ScriptableObject
{
    [SerializeField] private AnimationCurve _accelerationCurve;

    [SerializeField] private float _grip;
    [SerializeField] private float _brakingGrip;

    public float grip => _grip;
    public float brakingGrip => _brakingGrip;
}
