using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AtlyssDedicatedServer;

// TODO : Refactor code into multiple scripts for easier maintainability
// Gonna push the update for now.

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("ATLYSS.exe")]
public class Plugin : BaseUnityPlugin, ILogListener
{
    public enum LobbyTypeTag : byte
    {
        PUBLIC,
        FRIENDS,
        PRIVATE
    }

    private string serverName = "ATLYSS Server";
    private LobbyTypeTag serverType = LobbyTypeTag.PUBLIC;
    private AtlyssSteamLobbyTag serverTag = AtlyssSteamLobbyTag.GAIA;
    private string serverPassword = string.Empty;
    private string serverMOTD = string.Empty;
    private int serverMaxPlayers = 16;
    private int hostCharSaveSlot = 0;

    private bool shouldHostServer = false;
    private bool hostSpawned = false;
    private bool actionTriggered = false;
    private float timeSinceSpawn = 0f;

    private string playerName = string.Empty;

    internal static new ManualLogSource Logger;

    private ILogListener _bepinexConsoleListener;
    private static TextWriter _rawConsoleOut;

    internal static string lastServerMessage = string.Empty;
    internal static DateTime lastServerMessageTime;
    public static bool isProcessingConsoleCommand = false;

    private string GetArgValue(string[] args, string key)
    {
        int index = Array.IndexOf(args, key);
        return (index >= 0 && index + 1 < args.Length) ? args[index + 1] : null;
    }

    private void LogServerConfig()
    {
        Logger.LogInfo("=== Server Configuration ===");
        Logger.LogInfo($"Server Name      : {serverName}");
        Logger.LogInfo($"Server Type      : {serverType}");
        Logger.LogInfo($"Server Tag       : {serverTag}");
        Logger.LogInfo($"Server Password  : {(string.IsNullOrEmpty(serverPassword) ? "[None]" : serverPassword)}");
        Logger.LogInfo($"Server MOTD      : {(string.IsNullOrEmpty(serverMOTD) ? "[None]" : serverMOTD)}");
        Logger.LogInfo($"Max Players      : {serverMaxPlayers}");
        Logger.LogInfo("============================");
    }

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        if (!Application.isBatchMode)
        {
            Logger.LogWarning("Not running in batchmode, DedicatedServer plugin exiting.");
            return;
        }

        Console.CursorVisible = true;

