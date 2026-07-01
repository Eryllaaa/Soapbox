using System;
using UnityEngine;

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
    #region Lobby & Connection System
    // Notifie l'UI que le nombre de joueurs a changé
    public static event Action<int, int, string> OnLobbyRosterChanged;
    public static void EmitLobbyRosterChanged(int current, int max, string formattedRoster) => OnLobbyRosterChanged?.Invoke(current, max, formattedRoster);

    // Notifie l'UI si un Lobby Steam est actif (pour afficher le bouton Inviter)
    public static event Action<bool> OnSteamLobbyAvailabilityChanged;
    public static void EmitSteamLobbyAvailabilityChanged(bool available) => OnSteamLobbyAvailabilityChanged?.Invoke(available);
    #endregion
}