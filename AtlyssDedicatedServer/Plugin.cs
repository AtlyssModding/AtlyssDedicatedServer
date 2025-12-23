using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;
using AtlyssDedicatedServer.HarmonyPatches;
using UnityEngine;

namespace AtlyssDedicatedServer;

// TODO : Refactor code into multiple scripts for easier maintainability
// Gonna push the update for now.

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInProcess("ATLYSS.exe")]
public class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger = null!;

    internal static ServerHandler Handler = null!;
    internal static ConsoleListenerWithInput ConsoleListener = null!;

    private Plugin()
    {
        Logger = base.Logger;
    }

    // Utility scripts that launch the game directly through the executable (or through Proton)
    // This also allows launching a dedicated server while another game instance is open
    private void GenerateLaunchScripts()
    {
        var gameDir = Path.GetDirectoryName(Paths.ExecutablePath)!;

        // Should allow launching the game without prompting a restart through the Steam client? Not 100% sure
        File.WriteAllText(Path.Combine(gameDir, "steam_appid.txt"), "2768430");

        if (WineDetect.IsRunningInWine)
        {
            var dataPath = Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH");
            var clientInstallPath = Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH");
            var protonPath = Environment.GetEnvironmentVariable("STEAM_COMPAT_TOOL_PATHS")?.Split(":").FirstOrDefault(x => x.Contains("/Proton"));

            var executablePath = Paths.ExecutablePath.Replace('\\', '/');

            if (executablePath.IndexOf(':') != -1)
            {
                executablePath = executablePath.Substring(executablePath.IndexOf(':') + 1);
            }

            if (string.IsNullOrWhiteSpace(dataPath) || string.IsNullOrWhiteSpace(clientInstallPath) || string.IsNullOrWhiteSpace(protonPath))
            {
                Plugin.Logger.LogWarning("Couldn't generate a dedicated server launch script for Linux w/ Proton!");
            }
            else
            {
                File.WriteAllText(Path.Combine(gameDir, "start_dedicated_server.sh"),
                    $"export STEAM_COMPAT_DATA_PATH={dataPath}\n" +
                    $"export STEAM_COMPAT_CLIENT_INSTALL_PATH={clientInstallPath}\n" +
                    string.Join(" ", [
                        $"\"{protonPath}/proton\"",
                        "run",
                        $"\"{executablePath}\"",
                        "-batchmode",
                        "-nographics",
                        "-server",
                        "--doorstop-enabled",
                        "true",
                        "--doorstop-target-assembly",
                        $"\"{Path.Combine(Paths.BepInExAssemblyDirectory, "BepInEx.Preloader.dll")}\"",
                        "&"
                    ])
                );
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(Path.Combine(gameDir, "start_dedicated_server.cmd"), string.Join(" ", [
                "start",
                "\"Launch Dedicated Server\"",
                $"\"{Paths.ExecutablePath}\"",
                "-batchmode",
                "-nographics",
                "-server",
                "--doorstop-enabled",
                "true",
                "--doorstop-target-assembly",
                $"\"{Path.Combine(Paths.BepInExAssemblyDirectory, "BepInEx.Preloader.dll")}\""
            ]));
        }
        else
        {
            // Not 100% what a native Linux script would look like since Atlyss doesn't have a native executable
            Plugin.Logger.LogWarning("Couldn't generate a dedicated server launch script for current platform!");
        }
    }

    private void Awake()
    {
        // Plugin startup logic
        PluginConfig.Configure(Config, Environment.GetCommandLineArgs());
        GenerateLaunchScripts();

        if (!PluginConfig.IsActive)
        {
            Logger.LogInfo("Argument \"-server\" not found; starting in normal mode.");
            return;
        }

        Logger.LogInfo("Starting in dedicated server mode.");
        Logger.LogInfo($"Headless mode is {(PluginConfig.IsHeadless ? "on" : "off")}.");

        var obj = new GameObject("DedicatedServer");
        GameObject.DontDestroyOnLoad(obj);
        Handler = obj.AddComponent<ServerHandler>();

        Console.CursorVisible = true;

        try
        {
            var listeners = BepInEx.Logging.Logger.Listeners;
            var bepinexListener =
                listeners.FirstOrDefault(l => l.GetType() == typeof(ConsoleLogListener)) as ConsoleLogListener;

            if (bepinexListener != null)
            {
                listeners.Remove(bepinexListener);
                BepInEx.Logging.Logger.Listeners.Add(ConsoleListener = new ConsoleListenerWithInput(bepinexListener));
                Logger.LogInfo("Successfully detached default BepInEx console listener.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"An error occurred while accessing the console stream: {ex}");
        }

        if (PluginConfig.IsHeadless)
        {
            AudioListener.volume = 0f;
            PreventAudioChanges.LockAudio = true;
        }

        // Depends on Handler.ShouldHostServer being initialized!
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
    }
}