        try
        {
            Type consoleManagerType = Type.GetType("BepInEx.ConsoleManager, BepInEx");

            if (consoleManagerType == null)
            {
                Logger.LogError("Failed to find BepInEx.ConsoleManager type.");
                return;
            }

            PropertyInfo propInfo = consoleManagerType.GetProperty("StandardOutStream", BindingFlags.Public | BindingFlags.Static);

            if (propInfo != null)
            {
                _rawConsoleOut = (TextWriter)propInfo.GetValue(null, null);
            }

            if (_rawConsoleOut == null)
            {
                Logger.LogError("Failed to get raw console output stream via reflection.");
            }
            else
            {
                try
                {
                    var listeners = (List<ILogListener>)typeof(BepInEx.Logging.Logger).GetProperty("Listeners", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                    _bepinexConsoleListener = listeners.FirstOrDefault(l => l.GetType().Name == "ConsoleLogListener");

                    if (_bepinexConsoleListener != null)
                    {
                        BepInEx.Logging.Logger.Listeners.Remove(_bepinexConsoleListener);
                        Logger.LogInfo("Successfully detached default BepInEx console listener.");
                    }

                    BepInEx.Logging.Logger.Listeners.Add(this);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"An error occurred while detaching listener: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"An error occurred while accessing the console stream: {ex}");
        }

        string[] args = Environment.GetCommandLineArgs();
        if (args.Contains("-server"))
        {
            Logger.LogInfo("Starting in dedicated server mode.");
            shouldHostServer = true;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            // Server host char save slot with range check
            if (int.TryParse(GetArgValue(args, "-hostsave"), out int hostSlot))
            {
                if (hostSlot >= 0 && hostSlot <= 104)
                {
                    hostCharSaveSlot = hostSlot;
                }
                else
                {
                    hostCharSaveSlot = 0;
                    Logger.LogWarning("HostSave must be between 0 and 104. Defaulting to 0.");
                }
            }
            else
            {
                hostCharSaveSlot = 0;
            }

            serverName = GetArgValue(args, "-name") ?? "ATLYSS Server";

            if (serverName.Length > 20)
            {
                Logger.LogWarning($"Server name \"{serverName}\" is too long ({serverName.Length}/20). Defaulting to \"ATLYSS Server\".");
                serverName = "ATLYSS Server";
            }

            serverPassword = GetArgValue(args, "-password") ?? "";
            serverMOTD = GetArgValue(args, "-motd") ?? "Welcome to the server!";

            // Handle server type
            var typeFlags = new[] { "-public", "-private", "-friends" }.Where(args.Contains).ToList();
            if (typeFlags.Count > 1)
            {
                Logger.LogWarning($"Multiple server type flags detected ({string.Join(", ", typeFlags)}). Defaulting to PUBLIC.");
                serverType = LobbyTypeTag.PUBLIC;
            }
            else if (typeFlags.Count == 1)
            {
                serverType = typeFlags[0] switch
                {
                    "-public" => LobbyTypeTag.PUBLIC,
                    "-private" => LobbyTypeTag.PRIVATE,
                    "-friends" => LobbyTypeTag.FRIENDS,
                    _ => LobbyTypeTag.PUBLIC
                };
            }
            else
            {
                serverType = LobbyTypeTag.PUBLIC;
            }

            // Handle server focus
            var focusFlags = new[] { "-pve", "-pvp", "-social", "-rp" }.Where(args.Contains).ToList();
            if (focusFlags.Count > 1)
            {
                Logger.LogWarning($"Multiple lobby focus flags detected ({string.Join(", ", focusFlags)}). Defaulting to PVE.");
                serverTag = AtlyssSteamLobbyTag.GAIA;
            }
            else if (focusFlags.Count == 1)
            {
                serverTag = focusFlags[0] switch
                {
                    "-pve" => AtlyssSteamLobbyTag.GAIA,
                    "-pvp" => AtlyssSteamLobbyTag.DUALOS,
                    "-social" => AtlyssSteamLobbyTag.NOTH,
                    "-rp" => AtlyssSteamLobbyTag.LOODIA,
                    _ => AtlyssSteamLobbyTag.GAIA
                };
            }
            else
            {
                serverTag = AtlyssSteamLobbyTag.GAIA;
            }

            // Max players with range check
            if (int.TryParse(GetArgValue(args, "-maxplayers"), out int maxPlayers))
            {
                if (maxPlayers >= 2 && maxPlayers <= 250)
                {
                    serverMaxPlayers = maxPlayers;
                }
                else
                {
                    serverMaxPlayers = 16;
                    Logger.LogWarning("MaxPlayers must be between 2 and 250. Defaulting to 16.");
                }
            }
            else
            {
                serverMaxPlayers = 16;
            }

            LogServerConfig();
        }
    }

    private bool detected = false;

    void Update()
    {
        if (!shouldHostServer) return;

        if (!hostSpawned)
        {
            if (NetworkClient.localPlayer != null)
            {
                hostSpawned = true;
                Logger.LogInfo("[HostSpawnDetector] Host player spawned. Starting delay...");
            }
        }
        else if (!actionTriggered)
        {
            timeSinceSpawn += Time.deltaTime;

            if (timeSinceSpawn >= 30f)
            {
                actionTriggered = true;
                OnHostReady();
            }
        }

        if (detected) return;

        if (GameObject.FindObjectOfType<MainMenuManager>() != null)
        {
            detected = true;
            HostServer();
        }
    }

    private void OnHostReady()
    {
        Logger.LogInfo("[HostSpawnDetector] 30 seconds passed since host spawned — teleporting!");

        Player hostPlayer = GameObject.Find("[connID: 0] _player(" + playerName + ")").GetComponent<Player>();
        CharacterController hostCharacterController = hostPlayer.GetComponent<CharacterController>();

        hostCharacterController.enabled = false;
        hostPlayer.transform.SetPositionAndRotation(new Vector3(500, 50, 510), new Quaternion(0, 0, 0, 0));
        hostCharacterController.enabled = true;
    }

    private void HostServer()
    {
        Logger.LogInfo("[DedicatedServer] Hosting Server");

        ServerHostSettings_Profile hostSettingsProfile = ProfileDataManager._current._hostSettingsProfile;
        AtlyssNetworkManager anm = AtlyssNetworkManager._current;
        ProfileDataManager pdm = ProfileDataManager._current;
        LobbyListManager llm = LobbyListManager._current;

        anm._steamworksMode = true;

        anm._soloMode = false;
        anm._serverMode = false;

        anm._serverName = serverName;
        anm._serverPassword = serverPassword;
        anm._sentPassword = serverPassword;
        anm._serverMotd = serverMOTD;
        anm.maxConnections = serverMaxPlayers;

        llm._lobbyPasswordInput.text = anm._serverPassword;
        llm._lobbyTypeDropdown.value = (int)serverType;
        llm._hostLobbyRealm = serverTag;

        anm._bannedClientList.Clear();
        anm._mutedClientList.Clear();

        if (hostSettingsProfile._banList != null)
        {
            anm._bannedClientList.AddRange(hostSettingsProfile._banList);
        }
        if (hostSettingsProfile._mutedList != null)
        {
            anm._mutedClientList.AddRange(hostSettingsProfile._mutedList);
        }

        ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic;
        switch ((int)serverType)
        {
            case 0:
                lobbyType = ELobbyType.k_ELobbyTypePublic;
                break;
            case 1:
                lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
                break;
            case 2:
                lobbyType = ELobbyType.k_ELobbyTypePrivate;
                break;
        }


        playerName = pdm._characterFiles[hostCharSaveSlot]._nickName;
        pdm._characterFile = pdm._characterFiles[hostCharSaveSlot];

        SteamLobby._current.HostLobby(lobbyType);
        MainMenuManager._current._characterSelectManager.Send_CharacterFile();
    }

    [HarmonyPatch(typeof(ChatBehaviour), "New_ChatMessage")]
    class ChatMirrorPatch
    {
        static void Postfix(string _message)
        {
            if (Plugin.isProcessingConsoleCommand)
            {
                Plugin.isProcessingConsoleCommand = false;
                return;
            }

            string cleanMessage = StripUnityRichText(_message);

            if (cleanMessage == Plugin.lastServerMessage &&
                (DateTime.UtcNow - Plugin.lastServerMessageTime).TotalMilliseconds < 100)
            {
                return;
            }

            Console.WriteLine($"[Chat] {cleanMessage}");
        }

        static string StripUnityRichText(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }
    }

    [HarmonyPatch(typeof(HostConsole), "New_LogMessage")]
    class HostConsoleFixPatch
    {
        static bool Prefix(string _message)
        {
            string text = $"[{DateTime.Now.Hour}:{DateTime.Now.Minute}] " + _message; // buggy newline normaly here in game code

            // Cache
            lastServerMessage = _message;
            lastServerMessageTime = DateTime.UtcNow;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("{0}", text);
            Console.ResetColor();

            // ignore ui and ingame console.
            return false;
        }
    }

    [HarmonyPatch(typeof(AtlyssNetworkManager), "Console_GetInput")]
    class ConsolePatch
    {
        private static readonly StringBuilder inputBuffer = new StringBuilder();
        internal static readonly object consoleLock = new object();

        static bool Prefix()
        {
            if (!Console.KeyAvailable)
            {
                return false;
            }

            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            lock (consoleLock)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        string command = inputBuffer.ToString();
                        inputBuffer.Clear();

                        _rawConsoleOut.WriteLine();

                        isProcessingConsoleCommand = true;
                        HostConsole._current.Send_ServerMessage(command);

                        RedrawInput();
                        break;

                    case ConsoleKey.Backspace:
                        if (inputBuffer.Length > 0)
                        {
                            inputBuffer.Length--;
                            RedrawInput();
                        }
                        break;

                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            inputBuffer.Append(key.KeyChar);
                            _rawConsoleOut.Write(key.KeyChar);
                        }
                        break;
                }
            }
            return false;
        }

