using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public static class EventManager
{
    #region Race System
    public static event Action<int> OnRaceCountdownTick;
    public static event Action OnRaceCountdownGo;
    public static event Action OnRaceStarted;
    public static event Action OnRaceFinished;
    public static event Action OnRaceRestart;
    public static event Action<string, string> OnLeaderboardUpdated; // string affichage complet

    public static void EmitRaceCountdownTick(int time) => OnRaceCountdownTick?.Invoke(time);
    public static void EmitRaceCountdownGo() => OnRaceCountdownGo?.Invoke();
    public static void EmitRaceStarted() => OnRaceStarted?.Invoke();
    public static void EmitRaceFinished() => OnRaceFinished?.Invoke();
    public static void EmitRaceRestart() => OnRaceRestart?.Invoke();
    public static void EmitLeaderboardUpdated(string leaderboardText, string timerText) => OnLeaderboardUpdated?.Invoke(leaderboardText, timerText);
    #endregion
}