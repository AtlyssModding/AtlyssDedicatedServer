using System.Diagnostics.CodeAnalysis;
using BepInEx.Configuration;
using UnityEngine;

namespace AtlyssDedicatedServer;

internal static class PluginConfig
{
    internal static string ServerName { get; private set; } = null!;
    internal static SteamLobbyType ServerType { get; private set; }
    internal static AtlyssSteamLobbyTag ServerTag { get; private set; }
    internal static string ServerPassword { get; private set; } = null!;
    internal static string ServerMOTD { get; private set; } = null!;
    internal static int ServerMaxPlayers { get; private set; }
    
    internal static int CharacterSlotToUse { get; private set; }
    
    internal static bool PeriodicRestartEnabled { get; private set; }
    internal static string PeriodicRestartInterval { get; private set; } = null!;
    internal static int PeriodicRestartPlayerThresholdPercent { get; private set; }
    
    internal static bool IsActive { get; private set; }
    internal static bool IsHeadless { get; private set; }
    
    // Computed values
    internal static int PeriodicRestartIntervalSeconds { get; private set; }
    internal static int PeriodicRestartPlayerThreshold { get; private set; }

    public static void Configure(ConfigFile file, string[] args)
    {
        IsActive = args.Contains("-server");
        IsHeadless = IsActive && Application.isBatchMode && args.Contains("-nographics");
        
        ServerName = file.Bind("ServerData", "ServerName", "ATLYSS Server", "Name to set when launching the server.").Value;

        if (TryGetStringArgment(args, "-pve", out var serverName))
            ServerName = serverName;
        
        if (ServerName.Length > 20)
        {
            Plugin.Logger.LogWarning($"Server name \"{ServerName}\" is too long ({ServerName.Length}/20). Defaulting to \"ATLYSS Server\".");
            ServerName = "ATLYSS Server";
        }
        
        if (string.IsNullOrWhiteSpace(ServerName))
        {
            Plugin.Logger.LogWarning($"Server name \"{ServerName}\" is invalid. Defaulting to \"ATLYSS Server\".");
            ServerName = "ATLYSS Server";
        }
        
        ServerType = file.Bind("ServerData", "ServerType", SteamLobbyType.PUBLIC, "Type to set when launching the server.").Value;

        if (args.Contains("-public"))
            ServerType = SteamLobbyType.PUBLIC;
        else if (args.Contains("-friends"))
            ServerType = SteamLobbyType.FRIENDS;
        else if (args.Contains("-private"))
            ServerType = SteamLobbyType.PRIVATE;
        
        ServerTag = file.Bind("ServerData", "ServerTag", AtlyssSteamLobbyTag.GAIA, "Lobby tag to set when launching the server.").Value;
        
        if (args.Contains("-pve"))
            ServerTag = AtlyssSteamLobbyTag.GAIA;
        else if (args.Contains("-pvp"))
            ServerTag = AtlyssSteamLobbyTag.DUALOS;
        else if (args.Contains("-social"))
            ServerTag = AtlyssSteamLobbyTag.NOTH;
        else if (args.Contains("-rp"))
            ServerTag = AtlyssSteamLobbyTag.LOODIA;
        
        ServerPassword = file.Bind("ServerData", "ServerPassword", "", "Password to use for connecting to the server. Leave empty for a passwordless server.").Value;

        if (TryGetStringArgment(args, "-password", out var password))
            ServerPassword = password;
        
        ServerMOTD = file.Bind("ServerData", "ServerMOTD", "Welcome to ATLYSS Server!", "MOTD to display for clients that connect to the server.").Value;

        if (TryGetStringArgment(args, "-motd", out var motd))
            ServerMOTD = motd;
        
        ServerMaxPlayers = file.Bind("ServerData", "ServerMaxPlayers", 16, "Maximum number of players that can connect to the server (this includes the dummy host player).").Value;

        if (TryGetIntegerArgment(args, "-maxplayers", out var maxPlayers))
            ServerMaxPlayers = maxPlayers;
        
        if (!(ServerMaxPlayers >= 2 && ServerMaxPlayers <= 250))
        {
            ServerMaxPlayers = 16;
            Plugin.Logger.LogWarning("MaxPlayers must be between 2 and 250. Defaulting to 16.");
        }
        
        CharacterSlotToUse = file.Bind("ServerData", "CharacterSlotToUse", 0, "Character slot to use for the (idle) host character. " +
            "If the slot is empty, the first non-empty save slot will be used instead. " +
            "If all slots are empty, a new randomized character will be created in the first slot.").Value;

        if (TryGetIntegerArgment(args, "-hostsave", out var hostsave))
            CharacterSlotToUse = hostsave;
        
        PeriodicRestartEnabled = file.Bind("ServerHealth", "PeriodicRestartEnabled", false,
            "Enable restarting the server automatically after the given time interval and number of players left.").Value;
        
        if (args.Contains("-autorestart"))
            PeriodicRestartEnabled = true;
        
        PeriodicRestartInterval = file.Bind("ServerHealth", "PeriodicRestartInterval", "6h",
            "How much time to wait before restarting. Must be at least 30 minutes. " +
            "Format is specified in days, hours, minutes and/or seconds like 1d12h30m30s, 4h45m, 4h, 30m, 1d, etc.").Value;
        
        if (TryGetStringArgment(args, "-autorestartin", out var interval))
            PeriodicRestartInterval = interval;
        
        if (Utils.TryParseInterval(PeriodicRestartInterval, out int seconds))
        {
            if (seconds < 1800)
            {
                seconds = 1800;
                Plugin.Logger.LogWarning($"Specified restart time is too low (<30m). Increasing it to 30 minutes.");
            }
            
            PeriodicRestartIntervalSeconds = seconds;
        }
        else
        {
            PeriodicRestartIntervalSeconds = 4 * 3600;
            Plugin.Logger.LogWarning($"Specified restart time is invalid. Defaulting to 4 hours.");
        }
        
        PeriodicRestartPlayerThresholdPercent = file.Bind("ServerHealth", "PeriodicRestartPlayerThresholdPercent", 100,
            "Theshold of players (% of MaxPlayers) with which the server will proceed with a periodic restart. " +
            "If there are more players than that, the restart timer will be stalled at 1 minute left until enough players leave. " +
            "Set to 100 to ignore player counts and always restart the server.").Value;
        
        if (TryGetIntegerArgment(args, "-autorestartthreshold", out var restartPlayerThreshold))
            PeriodicRestartPlayerThresholdPercent = restartPlayerThreshold;
        
        if (!(0 <= PeriodicRestartPlayerThresholdPercent && PeriodicRestartPlayerThresholdPercent <= 100))
        {
            PeriodicRestartPlayerThresholdPercent = 100;
            Plugin.Logger.LogWarning($"Specified restart player threshold is invalid. Defaulting to 100 (threshold disabled).");
        }
        
        PeriodicRestartPlayerThreshold = Math.Clamp((int)Math.Ceiling(ServerMaxPlayers * PluginConfig.PeriodicRestartPlayerThresholdPercent / 100f), 1, ServerMaxPlayers);

        file.Save();
    }
    
    private static bool TryGetStringArgment(string[] args, string key, [NotNullWhen(true)] out string? value)
    {
        int index = Array.IndexOf(args, key);
        value = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        return value != null;
    }
    
    private static bool TryGetIntegerArgment(string[] args, string key, out int value)
    {
        int index = Array.IndexOf(args, key);
        var strValue = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;

        if (strValue == null)
        {
            value = 0;
            return false;
        }

        if (int.TryParse(index >= 0 && index + 1 < args.Length ? args[index + 1] : null, out value))
            return true;
        
        Plugin.Logger.LogWarning($"Value \"{strValue}\" supplied to argument \"{key}\" is not a valid integer.");
        return false;
    }
}