        internal static void RedrawInput()
        {
            string prompt = "> ";
            string line = prompt + inputBuffer.ToString();

            ClearInputLine();
            _rawConsoleOut.Write(line);
        }

        internal static void ClearInputLine()
        {
            int width = Console.WindowWidth;
            _rawConsoleOut.Write($"\r{new string(' ', width - 1)}\r");
        }
    }

    // Called for every new log event.
    public void LogEvent(object sender, LogEventArgs e)
    {
        if (e.Source.SourceName == "INPUT") return;

        lock (ConsolePatch.consoleLock)
        {
            ConsolePatch.ClearInputLine();

            string formattedMessage = $"[{e.Level,-7}:{e.Source.SourceName}] {e.Data}";
            _rawConsoleOut.WriteLine(formattedMessage);

            ConsolePatch.RedrawInput();
        }
    }

    // needed for ILogListener
    public void Dispose()
    {
        BepInEx.Logging.Logger.Listeners.Remove(this);
        BepInEx.Logging.Logger.Listeners.Add(_bepinexConsoleListener);
    }

    [HarmonyPatch(typeof(HostConsole), "Send_ServerMessage")]
    class ChatManagerPatch
    {
        static bool Prefix(ref string _message)
        {
            if (!string.IsNullOrWhiteSpace(_message))
            {
                // mimic internal behavior
                var inst = UnityEngine.Object.FindObjectOfType<HostConsole>();
                var cmdManagerField = AccessTools.Field(typeof(HostConsole), "_cmdManager");
                var cmdManager = cmdManagerField.GetValue(inst);

                string[] array = _message.Split(' ');

                if (_message.Contains('<') || _message.Contains('>'))
                {
                    Logger.LogInfo("'<' and '>' are not allowed in server messages.");
                    return false;
                }

                if (_message[0] == '/')
                {
                    AccessTools.Method(cmdManager.GetType(), "Init_ConsoleCommand")
                        ?.Invoke(cmdManager, new object[] {
                        array[0].TrimStart('/'),
                        array.Length > 1 ? array[1] : string.Empty
                        });
                }
                else
                {
                    AccessTools.Method(typeof(HostConsole), "Init_ServerMessage", new Type[] { typeof(string) })
                        ?.Invoke(inst, new object[] { _message });
                }
            }

            // skip original
            return false;
        }
    }


    // Mutes game audio
    [HarmonyPatch(typeof(SettingsManager), "Handle_AudioParameters")]
    class AudioParamsPatch
    {
        static bool Prefix(SettingsManager __instance)
        {
            AudioListener.volume = 0f;

            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkManager), "StopHost")]
    public class StopHostPatch
    {
        static void Postfix(NetworkManager __instance)
        {
            Debug.Log("[StopHostPatch] Host stopped. Scheduling shutdown in 5 seconds.");

            var obj = new GameObject("ShutdownScheduler");
            GameObject.DontDestroyOnLoad(obj);
            obj.AddComponent<ShutdownDelay>();
        }
    }

    public class ShutdownDelay : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(DelayedShutdown());
        }

        private IEnumerator DelayedShutdown()
        {
            yield return new WaitForSeconds(5f);

            Debug.Log("[ShutdownDelay] Shutting down game...");
            Application.Quit(); // clean exit
        }
    }
}
