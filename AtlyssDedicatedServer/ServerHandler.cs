using System.Collections;
using Mirror;
using Steamworks;
using UnityEngine;

namespace AtlyssDedicatedServer;

public class ServerHandler : MonoBehaviour
{
    private int ShutdownSecondsLeft;
    private bool PlayerThresholdNotified;
    private bool PlayerThresholdReached;
    private bool ShouldRestart;
    private bool IsAutomaticRestart;

    private void Awake()
    {
        StartCoroutine(ServerHeartbeat());
    }

    public void ScheduleShutdown(int seconds)
    {
        ShutdownSecondsLeft = seconds;
        PlayerThresholdNotified = false;
        PlayerThresholdReached = false;
        IsAutomaticRestart = false;
        ShouldRestart = false;
        HostConsole._current.Init_ServerMessage($"Server will shut down in {ShutdownSecondsLeft} seconds.");
    }

    public void ScheduleRestart(int seconds)
    {
        ShutdownSecondsLeft = seconds;
        PlayerThresholdNotified = false;
        PlayerThresholdReached = false;
        IsAutomaticRestart = false;
        ShouldRestart = true;
        HostConsole._current.Init_ServerMessage($"Server will restart in {ShutdownSecondsLeft} seconds.");
    }

    public void ScheduleAutomaticRestart()
    {
        ShutdownSecondsLeft = PluginConfig.PeriodicRestartIntervalSeconds;
        PlayerThresholdNotified = false;
        PlayerThresholdReached = false;
        IsAutomaticRestart = true;
        ShouldRestart = true;
        Plugin.Logger.LogInfo($"Scheduled automatic restart in {Utils.FormatInterval(PluginConfig.PeriodicRestartIntervalSeconds)}.");
    }

    public void CancelShutdownOrRestart()
    {
        if (ShutdownSecondsLeft == 0)
        {
            Plugin.Logger.LogInfo($"No pending shutdown or restart to cancel.");
            return;
        }

        HostConsole._current.Init_ServerMessage($"{(ShouldRestart ? "Restart" : "Shutdown")} cancelled.");
        ShutdownSecondsLeft = 0;
        ShouldRestart = false;
    }

    private IEnumerator ServerHeartbeat()
    {
        var oneSecond = new WaitForSeconds(1f);

        while (true)
        {
            // Tick every second
            yield return oneSecond;

            // Force host player to be hidden at all times
            if (Player._mainPlayer && !Player._mainPlayer._pVisual._forceHidden)
                Player._mainPlayer._pVisual.Network_forceHidden = true;

            // Start server if needed
            if (MainMenuManager._current && !NetworkServer.active && !NetworkClient.active)
            {
                StartServer();
                yield return oneSecond;
                continue;
            }

            if (NetworkServer.active && PluginConfig.PeriodicRestartEnabled && ShutdownSecondsLeft == 0)
            {
                ScheduleAutomaticRestart();
                continue;
            }

            if (NetworkServer.active && ShutdownSecondsLeft > 0)
            {
                bool supressTimer = false;

                if (!IsAutomaticRestart || ShutdownSecondsLeft > 60 || PlayerThresholdReached)
                    ShutdownSecondsLeft--;

                if (IsAutomaticRestart && ShutdownSecondsLeft <= 60 && !PlayerThresholdReached)
                {
                    if (NetworkServer.connections.Count <= PluginConfig.PeriodicRestartPlayerThreshold)
                    {
                        PlayerThresholdReached = true;
                    }
                    else
                    {
                        supressTimer = true;
                        if (!PlayerThresholdNotified)
                        {
                            HostConsole._current.Init_ServerMessage($"Server will restart once there are {PluginConfig.PeriodicRestartPlayerThreshold} or less players remaining.");
                            PlayerThresholdNotified = true;
                        }
                    }
                }

                if (!supressTimer)
                {
                    if (ShutdownSecondsLeft == 0)
                    {
                        StopServer();
                        if (!ShouldRestart)
                        {
                            yield return oneSecond;
                            Application.Quit();
                            yield break; // Unreachable
                        }

                        continue;
                    }

                    bool shouldPrintMessage =
                        ShutdownSecondsLeft <= 5 ||
                        ShutdownSecondsLeft <= 60 && ShutdownSecondsLeft % 15 == 0 ||
                        ShutdownSecondsLeft <= 300 && ShutdownSecondsLeft % 60 == 0 ||
                        ShutdownSecondsLeft <= 900 && ShutdownSecondsLeft % 300 == 0 ||
                        ShutdownSecondsLeft <= 3600 && ShutdownSecondsLeft % 900 == 0;

                    if (shouldPrintMessage)
                    {
                        HostConsole._current.Init_ServerMessage($"Server will {(ShouldRestart ? "restart" : "shut down")} in {Utils.FormatInterval(ShutdownSecondsLeft)}.");
                    }
                }
            }
        }
    }

