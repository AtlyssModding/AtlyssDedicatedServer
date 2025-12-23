using AtlyssDedicatedServer;
using HarmonyLib;

namespace AtlyssDedicatedServer.HarmonyPatches;

[HarmonyPatch(typeof(HostConsole), "New_LogMessage")]
class HostConsoleFixPatch
{
    internal static string LastServerMessage { get; private set; } = "";
    internal static DateTime LastServerMessageTime { get; private set; }
    
    static bool Prefix(string _message)
    {
        string text = $"[{DateTime.Now.Hour}:{DateTime.Now.Minute}] " + _message; // buggy newline normally here in game code

        // Cache
        LastServerMessage = _message;
        LastServerMessageTime = DateTime.UtcNow;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();

        // ignore UI and ingame console.
        return false;
    }
}