using System.Reflection.Emit;
using AtlyssDedicatedServer;
using HarmonyLib;

namespace AtlyssDedicatedServer.HarmonyPatches;

[HarmonyPatch(typeof(AtlyssNetworkManager), nameof(AtlyssNetworkManager.Console_GetInput))]
static class ConsolePatch
{
    static bool Prefix()
    {
        Plugin.ConsoleListener.ProcessInput();
        return false;
    }
}