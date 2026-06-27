using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(Camera))]
public class SplineFollowCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public SplineContainer roadSpline;

    [Header("Camera placement")]
    public float distanceBack = 12f;
    public float height = 6f;

    [Header("Aim")]
    public float lookAhead = 15f;
    public float lookHeight = 1.5f;
    [Range(0f, 1f)] public float playerLookBlend = 0.35f; // 0 = aim at road, 1 = aim at car

    [Header("Smoothing")]
    public float positionSmoothTime = 0.15f;
    public float rotationSmoothSpeed = 8f;

    [Header("Spline tracking")]
    public bool invertDirection = false; // tick if the cam ends up in front
    public float searchWindow = 0.05f;   // how far along the spline we look around last t
    public int searchSamples = 16;
    public int refineIterations = 5;
    public float reacquireDistance = 30f; // if the car gets this far from the tracked point, re-lock globally
    public int globalResolution = 6;
    public int globalIterations = 3;

    Vector3 _posVelocity;
    float _currentT;
    bool _hasT;
    bool _init;

    void LateUpdate()
    {
        if (player == null || roadSpline == null) return;

        var spline = roadSpline.Spline;
        float3 localPlayer = roadSpline.transform.InverseTransformPoint(player.position);

        float t;
        if (!_hasT)
        {
            // first lock: full search
            SplineUtility.GetNearestPoint(spline, localPlayer, out _, out t,
                globalResolution, globalIterations);
            _hasT = true;
        }
        else
        {
            // only search around last frame's t -> can't jump across the track
            t = FindLocalT(spline, localPlayer, _currentT);

            // safety net: car teleported (respawn?) -> re-lock globally
            Vector3 nearWorld = roadSpline.EvaluatePosition(t);
            if ((nearWorld - player.position).sqrMagnitude > reacquireDistance * reacquireDistance)
                SplineUtility.GetNearestPoint(spline, localPlayer, out _, out t,
                    globalResolution, globalIterations);
        }
        _currentT = t;

        Vector3 roadPos = roadSpline.EvaluatePosition(t);
        Vector3 tangent = ((Vector3)roadSpline.EvaluateTangent(t));
        Vector3 up = ((Vector3)roadSpline.EvaluateUpVector(t)).normalized;

        // tangent is zero at the spline ends
        if (tangent.sqrMagnitude < 1e-6f) tangent = transform.forward;
        tangent.Normalize();
        if (invertDirection) tangent = -tangent;
        if (up.sqrMagnitude < 1e-6f) up = Vector3.up;

        Vector3 desiredPos = roadPos - tangent * distanceBack + up * height;

        Vector3 roadLook = roadPos + tangent * lookAhead + up * lookHeight;
        Vector3 lookTarget = Vector3.Lerp(roadLook, player.position, playerLookBlend);

        // snap on the first frame, otherwise the cam flies in from (0,0,0)
        if (!_init)
        {
            transform.position = desiredPos;
            transform.rotation = Quaternion.LookRotation(lookTarget - desiredPos, up);
            _init = true;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref _posVelocity, positionSmoothTime);

        Quaternion desiredRot = Quaternion.LookRotation(lookTarget - transform.position, up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, desiredRot, rotationSmoothSpeed * Time.deltaTime);
    }

    // nearest t found only within a window around prevT (with wrap on closed splines)
    float FindLocalT(Spline spline, float3 target, float prevT)
    {
        bool closed = spline.Closed;
        float best = prevT;
        float bestDist = float.MaxValue;

        // coarse sweep
        for (int i = 0; i <= searchSamples; i++)
        {
            float ti = prevT - searchWindow + (2f * searchWindow) * (i / (float)searchSamples);
            ti = WrapT(ti, closed);
            float d = math.distancesq(SplineUtility.EvaluatePosition(spline, ti), target);
            if (d < bestDist) { bestDist = d; best = ti; }
        }

        // refine around the best sample
        float win = (2f * searchWindow) / searchSamples;
        for (int r = 0; r < refineIterations; r++)
        {
            float tA = WrapT(best - win, closed);
            float tB = WrapT(best + win, closed);
            float dA = math.distancesq(SplineUtility.EvaluatePosition(spline, tA), target);
            float dB = math.distancesq(SplineUtility.EvaluatePosition(spline, tB), target);
            if (dA < bestDist) { bestDist = dA; best = tA; }
            if (dB < bestDist) { bestDist = dB; best = tB; }
            win *= 0.5f;
        }
        return best;
    }

    static float WrapT(float t, bool closed)
    {
        if (!closed) return math.clamp(t, 0f, 1f);
        t %= 1f;
        return t < 0f ? t + 1f : t;
    }
}