    internal void StopServer()
    {
        if (!NetworkServer.active)
            return;

        Plugin.Logger.LogInfo("Stopping Server");

        if (NetworkClient.isConnected)
            AtlyssNetworkManager._current.StopHost();
        else
            AtlyssNetworkManager._current.StopServer();
    }

    internal void StartServer()
    {
        Plugin.Logger.LogInfo("Starting Server");

        LogServerConfig();

        ServerHostSettings_Profile hostSettingsProfile = ProfileDataManager._current._hostSettingsProfile;
        AtlyssNetworkManager anm = AtlyssNetworkManager._current;
        ProfileDataManager pdm = ProfileDataManager._current;
        LobbyListManager llm = LobbyListManager._current;

        anm._steamworksMode = true;

        anm._soloMode = false;
        anm._serverMode = false;

        anm._serverName = PluginConfig.ServerName;
        anm._serverPassword = PluginConfig.ServerPassword;
        anm._sentPassword = PluginConfig.ServerPassword;
        anm._serverMotd = PluginConfig.ServerMOTD;
        anm.maxConnections = PluginConfig.ServerMaxPlayers;

        llm._lobbyPasswordInput.text = PluginConfig.ServerPassword;
        llm._lobbyTypeDropdown.value = (int)PluginConfig.ServerType;
        llm._hostLobbyRealm = PluginConfig.ServerTag;

        anm._bannedClientList.Clear();
        anm._mutedClientList.Clear();

        if (hostSettingsProfile._banList != null)
            anm._bannedClientList.AddRange(hostSettingsProfile._banList);

        if (hostSettingsProfile._mutedList != null)
            anm._mutedClientList.AddRange(hostSettingsProfile._mutedList);

        ELobbyType lobbyType = PluginConfig.ServerType switch
        {
            SteamLobbyType.PUBLIC => ELobbyType.k_ELobbyTypePublic,
            SteamLobbyType.FRIENDS => ELobbyType.k_ELobbyTypeFriendsOnly,
            SteamLobbyType.PRIVATE => ELobbyType.k_ELobbyTypePrivate,
            _ => ELobbyType.k_ELobbyTypePublic
        };

        var slotToUse = PluginConfig.CharacterSlotToUse;
        
        if (!(0 <= slotToUse && slotToUse <= pdm._characterFiles.Length))
        {
            Plugin.Logger.LogWarning($"Save slot {slotToUse} is invalid! Valid slots are between 0 and {pdm._characterFiles.Length}. Defaulting to 0.");
            slotToUse = 0;
        }
        
        if (pdm._characterFiles[slotToUse]._isEmptySlot)
        {
            Plugin.Logger.LogWarning($"Slot {slotToUse} is empty! Creating a new character in it...");
            pdm._characterFile = pdm._characterFiles[slotToUse];

            var manager = ProfileDataManager._current._charSelectManager._characterCreationManager;

            manager.SelectRace(0);
            manager.RandomizeAll();
            manager._characterNameInputField.text = "Hostman";
            manager.Create_Character();
        }

        pdm._characterFile = pdm._characterFiles[slotToUse];

        SteamLobby._current.HostLobby(lobbyType);
        MainMenuManager._current._characterSelectManager.Send_CharacterFile();
    }

    private void LogServerConfig()
    {
        var autoRestartEnabled = PluginConfig.PeriodicRestartEnabled;
        var autoRestart = PluginConfig.PeriodicRestartIntervalSeconds;
        var Log = Plugin.Logger.LogInfo;
        var thresholdPct = PluginConfig.PeriodicRestartPlayerThresholdPercent;
        var thresholdPlayers = PluginConfig.PeriodicRestartPlayerThreshold;

        Log("=== Server Configuration ===");
        Log($"Server Name     : {PluginConfig.ServerName}");
        Log($"Server Type     : {PluginConfig.ServerType}");
        Log($"Server Tag      : {PluginConfig.ServerTag}");
        Log($"Server Password : {(string.IsNullOrEmpty(PluginConfig.ServerPassword) ? "[None]" : PluginConfig.ServerPassword)}");
        Log($"Server MOTD     : {(string.IsNullOrEmpty(PluginConfig.ServerMOTD) ? "[None]" : PluginConfig.ServerMOTD)}");
        Log($"Max Players     : {PluginConfig.ServerMaxPlayers}");
        Log($"Auto Restart    : {(!autoRestartEnabled ? "disabled" : $"{Utils.FormatInterval(autoRestart)}{(thresholdPct == 100 ? "" : $" at {thresholdPlayers} players left ({thresholdPct}%)")}")}");
        Log("============================");
    }
}